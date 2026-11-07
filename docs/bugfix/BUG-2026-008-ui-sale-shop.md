# BUG-2026-008：交互夹具选择不接受料理的售出商店

- 状态: 组件回归与完整 UI 先失败后通过，本地验证通过，待提交/合并
- 发现/更新日期: 2026-09-30，Asia/Shanghai
- 责任任务/分支: P1 原生 UI 验证，`feat/p1-game-adapter`
- 性质: 测试夹具错误；不是生产交易吞物或金额错误，非历史补录

## 现象与影响

Interaction 的前 12 个步骤通过，但最后要求在格斯商店售出两份香椿拌豆腐。实际商店背包中的料理灰显，多次点击均未售出；关闭菜单推进检查后报告 `Sale money or unrelated inventory changed`，仍有料理 2、资金 15110g。因此完整 UI 验证无法完成，不能把这次运行标为 13/13。

## 环境与前置条件

Windows、本地测试游戏 1.6.15 / SMAPI 4.4.0 / CP 2.9.0 本地构建 / alpha.2，单人独立 `XYSP1UI` 副本。Interaction 显式 ID、防误存时钟、不保存；原农场与副本的磁盘文件保持不变。该夹具不进入开发包。

## 复现步骤

1. 按 tests/GameHarness/README.md 的 Interaction 配置，从新鲜副本运行采购、制作、加工和烹饪。
2. 原生 CookingPage 制作料理 3 份，食用 1 份；核对恢复 175 能量、78 生命，余料理 2。
3. 旧步骤 12 打开 `Saloon` 的完整 ShopMenu；点击料理所在背包格，库存和 15110g 均不变。
4. 关闭并按 F8/鼠标中键核验，报告 `Completed=false`、`Step=12`、`Passed` 12 项及上述失败。

修复前现场：`.local/p1-ui-sale-before-2026-09-30.json`、`.local/p1-ui-sale-before-smapi-2026-09-30.txt`。一次食用输入先触碰家具，拾取了装饰碗；它未被售出，夹具没有把它计入目标产物或恢复量。此项属于测试操作干扰，完整重跑需避免该输入。

## 根因分析

InteractionScenario.Prepare 的售出步骤复用 `Saloon`，仅计算 sellPercentage 与预计售价，没有核对原生 `highlightItemToSell` 的接受规则。ShopMenu 依据 Data/Shops 的 SalableItemTags/类别决定能否售出；“该店能卖料理给玩家”不等于“该店会买玩家的料理”。此前 Persistence 仅覆盖购买/制作，未覆盖成品售出，因而没揭示这项夹具假设。

## 解决方案

将售出商店改为 SeedShop，并在打开时断言料理可售，失败直接给出商店拒绝原因。保留原生 Data/Shops 和食品数据，不为迁就夹具而扩大格斯收购范围。固定售出路由供夹具回归共用；实际购买、烹饪和食用流程不变。预计金额使用原生 sellToStorePrice(-1L) 与 sellPercentage，按单价取整后乘堆叠数，与真实售出方法一致。

## 单元测试

无游戏单元测试不能验证游戏的商店接受规则，未冒充已覆盖。新增依赖游戏的组件回归 `interaction sale shop accepts cooked food and pays exactly once`：用固定路由构造完整 ShopMenu，先断言允许卖料理，走原生库存点击售出 2 份，断言数量清零和精确付款，再点同格不二次付款。构造后先原生 update 越过 250ms 防误触时间，避免将安全计时误诊为商店拒绝。

回归位于 tests/GameHarness/ModEntry.cs；构建命令 `dotnet build tests/GameHarness/GameHarness.csproj -c Release -p:GamePath="<测试游戏绝对路径>"`，运行步骤见夹具 README。修复前固定路由保留 Saloon，真实运行通过前 11 项后失败于 `Interaction sale shop rejects cooked food: Saloon`，Restored=true；证据 `.local/p1-sale-shop-game-before-2026-09-30.json` 与 `.local/p1-sale-shop-game-before-smapi-2026-09-30.txt`。修复后 SeedShop 回归通过，整批售出料理 2、精确付款，第二次同格不再付款；全部 Memory 12 项通过，Failure=null、Restored=true。

## 验证结果

2026-09-30 的旧 Interaction 实际失败已保存；26 无游戏规则/提交/菜单场景与 26 Python 检查通过，不替代售出回归。GameHarness 两次 Release 均 0 警告/0 错误。修复后组件报告 `.local/p1-game-harness-2026-09-30.json` 与日志 `.local/p1-game-smapi-2026-09-30.txt`，两个 Error 为预期事务故障注入。

关闭前一进程后，从未保存的新鲜副本完整重跑 Interaction，13 步通过：皮埃尔售出料理 2，准确收入 480g（240g/份），资金 15110→15590g、料理 0、豆腐 1、酱油 4；Completed=true、Failure=null、Saved=false。报告 `.local/p1-ui-result-2026-09-30.json` 与 `.local/p1-ui-smapi-2026-09-30.txt`，最终 UI 日志无 ERROR/WARN。游戏退出、夹具禁用，原农场和副本磁盘哈希不变。NPC 日程、步行开店和厨房地图触发仍未覆盖。

## 风险与回滚

仅影响开发夹具与验收说明，无生产物品、配方、售价或存档格式变化。回滚夹具售出路由、前置检查和对应测试/记录会恢复无法完成的验证流程；不要删除或改写正式存档。

## 关联记录

[P1 UI 验证](../validation/P1-ui-2026-09-30.md)、[开发计划](../DEVELOPMENT_PLAN.md)、[交接](../HANDOFF.md)、tests/GameHarness/InteractionScenario.cs、tests/GameHarness/ModEntry.cs。尚未提交、推送、创建 PR 或合并。
