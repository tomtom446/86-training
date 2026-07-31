# OrderHub — 專案記憶

## 專案簡介

公司內部訂單管理系統：業務可建立／查詢訂單、管理商品與客戶。
內部使用、單一 SQL Server 資料庫，不需要考慮多租戶、高併發或微服務架構。

## 技術棧

- .NET 8 / ASP.NET Core MVC（Razor Views + Bootstrap 5，前端資源皆為本地檔案，不依賴 CDN）
- EF Core 8 + SQL Server（本機安裝，不使用 Docker）
- 測試：xUnit + EF Core InMemory（**不需要** SQL Server）

## 分層與慣例

- 三層：`OrderHub.Web`（Controller / View / ViewModel）→ `OrderHub.Core`
  （Domain / Services / Interfaces）→ `OrderHub.Infrastructure`（Repositories / Migrations / 種子資料）
- Controller 保持薄，只轉接 service 結果；商業邏輯一律放 Core 的 service
- 只有 repository 碰 `DbContext`；Controller / Service 不可直接用 EF Core
- Service 回傳 `ServiceResult<T>`，用它表達預期內的失敗，不要丟例外
- View 一律綁 ViewModel（mapping 手寫），不要把 domain model 直接丟給 View
- 使用者輸入用 DataAnnotations + ModelState 驗證；輸入錯誤絕不能變成 500
- 金額一律用 `decimal`；會員折扣**只在 `OrderService.CalculateTotal` 算一次**，
  `UnitPriceSnapshot` 存的是「下單當下的原價」，不要在建單時先把折扣乘進去（否則會重複折扣）
- 操作結果訊息用 `TempData["Success"] / TempData["Error"]`（`_Layout.cshtml` 有共用 alert 區塊）
- 參考檔：Controller 照 `ProductsController.cs`、Service 照 `ProductService.cs` 的寫法

## 常用指令

- `dotnet build`：建置
- `dotnet test`：跑全部測試（InMemory DB，不會動到你的資料庫）
- `dotnet run --project src/OrderHub.Web`：啟動網站（看 console 顯示的 `Now listening on:` 網址）

## 重要 / 危險檔案

- `src/OrderHub.Infrastructure/Migrations/**`：EF migration 是歷史紀錄，不要手改
- `src/OrderHub.Web/appsettings*.json`：連線字串等設定，改動前先問

## 不要做的事

- 不要未經同意就加新的 NuGet 套件
- 不要在 Controller / Service 直接使用 `DbContext`
- 不要為了「順手」重構與當前任務無關的程式碼
- 不要讀取或寫入任何機密檔（`*.pfx`、`appsettings.Production.json`、user-secrets）
