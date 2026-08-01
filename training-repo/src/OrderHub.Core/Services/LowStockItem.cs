using OrderHub.Core.Domain;

namespace OrderHub.Core.Services;

/// <summary>
/// 低庫存頁的讀取模型：一個低庫存商品，加上它近 30 天的售出數量。
/// </summary>
public record LowStockItem(Product Product, int SoldLast30Days);
