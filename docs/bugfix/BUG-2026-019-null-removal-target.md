# BUG-2026-019：NPC 清理空格时空目标触发机器保护异常

- 状态: 已修复待完整实机验证（10-11主机移动/Esc对话关闭短测通过，双端待验收）
- 发现/更新日期: 发现2026-10-05；更新2026-10-11，Asia/Shanghai
- 责任任务/分支: P1-NET-02 首次客户端准备 / `feat/p1-game-adapter`
- 性质: 生产游戏适配代码；本轮实机发现，新增实际 SDK 回归先红后修，非历史补录

## 现象与影响

诊断禁用的专用合作世界中，主机日志反复出现 `NullReferenceException`，调用链为 `MachineService.TryGetKind → BlocksRemoval → MachinePatches.ProtectNpc → GameLocation.checkDestroyItem → NPC` 寻路。用户同时报告角色不能移动、键盘无法操控；日志证实原生更新调用链被本模组异常中断，但尚不能仅由日志确定全部输入症状的因果。修后用户已回复主机仍无法移动。已观察时钟恢复/无旧ERROR与残余键盘故障分开，后者原因继续调查，不能将全部输入症状归因于NRE或因CLI通过计完整实机验收。

故障主机 PID9712、客户端 PID56700 已按客户端后主机顺序通过窗口 X 关闭，sky 无游戏窗口、CIM 核游戏/SMAPI 为0。修前第二次填写后实际界面已核创建overlay消失、进入客户端小屋；这证明创建后小屋画面已出现，仍不代实际键盘移动或持久化。关闭前主机截图春1凌晨02:00卧床；用户此前“春2保存”反馈未获磁盘支持：专用两 XML仍春1/年1，未持久化客户端 Farmer，八个受监视保存文件与故障前基线相同。没有 ReturnedToTitle 或成功保存证据；本轮未观察数据损失，不能把未持久化准备误写成成功。

本地 `error-summary.json` 的 `tryGetKindErrorFrameCount=47425` 是匹配栈帧数，不是独立异常次数。原始现场、源代码、日志和保存检查保留在 `.local/bug019-2026-10-05`，不提交个人完整日志或 XML。

## 环境与前置条件

Windows x64；隔离游戏1.6.15、SMAPI4.4.0、CP2.9.0；SDK7.0.302、net6.0/x64。源安装与旧源/UI/Stage3 世界不作复现输入。真实专用身份为 `XYSNET02 / 450604963`；主机/客户端 Mods 隔离，Saves、偏好和日志目录仍共享。

现场主模块 `EnableReadOnlyNetworkProbes=false`，GameHarness `Enabled=false`、`OpenPreview=false`；角色配置为 Network/Host 与 Network/Client。故障发生于普通合作游戏更新，未运行探针、离线报告 pair 或完整 NET02 验收。修复前生产 DLL 为 `5ef0df7577255bbc0132e7879fab4f40fda1bcbcad013a291c906a3dbc4b04ac`；夹具780构建没有改动。

最小回归在独立 CLI 中引用实际生产项目和合法本机 SDK/Harmony，全部游戏引用 `Private=false`，按显式路径解析程序集，不启动游戏、加载资产、读取或保存世界。与无游戏依赖的 MachineCore 检查分开运行。

## 复现步骤

1. 对修复前实现执行 [GameAdapter 入口](../../tests/GameAdapter/README.md) 的五个空目标案例，调用实际 `TryGetKind(null)`、`TryRead(null)`、`BlocksRemoval(null)` 和 private `ProtectNpc`。
2. 最后一例在独立 CLI 内临时注册实际生产 Harmony prefix，调用 SDK 的 `GameLocation.checkDestroyItem(null, Vector2.Zero, false)`。该原生空对象分支在读地图/Game1 前直接返回 false，因此未初始化 GameLocation 只用于此空分支；测试结束在 `finally` 撤销自己的补丁。
3. 预期：分类/读取/保护拒绝空目标，prefix 放行原生并保持 ref 返回值，原生仍返回 false。旧实现实际五项均抛空引用，0通过/5失败、退出1。
4. 在红证据保留后加入空目标防护，用相同入口重跑；实际五项通过、0失败、退出0。后续追加正常对象/设备测试不借用这五项的旧红结果。

本轮红/绿以及补充回归执行同一条以下命令（命令行的两个MSBuild路径属性使用双引号）；复跑日志使用新名字，不覆盖本轮证据：

```powershell
dotnet run --project tests/GameAdapter/GameAdapter.csproj -c Release "-p:GamePath=D:\code\stardew\.local\validation-game" "-p:SmapiPath=D:\code\stardew\.local\validation-game" -- --game-path "D:\code\stardew\.local\validation-game" --smapi-path "D:\code\stardew\.local\validation-game" --production "D:\code\stardew\src\XiangyeShiling\bin\Release\net6.0\XiangyeShiling.dll"
```

实际本轮结果日志为 `.local/bug019-null-object-adapter-red-2026-10-05.txt` 和 `-green-2026-10-05.txt`。命令不把旧生产 DLL 自动恢复到运行目录；重新观察旧实现必须使用独立源码/产物，不回退运行中的游戏。

## 根因分析

原生 NPC 路径可将空地图格的对象参数传为 null。生产 prefix 将该参数交给 `BlocksRemoval`，后者调用 `TryGetKind`；旧分类函数直接访问 `item.QualifiedItemId`，把“进入机器分类函数的对象一定非空”当成前提，异常因此越过 prefix 中断原生更新。空格既不是设备，也不应获得移除保护。

原生 SDK 的 null 分支本来返回 false，修复不能简单在 prefix 上把所有输入提前拒绝，否则会替换原生行为。此前 Memory/NPC 保护场景主要给非空机器对象，没有覆盖原生空格参数；MachineCore 的纯规则不编译/运行这个 SDK 适配入口。此次新增 CLI 同时覆盖 helper、实际 prefix 和临时注册后的真实原生 null 分支。

## 解决方案

在 [MachineService](../../src/XiangyeShiling/GameIntegration/MachineService.cs) 将 `TryGetKind`、`TryRead`、`BlocksRemoval` 的目标标为 nullable；`TryGetKind` 在访问对象属性前对 null 返回 false，以 `[NotNullWhen(true)]` 表达只有成功分类才保证对象非空。`TryRead` 同样声明成功输入非空，空目标返回 false 且 state 为 null。

`TryGetKind` 的 false 分支保留原有 kind 输出赋值，调用者必须先检查 bool，不得消费失败输出；`TryRead` 在未知/损坏或设备种类不匹配时返回 false，失败 state 不构成可用状态，不能笼统断言每一种失败都输出 null。注释明确这些输入/输出契约。

[MachinePatches.ProtectNpc](../../src/XiangyeShiling/GameIntegration/MachinePatches.cs) 接受 nullable 参数；空目标的 `BlocksRemoval=false` 使 prefix 返回 true，保留已有 `__result`，由原生 null 分支决定最终 false。普通物品和有效 Idle 仍按原规则放行，Processing/Ready/未知或损坏设备仍受保护；这些边界已用实际 SDK 实例补充回归，不借空输入红证据宣称全部先红后绿。

修复只改变空目标边界，不初始化设备、替换损坏 raw、变更机器 schema1、物品 ID、依赖版本、联机写入权限或事务流程；不调用 Execute/Gate/Ledger，也不启用诊断。

## 单元测试

[tests/GameAdapter/Program.cs](../../tests/GameAdapter/Program.cs) 的实际 SDK/生产入口回归不属于普通无 SDK 单元或 CI。首次五项记录如下，各项旧实现均因实际生产 `TryGetKind` 空引用失败，修后均通过：

| 具名测试 | 核心断言 |
|---|---|
| `null object is not a custom machine` | `TryGetKind(null)` 为 false，不分类为自定义机器 |
| `null object has no machine state` | `TryRead(null)` 为 false，空输入的 state 为 null |
| `null object does not block removal` | `BlocksRemoval(null)` 为 false，空格不受机器保护 |
| `actual NPC prefix passes null through without replacing native result` | 实际 private prefix 返回 true；原 ref result 分别为 false/true 时均保持原值 |
| `registered production NPC prefix preserves native empty-tile false` | 实际生产 prefix 临时注册到 SDK `checkDestroyItem` 后，空目标仍原生返回 false；finally 撤销补丁 |

相同 CLI 命令：修前日志0通过/5失败、退出1（9.016s）；修后日志5通过/0失败、退出0（4.003s），没有跳过。此红发生在修改空目标守卫之前，不是事后补造等价失败。随后实际10项全绿、13项全绿日志均保留；最终权威日志 `.local/bug019-null-object-adapter-final13-location-2026-10-05.txt` 为13通过/0失败、退出0（5.431s），显式 `LoadFromAssemblyPath(--production)` 并在测试循环外核 Assembly.Location 与指定路径一致、打印生产SHA为ad9，避免从别处加载产物后误标结果。

补充八个具名案例为修后防回退，不具有各自旧实现红：

| 具名测试 | 实际断言范围 |
|---|---|
| `ordinary O and BC SDK objects pass through without state or metadata changes` | 普通(O)/(BC)实际对象不分类、不解释任意机器key、不保护；prefix放行/ref结果、ID与raw/metadata引用不变 |
| `both custom idle SDK objects remain movable` | 两设备真实Idle状态可读/不保护，prefix放行/ref不变 |
| `both custom processing SDK objects remain protected` | 两设备由实际MachineRules.Start生成Processing，prefix及注册原生入口拒绝，raw/metadata不变 |
| `both custom ready SDK objects remain protected` | 同真实批次到Ready时仍拒绝，raw/metadata不变 |
| `both custom corrupt SDK objects retain raw and remain protected` | 损坏raw不可读、保护且原raw引用不变 |
| `both custom missing-state SDK objects remain protected without initialization` | 缺key不可读/保护，检查不初始化或补写state |
| `both custom future-state SDK objects retain raw and remain protected` | SchemaVersion999拒绝/保护，保留原raw |
| `both custom mismatched-kind SDK objects retain raw and remain protected` | state.Kind与设备ID不匹配拒绝/保护，不替换原raw |

两设备每种状态均核真实SDK实例、实际prefix和原身份/metadata引用；受保护状态另调用临时注册prefix的实际原生入口拒绝。普通/Idle非空仅测API与prefix放行，不进入未初始化地图的原生正文。

## 验证结果

2026-10-05 已执行五项 SDK CLI 红/绿、生产最终 Release 构建（0警告/0错误、退出0）、核心85项（退出0）、catalog40物品/30配方闭合（退出0）。日志分别为 `.local/p1-bug019-production-final-build-2026-10-05.txt`、`p1-bug019-core-2026-10-05.txt`、`p1-bug019-catalog-2026-10-05.txt`。未重跑105全 Python、Memory16/Interaction13/Persistence6、完整 World20 或双端 NET02。

实际构建命令为 `python .local/run-network-fixture-check.py p1-bug019-production-final-build-2026-10-05 -- python tools/build.py --game-path .local/validation-game`；核心为同runner前缀`p1-bug019-core-2026-10-05`后执行`dotnet run --project tests/MachineCore/MachineCore.csproj -c Release`，catalog为前缀`p1-bug019-catalog-2026-10-05`后执行`python tools/catalog.py`。runner只留本机日志；默认catalog检查不使用不存在的`--check`参数。

游戏停止状态刷新 runtime/host/client 生产 DLL、新开发 ZIP 和 current profiles。69个已登记文件 before/after 恰5变化，其他64相同，六config原字节禁用、八save不变。另一次目录补充审计确认原52个包相关文件匹配当前审计、当前55个，新增恰为 runtime/host/client 的生产 PDB，三份均与本次 build 相同，无移除；不能将69项日志写成整个目录只变5项。profiles 清单现在15个包文件。初稿只预期 runtime PDB 的审计退出1，修正完整三处预期后正式 final 退出0；这是本地审计预期遗漏，不是生产回归红。

生产 DLL `ad9d96199a86601bdb253eca628e1fe34464c5ee5e979e41cfac0e5176d17cd1`；夹具 `78026dd6a3f703fcb26bef41590289dab8505ae7621ab4813503666787582693` 未变。新鲜 host PID56016/窗口332594 已合作加载春1/年1，[main player]画面时间从06:10至13:40/14:40推进，但玩家仍在床。运行中 `.local/bug019-2026-10-05/fixed-host-loaded-checkpoint.json`及fixed-host-loaded-SMAPI.txt、`p1-bug019-fixed-host-checkpoint-2026-10-05.txt`实查72项匹配、六config禁用/八save同基线、同世界加载且无ERROR；userMovementVerified/nativePreparationSavedVerified/actualReportPairChecked/退出验证均false。这是运行中检查点，不是退出审计。自动Escape一次未开菜单，非用户物理Escape；用户已回复修后仍无法移动，残余输入原因继续调查；时钟恢复/NRE消除不代键盘恢复。此轮客户端未启动，持久化未成功，pair/Full P1未验收。详见[本轮验证](../validation/P1-null-removal-2026-10-05.md)。

## 风险与回滚

CLI 只覆盖确定的 SDK 入口，不能代替完整 NPC 更新、真实输入、普通物品原生移除、实际设备拆回或联机。非空普通/Idle 不得在未初始化 GameLocation 上调用会读地图的原生正文；状态保护回归应由 prefix 拦截或只测放行契约。修后用户仍无法移动，保持探针关闭，继续核按键绑定/焦点/原生限制/输入屏蔽等候选原因；均尚未证实，不用空目标修复成功结束残余调查，取得移动/保存证据再判断完整实机状态。

需要回滚时先退出两端并重新核进程0，保留当前产物、六配置、八save、before-source、红绿日志和两份 deployment 审计；用匹配生产 DLL/PDB/开发包和重新核对的 profiles 恢复明确的旧产物，不能只换单端 DLL。旧实现会重新暴露空目标异常，不建议继续实机准备；必要时停用主模块/内容包相关测试并维持诊断禁用。修复不迁移状态，不删除/重置个人存档或损坏 raw，不通过恢复旧保存掩盖尚未成功的客户端准备。

## 关联记录

计划 [P1-NET-02](../DEVELOPMENT_PLAN.md)、[HANDOFF](../HANDOFF.md)、[验证记录](../validation/P1-null-removal-2026-10-05.md)、[NET02准备](../NET02_TEST_SETUP.md)、[游戏适配 CLI](../../tests/GameAdapter/README.md)、[贡献规范](../../CONTRIBUTING.md)。代码/测试/记录均为累计未提交工作；分支 `feat/p1-game-adapter`，HEAD 基线 `8f2468d83b43fb805073f73bbc8b989cf716852e`。没有本次修复 commit SHA、完整 commit 正文、PR、推送、merge 或远程 CI；不得用基线替代修复 SHA。

10-05中断补充（历史）：操作者进一步确认移动和Esc菜单均无响应；物理Escape停止Computer Use，当时host56016未关闭。主机后来成功原生保存春2且专用两XML变化，无客户端Farmer；72项中其余70项/旧六save/六config相同，证据host-only-spring2/audit.json。此保存不证明输入恢复或客户端准备完成。当时输入观测器尚未实施，完整实机待验收，不以消除本空引用代替全部输入故障修复。

10-11接续：只读InputObservation四安全判据加旧85共89及SDK构建通过，生产ad9与ZIP未改。同主机17644实际合作加载春23，操作者确认“移动和菜单都正常，已关闭”。唯一原报告144样本支持D各层输入/向右42.24023像素，Escape关闭DialogueBox后CanMove恢复；常规GameMenu、持续走出床/农舍及客户端未实测，历史残余输入根因仍不确定，不称观测器修复输入。约30秒已无本NRE，用户ClosedGame/日志/CIM0及八save不变、六config恢复原字节禁用均核过；见 [输入观测记录](../validation/P1-input-observation-2026-10-11.md)。本次仅补实际集成验证，不新增根因修复、借作五项修前红或宣布完整双端验收；原Observing/false flags未修改。

10-12补核：10-11禁用诊断的另一轮host30744/client18620原生连接/春24准备保存及关闭通过，操作者确认两端能移动。完整两端日志无NullReferenceException，各有一条Steam成就启动ERROR；实际XML核已创建XYSNETClient与连接ID一致，85项审计仅专用两XML变化。详见 [准备记录](../validation/P1-network-preparation-2026-10-11.md)。这是双端普通操作/原生保存的集成补充，不证明旧残余输入根因、常规菜单或完整NET02/所有NPC/设备行为；保持完整实机待验收。
