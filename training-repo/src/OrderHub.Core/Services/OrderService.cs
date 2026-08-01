using OrderHub.Core.Common;
using OrderHub.Core.Domain;
using OrderHub.Core.Interfaces;

namespace OrderHub.Core.Services;

public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProductRepository _productRepository;
    private readonly ICustomerRepository _customerRepository;

    public OrderService(
        IOrderRepository orderRepository,
        IProductRepository productRepository,
        ICustomerRepository customerRepository)
    {
        _orderRepository = orderRepository;
        _productRepository = productRepository;
        _customerRepository = customerRepository;
    }

    public Task<PagedResult<Order>> GetOrdersAsync(int page, int pageSize, OrderStatus? status)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        return _orderRepository.GetPagedAsync(page, pageSize, status);
    }

    public Task<Order?> GetOrderAsync(int id) => _orderRepository.GetWithDetailsAsync(id);

    public Task<IReadOnlyList<Order>> GetCustomerOrdersAsync(int customerId) =>
        _orderRepository.GetByCustomerAsync(customerId);

    public async Task<ServiceResult<Order>> CreateOrderAsync(int customerId, IReadOnlyList<NewOrderLine> lines)
    {
        var customer = await _customerRepository.GetByIdAsync(customerId);
        if (customer is null)
            return ServiceResult<Order>.Fail("找不到指定的客戶");

        var lineError = ValidateLines(lines);
        if (lineError is not null)
            return ServiceResult<Order>.Fail(lineError);

        // 解析並驗證每一行的商品與庫存（收集所有錯誤，此階段不異動任何狀態）。
        var (resolvedItems, errors) = await ResolveLinesAsync(lines);
        if (errors.Count > 0)
            return ServiceResult<Order>.Fail(errors);

        // 全部驗證通過後才套用：扣庫存、建立訂單明細。
        var order = new Order
        {
            CustomerId = customer.Id,
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var (product, quantity) in resolvedItems)
        {
            product.StockQuantity -= quantity;

            // 快照存「下單當下的原價」；會員折扣只在 CalculateTotal 算一次，
            // 這裡不可以先把折扣乘進去，否則會重複折扣（Gold 會被折兩次）。
            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                Quantity = quantity,
                UnitPriceSnapshot = product.UnitPrice
            });
        }

        await _orderRepository.AddAsync(order);
        await _orderRepository.SaveChangesAsync();

        return ServiceResult<Order>.Ok(order);
    }

    /// <summary>
    /// 明細的輸入層級驗證（不碰資料庫）：非空、數量為正、不得重複商品。
    /// 依原優先序回傳第一個錯誤訊息，全部通過回 null。
    /// </summary>
    private static string? ValidateLines(IReadOnlyList<NewOrderLine> lines)
    {
        if (lines is null || lines.Count == 0)
            return "訂單至少需要一項商品";

        if (lines.Any(l => l.Quantity <= 0))
            return "商品數量必須大於 0";

        if (lines.Select(l => l.ProductId).Distinct().Count() != lines.Count)
            return "同一商品請勿重複加入，請調整數量即可";

        return null;
    }

    /// <summary>
    /// 逐行解析商品並驗證存在/停售與庫存，收集所有錯誤。
    /// 只讀取、不異動庫存；回傳已驗證的 (商品, 數量) 清單與錯誤清單。
    /// </summary>
    private async Task<(List<(Product Product, int Quantity)> Items, List<string> Errors)> ResolveLinesAsync(
        IReadOnlyList<NewOrderLine> lines)
    {
        var items = new List<(Product, int)>();
        var errors = new List<string>();

        foreach (var line in lines)
        {
            var product = await _productRepository.GetByIdAsync(line.ProductId);
            if (product is null || !product.IsActive)
            {
                errors.Add($"商品（Id={line.ProductId}）不存在或已停售");
                continue;
            }

            if (product.StockQuantity < line.Quantity)
            {
                errors.Add($"商品「{product.Name}」庫存不足（現有 {product.StockQuantity}，需求 {line.Quantity}）");
                continue;
            }

            items.Add((product, line.Quantity));
        }

        return (items, errors);
    }

    public async Task<ServiceResult<Order>> CancelOrderAsync(int id)
    {
        var order = await _orderRepository.GetWithDetailsAsync(id);
        if (order is null)
            return ServiceResult<Order>.Fail("找不到指定的訂單");

        if (order.Status != OrderStatus.Pending && order.Status != OrderStatus.Confirmed)
            return ServiceResult<Order>.Fail($"狀態為 {order.Status} 的訂單不可取消");

        // 先把庫存加回去，再改狀態。
        // （上方守衛已保證此時狀態必為 Pending/Confirmed；若先設成 Cancelled 再判斷，
        //   條件永遠為 false，庫存就永遠加不回去。）
        foreach (var item in order.Items)
        {
            var product = await _productRepository.GetByIdAsync(item.ProductId);
            if (product is not null)
                product.StockQuantity += item.Quantity;
        }

        order.Status = OrderStatus.Cancelled;

        await _orderRepository.SaveChangesAsync();

        return ServiceResult<Order>.Ok(order);
    }

    public decimal GetDiscountRate(CustomerTier tier) => tier switch
    {
        CustomerTier.Gold => 0.10m,
        CustomerTier.Silver => 0.05m,
        _ => 0m
    };

    public decimal CalculateSubtotal(Order order) =>
        order.Items.Sum(i => i.UnitPriceSnapshot * i.Quantity);

    public decimal CalculateTotal(Order order)
    {
        var tier = order.Customer?.Tier ?? CustomerTier.Standard;
        var subtotal = CalculateSubtotal(order);
        return Math.Round(subtotal * (1 - GetDiscountRate(tier)), 2);
    }
}
