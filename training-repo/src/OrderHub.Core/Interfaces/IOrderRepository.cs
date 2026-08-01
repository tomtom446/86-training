using OrderHub.Core.Common;
using OrderHub.Core.Domain;

namespace OrderHub.Core.Interfaces;

public interface IOrderRepository
{
    Task<PagedResult<Order>> GetPagedAsync(int page, int pageSize, OrderStatus? status);
    Task<Order?> GetWithDetailsAsync(int id);
    Task<IReadOnlyList<Order>> GetByCustomerAsync(int customerId);

    /// <summary>
    /// 統計 sinceUtc 之後、非 Cancelled 訂單各商品的售出數量（ProductId → 數量總和）。
    /// 一次 GROUP BY 查詢，避免逐商品查造成 N+1。
    /// </summary>
    Task<IReadOnlyDictionary<int, int>> GetSoldQuantitiesSinceAsync(DateTime sinceUtc);

    Task AddAsync(Order order);
    Task SaveChangesAsync();
}
