using OrderHub.Core.Domain;

namespace OrderHub.Tests;

public class ProductServiceTests
{
    [Fact]
    public async Task GetAll_ReturnsAllProductsIncludingInactive()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        TestSetup.AddProduct(db, sku: "SKU-A001");
        TestSetup.AddProduct(db, sku: "SKU-A002", isActive: false);

        var products = await service.GetAllAsync();

        Assert.Equal(2, products.Count);
    }

    [Fact]
    public async Task GetActive_ExcludesInactiveProducts()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        TestSetup.AddProduct(db, sku: "SKU-A001");
        TestSetup.AddProduct(db, sku: "SKU-A002", isActive: false);

        var products = await service.GetActiveAsync();

        Assert.All(products, p => Assert.True(p.IsActive));
        Assert.Single(products);
    }

    [Fact]
    public async Task GetLowStock_FiltersByThreshold_AndSortsByStockAscending()
    {
        // 門檻過濾（< 不是 <=）與庫存升冪排序。
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        TestSetup.AddProduct(db, sku: "SKU-STK08", stock: 8);
        TestSetup.AddProduct(db, sku: "SKU-STK02", stock: 2);
        TestSetup.AddProduct(db, sku: "SKU-STK10", stock: 10); // 剛好等於門檻 → 應排除

        var result = await service.GetLowStockAsync(10);

        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { "SKU-STK02", "SKU-STK08" }, result.Select(i => i.Product.Sku).ToArray()); // 升冪
        Assert.DoesNotContain(result, i => i.Product.Sku == "SKU-STK10");
    }

    [Fact]
    public async Task GetLowStock_ExcludesInactiveProducts()
    {
        // 停售商品即使低庫存也不出現。
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        TestSetup.AddProduct(db, sku: "SKU-ACT", stock: 3, isActive: true);
        TestSetup.AddProduct(db, sku: "SKU-INACT", stock: 1, isActive: false);

        var result = await service.GetLowStockAsync(10);

        Assert.Single(result);
        Assert.Equal("SKU-ACT", result[0].Product.Sku);
        Assert.Equal(0, result[0].SoldLast30Days); // 無訂單 → 售出量為 0（TryGetValue 的 false 分支）
    }

    [Fact]
    public async Task GetLowStock_SoldLast30Days_ExcludesCancelledAndOldOrders()
    {
        // 近 30 天售出量：排除 Cancelled 訂單、排除 30 天前的訂單，同商品跨訂單加總。
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        var customer = TestSetup.AddCustomer(db);
        var product = TestSetup.AddProduct(db, sku: "SKU-SOLD", stock: 2);
        var now = DateTime.UtcNow;

        db.Orders.AddRange(
            // 30 天內、非 Cancelled → 計入（5 + 4 = 9）
            new Order { CustomerId = customer.Id, Status = OrderStatus.Confirmed, CreatedAt = now.AddDays(-1),
                Items = { new OrderItem { ProductId = product.Id, Quantity = 5, UnitPriceSnapshot = 100m } } },
            new Order { CustomerId = customer.Id, Status = OrderStatus.Shipped, CreatedAt = now.AddDays(-10),
                Items = { new OrderItem { ProductId = product.Id, Quantity = 4, UnitPriceSnapshot = 100m } } },
            // 30 天內但 Cancelled → 不計入
            new Order { CustomerId = customer.Id, Status = OrderStatus.Cancelled, CreatedAt = now.AddDays(-2),
                Items = { new OrderItem { ProductId = product.Id, Quantity = 7, UnitPriceSnapshot = 100m } } },
            // 40 天前 → 不計入
            new Order { CustomerId = customer.Id, Status = OrderStatus.Shipped, CreatedAt = now.AddDays(-40),
                Items = { new OrderItem { ProductId = product.Id, Quantity = 3, UnitPriceSnapshot = 100m } } });
        db.SaveChanges();

        var result = await service.GetLowStockAsync(10);

        var row = Assert.Single(result);
        Assert.Equal("SKU-SOLD", row.Product.Sku);
        Assert.Equal(9, row.SoldLast30Days);
    }
}
