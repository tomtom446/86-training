using OrderHub.Core.Domain;
using OrderHub.Core.Services;

namespace OrderHub.Tests;

public class OrderServiceCancelTests
{
    private static async Task<Order> CreateOrderWithStatusAsync(
        Core.Services.OrderService service,
        Infrastructure.Data.OrderHubDbContext db,
        OrderStatus status)
    {
        var customer = TestSetup.AddCustomer(db);
        var product = TestSetup.AddProduct(db);
        var result = await service.CreateOrderAsync(customer.Id, new[] { new NewOrderLine(product.Id, 1) });
        var order = result.Value!;
        order.Status = status;
        await db.SaveChangesAsync();
        return order;
    }

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Confirmed)]
    public async Task CancelOrder_ActiveOrder_SetsStatusCancelled(OrderStatus initialStatus)
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);
        var order = await CreateOrderWithStatusAsync(service, db, initialStatus);

        var result = await service.CancelOrderAsync(order.Id);

        Assert.True(result.Success);
        Assert.Equal(OrderStatus.Cancelled, db.Orders.Single(o => o.Id == order.Id).Status);
    }

    [Fact]
    public async Task CancelOrder_ActiveOrder_RestoresProductStock()
    {
        // 回歸測試（客訴3）：取消訂單後，商品庫存應加回。
        // 舊 bug：CancelOrderAsync 先把狀態設為 Cancelled，才判斷
        //        「狀態是否為 Pending/Confirmed」，條件永遠為 false，庫存從未加回。
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);
        var customer = TestSetup.AddCustomer(db);
        var product = TestSetup.AddProduct(db, stock: 10);

        var create = await service.CreateOrderAsync(customer.Id, new[] { new NewOrderLine(product.Id, 3) });
        Assert.True(create.Success);
        Assert.Equal(7, db.Products.Single(p => p.Id == product.Id).StockQuantity); // 建單後扣庫存

        var cancel = await service.CancelOrderAsync(create.Value!.Id);
        Assert.True(cancel.Success);
        Assert.Equal(10, db.Products.Single(p => p.Id == product.Id).StockQuantity); // 取消後庫存加回
    }

    [Theory]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task CancelOrder_NotCancellableStatus_Fails(OrderStatus initialStatus)
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);
        var order = await CreateOrderWithStatusAsync(service, db, initialStatus);

        var result = await service.CancelOrderAsync(order.Id);

        Assert.False(result.Success);
        Assert.Equal(initialStatus, db.Orders.Single(o => o.Id == order.Id).Status);
    }

    [Fact]
    public async Task CancelOrder_NotFound_Fails()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);

        var result = await service.CancelOrderAsync(12345);

        Assert.False(result.Success);
        Assert.Contains("找不到", result.ErrorMessage);
    }
}
