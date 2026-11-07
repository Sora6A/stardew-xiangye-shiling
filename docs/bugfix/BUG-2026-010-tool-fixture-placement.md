# BUG-2026-010：工具夹具选入家具碰撞格，导致正常重放设备被拒绝

- 状态: 夹具修正后本地实机验证通过，待提交/合并
- 发现/更新日期: 2026-10-01，Asia/Shanghai
- 责任任务/分支: P1 原生工具击落、掉落拾取与重放验收，`feat/p1-game-adapter`
- 性质: 测试夹具现场准备错误；本轮真实失败留档，非历史补录；不修改生产放置权限或游戏碰撞规则

## 现象与影响

修复前，Memory 夹具在已有 12 项及新增两项占用格放置保护回归通过后，执行豆坊工具链回归。空闲豆坊由真实 `Pickaxe.DoFunction` 击落，生成真实 `Debris`；驱动 `Debris.updateChunks` 后，掉落实体完成吸取、设备进入真实背包。随后对原测试格调用 `Utility.tryToPlaceItem` 返回 `false`，触发 `BeanWorkshop: native placement was rejected`，该次完整 Memory 运行失败。

预期是在原生允许放置的空格完成击落、吸取与重放，核对新设备 instance ID、携带设备数量和旧请求拒绝。旧运行失败发生在重放前置检查，当时未执行豆坊的重放后状态、旧请求拒绝和新请求开工断言；之后的酱缸工具链用例也未执行。不能将旧报告中的 14 条 `Passed` 写成整轮 Memory 成功。2026-10-01 修正选址后独立全量复跑，16 项通过，包含两台设备完整工具链与恢复断言。

失败报告为 `Restored=true`，没有 `RestorationFailure` 字段，也没有工具夹具恢复失败日志。本轮未保存，未操作正式农场；现有证据没有玩家存档损失。此处修复对象是测试场地选择，不据此放宽生产设备在家具占用格上的放置限制。

## 环境与前置条件

Windows、游戏 1.6.15、SMAPI 4.4.0、Content Patcher 2.9.0 固定标签本地构建；.NET SDK 7.0.302、net6.0/x64。主模块和内容包为 `0.1.0-alpha.2`，运行于工作区 `.local/validation-game` 副本。单人主玩家、独立 `XYSP1UI` 测试农场，Memory 运行不保存。

[GameHarness](../../tests/GameHarness/README.md) 默认禁用且不进入开发 ZIP。复现需在专用测试副本中明确启用 `Enabled=true`、`Scenario=Memory`、`FarmName=XYSP1UI`、`OpenPreview=false`，并部署包含本轮占用格修复及工具回归的构建。测试游戏与 Mods 已隔离，但存档和日志仍使用正常用户目录；不能把该配置用于正式农场。

故障版本的 [ModEntry](../../tests/GameHarness/ModEntry.cs) 取 `player.Tile + new Vector2(1, 0)` 为外层测试格，仅检查 `location.objects` 中是否已有对象；没有检查家具、地图阻挡及其他原生放置前提。该格同时传给 [ToolRemovalScenarios](../../tests/GameHarness/ToolRemovalScenarios.cs)。

## 复现步骤

以下针对修正选址前的工具夹具，保留生产占用格保护；不能在修正完成后将旧证据冒充当前构建的新失败。

1. 使用专用 `XYSP1UI` 测试副本，保留农舍家具，保持故障夹具直接使用玩家右侧格的选择逻辑。不要为了让测试通过而移除床或其他家具。
2. 构建夹具：`dotnet build tests/GameHarness/GameHarness.csproj -c Release -p:GamePath=D:/code/stardew/.local/validation-game`。部署同次主模块、内容包及夹具，配置 Memory 并加载专用农场；实际修复前失败与修复后全量结果分列于下文，不能混用构建和报告。
3. 前 12 项事务、菜单和售出回归通过；新增豆坊、酱缸占用格回归各覆盖加工、待领取、损坏和空闲目标，并通过。
4. 豆坊工具场景以低层 `Object.placementAction` 准备临时设备，验证忙碌/损坏设备不能击落，再对空闲设备调用原生镐头动作。驱动真实掉落的 `updateChunks`，确认唯一掉落被实际吸取并进入背包。
5. 使用吸取后的背包 carrier 对同一测试格调用 `Utility.tryToPlaceItem`。实际返回 `false`，首个工具用例抛出上述异常；finally 执行恢复。
6. 对照本机红证据 `.local/p1-tool-placement-before-2026-10-01.json` 和 `.local/p1-tool-placement-before-smapi-2026-10-01.txt`：14 条已通过记录、非空 `Failure`、`Restored=true`。完整日志与游戏程序集不提交仓库。

## 根因分析

已确认的夹具错误是把“对象字典中没有设备”当成“该设备可由玩家原生放置”。家具不是以普通设备条目存于 `location.objects`，仅查该字典不足以证明场地合法。故障夹具初始化临时设备时直接调用 `Object.placementAction`；该低层动作不会先执行 `Utility.playerCanPlaceItemHere` 的完整放置检查，因此准备阶段成功不能证明实际重放允许。

游戏 1.6.15 的 `Utility.tryToPlaceItem` 先调用 `playerCanPlaceItemHere`；设备路径经过 `Object.canBePlacedHere` 与 `GameLocation.CanItemBePlacedHere`，检查地图、家具等碰撞前提，再决定是否调用放置动作。实测重放返回 `false`，而之前的低层放置成功，暴露了两条入口的前置条件差异。生产补丁继续拒绝占用对象格，不能通过跳过原生预检来修复测试。

本机测试存档 XML 中玩家位置为 `(576, 608)`；农舍床的边界为 `(576, 512, 128, 192)`，覆盖格坐标 X=9–10、Y=8–10。几何调查指向旧逻辑选择的玩家右侧格落入家具碰撞范围。修正后在同一现场实测，两台设备的 `ToolFixtures` 都记录原请求格 `(10, 9)` 与家具 `(F)2048` 相交，改选 `(8, 7)`、站立格 `(7, 7)`，完整原生预检为 `true`，并成功完成重放。这补上了选址诊断，与现场准备错误的归因一致；旧失败报告仍没有故障当场完整碰撞快照，不能把新运行的诊断改写成旧日志已经记录的内容，也不据此断言当场唯一阻挡物。

此前的事务和占用格回归只需要准备出临时地图对象；其低层初始化不等于普通玩家可以在该格放置。因此那些回归通过不会自动覆盖原生工具拾取后重放所需的家具和站立位置前提。

## 解决方案

`ToolRemovalScenarios.SelectFixture` 已实现现场选择、恢复和诊断修正，并在 2026-10-01 实际游戏中通过完整回归：

- 将外层传入格作为搜索起点，逐圈搜索 X/Y 偏移各不超过 8 格的候选位置；目标必须在地图内且无对象，并排除工具攻击范围内的怪物。为候选格检查四个正交相邻站立格，保留家具等碰撞检查；站立格的 Farmer 检查仅排除自身，并另外检查其他玩家。
- 使用 `player.setTileLocation` 和 `faceDirection` 设置真实站位，核对 `player.Tile`，再调用 `Utility.playerCanPlaceItemHere` 完整预检。原生允许的距离以该方法为准，不将对角距离或手写曼哈顿距离规则作为本故障根因。
- 不删除或移动家具、原有设备和地形来制造可放置条件。找不到合适位置时明确拒绝运行并留下原因，不能绕过 `Utility` 或直接塞入背包/地图来替代真实链路。
- 记录实际选中的原本空闲格作为 `fixtureTile`；finally 只在选址成功后清理该格和新增掉落，不再删除外层传入格。外层原对象引用与状态只做保留核验，恢复玩家位置、工具状态、库存槽引用与数量、时钟、菜单及所暂存的游戏状态，保留测试前对象和掉落。
- `LastSelections` 记录原请求格、`RequestedFurnitureIds`、最终设备格、站立格及 `NativePrecheck`；`ModEntry` 将其写入结果的 `ToolFixtures`。重放继续走 `Utility.tryToPlaceItem`，同时断言地图对象、背包扣减和新 instance ID；不能只用其布尔值证明完整放置成功。

不修改配方、设备状态 schema、物品 ID、生产移除保护或联机拒绝行为；不需要玩家数据迁移。两台设备的原生组件工具链已通过，物理输入、其他地图/家具组合和联机仍不在本次覆盖内；P1 总体验收未完成，P2 不开始。

## 单元测试

本问题依赖真实农舍家具、地图碰撞、玩家站位和游戏放置 API；无游戏依赖的 MachineCore 不包含这些组件。没有把手写碰撞结果或低层 `placementAction` 成功包装成能够揭示本故障的纯单元测试。本轮回归入口是实际游戏组件集成测试：

- `bean native pickaxe protects batches and pickup replacement rejects old requests`
- `crock native pickaxe protects batches and pickup replacement rejects old requests`

构建及执行步骤见上文和 GameHarness README。修复前，豆坊用例实际走到拾取后的原生重放断言并失败，整条用例未计入 `Passed`；酱缸用例未执行。修复后在保留原家具的同一独立农场中全量复跑，两条用例均通过：选址原生预检通过、忙碌/待领取/损坏状态不变、空闲设备仅生成并吸取一个真实掉落、重放仅消耗一台携带设备、新实例空闲且拥有新 ID、旧请求无副作用地拒绝、新请求正确扣料；finally 的库存、玩家、外层原对象和掉落实体恢复断言全部通过。修复前真实失败与修复后真实绿色均保留，没有用纯单元测试结果替代本组件证据。

## 验证结果

2026-10-01 修复前实际游戏结果：

| 检查 | 实际结果 |
|---|---|
| 原有 12 项 Memory 回归 | 通过 |
| 两设备占用格保护回归 | 通过，前置 `Passed` 合计 14 项；对应 [BUG-009](BUG-2026-009-occupied-placement.md) |
| 豆坊空闲击落与掉落吸取的前置断言 | 已走过；随后原生重放失败，整条工具用例未通过 |
| 豆坊重放后状态、旧请求与新请求断言 | 未执行到 |
| 酱缸完整工具链 | 未执行到 |
| 整轮 Memory | 失败，`Failure` 为 `BeanWorkshop: native placement was rejected` |
| finally 恢复 | 报告 `Restored=true`；没有恢复失败记录 |

修复后同日完整实机绿色证据为 `.local/p1-world-game-2026-10-01.json` 与 `.local/p1-world-smapi-2026-10-01.txt`，与上面的失败报告分别保存：

| 检查 | 修复后实际结果 |
|---|---|
| 全量 Memory | 16 项通过：原有 12 项、占用格 2 项、工具链 2 项；`Failure=null`、`Restored=true` |
| 豆坊与酱缸完整原生工具链 | 两项均通过，包含忙碌保护、击落、真实掉落吸取、背包重放、新 instance ID、旧请求拒绝和新请求开工 |
| 两台设备原请求格诊断 | `(10, 9)`，`RequestedFurnitureIds=["(F)2048"]` |
| 两台设备最终场地 | `Selected=(8, 7)`、`Standing=(7, 7)`、`NativePrecheck=true` |
| 恢复与家具保留 | finally 所有恢复断言通过，无恢复失败记录；未移除家具或外层原对象 |
| 日志 | 两个预期事务故障 ERROR；无额外 ERROR/WARN，工具用例各有 PASS |
| 包与存档审计 | `python .local/audit-p1-world-2026-10-01.py` 通过：开发包 10 文件与构建/部署一致，源测试档和 UI 副本磁盘哈希未变 |
| 运行结束状态 | 测试游戏退出，未发现 SMAPI 进程；`Enabled=false`、`OpenPreview=false`、`Scenario=Memory` |

修复前日志的两个事务 ERROR 分别来自预期的库存回调异常和静默改写故障注入，并各有通过断言；工具用例的第三个 ERROR 是真实失败，不能一并视为预期故障。修复后日志仅剩这两个预期事务故障，工具失败不再出现。完整日志、报告、包与磁盘审计 `.local/p1-world-final-audit-2026-10-01.json` 仅保留本机，不随仓库提交。上述组件测试结果不等同于物理鼠标/手柄拆回操作、地图步行入口或联机验收。

## 风险与回滚

搜索得到的场地受实际地图、家具和角色位置影响，应由原生前置检查决定是否可执行。复跑前核对专用农场和夹具配置，结束后退出不保存、禁用夹具；不要将错误场地选择推广为设备放置规则缺陷，也不要为了测试成功清除玩家家具。

若需回滚夹具选址修正，仅回滚测试实现并保留本记录和红证据；旧夹具仍会在该现场失败，不能宣称工具链已验收。不回滚生产占用格保护，不删除原设备状态或正式存档。测试环境恢复从专用副本备份进行；本修正没有生产数据迁移。

## 关联记录

[P1 计划](../DEVELOPMENT_PLAN.md)、[交接](../HANDOFF.md)、[GameHarness](../../tests/GameHarness/README.md)、[工具回归](../../tests/GameHarness/ToolRemovalScenarios.cs)、[占用格保护 BUG-009](BUG-2026-009-occupied-placement.md)。修复前和修复后的本机报告、日志与审计路径见上文。基线 `8f2468d83b43fb805073f73bbc8b989cf716852e` 不是本修复提交。本轮未提交、推送、建 PR 或合并；关联提交与远程记录待实际完成后补充。
