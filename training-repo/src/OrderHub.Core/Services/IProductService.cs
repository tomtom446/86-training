using OrderHub.Core.Domain;

namespace OrderHub.Core.Services;

public interface IProductService
{
    Task<IReadOnlyList<Product>> GetAllAsync();
    Task<IReadOnlyList<Product>> GetActiveAsync();

    /// <summary>販售中且庫存低於 threshold 的商品，附各自近 30 天售出數量，依庫存升冪。</summary>
    Task<IReadOnlyList<LowStockItem>> GetLowStockAsync(int threshold);
}
