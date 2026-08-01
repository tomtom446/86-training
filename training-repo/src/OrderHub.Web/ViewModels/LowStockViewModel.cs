namespace OrderHub.Web.ViewModels;

public class LowStockViewModel
{
    public int Threshold { get; set; } = 10;

    public IReadOnlyList<LowStockRowViewModel> Products { get; set; } = Array.Empty<LowStockRowViewModel>();
}

public class LowStockRowViewModel
{
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int StockQuantity { get; set; }
    public int SoldLast30Days { get; set; }

    /// <summary>庫存低於 5，列要用警示色標記。</summary>
    public bool IsCritical => StockQuantity < 5;
}
