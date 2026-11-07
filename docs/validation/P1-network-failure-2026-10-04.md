# P1-NET-02 真实 Tick 失败路径验证

任务起始于2026-10-04，交接更新至2026-10-05，Asia/Shanghai；本机证据沿用起始日文件名，不覆盖10-02记录。分支 `feat/p1-game-adapter`，基线 `8f2468d83b43fb805073f73bbc8b989cf716852e`，无新提交。本轮运行上轮已部署的夹具780构建，实际验证两种受控失败；没有修改生产/夹具源码、重新构建或重跑85核心/105完整Python。

两个独立主机进程都合作加载既有专用世界 XYSNET02 / 450604963，客户端未启动。读后门禁拒绝保留Protocol，读前Scope拒绝保持Protocol=null，实际报告均落盘并留原字节。两窗口已关闭，游戏/SMAPI为0，六配置恢复原字节且禁用；24项最终哈希全部与本轮before相同，包含八存档、源码、构建、部署、两个profiles及ZIP。首次客户端Farmer、握手/探针/双端配对、标题/重连与完整NET02/P1仍未验收。

本轮是 [10-02观测扩展](P1-network-observation-2026-10-02.md) 的实际集成验证，没有新增生产缺陷、修复或BUG编号。此前“新构建未实机运行”的记录只描述当时，不改写成当时已经通过。现场和接手步骤见 [HANDOFF](../HANDOFF.md)。

## 环境、前置条件与复跑

Windows x64，隔离游戏1.6.15 / SMAPI4.4.0 / CP2.9.0，Python3.10.9；生产DLL `5ef0df7577255bbc0132e7879fab4f40fda1bcbcad013a291c906a3dbc4b04ac`、夹具DLL `78026dd6a3f703fcb26bef41590289dab8505ae7621ab4813503666787582693`。DLL来自此前已核Release，运行日志确认4个模组加载和专用合作世界；本轮没有升级依赖或改源安装。Saves/偏好/SMAPI日志仍共享普通用户目录，只有两端Mods隔离。

可复跑步骤如下；每次使用新的本地证据目录、实际PID和报告SHA，不复用本轮一次性脚本输出：

1. 核对任务分支和累计修改，确认目标游戏进程停止。保留专用两XML、旧六保存、六配置和当前包/清单的原字节与哈希；用正式身份工具只读预检450604963及14包。主机和客户端原配置均应禁用，角色仍Host/Client，OpenPreview=false，不创建另一个世界。
2. 读后负例：只把host的GameHarness.Enabled临时改为true，保留Network/Host及专用身份，生产EnableReadOnlyNetworkProbes保持false。通过可见游戏窗口合作→主持→XYSNET02加载；无需客户端，也不等待Ready。SaveLoaded启动观察，Tick通过Scope与API读取后因ProbeConfigured/ProbeAllowed=false失败，catch应写非空Protocol。保持报告Step0及Failure，不能视为成功probe。
3. 归档原报告和日志，实际检查Failure与快照，关闭测试窗口并核进程0。本轮Escape两次未打开菜单，使用窗口X正常关闭；没有返回标题，不宣称标题回调已执行，也不睡觉或保存。
4. 读前负例：另一个新进程启动前，把同一host夹具的NetworkRole临时改为Observe，保持Enabled=true、Network及真实世界身份，生产探针仍false。这是刻意误配，不是合法Host或真实Client。合作加载同一专用农场，Scope先拒绝，尚未获取API，Protocol应为null。两次新进程不是同进程重连/跨Tick旧观测测试。
5. 归档第二份报告和日志后关闭窗口，确认进程0。先核本轮报告已留存，再将已知报告移出Mods，恢复host夹具的before原字节。不要自动清理未知文件；正式身份预检只在恢复正确角色和禁用配置后运行。比较全部24项并核两XML/六旧保存，没有保存动作也不能省略磁盘审计。

Network Entry直接注册SaveLoaded/UpdateTicked后return，不进入Memory/Persistence准备；两条拒绝均早于baseline指纹采集和RunClient，未调用TrySendNetworkProbe/Execute/可写事务。生产探针开关false仅关闭额外probe/trace，普通只读桥仍存在，不能泛称全部网络或世界写入关闭。

## 已执行断言与结果

本地 `.local/check-net02-failure-report-2026-10-04.py` 读取真实报告，断言后按原字节归档；既有 `tools/check_network_reports.py` 实际用于第一份失败报告的端点拒绝。它们没有生成双端成功摘要。

| 场景 | 实际命令、报告与断言 |
|---|---|
| 读后门禁失败 | `python .local/check-net02-failure-report-2026-10-04.py after-read --pid 52332`，退出0；日志 `.local/p1-network-failure-after-read-2026-10-04.txt`。真实合作主机Scope通过并访问MachineService API，Failure精确为 `Main module probe gate is closed or diagnostic session is faulted.`；Protocol非null、Active/IsHost=true，ProbeConfigured/Allowed/Faulted/WritesEnabled=false，Trace/Peers为空、LastSequence/QueueCount/Evicted为0 |
| 实际失败端点拒绝 | 同一命令对归档前的原报告执行 `check_network_reports.endpoint(report, "Host")`，实际ValueError为 `Failed or non-synthetic report`；此输入TargetIsSynthetic=true，所以是Failure拒绝。保留Protocol没有抵消Failure，未执行validate_pair或双端CLI |
| 读前Scope失败 | `python .local/check-net02-failure-report-2026-10-04.py before-read --pid 39000`，退出0；日志 `.local/p1-network-failure-before-read-2026-10-04.txt`。临时Observe在同一专用合作世界的Scope校验被拒绝，Failure精确为 `Network probe requires the configured independent XYSNET world and host/client role.`，Protocol=null。此场景没有执行端点Failure拒绝检查，不能借第一场景结果替代 |
| 结束文件审计 | `python .local/audit-net02-failure-2026-10-04.py`，退出0；日志 `.local/p1-network-failure-final-audit-2026-10-04.txt`。24项before/after全部相同、六配置原字节恢复且关闭、八save未变，正式身份/14包预检通过，无network报告留在host Mods |

两报告都为schema2、Episode1/Step0、Passed/ProbeCases/BurstRequestIds空、Basic/Full/Reconnect/Saved/ReturnedToTitle=false，Baseline与前后指纹null、InventoryReferencesUnchanged=false。这是尚未进入指纹阶段的默认值，BaselineVisibleMachineCount=0不能解释为实测没有机器或库存未变；磁盘不变依据独立审计。第一份Protocol包含有效主机/会话信息，不证明Ready或连接了客户端。

第一份报告SHA为 `b757f8d86b6fa5157fca22c458f96f3e6f2f51a80a5a01564f55b398ed2db703`，HarnessRunId为 `830b4a97-374a-4f3b-a6c9-7ff90d0b4555`；第二份SHA为 `731c967dfde0f4f9d5cd722b2067fa8d85b75dd8e5c63e01166ece079aaaff6e`，HarnessRunId为 `31741765-a65c-4037-a439-6b4f11b2153b`。分别归档在 `.local/net02-failure-2026-10-04/after-read`、before-read；第二份Mods原文件在核归档后移到before-read/retired-network-probe-result.json，哈希相同。日志完整留本地，不上传个人路径、存档或截图。

独立只读复核覆盖原报告/active-check哈希、两实际分支边界及最终文件审计，直接重算当前24项均与before/after一致；审阅代理没有启动游戏、修改文件或重跑一次性脚本。`python tools/check_workflow.py`实际退出0、维护结构/本地链接通过、0提交正文检查，日志`.local/p1-network-failure-maintenance-2026-10-04.txt`；`git diff --check`退出0，既有DESIGN CRLF提示非失败。交付前CIM仍0、分支/HEAD未变，CI和远程保护未经读取。

## 审计、回滚与验收边界

进入前的24项原字节/哈希在 `.local/net02-failure-2026-10-04/before` 和before-hashes.json，结束证据为final-audit.json。六配置、八save、NetworkScenario源、生产/build夹具、四部署夹具、旧/当前profiles、ZIP全部相同，changedKeys为空。profiles仍为 `9a4aa669b3413bd3ebdd3cf38b4c71d87c66e66fcb6f8f23c8a5ee1f78bc861c`，ZIP仍为 `7907c05e9e54c3d95cfb261ba2653ed574c9f9f2a12759c419e622fc93e05096`；本轮不重跑prepare_multiplayer或复制DLL。

如测试途中中断，先停止实际目标进程，核当前报告归档，再恢复本轮before/config/host/GameHarness/config.json；其他配置若出现来源不明变化须调查，不能一并盲恢复。存档和源码不需要回滚，本轮未写它们。报告、备份和失败日志保留，已有一次性脚本拒绝覆盖；恢复后重新只读核包/配置/存档。不可恢复运行中的内存，不能用旧PID/窗口继续。

本轮实际覆盖的是Scope拒绝前未读取，以及正常读取后门禁Require抛错并由catch落盘。没有触发ReadNetworkDiagnostics内部抛错、GetApi缺失、Faulted=true、OutOfMemory或I/O失败；不能扩大为所有异常路径已经通过。两新进程也不验证同进程跨Tick/重连无旧快照借用，ReturnedToTitle未执行。报告写盘的一般失败风险、快照可能早于后续发送/异常的时间边界仍在。

客户端首次创建、专用准备保存、真实握手/五拒绝/九发八回/配对、标题/重连/换世界/错版分屏/SDK预算及两端指纹仍待。NET03/04的可信库存所有权和可写提交未实现，联机加工/放置拒绝继续保留，有效Idle原生移除边界不变。P1未通过，不扩展P2。

本轮无提交/推送/PR/merge/新SHA，单元测试85/105和10-02具名人工测试仍属各自历史阶段，未伪称本次重跑。下一步按 [NET02准备](../NET02_TEST_SETUP.md) 与 [HANDOFF](../HANDOFF.md) 在禁用状态继续首次客户端；真实网络运行和本轮故意失败验证必须分开归档。规范见 [CONTRIBUTING](../../CONTRIBUTING.md)。
