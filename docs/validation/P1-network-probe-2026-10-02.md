# P1-NET-02 双端诊断与配置准备验证

本页是同日早期81核心/37Python及881d6bbf…/26556d39…准备证据；当前schema2/配对增补后的85核心/77Python与新hash见 [后续验证](P1-network-pair-2026-10-02.md)。保留本页原结果，不用旧构建替新DLL验收。

日期：2026-10-02，Asia/Shanghai；任务跨 10-01/10-02。分支 `feat/p1-game-adapter`，基线 `8f2468d83b43fb805073f73bbc8b989cf716852e`。本记录仅证明代码、构建、文件准备与完整性检查，实际双客户端未启动，NET-02/P1 均未验收。

## 本轮实现

生产只读桥新增真实接收结果观测、owner 线程的 256 条有界 trace、连接/会话快照和默认关闭的固定探针入口。GetApi 保持返回 MachineService，新增 ReadNetworkDiagnostics/TrySendNetworkProbe；读写规则与提交器没有接入此桥。

GameHarness 新增默认禁用的 Network 模式，只在匹配 `XYSNET` 专用农场、非零独立世界 ID、Host/Client 角色和两个显式开关下运行。它不自动加载/创建/保存，不注入设备/材料，不改世界或背包。固定 synthetic Start/Collect、actor/session 负例和 malformed 请求不定位真实设备，合法请求仍要求 WritesDisabled。记录两端各自库存值/引用、可见设备 raw 的指纹，空设备集合如实记录数量 0。

客户端共享证据判据绑定 run/session/world/disconnect generation、唯一 Ready 主机/ConnectionId、线程、Type、实际读正文尝试与发送之后的拒绝；不完整或跨连接证据不通过。五项拒绝和九发八回仅能产生 BasicProbesCompleted 候选，主机观察端不自行通过；FullNet02Accepted 恒 false。完整范围见 [准备说明](../NET02_TEST_SETUP.md) 与 [主机契约](../P1_HOST_PROTOCOL.md)。

配置工具新增显式输出/可选整理后的夹具、两端写前预检、角色配置原文保留与 schemaVersion 2 实际文件审计。全预检不是 I/O 事务，SDK 实际二进制身份不是清单校验的结论，runtimeBinaryVersionVerified=false。

## 自动检查与构建

环境：Windows x64，SDK 7.0.302/net6.0，Python 3.10.9；生产/夹具引用本地测试游戏 1.6.15、SMAPI 4.4.0、CP 2.9.0 固定标签构建。没有新依赖、schema 或 catalog 变更。

| 命令 | 本轮实际结果 | 本地证据 |
|---|---|---|
| `dotnet run --project tests/MachineCore/MachineCore.csproj -c Release` | 81 项通过，退出 0 | `.local/p1-network-probe-core-green-2026-10-02.txt` |
| `python -m unittest discover -s tests -v` | 37 项通过，退出 0 | `.local/p1-network-probe-python-green-2026-10-02.txt` |
| `dotnet build tests/GameHarness/GameHarness.csproj -c Release -p:GamePath=D:/code/stardew/.local/validation-game` | 生产及夹具 0 警告/0 错误，退出 0 | `.local/p1-network-probe-harness-build-2026-10-02.txt` |
| `python tools/build.py --game-path .local/validation-game` | 生产 Release 0 警告/0 错误，10 文件开发 ZIP，退出 0 | `.local/p1-network-probe-build-2026-10-02.txt` |
| `python tools/catalog.py` | 40 物品/30 配方闭合、清单/生成本地化通过，退出 0 | `.local/p1-network-probe-catalog-2026-10-02.txt` |
| `python tools/check_workflow.py` | 维护结构/本地链接通过，0 提交正文检查，退出 0；不是远程 CI | `.local/p1-network-probe-maintenance-2026-10-02.txt` |
| `git diff --check` | 已跟踪差异检查通过；已有 DESIGN 的 CRLF 归一提示，不是失败 | 本地命令输出，未执行暂存/提交 |

81 项为前轮 68＋6 ProbePlan＋3 ProbeEvidence＋4 Diagnostic；37 项为前轮 26＋11 配置工具。Diagnostic 测试直接验证会话观测与真实委托调用次数，但没有调用实际 SMAPI transport。文档收尾后的登记/维护/差异检查实际通过并登记 HANDOFF；维护检查不等于远程 CI 或提交正文已检查。

## 修复前后证据

- [BUG-015](../bugfix/BUG-2026-015-multiplayer-profile-preflight.md)：先在旧实际工具执行两个临时目录回归，退出 1、1 failure/1 error；修复后 11 项绿，包含 I/O 失败仍可能单侧更新且旧审计不能代表当前一致的负例。日志 `bug015-profiles-red/green-2026-10-01.txt`。
- [BUG-016](../bugfix/BUG-2026-016-network-probe-evidence.md)：提取实际弱夹具判据后加入旧世界 null-ID 回归，先红退出 -532462766；修复后共享判据三项与最终 81 项绿。红日志 `bug016-probe-evidence-red-2026-10-01.txt`，没有游戏旧版先红证据。
- [BUG-014](../bugfix/BUG-2026-014-readonly-reconnect.md) 仍是历史补录；不把本轮红/绿归到它，也不改写其缺少原始修前日志的事实。

## 实际文件准备与哈希

构建/包/已停止测试 runtime 的两项目文件及新 `.local/net02-profiles/host/Mods`、`client/Mods` 一致。夹具来自整理后的 `.local/net02-probe-harness`，仅 manifest、GameHarness.dll、GameHarness.deps.json；不把完整构建目录的第三方 DLL/PDB 混入。根任务实际两次运行准备 CLI，第二次核验角色配置原文保留；旧 `.local/multiplayer` 全部文件保持原状。

| 对象 | 实际 SHA-256 |
|---|---|
| 生产 XiangyeShiling.dll | `881d6bbfb3df022e27a6eb158adac388261a7d343b41cd866f4113372dcbd77a` |
| `artifacts/XiangyeShiling-0.1.0-alpha.2-dev.zip` | `26556d3904cbc942cdecf046ab1393dfc0e3a7f9a2c6b4b6b44c351a8b712d87` |
| 本地 GameHarness.dll | `6f11dcbea0ccf812e328c74777b93d80e4f1eac78298e12624a811b21433819a` |
| 新 profiles.json | `6bec6f8c1e96e1c13f423635088f35517822cb7c3a5025865e0785a4d8d3fab5` |

两端 GameHarness.Enabled/OpenPreview=false，Scenario=Network，NetworkRole 分别 Host/Client；主模块 EnableReadOnlyNetworkProbes=false。两处专用农场名为 XYSNET02、世界 ID=0，因此没有可误启用的完整身份。runtime 原有 Memory/XYSP1UI/8624092802 禁用配置原文保留，默认主模块探针关闭。启动脚本已生成但未运行，没有创建或安装 NET02 农场。

实际审计脚本 `.local/prepare-and-audit-network-probe-2026-10-02.py` 退出 0，证据 `.local/p1-network-probe-package-audit-2026-10-02.json`。核验源/旧 UI/旧 Stage3 各两个存档文件准备前后哈希相等；源与 UI 还匹配已有基线。Stage3 仅证明这次准备前后未变，不追溯声称与 09-28 原始基线相同。完整存档路径/hash 留本机，公开文档不含存档原文或完整日志。

上一轮 4513c411… DLL / c73726a2… ZIP 是 [10-01 只读桥历史验证](P1-readonly-transport-2026-10-01.md)。旧 ZIP 另存 `.local/p1-network-probe-before-2026-10-02.zip`，原证据不改写；此前 World UI/Memory/Persistence 的游戏通过项不能借给本次 DLL。

## 未执行与接续

实际双端 transport、两端玩家库存核对、九条同主机窗口中的八次读正文/一次 RateLimited、16 帧消费/128 邮箱、握手重试、版本/分屏排除、连接替换、旧排队消息及返回标题/换世界尚未实测。客户端八回并不能证明主机未读第九条，指纹也不覆盖未加载地图或未知外部副作用。

下一步使用全新独立 XYSNET 合作农场及小屋，明确非零游戏 ID，再按 [NET02 准备](../NET02_TEST_SETUP.md) 同步两端主模块/夹具身份、开启测试、归档各进程报告并核对主机 trace。不复用源档、旧 UI 或 Persistence Stage3，不清旧 marker。本轮查询游戏/SMAPI 为 0，旧 World20 仍停在历史 7/20，内存现场不能恢复。

本轮未 fetch、提交、推送、PR 或合并；没有新提交 SHA/完整正文/远程成功状态，远程 CI/保护未核实。全部累计改动仍在任务分支工作区，详见 [HANDOFF](../HANDOFF.md)。
