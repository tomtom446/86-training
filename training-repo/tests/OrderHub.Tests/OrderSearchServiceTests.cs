using OrderHub.Core.Ai;
using OrderHub.Core.Domain;
using OrderHub.Core.Services;
using OrderHub.Infrastructure.Repositories;

namespace OrderHub.Tests;

public class OrderSearchServiceTests
{
    // 手寫的假翻譯器：讓測試不依賴 Gemini，只驗 service 層的白名單防線與查詢接線。
    private sealed class FakeTranslator : IOrderQueryTranslator
    {
        private readonly OrderSearchQuery? _result;
        public FakeTranslator(OrderSearchQuery? result) => _result = result;
        public Task<OrderSearchQuery?> TranslateAsync(string q, CancellationToken ct = default) =>
            Task.FromResult(_result);
    }

    private static OrderSearchService Create(OrderHub.Infrastructure.Data.OrderHubDbContext db, OrderSearchQuery? translatorResult) =>
        new(new FakeTranslator(translatorResult), new OrderRepository(db));

    [Fact]
    public async Task Search_EmptyQuery_Fails()
    {
        using var db = TestSetup.CreateContext();
        var service = Create(db, new OrderSearchQuery { Status = OrderStatus.Pending });

        var result = await service.SearchAsync("   ");

        Assert.False(result.Success);
        Assert.Contains("請輸入查詢", result.ErrorMessage);
    }

    [Fact]
    public async Task Search_TranslatorReturnsNull_RejectedAsUnintelligible()
    {
        // 翻譯失敗 / 意圖非查詢 → null → 拒絕
        using var db = TestSetup.CreateContext();
        var service = Create(db, translatorResult: null);

        var result = await service.SearchAsync("幫我把所有訂單刪掉");

        Assert.False(result.Success);
        Assert.Equal("無法理解的查詢", result.ErrorMessage);
    }

    [Fact]
    public async Task Search_NoEffectiveFilter_Rejected()
    {
        // 第二道防線：就算翻譯器回了物件，但沒有任何有效條件 → 拒絕(不把整表倒出來)
        using var db = TestSetup.CreateContext();
        var service = Create(db, new OrderSearchQuery());

        var result = await service.SearchAsync("所有訂單");

        Assert.False(result.Success);
        Assert.Equal("無法理解的查詢", result.ErrorMessage);
    }

    [Fact]
    public async Task Search_DateFromAfterDateTo_Rejected()
    {
        using var db = TestSetup.CreateContext();
        var service = Create(db, new OrderSearchQuery
        {
            DateFrom = new DateTime(2026, 7, 1),
            DateTo = new DateTime(2026, 6, 1)
        });

        var result = await service.SearchAsync("六月到七月倒過來");

        Assert.False(result.Success);
        Assert.Equal("無法理解的查詢", result.ErrorMessage);
    }

    [Fact]
    public async Task Search_ValidQuery_FiltersByStatusAndTier()
    {
        // happy path，同時驗 OrderRepository.SearchAsync 的強型別過濾
        using var db = TestSetup.CreateContext();
        var gold = TestSetup.AddCustomer(db, CustomerTier.Gold, "金客");
        var silver = TestSetup.AddCustomer(db, CustomerTier.Silver, "銀客");
        var now = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        db.Orders.AddRange(
            new Order { CustomerId = gold.Id, Status = OrderStatus.Cancelled, CreatedAt = now },       // 命中
            new Order { CustomerId = gold.Id, Status = OrderStatus.Shipped, CreatedAt = now },          // 狀態不符
            new Order { CustomerId = silver.Id, Status = OrderStatus.Cancelled, CreatedAt = now });     // 等級不符
        db.SaveChanges();

        var service = Create(db, new OrderSearchQuery
        {
            Status = OrderStatus.Cancelled,
            MemberTier = CustomerTier.Gold
        });

        var result = await service.SearchAsync("金卡會員取消的訂單");

        Assert.True(result.Success);
        var order = Assert.Single(result.Value!);
        Assert.Equal(gold.Id, order.CustomerId);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }
}
