# BUG-2026-012：世界输入夹具将掉落观察站位选入农舍墙区

- 状态: 本地验证通过；原选址回归先红后绿，11-06两设备自然拾取/完整20项及退出审计已补齐，待提交/合并
- 发现/更新日期: 2026-10-01发现，2026-11-06补完整实机结果，Asia/Shanghai
- 责任任务/分支: P1 实际工具击落、自然拾取与重放，`feat/p1-game-adapter`
- 性质: 测试夹具观察场地选择错误；本轮实机现场留档，非历史补录，不改变生产放置/移除权限

## 现象与影响

WorldInteraction 第二轮独立游戏进程已通过豆坊的 7 个阶段：`Craft`、`Place`、`Start`、`ProcessingPickaxe`、`ReadyPickaxe`、`Collect`、`IdleKnockDown`。空闲豆坊由实际鼠标挥镐移除，地图空出并生成单一真实 Debris 和设备 payload；观察准备将玩家一次定位到磁吸范围外，报告随后处于 `Step=7` 的 `WalkPickup` 阶段。

该次观察站位被选为 `(8,2)`，玩家像素位置为 `(512,144)`，截图中玩家位于墙后不可见。实际农舍的墙区矩形为 `(1,1,10,3)`，地板区为 `(1,3,10,9)`，`(8,2)` 明确处于墙区、地板区以外。这不满足夹具所需的有效观察站位，妨碍继续审查正常方向键走近拾取。

现场报告为 `Completed=false`、`Step=7`、`Failure=null`、`Saved=false`，7 项 Passed 不等于整轮成功，也不代表已经拾取或重放。真实掉落仍为一份，`PickupObserved=false`。本问题只证明观察站位选错，**没有旧版游戏从该格实际走路碰撞的证据，不能宣称没有可行路线或键盘失效由墙区导致**。

现场分别尝试 SKY 单键 `s`、`Down`、`a`，以及激活窗口后 `s+Down` 组合输入，报告未观察到阶段移动输入和位移：`StageMovementInputs=0`、`MovementTrail` 无本阶段记录。短键注入是否被游戏轮询接受、焦点或其他输入条件仍未确认；这与已经定位的墙区选址缺陷分开调查。没有以自动定位冒充实际方向键走动或自然拾取。

原 7 项现场已退出且禁用夹具；`python .local/audit-p1-world-ui-2026-10-01.py --preflight` 实际通过，源/UI 四文件 SHA-256 未变，开发 ZIP 10 文件与 build/部署一致，证据 `.local/p1-world-ui-preflight-2026-10-01.json`。这验证了该轮停止时的磁盘/包状态，不替代新轮退出审计；`Saved=false` 仅说明夹具没有调用保存，不能单独证明无外部写盘。无已知正式玩家数据损失。加工保护和已发生的单掉落证据保留，不把它们拼接为完整 20 步通过。

修正后新进程中，WallProbe 已为 `NativeItemPrecheckAllows=true`、`NativeWall=true`、`ObservationEligible=false`，初始化没有 Failure；豆坊前 7 步在同次运行完整重做并通过。真实 Idle 单掉落后新观察站位为 `(3,6)`、Position `(192,400)`，截图中角色位于可见地板；该时现场掉落坐标 `(397.3125,481.1555)`、DropCount=1，`DropObserved/RelocatedForObservation=true`。这验证原生墙格拒绝和有效观察准备，但该轮最后记录仍是 Step7、Completed=false，未取得实际键盘自然拾取结果，随后中断；不能因此写完整 20 项通过。

## 环境与前置条件

Windows、游戏 1.6.15、SMAPI 4.4.0、Content Patcher 2.9.0 固定标签本地构建；.NET SDK 7.0.302、net6.0/x64。主模块/内容包为未发布的 `0.1.0-alpha.2`，使用 `.local/validation-game`，夹具不进入开发 ZIP。

WorldInteraction 固定单人主玩家副本：农场名 `XYSP1UI`，game ID `8624092802`，保存名/文件夹 `XYSP1UI_8624092802`。只改本次内存，不保存也不恢复内存；准备材料/图纸、起始位置和冻结时钟后，由实际鼠标执行制作、放置、开工、领取及挥镐。观察移远只允许在原生实际移除和真实新增掉落之后执行一次；后续必须真实方向键走近并由正常游戏更新吸取。

故障版本入口 [WorldInteractionScenario](../../tests/GameHarness/WorldInteractionScenario.cs) 的 `CanStand` 使用 `CanItemBePlacedHere`，碰撞掩码为 `CollisionMask.All & ~CollisionMask.Farmers`，另查其他玩家边界。`FindObservationTile` 遍历地图内部候选并接受该查询为 true 的格，筛掉正常磁吸范围内的位置，再取距离最近者；没有额外识别农舍墙和地板。

## 复现步骤

以下针对补墙/地板过滤前的夹具。修正后须用同样场景作回归，不能用修改后的候选结果冒充旧轮已记录的信息。

1. 游戏关闭时部署故障版本 GameHarness，明确配置 `Enabled=true`、`Scenario=WorldInteraction`、`FarmName=XYSP1UI`、`SaveFixtureId=8624092802`、`OpenPreview=false`，只加载固定 UI 副本，保留原农舍家具和地图。
2. 用真实 UI 制作豆坊、放置、开工；对 Processing 和 Ready 分别实际挥镐并核验保护，再实际菜单领取豆腐。该轮前六项通过。
3. 对 Idle 豆坊实际鼠标挥镐。原生移除后出现且仅出现一份真实设备 Debris；夹具 Tick 观察到掉落，再执行一次移远准备。核验 `IdleKnockDown` 后进入 `Step=7` 的 WalkPickup。
4. 查本轮报告 `PlayerTile=[8,2]`、`PlayerPosition=[512,144]`、`DropCount=1`、`RelocatedForObservation=true`、`PickupObserved=false`；截图中玩家在墙后不可见。按农舍原生 `getWalls`/`getFloors` 定义确认该格属于墙、不是地板。
5. 保留本轮报告和 SMAPI 日志，已保存本机证据 `.local/p1-world-ui-wall-before-2026-10-01.json`、`.local/p1-world-ui-wall-before-smapi-2026-10-01.txt`；该轮退出后 preflight 已核对磁盘未写入。报告/完整日志、截图和游戏检查资料不提交远程。
6. 退出整个测试游戏，不保存并禁用夹具。修正后以未保存的固定副本启动新进程，必须重新完整运行，不能从旧轮第七项继续拼接出整轮绿色。

为给修正留下明确的揭示回归，随后新增 `VerifyWallPreparationRegression`，在任何材料/图纸准备前检查实际固定农舍 `(8,2)`。暂恢复旧无墙过滤 `CanStand`、保留其他完整协议并部署后，实际新进程启动即失败：`Step=0`、`Passed=[]`、`Stopped=true`，Failure 为 `BUG-2026-012: observation preparation accepted the native house wall (8,2).`；`WallProbe` 为 `NativeItemPrecheckAllows=true`、`NativeWall=true`、`ObservationEligible=true`。本机证据 `.local/bug012-wall-regression-red-2026-10-01.json` 和 `.local/bug012-wall-regression-red-smapi-2026-10-01.txt` 已保存；这是新增回归在旧实际判据的实机红证据，不再仅依赖静态推断。

该旧红报告的 `Preparation.MaterialsAndBlueprintsProvided=true` 是当时固定报告声明，不能作为已准备的证据；实际 Inventory 仍为源档原工具，源码 `Setup` 在给图纸/素材前执行回归并抛出。当前报告改用实际 `InventoryPrepared` 标志。保留旧报告，不回写历史字段；新轮使用实际标志和阶段/库存交叉核验。

## 根因分析

已确认的根因是把物品放置查询当成农舍玩家观察站位查询。`CanItemBePlacedHere` 即使涵盖对象和若干碰撞掩码，也不能单独证明玩家处于农舍正常地板。`FindObservationTile` 只按该查询和磁吸距离选位置，因而接受了实际墙格 `(8,2)`。

本机游戏检查事实支持需要额外过滤：游戏 1.6.15 的 `FarmHouse.getRandomOpenPointInHouse` 在检查 Back 层、`CanItemBePlacedHere` 之外，还显式调用 `isTileOnWall(x,y)` 排除墙；未升级农舍 `getWalls` 给出 `(1,1,10,3)`，`getFloors` 给出 `(1,3,10,9)`。这些接口定义与本轮报告位置一致，证明旧观察条件缺少农舍墙/地板语义。检查来自本机 `.local/inspection`，不提交反编译游戏源文件或长段代码。

不能简单通过带 `skipCollisionEffects=true` 的 `isCollidingPosition` 代替只读查询：本环境 `FarmHouse` override 没有将该参数转交给 base，因此不能据此保证无碰撞副作用。新夹具应使用只读墙/地板与场地查询，而不是为了测试选址触发原生碰撞事件。

此前成功的实际制作、放置与工具保护位于有效设备格/相邻站位；掉落后的远处观察格是独立选择，前面阶段通过不能覆盖这个位置。此次也没有真实移动输入被夹具观察到，故尚不能把墙区选择错误扩展成“玩家撞墙导致输入无效”的因果结论。

## 解决方案

已实现、通过 Release 和新进程原生选址回归；后续实际拾取仍待验收的修正：

- `CanStand` 对 FarmHouse 加入原生墙/地板语义：拒绝墙区，要求处于地板，同时保留物品/家具和其他玩家等既有只读限制。不能删除家具、放宽生产权限或改动真实掉落来制造可走位置。
- 观察候选使用四邻只读场地查询建立连通集合，再从符合磁吸范围外条件的位置选择；保留选择依据，明确该连通判断是夹具查询结果，仍要由实际方向键走近和自然吸取验证，不能宣称已执行原生玩家路径。
- `VerifyWallPreparationRegression` 将本轮 `(8,2)` 作为揭示回归，在任何材料/图纸准备前要求原生物品查询允许、原生 wall 为 true、实际 `CanStand` 为 false；将三项写入 `WallProbe`，不主动触发工具或拾取。负例环境发生改变应明确失败，不能跳过回归。观察候选应在有效地板且具有四邻查询连通证据；没有合法候选则拒绝准备，不随机放到墙内或替玩家拾取。
- 只在观察到原生 Idle 击落和单一真实 Debris 后移远一次，不继续定位玩家；自然拾取阶段不得调用 collect/updateChunks 或用传送代替方向键输入。

修正仅涉及本地夹具场地准备，不改变物品 ID、配方、schema、生产放置/移除权限或联机拒绝。未知/损坏状态的保留规则不变，不需要玩家存档迁移。

## 单元测试

本缺陷依赖真实 FarmHouse 地图墙/地板、家具和场地查询，不以手写一份地图常量或无游戏规则测试替代集成回归。当前没有新增能够单独证明原生墙/地板过滤正确的纯单元测试；原生墙格回归已实际先红后绿，自然拾取与完整 20 步仍待验收。

实际回归入口为 WorldInteraction 的 `VerifyWallPreparationRegression` 与正常游戏运行，验收至少包含：

1. 同一测试农舍中，旧候选 `(8,2)` 被新的 `CanStand` 拒绝；记录原生墙/地板查询和实际候选坐标。
2. 四邻只读查询选出正常地板上的磁吸范围外站位；确认玩家位置可见，不移动家具或掉落。
3. 实际鼠标击落后仅一个真实设备 Debris/payload；实际方向键输入产生被观察到的移动输入和玩家位移，再由游戏自然磁吸拾取，背包恰一台设备且掉落终止。
4. 后续鼠标重放真正消耗该 carrier，新实例身份符合契约；两设备及损坏保护完整 20 步来自同次运行。

构建命令：

```powershell
dotnet build tests/GameHarness/GameHarness.csproj -c Release -p:GamePath=D:/code/stardew/.local/validation-game
```

已执行修复前回归：旧 `CanStand` 在实际 FarmHouse 接受 `(8,2)`，新回归于 Step0 停止，Report 的 WallProbe 明确记录物品预检允许且原生墙=true/观察候选=true，SMAPI 有对应 WARN。随后退出该进程并重新应用墙/地板 guard 和四邻 BFS，同一构建命令 0 警告、0 错误。

修正后相同实际农舍回归通过，绿色 probe 保存在 `.local/bug012-wall-regression-green-2026-10-01.json`：Step0、Failure=null、ObservationEligible=false，其余两项原生正控制仍 true。Step0 绿色只表示初始化选址回归通过，报告 Completed=false，不能用它代替 20 步整体。该同进程随后重做豆坊前 7 项，在真实单掉落后选择 `(3,6)` 可见地板观察位。夹具 DLL SHA-256 为 `72bf25b9ab04ce90bf1a306a1052a6be4f00a96dd3bde133354a35ae0e2e4f4f`，仅本地夹具，不是生产包 DLL。

中断前新轮 `.local/p1-world-ui-in-progress-2026-10-01.json` 为 Step7/Passed 7、Failure=null、Completed=false、PickupObserved=false、StageMovementInputs=0、MovementTrail=[]。有效地板上 SKY 短键 `d` 未被观察为移动，主任务曾请求操作者实际长按 D/右方向键走近掉落，未取得完成证据；没有调用拾取或推进核验。真实方向键位移、自然吸取、重放及完整 20 步继续待验证。

随后新增独立只读 checkpoint 审计实际通过，`.local/p1-world-ui-checkpoint-audit-2026-10-01.json` 核对源/UI 四文件哈希不变、生产部署/ZIP/build一致、开发包10文件，并记录同一未完成现场：Step7/Passed7、Failure=null、移动输入0/轨迹0、真实掉落1、`harnessEnabledForPendingInput=true`。当时 PID13880/Enabled=true，属于历史运行中检查，不表示退出或20项通过。

此后两种进程查询均为0，退出动作未被观察。已保留 `.local/p1-world-ui-interrupted-2026-10-01.json` 与 `.local/p1-world-ui-interrupted-smapi-2026-10-01.txt`；最后仍7/20、方向输入/轨迹0、一份真实Debris尚未拾取。配置已恢复 Enabled/OpenPreview=false、Scenario=Memory。新增 `.local/p1-world-ui-interruption-audit-2026-10-01.json` 实际通过源/UI四文件哈希与生产ZIP/build/部署一致，明确 `completed=false`、`exitObserved=false`、`processCountAtCleanup=0`、`harnessDisabled=true`。该中断审计不等于完整20项最终审计，不据此猜测进程消失原因或宣称执行了正常退出动作。

原有 28 项 MachineCore 绿色包含 [BUG-011](BUG-2026-011-world-tool-proof.md) 的纯工具证据判据，不覆盖本次地图选址；不能引用其通过关闭本 bug。

## 验证结果

| 检查 | 2026-10-01 实际状态 |
|---|---|
| 修正前第二轮 WorldInteraction | 已运行，豆坊前 7 项 Passed，Step=7 WalkPickup；Completed=false、Failure=null、Saved=false |
| 真实 Idle 工具击落 | 已观察原生目标命中、地图移除及单一 Debris/payload；不等于自然拾取通过 |
| 错误观察站位 | PlayerTile `(8,2)`、Position `(512,144)`；截图墙后不可见，原生墙/地板定义确认选入墙区 |
| 实际键盘走近/自然拾取 | 未通过；所尝试短键未观察到 StageMovementInputs/MovementTrail，原因未确认 |
| 新增揭示回归、旧 CanStand 实机红 | 已执行，Step0/Passed 0/Stopped=true；WallProbe 三项均 true，实际接受墙格而失败 |
| 红回归结束 | 已退出，SMAPI 进程 0；保留报告/日志，未保存 |
| 修正后 GameHarness Release | 已执行，墙/地板 guard+BFS 恢复后 0 警告、0 错误 |
| 修正后 `(8,2)` 原生回归 | 已执行通过，物品允许/wall=true、ObservationEligible=false；Step0绿证据独立保存，不是整体完成 |
| 修正后地板连通观察候选 | 已执行，真实单掉落后选择 `(3,6)`、Position `(192,400)`，截图角色可见地板；未移动家具或掉落 |
| 新进程豆坊前 7 项 | 同次完整重做通过，Processing/Ready/Idle 每项 TargetClicks=1、ActualTargetHits=1、270→268；两保护 raw/库存不变 |
| 新轮实际键盘走近/自然拾取 | 尚未通过，中断前 StageMovementInputs=0/MovementTrail=[]；长按请求未取得完成证据 |
| 修正后完整 20 步及存档/包审计 | 未执行，待新进程完整重跑并退出后审计 |
| 本轮失败证据保存 | 7 项现场与新增 Step0 红回归分别保存报告/日志，不拼接或覆盖 |
| 原 7 项轮退出禁用及存档/包 preflight | 已执行，SMAPI 0、禁用；源/UI 四文件 SHA-256 未变、开发包 10 文件与 build/部署一致 |
| 新轮历史进程/配置检查点 | 当时PID13880、Enabled=true/WorldInteraction、固定身份、冻结800；此后进程消失，不能复用 |
| 新轮独立只读 checkpoint 审计 | 已实际通过，源/UI四文件哈希不变、生产部署/ZIP/build一致、开发包10文件；harnessEnabledForPendingInput=true，仍Step7/移动输入轨迹0/掉落1 |
| 最新中断清理 | 两种进程查询均0，退出动作未观察；Enabled/OpenPreview=false、Scenario=Memory，保留最后7/20报告/日志 |
| 最新中断存档/包审计 | 已实际通过，源/UI四文件哈希不变、生产ZIP/build/部署一致、harnessDisabled=true；completed=false/exitObserved=false，不是20项最终审计 |
| 提交、推送、PR、合并 | 未执行，无本修复 SHA/PR/merge 结果 |

原 7 项现场不写为设备损失、生产保护失败或方向键确定失效；它的 `Failure=null` 表示尚未产生夹具断言异常，不能解释为整轮通过。新增 Step0 红/绿回归和新轮 7 项分别列出，不能拼接。候选拒绝/有效地板观察已通过，实际走近吸取和完整 20 项仍待验证；当前 P1 总体验收未完成，P2 不开始。

中断后接手先核对进程0、夹具禁用、独立副本与哈希，保留既有红/绿/中断报告。WorldInteraction 没有磁盘阶段保存，旧内存的 WalkPickup 现场不可恢复；要验收须新进程从制作开始完整重跑，再在有效地板真实长按方向键走近自然吸取。不能对关闭的旧窗口继续输入、复用PID/截图或用后续用户答复补齐旧轮；若新轮仍没有真实事件/位移，继续留证调查，禁止传送吸取。此记录不替代 [HANDOFF](../HANDOFF.md) 的进程/配置和接手责任。

## 风险与回滚

2026-11-06后续验证：同一新9004完整20/20及退出审计通过；墙格负例仍拒绝，两设备在有效地板各做一次明确的磁吸外观察定位，随后由操作者真实走近、原实体自然消费且同payload入包、重放新身份。两段位移分别61/60条，见[完整记录](../validation/P1-world-ui-2026-11-06.md)。上文旧7/20与中断证据保持原样，不补写旧输入；本次通过补齐固定农舍布局下的完整实际拾取集成，不外推其他房型/家具或当前链保存。仍未提交/合并。

四邻查询连通与正常地板过滤改善准备条件，但仍不等于执行过完整原生玩家碰撞/步行；其他农舍升级布局、家具和原生地图差异需在实际环境验证。没有观察到方向键事件或位移时，继续调查输入采样，不能通过 Tick 移动玩家到掉落边上标绿。

回滚仅撤回夹具观察站位修正，保留本记录与旧现场证据；旧逻辑仍可能选到墙区，不能继续宣称世界拾取验收成功。不回滚生产移除保护，不删设备/掉落/家具或正式存档。模式不恢复本次内存，停止必须退出游戏且不保存、禁用 Enabled/OpenPreview，并对源/UI 主文件和 SaveGameInfo 做哈希审计；审计未完成如实交接。

## 关联记录

[P1 计划](../DEVELOPMENT_PLAN.md)、[交接](../HANDOFF.md)、[WorldInteraction 记录](../validation/P1-world-ui-2026-10-01.md)、[夹具入口](../../tests/GameHarness/WorldInteractionScenario.cs)、[GameHarness 说明](../../tests/GameHarness/README.md)、[工具证据 BUG-011](BUG-2026-011-world-tool-proof.md)、[原生放置场地 BUG-010](BUG-2026-010-tool-fixture-placement.md)。本机现场、原生红绿回归及运行中哈希检查点结果见正文；完整世界输入链和退出后最终审计待实际执行后补充。

任务分支 `feat/p1-game-adapter`，基线 `8f2468d83b43fb805073f73bbc8b989cf716852e`；基线不是本修复提交。本轮未提交、推送、建 PR 或合并。未来修复提交须关联并同步本记录、揭示回归和实际验证，附完整提交正文，不以计划中的远程动作代替结果。
