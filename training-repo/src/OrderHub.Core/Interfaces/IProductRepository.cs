using OrderHub.Core.Domain;

namespace OrderHub.Core.Interfaces;

public interface IProductRepository
{
    Task<IReadOnlyList<Product>> GetAllAsync();
    Task<IReadOnlyList<Product>> GetActiveAsync();

    /// <summary>販售中且庫存低於 threshold 的商品，依庫存量升冪排序。</summary>
    Task<IReadOnlyList<Product>> GetLowStockActiveAsync(int threshold);

    Task<Product?> GetByIdAsync(int id);
    Task SaveChangesAsync();
}
