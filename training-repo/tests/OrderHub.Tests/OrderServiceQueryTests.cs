using OrderHub.Core.Domain;

namespace OrderHub.Tests;

public class OrderServiceQueryTests
{
    [Fact]
    public async Task GetOrders_WithStatusFilter_ReturnsOnlyMatchingStatus()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);
        var customer = TestSetup.AddCustomer(db);

        db.Orders.AddRange(
            new Order { CustomerId = customer.Id, Status = OrderStatus.Pending, CreatedAt = DateTime.UtcNow },
            new Order { CustomerId = customer.Id, Status = OrderStatus.Shipped, CreatedAt = DateTime.UtcNow },
            new Order { CustomerId = customer.Id, Status = OrderStatus.Shipped, CreatedAt = DateTime.UtcNow });
        db.SaveChanges();

        var result = await service.GetOrdersAsync(1, 20, OrderStatus.Shipped);

        Assert.All(result.Items, o => Assert.Equal(OrderStatus.Shipped, o.Status));
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task GetOrders_ReportsTotalCountAndTotalPages()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);
        var customer = TestSetup.AddCustomer(db);

        for (var i = 0; i < 45; i++)
            db.Orders.Add(new Order { CustomerId = customer.Id, Status = OrderStatus.Confirmed, CreatedAt = DateTime.UtcNow.AddMinutes(-i) });
        db.SaveChanges();

        var result = await service.GetOrdersAsync(1, 20, null);

        Assert.Equal(45, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public async Task GetOrders_FirstPage_ContainsNewestOrders_NotSkipped()
    {
        // 回歸測試（客訴 1）：新建立的訂單應出現在第一頁；最後一頁不應是空白。
        // 舊 bug：repository 用 Skip(page * pageSize)，第一頁被整頁跳過。
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);
        var customer = TestSetup.AddCustomer(db);

        // 25 筆訂單，CreatedAt 遞增：第 25 筆最新
        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 25; i++)
            db.Orders.Add(new Order { CustomerId = customer.Id, Status = OrderStatus.Pending, CreatedAt = baseTime.AddMinutes(i) });
        db.SaveChanges();
        var newestCreatedAt = baseTime.AddMinutes(24);

        var page1 = await service.GetOrdersAsync(1, 20, null);
        var page2 = await service.GetOrdersAsync(2, 20, null);

        // 第一頁應是滿的 20 筆，且最上面就是最新那筆
        Assert.Equal(20, page1.Items.Count);
        Assert.Equal(newestCreatedAt, page1.Items.First().CreatedAt);
        Assert.Contains(page1.Items, o => o.CreatedAt == newestCreatedAt);

        // 最後一頁（第 2 頁）應有剩下的 5 筆，不是空白
        Assert.Equal(5, page2.Items.Count);
    }

    [Fact]
    public async Task GetCustomerOrders_ReturnsOnlyThatCustomersOrders()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);
        var customerA = TestSetup.AddCustomer(db, name: "客戶A");
        var customerB = TestSetup.AddCustomer(db, name: "客戶B");

        db.Orders.AddRange(
            new Order { CustomerId = customerA.Id, Status = OrderStatus.Pending, CreatedAt = DateTime.UtcNow },
            new Order { CustomerId = customerB.Id, Status = OrderStatus.Pending, CreatedAt = DateTime.UtcNow },
            new Order { CustomerId = customerA.Id, Status = OrderStatus.Shipped, CreatedAt = DateTime.UtcNow });
        db.SaveChanges();

        var orders = await service.GetCustomerOrdersAsync(customerA.Id);

        Assert.Equal(2, orders.Count);
        Assert.All(orders, o => Assert.Equal(customerA.Id, o.CustomerId));
    }
}
