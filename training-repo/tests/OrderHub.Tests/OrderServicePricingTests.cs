using OrderHub.Core.Domain;
using OrderHub.Core.Services;

namespace OrderHub.Tests;

public class OrderServicePricingTests
{
    [Theory]
    [InlineData(CustomerTier.Standard, 0)]
    [InlineData(CustomerTier.Silver, 0.05)]
    [InlineData(CustomerTier.Gold, 0.10)]
    public void GetDiscountRate_ReturnsExpectedRate(CustomerTier tier, decimal expected)
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);

        Assert.Equal(expected, service.GetDiscountRate(tier));
    }

    [Fact]
    public void CalculateSubtotal_SumsQuantityTimesSnapshotPrice()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);

        var order = new Order
        {
            Items =
            {
                new OrderItem { Quantity = 2, UnitPriceSnapshot = 150m },
                new OrderItem { Quantity = 3, UnitPriceSnapshot = 40m }
            }
        };

        Assert.Equal(420m, service.CalculateSubtotal(order));
    }

    [Theory]
    [InlineData(CustomerTier.Standard, 1000, 1000)]
    [InlineData(CustomerTier.Silver, 1000, 950)]
    [InlineData(CustomerTier.Gold, 1000, 900)]
    public void CalculateTotal_AppliesTierDiscountOnSubtotal(CustomerTier tier, decimal unitPrice, decimal expectedTotal)
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);

        var order = new Order
        {
            Customer = new Customer { Tier = tier },
            Items = { new OrderItem { Quantity = 1, UnitPriceSnapshot = unitPrice } }
        };

        Assert.Equal(expectedTotal, service.CalculateTotal(order));
    }

    [Theory]
    [InlineData(CustomerTier.Gold, 900)]
    [InlineData(CustomerTier.Silver, 950)]
    [InlineData(CustomerTier.Standard, 1000)]
    public async Task CreateOrder_DoesNotBakeDiscountIntoSnapshot_SoTotalIsDiscountedOnce(CustomerTier tier, decimal expectedTotal)
    {
        // 回歸測試（客訴2）：Gold 會員應付總額被折了兩次。
        // 舊 bug：CreateOrderAsync 針對 Gold 先把 0.9 乘進 UnitPriceSnapshot，
        //        CalculateTotal 又折一次 → 變成 0.81。Silver 沒被預折所以正常。
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);
        var customer = TestSetup.AddCustomer(db, tier);
        var product = TestSetup.AddProduct(db, unitPrice: 1000m, stock: 10);

        var result = await service.CreateOrderAsync(customer.Id, new[] { new NewOrderLine(product.Id, 1) });
        Assert.True(result.Success);

        // 快照一律存原價，不可預折
        Assert.Equal(1000m, result.Value!.Items.Single().UnitPriceSnapshot);

        // 重新載入（含 Customer 導覽屬性）後，折扣只算一次
        var reloaded = await service.GetOrderAsync(result.Value.Id);
        Assert.Equal(expectedTotal, service.CalculateTotal(reloaded!));
    }

    [Fact]
    public void CalculateTotal_WithoutCustomer_UsesStandardRate()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);

        var order = new Order
        {
            Items = { new OrderItem { Quantity = 2, UnitPriceSnapshot = 250m } }
        };

        Assert.Equal(500m, service.CalculateTotal(order));
    }
}
