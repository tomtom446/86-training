using Microsoft.AspNetCore.Mvc;
using OrderHub.Core.Services;
using OrderHub.Web.ViewModels;

namespace OrderHub.Web.Controllers;

public class ProductsController : Controller
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    public async Task<IActionResult> Index()
    {
        var products = await _productService.GetAllAsync();

        var vm = new ProductListViewModel
        {
            Products = products.Select(p => new ProductRowViewModel
            {
                Sku = p.Sku,
                Name = p.Name,
                UnitPrice = p.UnitPrice,
                StockQuantity = p.StockQuantity,
                IsActive = p.IsActive
            }).ToList()
        };

        return View(vm);
    }

    public async Task<IActionResult> LowStock(int? threshold = null)
    {
        var effectiveThreshold = threshold ?? 10;

        var vm = new LowStockViewModel { Threshold = effectiveThreshold };

        // 門檻必須大於 0；輸入錯誤要顯示表單驗證訊息，不能變成 500。
        if (effectiveThreshold <= 0)
        {
            ModelState.AddModelError(nameof(vm.Threshold), "庫存門檻必須大於 0");
            return View(vm);
        }

        var items = await _productService.GetLowStockAsync(effectiveThreshold);
        vm.Products = items
            .Select(i => new LowStockRowViewModel
            {
                Sku = i.Product.Sku,
                Name = i.Product.Name,
                StockQuantity = i.Product.StockQuantity,
                SoldLast30Days = i.SoldLast30Days
            })
            .ToList();

        return View(vm);
    }
}

