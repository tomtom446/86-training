using OrderHub.Core.Domain;
using OrderHub.Core.Interfaces;

namespace OrderHub.Core.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;
    private readonly IOrderRepository _orderRepository;

    public ProductService(IProductRepository productRepository, IOrderRepository orderRepository)
    {
        _productRepository = productRepository;
        _orderRepository = orderRepository;
    }

    public Task<IReadOnlyList<Product>> GetAllAsync() => _productRepository.GetAllAsync();

    public Task<IReadOnlyList<Product>> GetActiveAsync() => _productRepository.GetActiveAsync();

    public async Task<IReadOnlyList<LowStockItem>> GetLowStockAsync(int threshold)
    {
        var products = await _productRepository.GetLowStockActiveAsync(threshold);

        var sinceUtc = DateTime.UtcNow.AddDays(-30);
        var soldByProduct = await _orderRepository.GetSoldQuantitiesSinceAsync(sinceUtc);

        // 已依庫存升冪由 repository 排好，這裡只做映射（找不到售出紀錄視為 0）。
        return products
            .Select(p => new LowStockItem(p, soldByProduct.TryGetValue(p.Id, out var qty) ? qty : 0))
            .ToList();
    }
}
