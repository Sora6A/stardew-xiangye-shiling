# P1-NET-02 双端报告配对工具验证

日期：2026-10-02，Asia/Shanghai。分支 `feat/p1-game-adapter`，基线 `8f2468d83b43fb805073f73bbc8b989cf716852e`；没有本轮提交、推送、PR 或合并。实际双端未启动/未验收，以下报告正例全部人工构造，不是游戏结果。

## 变更与证据边界

只读会话新增 `LastQuota`，记录实际 TakeQuota 的固定窗口起点/计数/Allowed；被来源/方向/时间筛掉的调用清为 null，第九条超额仍 Count=8/Allowed=false、未读正文。Processed trace 新增本地连接 GUID 与 Quota；SendAttempt 只在实际 SDK 对端引用仍匹配时标记连接，否则 token 为 null，不能假借旧连接。没有新增网络报文字段、设备 schema、依赖或写事务入口。

GameHarness Network 报告升级 schema2，增加首次真实 Ready 的 Baseline（run/session/世代/玩家/线程/peer/序号/丢弃量）、当前库存/raw指纹和引用一致标记、burst发送前序号。标题另写 network-probe-title-result.json，保留最后的 active 报告；标题文件不能用作基础配对。五探针/八回仍只是本地候选，FullNet02Accepted 恒 false。

[check_network_reports.py](../../tools/check_network_reports.py) 只读取两端 schema2，要求独立进程/run、同生产DLL/fixture/session、互为peer、实际Hello发送/HelloAccepted/WelcomeAccepted challenge关系；各端本地连接/世代/线程/时钟分别验证，不跨端强制相等。核对各自 before/current 指纹、完整序号/无丢弃、五个请求的主机产生拒绝/紧邻发送及客户端后续接受。burst要求九次实际客户端发送、八个共同RequestId，以及主机同一实际窗口 Count1–8 的八次读正文与唯一一次 Count8/未读的 RateLimited。不能读取第九条正文给它伪造 RequestId。

输入上限2MiB，读取本身有界，重复JSON键/非有限数/数值溢出/已知字段错类型拒绝。旧schema1缺少基线与窗口，不可补默认值验收。成功摘要只写 BasicProbeEvidenceVerified=true、FullNet02Accepted=false，含输入文件SHA；未知附加字段不影响已知证据要求。输出限工作区.local严格后代，不覆盖源报告；校验拒绝不写新摘要。已经存在的旧摘要仅属于其记录的输入哈希，不能代表本次失败输入或写盘错误。

## 本轮检查

环境：Windows x64、SDK7.0.302/net6.0、Python3.10.9；编译引用本地游戏1.6.15/SMAPI4.4.0。CP2.9.0固定标签构建仍是测试环境；包hash一致不证明实际CP/SDK加载或强认证。

| 命令/检查 | 实际结果 | 本地证据 |
|---|---|---|
| `dotnet run --project tests/MachineCore/MachineCore.csproj -c Release` | 85通过，前轮81＋4实际quota观测场景，退出0 | `.local/p1-network-pair-core-green-2026-10-02.txt` |
| `python -m unittest discover -s tests -v` | 77通过，前轮37＋40人工双端报告专项 | `.local/p1-network-pair-python-green-2026-10-02.txt` |
| `dotnet build tests/GameHarness/GameHarness.csproj -c Release -p:GamePath=D:/code/stardew/.local/validation-game` | 生产/夹具0警告0错误，退出0 | `.local/p1-network-pair-harness-build-2026-10-02.txt` |
| `python tools/build.py --game-path .local/validation-game` | Release0警告0错误，10文件ZIP，退出0 | `.local/p1-network-pair-build-2026-10-02.txt` |
| 实际刷新停止的测试配置并审计 | build/ZIP/runtime/新两端一致，六配置原文禁用保留，旧profiles/六存档准备前后不变，退出0 | `.local/p1-network-pair-package-audit-2026-10-02.json` |
| `python tools/catalog.py` | 40物品/30配方闭合与生成物通过，退出0 | `.local/p1-network-pair-catalog-2026-10-02.txt` |
| `python tools/check_workflow.py` | 维护结构/本地链接通过，退出0，0提交正文检查 | `.local/p1-network-pair-maintenance-2026-10-02.txt` |
| `git diff --check` / 本轮相关未跟踪源码空白搜索 | 已跟踪差异通过、相关源码无行尾空白匹配；已有DESIGN有CRLF归一提示，非失败 | 本机命令输出，未暂存/提交 |

四个 QuotaDiagnosticScenarios 直接执行实际会话：八读/第九不读、筛选清观测/连接更换额度、Hello与坏正文占额度/999→1000边界、外线程拒绝保留owner证据。它们不调用SDK网络。40个报告测试由 synthetic_pair 手工构造，覆盖配对关联/额度、额外回执、独立本地标记、严格JSON与临时工作区CLI；CLI正例不使用真实游戏报告。

文档收尾后的登记、维护与差异实际通过并已登记 HANDOFF，远程CI/保护未核实；维护结构通过不代表提交正文或网络验收。

## BUG-017 实际先红后绿

第一阶段37专项实际3failures、exit1：CLI根路径默认参数早绑定、ReconnectVerified声明未拒、未知附加字段1e999数值溢出未拒。红日志 `.local/bug017-network-report-red-2026-10-02.txt`；三修复后37专项绿，日志同前缀green。不是游戏故障、也不是历史补录。

继续审查后新增具名“缺实际Hello发送”“客户端同窗口Allowed Count重复”回归，第二阶段40专项实际2failures、exit1，日志 `.local/bug017-network-report-second-red-2026-10-02.txt`。再修客户端Hello关联和所有本端窗口计数守恒，最终77全量包含40专项全部绿。Host.Step严格int/MAX+1有界读等附加检查没有独立旧红，不冒称所有40项逐个先红。完整原因、复现、断言与回滚见 [BUG-017](../bugfix/BUG-2026-017-network-report-validation.md)。

## 包/配置完整性

| 对象 | 当前SHA-256 |
|---|---|
| XiangyeShiling.dll | `5ef0df7577255bbc0132e7879fab4f40fda1bcbcad013a291c906a3dbc4b04ac` |
| 开发ZIP | `7907c05e9e54c3d95cfb261ba2653ed574c9f9f2a12759c419e622fc93e05096` |
| GameHarness.dll | `d2f054089f1dedf7a31ed63cadd5c49befbdd4feccf7541dbcbc02fec03db345` |
| 新profiles.json | `cf540bb6c55ef2bcbf069aeceeab5b470e690d2881b984601e50bfb1a6d54e8c` |

实际 `.local/refresh-network-pair-2026-10-02.py` 刷新当前自有两个包与整理好的夹具，准备工具更新新host/client并核真实hash。原有六份config原字节保留，两端主模块/夹具开关false、XYSNET02/独立ID0；runtime仍禁用Memory/XYSP1UI。旧.local/multiplayer未变，源/旧UI/旧Stage3六个存档文件与本次前后及上次审计相同，没有新农场/网络/实际pair结果。

ZIP仅10文件、唯一DLL为主模块，不包含游戏/SDK/Harmony/夹具。当前新DLL尚未实机运行；上轮81/37及881d6bbf…/26556d39…证据保留为 [此前诊断准备](P1-network-probe-2026-10-02.md)，其ZIP备份 `.local/p1-network-pair-before-2026-10-02.zip`。旧Memory/Interaction/Persistence/World20结果不转给新DLL。

## 接续与未执行

建立全新独立XYSNET合作农场/小屋与非零ID、明确两端角色，再按 [NET02准备](../NET02_TEST_SETUP.md) 实际运行并归档active/title报告。对active报告运行配对工具，将五项拒绝/主机额度与两端库存观测一起核对；还需独立文件哈希审计，Saved=false不能证明磁盘未写。机器count0仅证明空可见集合，不等于真实设备保护。

实际报告配对、SDK送达/连接生命周期、16帧预算/128邮箱、版本/分屏、未握手/重试、断连/标题/换世界和远端背包事务尚未实测。基础工具不认证恶意peer、不验收未加载地图或外部副作用，不开放门禁。World20仍是历史7/20中断，P1未完成、P2/P3不开始。

本轮无提交SHA/正文/PR/merge；工作区累计改动保留在任务分支，详见 [HANDOFF](../HANDOFF.md)。
