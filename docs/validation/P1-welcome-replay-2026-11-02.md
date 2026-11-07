# P1-NET-02：旧Welcome受控重放

日期：2026-11-02，Asia/Shanghai。当前实现、112项核心、生产/SDK夹具/两CLI构建、人工CLI正负例及停止部署通过；**真实两端只完成首次Ready，主机在客户端返回标题前截止，未发送旧Welcome，OldWelcomeReplayRejected未验收，Full/P1未验收，P2冻结**。两端已关闭、八save不变、六config恢复禁用、85项保持部署版、报告移出后实际预检通过。10-12正常重连保留原证据，不能代替新DLL或主动重放。

## 目标与能力边界

先在各自同一进程观察首次Ready→客户端原标题/主机gap→同ID新Ready，冻结三个原检查点；再由主机将上一连接真实已发送的Welcome原字节向当前新连接重发一次。客户端须先留下新握手成立的BeforeReplay，随后实际读取旧challenge并记录WelcomeRejected，当前session/Ready/连接、库存值/每load引用及可见设备raw保持。

只验证**新Ready之后的旧Welcome**。未Ready前的拒绝只在纯会话回归覆盖；其他旧报文、无缺席快照的换连接、第三位peer保持、负载队列、16每Tick/128邮箱、强认证、真实远端库存和写入仍待。CLI仅输出精确子集`OldWelcomeReplayRejected=true`，广义`OldPacketReplayVerified`、`LoadedQueueLifecycleVerified`和`FullNet02Accepted`仍false。机器数0不证明实际已放置机器保护。

## 实现与不变量

- `src/XiangyeShiling/Networking/WelcomeReplayBuffer.cs`只保存当前主机世界内、真实发送成功返回后的相邻两次连接Welcome，最多32个peer；同连接重试不更新一次额度，同连接更换challenge会丢弃来源链。世界/标题/故障Reset丢弃整个实例。无磁盘载入或任意JSON入口。
- `MachineService.TryReplayPreviousNetworkWelcome(Guid expectedConnectionId)`仅主线程、显式开启诊断、精确专用世界/主机、唯一当前Ready客户端可用；参数仅防快照过时，不选payload/peer/actor/session。每新连接取用一次，即使发送失败也不重试，与原探针共享16次/秒预算。返回true只表示发送尝试。
- `ReadOnlyProtocolSession`准入不变；格式合法但不符合当前握手的Welcome增加`WelcomeRejected`及实际challenge诊断，不更换session/Ready或清空LastRejection。非法载荷仍InvalidMessage，未读限流仍无challenge。
- `ProtocolReceiveKind.WelcomeRejected`、`ProtocolTraceKind.WelcomeReplayAttempt`仅追加诊断枚举；线上格式和设备schema、物品/配方/依赖不变。
- `NetworkWelcomeReplayScenario`为独立主动模式，复用被动观察但不另写被动最终文件；嵌套Lifecycle冻结在重放前，声明ProbeSent=false仅指该冻结区间。主机新Ready后等待两秒再重放一次；客户端若没捕获Before，不补造、不拼接。五分钟从首次SaveLoaded开始，不自动加载、操作玩家、冻结时钟或保存。
- `NetworkWelcomeReplayEvidence`同时被SDK夹具、核心场景和CLI编译。检查原双Lifecycle关联、当前连接/挑战、两端trace完整前缀、主机两条尝试/发送及客户端一条真实读取拒绝、前后状态与指纹不变。被动Healthy明确拒绝主动重放trace。

## 本地检查与原始记录

本轮在`feat/p1-game-adapter`，基线HEAD仍`8f2468d83b43fb805073f73bbc8b989cf716852e`，累计未提交来源保留。没有新commit/push/PR/merge或远程CI。

| 检查/命令 | 实际结果与日志（均在.local） |
|---|---|
| 恢复旧停止现场与新before快照 | `p1-net02-replay-resume-readback-2026-11-02.txt`及`p1-net02-welcome-replay-before-2026-11-02.txt` exit0；85项一致、八save不变、六config禁用、CIM0；另保存129份累计源码/文档原字节 |
| `dotnet run --project tests/MachineCore/MachineCore.csproj -c Release` | `p1-net02-welcome-replay-core-2026-11-02.txt` exit0，112项（101＋11）；首次编译日志`...core-first...`为测试调用方名称拼写错误，修正后通过，非协议缺陷或修复前红证据 |
| `python tools/build.py --game-path .local/validation-game` | `p1-net02-welcome-replay-production-build-2026-11-02.txt` exit0；生产Release 0警告/0错误，开发ZIP更新 |
| `dotnet build tests/GameHarness/GameHarness.csproj -c Release -p:GamePath=D:/code/stardew/.local/validation-game -p:BuildProjectReferences=false` | `p1-net02-welcome-replay-harness-build-2026-11-02.txt` exit0，0警告/0错误 |
| `dotnet build tools/NetworkWelcomeReplayCheck/NetworkWelcomeReplayCheck.csproj -c Release`及原NetworkLifecycleCheck | 新`...cli-build...`/原`...passive-cli-build...`日志exit0，各0警告/0错误 |
| 实际新CLI人工输入进程验证 | `p1-net02-welcome-replay-cli-synthetic-2026-11-02.txt` exit0；完整人工pair0，同角色/缺字段/重复字段/超2MiB四例均REJECT/1/stdout空，原人工输入hash不变；不是SDK证据 |
| 停止部署/实际身份预检 | `p1-net02-welcome-replay-deployment-2026-11-02.txt` exit0；85项恰16项构建/部署/包变化，八save及六禁用config原字节不变；实际preflight0 |

11项新核心场景在`tests/MachineCore/WelcomeReplayScenarios.cs`，名称和逐项结果见原日志：覆盖旧原字节一次取用、过时/错角色/未Ready拒绝、来源/世界边界、32项/线程约束、握手前后旧挑战读取拒绝与会话/LastRejection保持、非法/未知来源/限流不伪造读取、完整人工配对、错误challenge/连接/读取/发送拒绝、状态变更/拼接/能力冒称、严格reader正负例。只测纯逻辑，不能以人工报告替代实际网络。

公共Python未改，116项为10-12历史结果，本轮未重复运行。未发现需新增bugfix记录的协议行为缺陷；这是可观测性及独立验证入口扩展，不能将普通编译迭代记为产品bug红绿。

## 部署与本地资源

独立`.local/validation-game`和`.local/net02-profiles/{host,client}`；源安装未改。游戏/SMAPI/CP引用保持原1.6.15/4.4.0/2.9.0，没有升级依赖。新生产DLL SHA `d715ac09ec89f55ee1a2091bb4c1fc9b227b1056bf41fbc8bc3916785b216608`，夹具DLL SHA `72da6aad2435b9373d7f2f21bd544ecab57048ca46eccee1c6eb233077c89b7e`，alpha2 ZIP SHA `2fd97bb3484c1b2afa41075c6c5a1cd60de709628644098b0c0ffcd7e053bd3b`。

本轮`.local/net02-welcome-replay-2026-11-02`的baseline/before/before-source保存原85项及源码；deployment/audit.json保存部署后hash与实际preflight。当前六份配置已恢复原字节禁用，进程0，八save仍与春24准备基线相同；最新审计是after-exit/after-retirement-audit.json，原final-audit保留首次预检拒绝。一次性snapshot/deploy/enable/closeout均已执行，禁止覆盖重跑。人工报告在cli-synthetic，和SDK报告分开，原个人XML/日志均不得提交。

## 实机步骤与严格配对

停止进程并核本轮deployment85项后，备份四角色config原字节；只对host/client的主模块设EnableReadOnlyNetworkProbes=true，夹具设Enabled=true/Scenario=NetworkWelcomeReplay/OpenPreview=false，保持原Host/Client/XYSNET02/450604963。runtime两份仍禁用。先启动并主持Host，核原生日志MainPlayer再开Client加入已有XYSNETClient；客户端加入后等10秒，原标题停5秒，同一窗口重新加入同角色再等10秒。主机始终保持原世界，不操作物品、不睡觉或保存。

两个唯一`network-welcome-replay-<HarnessRunId>.json`完成后，绑定实际PID、mods-path、DLL、原生日志/加载次数、存档与config来源，归档原字节再运行：

```powershell
dotnet run --project tools/NetworkWelcomeReplayCheck/NetworkWelcomeReplayCheck.csproj -c Release -- <归档host.json> <归档client.json>
```

工具不加载SDK，只读两个2MiB有界文件，所有层级完整字段/重复/大小写别名/未知成员严格校验；拒绝exit1且stdout无摘要，通过exit0的摘要含原两SHA。不鉴别手写JSON真伪，仍需独立真实现场审计。本轮原SDK报告未完成，实际CLI安全拒绝1，不得填写重放成功。

结束先客户端再主机返回标题并关闭，核CIM0/八save原字节不变，归档原报告/日志/四config/85项退出snapshot；恢复四config本轮before原字节为Network/Disabled，runtime两份不动；唯一报告先核归档hash再移出Mods，未知文件不删，最后实际身份预检。缺失标题/故障/截止/阶段归档失败如实记录，不能修改原报告补证据。

## 实际首次Ready、截止与关闭审计

Computer Use技能的node_repl/@oai/sky可调用，本轮用鼠标完成合作/主持/加入/已有角色选择。首个隐藏启动3232没有可操作窗口，日志未SaveLoaded，原日志/CIM归档startup-hidden后只终止该进程；随后可见主机18276与客户端17048分别绑定独立mods-path，未更新代码/DLL或放宽判据。

| 原生日志时点 | 实际行为/边界 |
|---|---|
| 23:03:26 | Host18276 MainPlayer加载春24一次，开始五分钟 |
| 23:05:02 | Client17048 Farmhand加载同世界春24一次；两端均记录首次Ready，等待客户端标题/主机断连 |
| 23:08:26 | Host外层报告Failed，Failure=Welcome replay exceeded five minutes.；嵌套生命周期仍WaitingDisconnect |
| 退出时 | Client仍WaitingLifecycle/内层WaitingTitle、Failure=null；没有原标题、Gap、新Ready或Before/AfterReplay，没有重放发送 |

自动Escape/e未打开菜单，显式激活后仍未观察响应；这是本轮自动输入结果，未得到物理键盘对照，不认定玩家无法移动或新的生产输入bug。之后Alt+F4也未观察关闭，鼠标窗口X可用；客户端先最大化以显示完整关闭按钮，随后客户端、主机均通过X退出。截图曾因焦点变化显示其他前台窗口，重新选择/激活后确认游戏才输入，没有向其他应用点击。后续必须重新观测窗口/坐标，不能复用本轮截图。

保留的Host原报告SHA `744a219c16b8deb37e7fd39e24f78712466500729f9ecaa796c7ea01e7dccc7d`，Client SHA `f96ff042109c9095f46c16d1d059ca4e92f9017696618b702c3678cf61798036`。新公共CLI对这两个原字节实际REJECT/exit1/stdout空，after-exit保存拒绝输出，两端原报告/日志/config/85项退出snapshot及retired原文件；没有active成功目录，不修改Failure、拼接旧轮或从人工JSON补检查点。两日志各一次SaveLoaded/无ReturnedToTitle/无原生Save调用、无NRE及ERROR行，均有ClosedGame；不把窗口X退出当标题清理。

`p1-net02-welcome-replay-closeout-2026-11-02.txt`实际exit1：报告归档、四config恢复、八save不变、85项等于停止部署均已完成；私有收尾脚本先预检后移报告，身份工具因新报告仍在Mods作为未知文件而拒绝。它保护了未知文件边界，非协议bug/修复前红；原final-audit.profilePreflightPassed=false保留，不能改为成功。原两文件随后已在核归档hash后移入retired，未删未知文件。

`p1-net02-welcome-replay-closeout-after-retirement-2026-11-02.txt`实际exit0：独立只读补核CIM0/85项匹配deployment/八save不变/六config原字节禁用/原报告及retired哈希相同/拒绝无摘要，且**移出报告后实际身份预检0**；补充after-retirement-audit.json和preflight输出，不覆盖首次失败。以此为下一轮停止基线，新一轮必须另建目录/备份，不重新执行本轮one-shot。

CI配置增加两个无SDK CLI构建命令；本地同命令已通过，不表示远程CI已运行或分支保护已配置。`p1-net02-welcome-replay-maintenance-2026-11-02.txt`实际exit0，结构/链接通过、0提交正文检查；`p1-net02-welcome-replay-delivery-2026-11-02.txt`实际exit0，26项本轮新增/修改源码/文档/CI（含未跟踪文件）UTF-8/空白/冲突标记通过，tracked diff --check0，分支/HEAD不变、85项当前读回/CIM0匹配收尾。完整路径清单在delivery-text-audit.json，不重复构建或单元测试；本轮未提交/推送/PR/merge。

## 风险、回退与交接

正常模式所有探针仍默认禁用，联机交易继续拒绝。新增诊断解析发生在原8读/秒quota之后，载荷仍受原codec限额；内存只留最多32个peer的两份受限本机Welcome，Reset清除，不持久化。

需要回退时先关测试进程、恢复本轮四config原字节；从本轮before恢复已知旧生产/夹具DLL及匹配PDB、ZIP/profile元数据，逐项核85hash与只读预检。不要回滚或删除玩家物品、不要用春23覆盖春24。源码回退依据before-source逐项处理本轮增量，保留此前累计树，禁止git reset/clean。当前后续状态见[HANDOFF](../HANDOFF.md)；契约见[协议](../P1_HOST_PROTOCOL.md)、操作见[夹具README](../../tests/GameHarness/README.md)。
