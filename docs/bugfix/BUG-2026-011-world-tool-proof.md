# BUG-2026-011：世界输入夹具将无关挥镐与被忽略的目标点击拼成有效命中

- 状态: 本地验证通过；原纯判据先红后绿，11-06完整WorldInteraction20项及退出审计已补齐，待提交/合并
- 发现/更新日期: 2026-10-01发现，2026-11-06补完整实机结果，Asia/Shanghai
- 责任任务/分支: P1 实际鼠标放置、工具拆回与自然拾取验收，`feat/p1-game-adapter`
- 性质: 新增测试夹具的证据判定缺陷；启动 WorldInteraction 前静态审查发现，非历史补录，非线上玩家事故

## 现象与影响

WorldInteraction 的旧 `RequireToolProof` 只要求本阶段有目标格鼠标点击、玩家体力低于阶段初值、当前仍选择同一把镐。这三个条件不能证明原生工具动作真正命中过目标设备：操作者先在空地挥镐消耗体力，再于该动画进行中点击设备；游戏可能忽略第二次使用工具，但 SMAPI ButtonPressed 仍能被夹具记为一次目标点击。旧判据把这两次动作拼接后返回通过。

加工中、待领取和损坏设备本来就应保持对象/状态/库存不变。如果未真正击中设备却仅核对这些数据没变化，夹具会对移除保护产生假阳性。因此“机器没变”必须与同一阶段、同一玩家、同一镐、同一目标的原生实际命中证据同时成立。发送过输入或看见工具动画都不足以单独证明目标被击中。

本问题尚未在 WorldInteraction 游戏运行中触发。旧判据已提取为无游戏依赖的实际 helper，使用能够揭示该缺陷的输入复现并保留失败证据；这不是旧游戏实机挥镐日志。未发生已知玩家存档损失，也未改变生产移除保护、配方或状态格式。尚未通过实机观察的阶段不得标为已验收。

## 环境与前置条件

纯回归环境为 Windows、.NET SDK 7.0.302、net6.0；执行 [MachineCore](../../tests/MachineCore/MachineCore.csproj) 不需要加载游戏程序集。该项目通过 `Compile Include="../GameHarness/WorldToolProof.cs"` 链接 [WorldToolProof](../../tests/GameHarness/WorldToolProof.cs)，测试直接调用夹具使用的同一个判定实现，没有维护一份仅供测试的重复逻辑。

集成环境为 `.local/validation-game` 游戏 1.6.15、SMAPI 4.4.0、CP 2.9.0 固定标签本地构建，双包未发布的 `0.1.0-alpha.2`。WorldInteraction 只允许 `XYSP1UI`、game ID `8624092802`、保存名/文件夹 `XYSP1UI_8624092802` 的单人主玩家副本，显式启用后运行；只改本次内存，不保存也不恢复内存。2026-10-01 修正后的同一进程已通过前 7/20 项豆坊实机步骤，停在自然拾取阶段；这不等于完整世界输入链通过。该运行现已中断；报告保留7/20，实际进程为0、夹具禁用，源/UI四文件哈希核验未变。后续须新进程完整重跑，不能恢复Step7内存。运行状态以 [交接](../HANDOFF.md) 为准。

## 复现步骤

以下为修正前**纯判据负例**，不是要求在正式农场重放未经验证的夹具：

1. 将旧 `RequireToolProof` 的同一布尔逻辑提取到 `WorldToolProof.IsSatisfied`，保留 `targetMouseClicks > 0 && afterStamina < beforeStamina && correctPickaxe`，不添加真实命中数要求。提取是为了让实际判据能够在无游戏依赖项目中执行，未声称原生观察接入已完成。
2. 在 [Program](../../tests/MachineCore/Program.cs) 加入 `world tool protection rejects unrelated swing followed by ignored target click`。输入 `targetMouseClicks=1`、`completedTargetHits=0`、`beforeStamina=270`、`afterStamina=268`、`correctPickaxe=true`，断言判据必须返回 false。
3. 执行 `dotnet run --project tests/MachineCore/MachineCore.csproj -c Release`。旧判据实际返回 true，命令退出码 1，在新增回归抛出：`BUG-2026-011: unrelated stamina loss plus an ignored target click must not pass protection.`
4. 本机摘录 `.local/bug011-tool-proof-red-2026-10-01.txt` 保留实际命令、退出码、异常与输入。此前 16 个规则和 2 个菜单场景通过，执行于新增负例停止；后续提交协调场景没有执行到，不能把该红轮写成完整核心通过。
5. 修正同一个 helper 后重新执行同一命令，完整 28 项通过，具体断言见下文。当前 helper 已修正，直接运行当前构建预期通过；旧失败只按留档说明，不能冒充当前新失败。

与数值负例对应的输入序列来自静态审查：空地挥镐导致体力下降；动画内目标点击可能被原生输入忽略，却被旧 `ObserveInput` 计数。该序列尚未执行旧 WorldInteraction 游戏复现，记录保留此证据边界。

## 根因分析

旧 `WorldInteractionScenario.RequireToolProof` 将“目标鼠标输入”和“阶段累计体力下降”视作同一次已完成的命中。`ObserveInput` 仅观察 SMAPI 输入事件、鼠标格/距离和当前镐，并未排除 `Player.UsingTool` 时的目标点击，也未观察原生 `Pickaxe.DoFunction` 实际处理目标。因此输入意图、任意位置体力消耗与目标实际命中之间缺少关联。

旧的 `StageSwingObserved` 即使看见玩家处于工具动画，也只说明有某次挥动，不保证该挥动处理所选设备格。保护阶段的数据不变是必要的结果断言，但在本缺陷的输入序列下，即使完全没击中设备也会成立。

此前 Memory 的工具组件场景直接调用真实 `Pickaxe.DoFunction`，能够验证生产保护，但不包含“鼠标事件到原生工具调用”的世界输入关联。它们通过不会覆盖这个新 WorldInteraction 判据缺陷。没有将该夹具问题归因于生产设备状态逻辑，也没有通过放宽工具范围或移除保护来修复证据。

## 解决方案

已完成的纯判据修正：

- `WorldToolProof.IsSatisfied` 增加 `completedTargetHits > 0`，同时保留目标鼠标输入、阶段体力下降和相同镐要求。实际目标命中数由调用方的只读原生观察提供；不能由鼠标事件或动画观察冒充命中计数。
- `MachineCore.csproj` 链接实际 helper，`Program.cs` 对无真实命中的组合负例、真实命中正例及缺失鼠标/缺失体力消耗/错误镐负例执行断言。

原生观察接入已完成并执行 Release 构建、部分实机验证：

- WorldInteraction 通过只读 Harmony observer 观察游戏自己调用的 `Pickaxe.DoFunction`；在正确阶段、固定玩家/位置、同一把镐和实际目标格匹配后，记录完成目标命中。observer 不主动调用工具方法，不修改原生参数、返回值、地图、背包或掉落；只读取已经发生的游戏动作。
- `ObserveInput` 排除 `Player.UsingTool` 中的目标点击，避免动画内被忽略的输入计为新的有效操作；真实保护核验调用同一个 `WorldToolProof`。
- GameHarness 引用测试安装自带 `smapi-internal/0Harmony.dll`，`Private=false`，不复制或分发第三方程序集。生产模块已使用同一安装的 Harmony；本次没有提升依赖版本、改变主模块分发包或引入生产持久化字段。

observer 的 prefix 只记录动作前体力，postfix 在原生调用完成后核对阶段、玩家、位置、镐引用与实际目标格，记录前后体力和命中身份。观察异常保存为 `ObserverFailure`，下次 Tick 停止夹具；不将异常抛回原生工具调用。人工审查未发现 observer 修改原生参数或游戏状态。

2026-10-01 豆坊的加工中挥镐、待领取挥镐和空闲拆回均由实际目标格鼠标左键触发：每阶段目标输入 1、完成原生命中 1、观察到工具动画、体力 270→268。两个保护阶段还通过原始状态、对象引用、背包与掉落不变断言；空闲阶段则产生原生 Debris，见 [世界 UI 记录](../validation/P1-world-ui-2026-10-01.md)。前 7 项不含自然拾取、重新放置、发酵缸或损坏状态命中，不能推算其结果。

原生观察必须在正常鼠标操作下验证计数正确，并在失败时拒绝推进，而不是自动补击、调用生产 Execute 或替玩家执行工具。报告分别保留目标输入与实际完成命中数，便于交接审查。新增走近输入/位移记录属于本轮 WorldInteraction 证据补强，不替代本 bug 的命中断言。

## 单元测试

测试文件：[Program.cs](../../tests/MachineCore/Program.cs)；实际被测实现：[WorldToolProof.cs](../../tests/GameHarness/WorldToolProof.cs)。命令：

```powershell
dotnet run --project tests/MachineCore/MachineCore.csproj -c Release
```

| 测试名称 | 输入/关键断言 | 修复前 | 修复后 |
|---|---|---|---|
| `world tool protection rejects unrelated swing followed by ignored target click` | `(clicks=1,hits=0,270→268,correctPickaxe=true)` 必须 false；无关体力消耗和被忽略的点击不能合并成命中 | 实际 false 断言失败，旧 helper 返回 true，退出码 1 | 通过，helper 返回 false |
| `world tool protection requires completed target hit and matching pickaxe` | `(1,1,270→268,true)` 为 true；没有鼠标输入、体力不降、错误镐各为 false | 新增配套用例；首个负例已停止执行，没有修复前运行结果 | 所有正负断言通过 |

修复后的完整命令为 28 项通过：16 规则、8 提交协调、2 菜单、2 世界夹具判据。它证明 helper 不再接受上述假阳性；调用方的命中来源另由上述部分实机记录验证，不能仅用纯测试代替游戏实际鼠标命中、保护、掉落或自然拾取验收。

## 验证结果

| 检查 | 2026-10-01 实际状态 |
|---|---|
| 修复前纯判据回归 | 已执行，退出码 1；揭示负例失败，本机红摘录已留档 |
| 修复后同命令完整回归 | 已执行，28 项通过；两项使用实际共享 helper |
| WorldInteraction 只读原生命中 observer | 已实现并审查；豆坊三个工具阶段分别记录目标输入 1、原生命中 1、体力 270→268，两个保护阶段通过 |
| 修正后的 GameHarness Release | 已执行，0 警告、0 错误；实际部署 DLL 与构建 SHA-256 一致，详见世界 UI 记录 |
| 完整 20 步 WorldInteraction 实机 | 同一进程前 7 项已通过，Step=7、Completed=false、Failure=null；自然拾取及其后步骤待验证，不得合并历史 Memory 结果 |
| 运行中独立检查点审计 | 已执行，当时源/UI 共 4 个存档文件哈希未变、旧生产部署与 ZIP 一致且夹具启用；现已中断/禁用，不能代替完整 20 项最终审计 |
| 该世界输入链保存重载、英文/手柄/尺寸矩阵、联机 | 未执行，仍是 P1 剩余范围 |
| 提交、推送、PR、合并 | 未执行；没有本 bug 修复 SHA、PR 或 merge 结果 |

本次没有旧游戏错误挥镐实机日志，没有伪造原生命中报告。`.local` 摘录仅用于本机复查，不提交完整路径/日志/存档。后续如 observer 或实际输入继续失败，保留该次报告和日志，按事实更新本节及交接，不能用 28 项纯回归宣称完整 UI 已通过。P1 总体验收仍未完成，P2 不开始，联机放置/加工保持拒绝。

## 风险与回滚

2026-11-06后续验证：同一新9004完整世界链20/20及退出审计通过；8工具阶段/9条正确原生命中、保护阶段raw/库存/地图/掉落保持均核，发酵缸Ready实际两次命中。详见[完整记录](../validation/P1-world-ui-2026-11-06.md)，最终原报告SHA`ce6a7c6a535d2acea6f526396755682df45bc405eeafef6b1917dccd9ac7bc54`。上文10-01的7/20和28核心是原历史结果，不拼接；本次未改工具准入，不将旧保存或联机写入标绿。当前本bug的完整世界输入集成已补齐，仍未提交/合并。

剩余风险主要在证据来源：纯 helper 接受或拒绝正确，并不自动保证原生 observer 的目标解析、调用完成时机和身份过滤正确。observer 不能改变游戏工具行为；若只读接入异常、无法确认实际目标或该阶段没有完成命中，应保留现场并拒绝验收。测试前核对专用副本，结束退出不保存、禁用夹具并比较源/UI 存档哈希。

若需撤回原生观察或 helper 修正，仅撤回测试夹具与链接测试；保留本记录和红证据，并明确旧判据会接受假阳性，因此不再使用它宣称工具保护已验收。禁止回滚生产保护或清空玩家机器状态来通过测试；无玩家数据迁移，不需要删除正式存档。需要复跑时只使用未保存的独立副本，不在正式农场启用测试模式。

## 关联记录

[P1 计划](../DEVELOPMENT_PLAN.md)、[交接](../HANDOFF.md)、[WorldInteraction 实测与未完成项](../validation/P1-world-ui-2026-10-01.md)、[GameHarness](../../tests/GameHarness/README.md)、[实际判据](../../tests/GameHarness/WorldToolProof.cs)、[单元测试](../../tests/MachineCore/Program.cs)、[WorldInteraction 入口](../../tests/GameHarness/WorldInteractionScenario.cs)。本机修复前摘录路径见上文；部分集成报告为 `.local/p1-world-ui-in-progress-2026-10-01.json`，运行中完整性检查为 `.local/p1-world-ui-checkpoint-audit-2026-10-01.json`，中断审计为 `.local/p1-world-ui-interruption-audit-2026-10-01.json`；完整 20 项和退出后最终审计尚无通过结果。

基线 `8f2468d83b43fb805073f73bbc8b989cf716852e` 不是本 bug 修复提交。当前任务分支 `feat/p1-game-adapter`；本轮未提交、推送、建 PR 或合并，关联提交和远程记录待实际完成后补充。未来 `fix` 提交应将实际修正、揭示回归及本记录一起审查并使用完整提交正文。
