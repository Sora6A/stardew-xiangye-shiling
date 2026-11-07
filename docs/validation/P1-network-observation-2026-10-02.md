# P1-NET-02 失败观测扩展与停止状态部署

本文件保留10-02构建/部署时点。10-04已用同一780构建实际执行Scope读前/门禁读后两个失败，结果另记 [真实失败验证](P1-network-failure-2026-10-04.md)；以下“未启动/未实机异常”是原时点，不代表最新覆盖。当前状态以HANDOFF为准。

日期：2026-10-02，Asia/Shanghai。分支 `feat/p1-game-adapter`，Git 基线 `8f2468d83b43fb805073f73bbc8b989cf716852e`；本轮修改尚未提交。当前游戏/SMAPI 进程为 0，专用世界 XYSNET02 / 450604963 的双端诊断仍关闭，首次客户端 Farmer 未核实。夹具已编译并部署，真实 Tick 异常、探针及双端报告配对均未执行，P1-NET-02/P1 未验收。

本记录接续 [历史合作准备](P1-network-live-2026-10-02.md)。旧夹具 d2 构建曾在准备实例加载，不是本轮新夹具的实机证据；旧 PID 消失的退出动作未观察，手动客户端加入请求尚无回复。动态现场及具体操作入口以 [HANDOFF](../HANDOFF.md) 为准。

## 变更目的与行为

此前文档已明确 Network 夹具在 Tick 失败时写入 `Protocol=null`，即使该 Tick 已成功读取诊断也不保留。这一缺失使门禁、连接或发送失败的调查信息不足。本轮补充这项观测能力，依据是源码检查和既有待办，没有观察到新的玩家事故，也没有修复前失败回归证据，不建立新的已修复 BUG 记录。

只修改 `tests/GameHarness/NetworkScenario.cs` 的 Tick：进入检查前将局部 `observed` 置 null；`ReadNetworkDiagnostics()` 成功返回后立即赋值；后续 catch 用同一局部值写失败报告。读取前失败仍为 null，下一 Tick 重新初始化，不借用上一帧或旧连接的观测，也不在 catch 再读 API。关键注释说明观测的来源和时间边界。

保留的快照可能早于后续发送或异常，不能称为异常后的最终状态。`Failure` 仍非 null，离线配对必须拒绝；schema2、`FullNet02Accepted=false`、只读写入门禁和活动/标题分文件都不变。报告写入本身仍可能 I/O 失败并传播，本轮没有新增文件事务或自动回滚保证，旧报告也不能当成当前失败轮次的输出。生产代码、公共 API、设备状态格式和依赖均未改变；40 物品/30 配方冻结范围及 7/5 原型不变。

## 实际检查与未覆盖范围

环境：Windows x64，Python 3.10.9，.NET SDK 7.0.302，目标 net6.0/x64；编译引用为隔离副本的游戏 1.6.15、SMAPI 4.4.0。所有结果为本地结果。

| 检查 | 命令、断言与结果 |
|---|---|
| 夹具 Release | `dotnet build tests/GameHarness/GameHarness.csproj -c Release -p:GamePath=D:/code/stardew/.local/validation-game -p:SmapiPath=D:/code/stardew/.local/validation-game`，实际退出 0，0 警告/0 错误；日志 `.local/p1-network-observation-harness-build-2026-10-02.txt` |
| 既有失败报告门禁 | `python -m unittest discover -s tests -p test_network_reports.py -k failed_saved_or_claimed_full_acceptance -v`，`NetworkReportPairTests.test_failed_saved_or_claimed_full_acceptance_reports_are_rejected`，1 项 OK，退出 0；日志 `.local/p1-network-observation-failure-gate-2026-10-02.txt` |
| 独立只读复核 | 复核 Tick 局部初始化/读取后赋值/catch 来源、Failure/schema2/Full 与标题文件边界；重新比较审计 24 项、两端各 14 包文件及各 3 角色配置，源码/产物/角色哈希一致，无差异；未修改、构建或启动游戏 |

具名 Python 测试使用人工完整报告，分别把 Failure、Saved、Full、Reconnect、ReturnedToTitle 或库存引用不变标记变成拒绝条件，要求 `validate_pair` 抛出 ValueError。它证明完整 Protocol 不抵消 Failure，不执行 C# Tick catch、SMAPI 或游戏 SDK。读取前 null、读取后保留及无跨 Tick 借用仍只有源码复核，没有实际异常路径运行证据。

前轮 85 核心/全 105 Python 与 BUG-015～018 的证据保持原阶段；本轮没有重跑或累加。无游戏核心不编译 NetworkScenario，本轮生产 DLL 字节相同。两端正常/异常连接、实际配对、标题/重连、完整 NET02 和事务验收仍未执行。夹具尚无新构建实机运行结果；不能把构建、人工报告测试或文件审计写成网络通过。

`python tools/check_workflow.py` 初次与最终检查均实际退出 0，维护结构/本地链接通过、0 提交正文检查，日志分别为 `.local/p1-network-observation-maintenance-2026-10-02.txt`、`.local/p1-network-observation-maintenance-final-2026-10-02.txt`；`git diff --check` 两次退出 0，既有 DESIGN 的 CRLF 提示非失败。最终 CIM 复查仍无游戏/SMAPI 进程，分支与 HEAD 未变。本轮未推送、未读取远程 CI 或分支保护。

## 部署、身份与文件证据

部署前确认所有目标游戏进程停止。`.local/net02-observation-2026-10-02/before-hashes.json` 记录 24 项实际基线，并在同目录 before 留原字节；旧 runtime PDB 另存 `before-runtime-GameHarness.pdb`。两份 SMAPI 历史日志亦先本地归档，完整日志和个人存档不提交。

一次性刷新脚本 `.local/refresh-net02-observation-harness-2026-10-02.py` 已执行，实际退出 0，日志 `.local/p1-network-observation-deployment-2026-10-02.txt`。它更新 runtime 的自有夹具 DLL/PDB、curated 白名单三文件，然后调用正式工具：

```powershell
python tools/prepare_multiplayer.py --source-mods .local/validation-game/Mods --runtime .local/validation-game --output .local/net02-profiles --harness-dir .local/net02-probe-harness
```

正式工具保留现有目标配置原字节，重新生成 schema2 清单并复核 14 包文件。`profiles.json` 的角色配置哈希现在对应 450604963 的当前禁用绑定；旧初建清单只属于历史，不能用来证明新部署。刷新不是整批 I/O 事务；`refresh-started.json` 是禁止覆盖已有执行的标记，不是成功本身，成功依据为实际退出、后核与审计。不要删除标记或盲目重跑一次性脚本。

| 产物 | 本轮 SHA-256 |
|---|---|
| 生产 DLL，build/runtime/两端 | `5ef0df7577255bbc0132e7879fab4f40fda1bcbcad013a291c906a3dbc4b04ac`，与本轮 before 相同 |
| GameHarness DLL，build/runtime/curated/两端 | `78026dd6a3f703fcb26bef41590289dab8505ae7621ab4813503666787582693`，五处一致 |
| `.local/net02-profiles/profiles.json` | `9a4aa669b3413bd3ebdd3cf38b4c71d87c66e66fcb6f8f23c8a5ee1f78bc861c`，重新生成 |
| 开发 ZIP | `7907c05e9e54c3d95cfb261ba2653ed574c9f9f2a12759c419e622fc93e05096`，未改；10 文件，无夹具/PDB/第三方 DLL |

这些是文件 SHA-256，不是 Git 提交 SHA。`deployment-audit.json` 的 24 项前后比较恰有 7 项变化：NetworkScenario 源、build 夹具、current profiles，以及 runtime/host/client/curated 的四份夹具 DLL。其余 17 项不变，包括六配置、八存档、生产 build、旧 profiles 与 ZIP。完整旧 `.local/multiplayer` 树及 runtime 四份历史报告也保持原哈希。runtime PDB 与新 build 匹配，旧 PDB 单独保留；curated/profiles 不含 PDB。

四角色配置保持 `EnableReadOnlyNetworkProbes=false` / `Enabled=false`，Network、Host/Client 与 XYSNET02/450604963 身份正确。runtime 的主模块和夹具配置也保持本轮 before 的原字节和禁用状态，不改成角色配置。六配置“不变”仅指本轮观测阶段前后；旧 live 启动时曾发生格式/default 写回，不能外推成自最初准备以来从未变化。

八存档为三组受保护旧副本及新世界两 XML，均未写入。XYSNET02 主 XML 与 SaveGameInfo 的本轮 SHA 分别为 `d459be380b0485b80793556e95a9eb316b2ff2b741344ae5c37e356fce2c4e59`、`1fe86e0966e13de68620d4d7a2e6ed36dbcc7242083fe53bdb3b9001e5070ffc`，与初始专用保存一致。不由主机初始保存推断客户端 Farmer 已创建或保存。

## 回滚与接续

如需撤回本轮扩展，先停止使用这些文件的游戏进程，再从本轮 before 恢复 NetworkScenario 及原自有夹具 DLL，同时恢复配套的旧 runtime PDB。保持当前世界身份和禁用开关，用正式 prepare_multiplayer 的当前显式路径重新生成清单并复核实际文件；不能把旧 profiles 哈希表覆盖到不匹配的 DLL 上。未改配置/存档，无需为了回滚代码恢复或删除它们；历史报告、旧副本及审计仍保留。恢复后构建/部署也须单独留新证据，不能覆盖本记录。

下一步从禁用诊断的新进程继续专用合作准备，实际核实首次客户端 Farmer 与小屋，再由操作者完成专用准备保存、正常退出并核对两 XML/配置。准备完成且两端停止后，再安排另一次启用诊断的短时运行；主机 60 秒等待窗口不能消耗在首次角色创建。实际运行后归档两份 active 报告并执行离线配对，Failure 非 null 始终拒绝；Basic 通过仍不等于 Full NET02 或可写事务通过。P1 未通过前不扩展 P2。

本轮未提交、推送、创建 PR 或合并；没有新的 commit/merge SHA，也没有远程成功结果。相关规范见 [CONTRIBUTING](../../CONTRIBUTING.md)，操作契约见 [NET02 准备](../NET02_TEST_SETUP.md)、[夹具说明](../../tests/GameHarness/README.md)、[主机协议](../P1_HOST_PROTOCOL.md)。
