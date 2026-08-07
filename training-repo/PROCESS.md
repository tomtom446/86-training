# PROCESS.md — 我的練習心得

> 一個原則：**寫「具體發生的事」，不寫感想文。**
> 貼上當時真實的 prompt、真實的數字、真實的錯誤訊息——三個月後的你（和你的同事）才用得上。

#### 使用的 agent 與模型：

Claude Code（Opus 4.8, 1M context）。設定檔、hooks、subagents 都放在 `training-repo/.claude/`。

---

## 通用四問

### 1. 我的任務拆解

（開工前你把任務拆成哪幾步？實際做的時候順序有變嗎？為什麼變？）

- 開工前拆成：**① 練習1 建 agent 設定 → ② 建立 baseline（先 `dotnet build` + `dotnet test` 確認 28 個測試全綠）→ ③ 三個 bug 各自「重現→定位→修→補測試→commit」→ ④ 寫 PROCESS.md**。
- 實際做的時候順序有變：我原本想「先修 bug 再補設定」，但改成**先跑一次 baseline** 才動手。原因是如果不知道原本幾個測試綠，改完根本分不清是我修好的還是本來就過的。先有 baseline（28 綠）當基準，後面每加一個回歸測試我都能明確看到 28→29→32→33。
- 另一個順序調整：三個 bug 我**先讀完整份 `OrderService.cs` 和 repository 才動手**，而不是逐一 bug 讀。因為讀第一個 bug（分頁）時就順手看到了折扣和取消的可疑寫法，一次讀完比來回讀省事。

### 2. AI 幫上大忙的地方

（哪件事 agent 做得又快又好？**貼上當時的提問原文**，說明為什麼這樣問有效。）

- **同時定位三個 bug 的根因**。我沒有丟「幫我修 bug」，而是把客訴的**具體症狀**照抄給它，一次交辦：

  > 「客訴1：新訂單在列表第一頁找不到、最後一頁常空白。客訴2：Gold 會員應付總額比手算少，Silver 正常。客訴3：取消訂單後庫存沒加回、越退越少。先讀 OrderService 跟 OrderRepository，逐一告訴我每個症狀的根因在哪一行、為什麼，先不要改。」

  這樣問有效的原因：**症狀夠具體（哪一頁、哪個等級、什麼操作後）**，它就能直接對到程式碼。三個根因它都一次點出來，而且都能對到確切的行：`Skip(page * pageSize)`（1-based 卻沒 -1）、Gold 在建單時 `UnitPriceSnapshot` 被預折一次、`CalculateTotal` 又折一次、`CancelOrderAsync` 先設 Cancelled 才判斷狀態。
- **產回歸測試 + 自我驗證測試「修復前真的會失敗」**。每個 bug 修完，我要求它**先把修好的那行 stash 掉、只跑新測試、確認 FAIL，再還原**。三個都親眼看到紅→綠，不是它說「測試會過」就算數。

### 3. AI 誤導我的地方，與我如何發現

（agent 說錯／改錯／過度自信的時刻。你靠什麼抓到——對照程式碼？頁面實測？跑測試？）

- **它第一個 commit 把 message 弄壞了還往下走**。它用 PowerShell 的 here-string 語法在 bash 工具裡下 `git commit`，引號被吃掉，commit 標題變成一個 `@`、內文「Don'ts」被截成「Donts」。**我靠 `git log -1 --format=%B` 對照發現**——它當下沒察覺，是我要求印出實際訊息才看到。後來改成把訊息寫進檔案用 `git commit -F` 才乾淨，並 `--amend` 修好。教訓：**commit 完一定要回頭看 `git log`，別假設它寫進去的就是它給我看的。**
- **改折扣的地方差點選錯層**。修客訴2 時，一個直覺解是「把 `CalculateTotal` 的折扣拿掉」或「只在建單折」。我沒照單全收，**去對照 `OrdersController.MapToDetails` 跟明細頁 ViewModel**，發現頁面是「單價快照顯示原價、折扣另列一行、總額折一次」的設計。所以正解是**建單時存原價、折扣統一在 `CalculateTotal` 算一次**，而不是動 `CalculateTotal`。靠讀 View/ViewModel 才確認方向對。
- **一個小編譯錯誤**：它在 pricing 測試用了 `NewOrderLine` 卻沒 `using OrderHub.Core.Services;`，`dotnet test` 直接報 `CS0246`。跑測試就抓到了，補 using 解決。

### 4. 我會帶回日常工作的一招

（一個具體、可複製的做法，不要寫「要多驗證」這種口號——寫出**操作步驟**。）

**「回歸測試要先證明它會紅」**——修完 bug、加完測試後，不要只看到一片綠就收工。操作步驟：

1. `git stash push -- <只有修復的那個檔>`（把修復退掉，但**保留新測試**）。
2. `dotnet test --filter "FullyQualifiedName~<新測試名>"` → **必須看到 FAIL**。
3. `git stash pop` 還原修復 → 再跑一次 → 綠。
4. 才 commit。

這招能擋掉「測試其實沒測到 bug、恆真通過」的假安全感——這三個 bug 本來就是因為舊測試只驗了旁邊的屬性（見下方思考題）才被放過的。

## 自我驗證（做到哪個階段答哪題）

### 第一階段 — Agentic Coding

練習 1

1. **能**。Web＝Controller/View/ViewModel 只做接線與顯示；Core＝Domain/Service/Interface，商業邏輯（折扣、庫存、狀態轉移）都在這；Infrastructure＝EF Core DbContext、Repository、Migration、種子資料。
2. **有核對，且抓到一處過度簡化**：agent 一開始把建單流程講成「扣庫存→存訂單」，但實際上 `CreateOrderAsync` 是**先把所有明細跑完、收集 errors，任何一項失敗就整筆不寫入**（`errors.Count > 0` 直接 return，不會 `AddAsync`）——是「全有或全無」，不是逐項寫入。
3. **知道**。商業邏輯放 Core 的 service（透過 interface 注入）；新增頁面要動 Controller（薄）＋Core service/interface＋repository＋ViewModel＋View＋測試，資料存取一律走 repository、不在 service/controller 直接碰 `DbContext`。

練習 2

1. ✅ 三個 bug 都先理解症狀、對到頁面流程（`/Orders` 分頁、`/Orders/Details` 金額、`/Products` 庫存）才動程式。
2. ✅ 給 agent 的是具體觀察（第一頁/最後一頁、Gold vs Silver、庫存 10→7→10），不是只貼客訴原文。
3. ✅ 每個修復都用回歸測試「修復前紅、修復後綠」證明症狀消失（分頁/金額/庫存）。
4. ✅ 每個 bug 補一個回歸測試，`dotnet test` 全綠（28 → 33）。
5. ✅ 三個獨立 commit，message 均為「症狀 → 根因 → 修法」格式。
6. **（思考題）為什麼原本的測試沒抓到這三個 bug？**
   因為**舊測試每個都只驗了 bug「旁邊」的屬性，剛好避開了出錯的那個**：
   - 分頁：`GetOrders_ReportsTotalCountAndTotalPages` 只斷言 `TotalCount=45`、`TotalPages=3`，**從沒斷言 `Items` 的內容或筆數**——而 `TotalCount/TotalPages` 是從 `PagedResult` 算的，跟壞掉的 `Skip` 無關，所以永遠綠。
   - 折扣：`CalculateTotal_AppliesTierDiscountOnSubtotal` 是**直接手搓一個 `UnitPriceSnapshot=原價` 的 order 去測 `CalculateTotal`**，沒有走 `CreateOrderAsync`，就測不到「建單時被預折」這半段，雙重折扣被藏起來。
   - 庫存：`CancelOrder_ActiveOrder_SetsStatusCancelled` 只斷言**狀態**變成 Cancelled，**沒斷言庫存**，所以庫存沒加回也照樣綠。
   共通點：**測試覆蓋率看起來有，但斷言沒打在關鍵不變量上**。我補的三個回歸測試都是專門去斷言那個被漏掉的屬性（第一頁內容、快照＝原價且總額只折一次、取消後庫存加回）。

練習 3

> 註：以下 1–4 需要在瀏覽器實測，等我本機 `dotnet run` 起來後逐項勾選；目前先記錄「程式邏輯與 service 測試」層面的驗證狀態。

1. （待頁面實測）不帶參數應走門檻 10、帶 `?threshold=3` 結果隨之縮小——邏輯已由 `GetLowStock_FiltersByThreshold_AndSortsByStockAscending` 覆蓋（門檻 10 → 只回庫存 2、8）。
2. （待頁面實測）`?threshold=0`／`-1` 應顯示表單驗證錯誤而非 500——controller 走 `ModelState.AddModelError` 後 `return View`，不會拋例外，讀 code 已確認。
3. ✅ 售出數量排除 Cancelled：`GetLowStock_SoldLast30Days_ExcludesCancelledAndOldOrders` 用一筆 Cancelled + 一筆 40 天前的訂單驗證，結果只算到 9（5+4）。
4. ✅ 停售商品不出現：`GetLowStock_ExcludesInactiveProducts` 驗證停售的低庫存商品被排除。
5. ✅ 分層與命名跟既有 Products 一致：我請 agent 用 code-reviewer 角度整份審過，回報 layering/ViewModel 綁定/驗證/單次 GROUP BY（無 N+1）皆符合慣例；我自己也對照 `ProductsController.Index` 與 `ProductRepository` 確認命名一致。
6. ✅ 4 個新 service 測試，`dotnet test` 全綠（33 → 36）。

練習 4

1. ✅ 重構後 `dotnet test` 全綠 36，且**沒有新增或修改任何測試**——這正是「行為不變」最直接的證據。
2. **改善了什麼**：`CreateOrderAsync` 從「邊驗證邊扣庫存」的長方法，拆成 `ValidateLines`（輸入層級）＋ `ResolveLinesAsync`（商品/庫存，收集所有錯誤）＋瘦身後的主流程「先全部驗完、再套用」。**沒有改變什麼**：錯誤訊息文字與觸發順序、逐行錯誤收集、成功/失敗與落地效果完全一致；折扣、取消、分頁邏輯未動。
3. ✅ 我有從 code review 角度看 diff：確認唯一的行為面差異只是「失敗時不再先扣記憶體中的庫存」，而因為失敗本來就不 `SaveChanges`、重複商品也已被前置驗證擋掉，對外可觀察結果不變。

### 第二階段 — 自建 MCP Server（活動 2）

> 註：以下需要 `npx`（MCP Inspector）、SQL Server 與把 server 接進 CLI 才能實測的項目，等我本機跑起來後再逐項勾選；目前先記錄「已建置並 build 通過」的程式面狀態與設計思考。

已完成（程式與 build）：
- 練習 1：`src/OrderHub.Mcp`（net8.0，stdio）三個唯讀工具 `get_order` / `low_stock` / `customer_orders`——工具只轉接 service/repository，金額重用 `OrderService`，entity 一律投影成匿名物件避免循環參照，log 走 stderr。`dotnet build` 綠。
- 練習 3：`.mcp.json` 已把 orderhub 接進 Claude Code。
- 練習 4：新增 `cancel_order`（`Destructive=true, Idempotent=false`），三個唯讀工具補 `ReadOnly=true`；規則仍在 service 層，工具只轉接。
- 練習 5：`orderhub://discount-rules` Resource 與 `low_stock_report` Prompt，並在 Program.cs 註冊。

待實測（我本機要做的）：
- [ ] 練習 0：接 Playwright MCP，請 agent 自己建單並截圖；對比活動 1 練習 2 當時我手動重現 bug 的步驟。
- [ ] 練習 2：`npx @modelcontextprotocol/inspector dotnet run --project src/OrderHub.Mcp` → List Tools，手動呼叫 `low_stock`(10) 對照 `/Products`，`get_order` 給不存在 Id 應回清楚訊息而非 exception。
- [ ] 練習 3：關/開 MCP 各問一次「哪些商品庫存低於 5」，比較 agent 繞多遠。
- [ ] 練習 4：對 agent 說「取消訂單 X」，觀察按允許前資料不被動；成功後回 `/Products` 確認庫存回補；重複取消/取消已出貨單得到清楚拒絕訊息。
- [ ] 練習 5：`@` 選 `orderhub://discount-rules` 問 Gold 買 1000 應付多少；`/mcp__orderhub__low_stock_report` 一鍵產報告。

**練習 5c 思考題 — Resource / Prompt 放在 server 的價值**：
- **折扣規則用 Resource 給 vs 讓 agent 自己讀 `OrderService.cs`**：Resource 是一份**團隊共用、進版控、單一真相**的背景知識，agent 不必去翻程式碼推斷規則（省 context、也不會因讀錯檔而誤解）。規則改版時只改這一處；缺點是若寫死字串會和程式碼變成兩份真相，所以理想上 Resource 內容可動態組出。
- **Prompt 範本放 server vs 每個人自己打一段話**：範本放 server 等於把「問法」也版本化、全隊一致，改一次全隊生效；不必每個人記住要先呼叫 `low_stock` 再彙整。它把「該怎麼問」的知識從個人腦袋搬進可維護的資產。

---

## 附錄：值得留下的對話片段

（貼 1–2 段最有代表性的 prompt 與回應**摘要**——不用貼全文，重點是「我怎麼問」和「它怎麼答」。）

**片段 1 — 一次交辦三個症狀、要求先定位不要改**

> 我：把三張客訴的具體症狀照抄給你，先讀 `OrderService` 跟 `OrderRepository`，逐一告訴我根因在哪一行、為什麼，**先不要改**。
>
> 它（摘要）：客訴1＝`OrderRepository.GetPagedAsync` 的 `Skip(page * pageSize)`，page 是 1-based，第一頁就跳掉整頁 20 筆；客訴2＝`CreateOrderAsync` 對 Gold 把 `UnitPriceSnapshot` 先乘 0.9，`CalculateTotal` 又乘 0.9，變 0.81，Silver 沒被預折所以正常；客訴3＝`CancelOrderAsync` 先 `Status = Cancelled` 才判斷 `Status == Pending/Confirmed`，永遠 false，庫存加不回。

**片段 2 — 要求證明回歸測試「修復前會失敗」**

> 我：修好之後，先把修復那行退掉、只跑新測試，讓我看到它 FAIL，再還原。
>
> 它：`git stash` 掉修復 → 跑 `--filter` 只跑新測試 → 三個都印出 `[FAIL]`（例如 Gold 案例 expectedTotal 900 但實得 810）→ `git stash pop` 還原 → 再跑全綠（33 passed）。這樣我才確定測試真的咬得到 bug。
