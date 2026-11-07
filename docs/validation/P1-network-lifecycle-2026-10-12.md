# NET02同ID重连只读观测（2026-10-12）

当前：真实同进程同ID重新握手和主机原世界会话保持已由原双报告严格CLI配对通过；终止后最终双报告仍通过，CIM0/八save不变/六config原字节禁用/85项匹配batch基线/身份预检通过。客户端有收尾标题快照，主机本轮缺此证据，不能扩大标题结论。原反向角色和截止失败独立保留，源码/DLL/既有101核心编译结果不变。Full/旧报文重放/带负载旧队列仍false，P1未总验收、P2冻结。

## 实现与边界

[Scenario](../../tests/GameHarness/NetworkLifecycleScenario.cs)只显式Enabled/Scenario=NetworkLifecycle生效，独立返回，不调用Basic/Memory/世界准备入口。限定Host/Client、XYSNET名称6–20、真实非零ID、多人主屏/非分屏/OpenPreview=false；主模块只读诊断另须绑定启用。模式不发probe/旧报文，不自动加载/操控/冻结时钟/保存；Saving事件标失败，Saved=false仅表示夹具没有调用保存，磁盘仍独立审计。

一次进程最多Host一次SaveLoaded、Client两次：首次Ready→客户端标题（session=null/queue0/peers空）及主机peer间隙→同ID新连接/新challenge/实际新Hello与Welcome。原检查点不被第二次加载清空，重新读取的客户端物品允许重建引用；引用一致仅各load内部，库存值/可见raw及机器数量跨重连仍须保持。5分钟从首次SaveLoaded开始，队列丢弃/trace淘汰/额外加载/错角色世界/指纹改变/超时失败，不拼接新进程。

Lifecycle schema1使用独立唯一network-lifecycle-RunId.json，与Basic schema2不能混用。字段含HarnessRunId/ProcessId/两DLL SHA、实际世界/角色、InitialReady/DisconnectOrTitle/Reconnected/可选ClosingTitle。共享[Evidence](../../tests/GameHarness/NetworkLifecycleEvidence.cs)检查各本端完整序号/单线程/新旧连接及实际握手，再跨端匹配共同session/互为peer/旧新客户端挑战。主机本地challenge、连接GUID、线程、时钟、generation不能跨端要求相等。

只证明同ID重新握手及主机原世界session保持，不证明线上迟到旧Welcome被拒、带负载旧队列/重试/版本/分屏/预算或可写权。OldPacketReplayVerified/LoadedQueueLifecycleVerified/FullNet02Accepted必须false；机器数0只证明空可见集合。

## 编译及工具检查

`p1-net02-lifecycle-core-first-2026-10-12.txt`97项是原89＋8纯判据；新增reader4后，BUG021异常边界实际留红，修后`p1-bug021-core-green-2026-10-12.txt`101项exit0。人工完整pair和各种变异只检共享源码，不是实机证明。CLI对实际人工不完整输入的具名1红1绿见[BUG021](../bugfix/BUG-2026-021-lifecycle-cli-invalid-data.md)，缺字段/别名/重复/未知字段/类型/超2MiB均严格拒绝。

SDK `dotnet build tests/GameHarness/GameHarness.csproj -c Release -p:GamePath=<隔离runtime> -p:BuildProjectReferences=false`exit0/0警告0错误；生产源码/产物不重建。独立CLI `dotnet build tools/NetworkLifecycleCheck/NetworkLifecycleCheck.csproj -c Release`修正所需四纯协议源链接后exit0/0警告0错误，不引用游戏/SMAPI。首版Networking通配符引入未链接事务类型的编译失败另留日志，不算BUG021修前红。

实际离线命令（两个输入只读、JSON仅stdout）：

```powershell
dotnet run --project tools/NetworkLifecycleCheck/NetworkLifecycleCheck.csproj -c Release -- <已归档host报告.json> <已归档client报告.json>
```

严格schema1所有顶层及已存在嵌套结构的字段必须完整，无大小写别名/未知成员/重复或默认能力；最大每输入2MiB。通过仅SameIdRehandshakeObserved/HostWorldSessionPreserved=true、Full/旧报文/负载队列false，附原两SHA；不修改输入、不写输出文件。输入错误安全REJECT/exit1，不输出个人路径堆栈；工具不能证明人工输入确来自SDK，须实际进程/二进制/日志/保存审计。

## 停止基线与部署

分支feat/p1-game-adapter，HEAD8f2468d83b43fb805073f73bbc8b989cf716852e；累计未提交树保留，无本轮commit/push/PR/merge/远程CI。游戏1.6.15 build24356/SMAPI4.4.0/CP2.9.0，专用世界450604963/XYSNET02春24年1、已创建XYSNETClient；旧三组六save保护。

新的`.local/net02-lifecycle-2026-10-12`保留原85项及11源入口before原字节/基线/实际禁用预检。停止部署`p1-net02-lifecycle-deployment-2026-10-12.txt`exit0，恰8处变化：build DLL/PDB、runtime DLL/PDB、curated/host/client DLL及profiles.json；其余77项含六config/八save/生产/ZIP不变。新夹具SHA `f8494eb1ea3f49d05c6b99b98aa26ae7a64fc60e5ca834bd1a0f67d0d833d9cc`，build及四部署处一致，runtime PDB匹配build。profiles仍15包文件/无游戏程序集；实际禁用profile预检通过。

## 实机步骤与收尾

当前检查点维护exit0/0提交正文检查；tracked diff及21本轮未提交源码/文档UTF-8/空白检查通过。独立85项读取审计只有四启用config不同于部署基线，八save/其他81项不变，两实际进程25468/35192、报告0；`handoff-checkpoint-audit.json`及`p1-net02-lifecycle-handoff-text-check-final-2026-10-12.txt`留档。初版私有文本检查把既有Markdown两个尾空格换行误报为失败，保留原文并识别合法换行后另留final；不是产品bug。此时尚无真实InitialReady，后续不能用本检查点代替重连/结束恢复审计。

已执行启用审计`p1-net02-lifecycle-enable-2026-10-12.txt`exit0：恰四角色config变化，完整候选/before原字节在enabled，其余81项含八save不变。新Normal主机25468/客户端35192的独立mods-path已核；`p1-net02-lifecycle-startup-final-2026-10-12.txt`exit0，实际两个归属进程、无Lifecycle报告、八save及其他81项不变，启动两日志/config/85hash在startup。启动日志有Steam未加载成就ERROR，不声称零ERROR；未进入世界，五分钟计时尚未启动。阶段审计初稿用错save路径前缀实际exit1，未生成目录或改文件；私有脚本改正后另留final，不列产品bug/修前红。文档patch一次精确上下文未匹配，整批未应用，核无变化后重新应用；不是产品缺陷。已有一次性snapshot/deploy/enable不能覆盖重跑。

停止状态按新deployment审计核包，备份四config原字节，再临时设主模块probe=true/夹具Enabled=true/Scenario=NetworkLifecycle/OpenPreview=false/原Host与Client/450604963。主持春24、已有客户端加入，等待两InitialReady；仅客户端退出到标题并停至少5秒，主机保持当前世界，核client标题与host Gap。客户端在**同一进程**重新加入同角色，核两Reconnected/原session及新挑战、归档原双报告后离线配对。

最终先client后host返回标题/关闭，不睡觉/保存；CIM0后归档最终报告/日志/config/8save，核专用及旧六save/生产/ZIP不变，恢复四config本轮before原字节、runtime两份不改。唯一已核报告归档后移出Mods，未知文件不删；恢复Network/Disabled后实际只读configure_network_fixture预检。完整个人XML/日志只在.local，不提交。实时过程和下一步见[HANDOFF](../HANDOFF.md)。

## 首轮实际角色反向拒绝与重试

22:07首次加载：host profile/PID25468/声明Host的日志实际Farmhand with 2 players；client profile/PID35192/声明Client实际MainPlayer with 1 player。每份日志头部对应独立mods-path，只有一次spring24加载；两个角色与配置相反已经足以使Scope拒绝。首次初读只看到通用Failure时怀疑回调时序，完整日志后已纠正，未据此放宽门禁、改源码或新增BUG。SMAPI4.4官方[SCore](https://github.com/Pathoschild/SMAPI/blob/4.4.0/src/SMAPI/Framework/SCore.cs)也表明在IsWorldReady分支打印实际角色后才触发SaveLoaded，不将这个怀疑写成已证实根因。

两个真实schema1报告Status=Failed，Failure为Scope拒绝；InitialReady/DisconnectOrTitle/Reconnected/ClosingTitle均null，各能力false。Host SHA `8ed7b4e3c611c654318aa02c6193b4f02e57c5d3b28ec5edbd4914cd6302aca2`，Client SHA `191bb63f3e03505c2eb1b2969a1487818ea825ad517554729124af2430f09c53`；不能由null指纹/count推断库存/实际机器保护。两端原生日志确认返回标题/ClosedGame/Disposing，无Save调用或NRE，各一启动Steam ERROR；这不是诊断API标题清理证明。

`p1-net02-lifecycle-reversed-role-closeout-2026-10-12.txt`实际exit0：CIM0、八save原hash不变、六config原字节禁用、全部85项等于停止部署、唯一两报告归档/hash核验后移出Mods、只读身份预检0。对这两份实际失败报告运行已构建CLI，子进程安全REJECT/1、stdout空，无成功摘要；不属于BUG021缺字段红或真实重连。完整原字节及beforeRestore/afterHashes/源报告身份/日志在after-reversed-role，不覆盖startup及旧Basic。共享核心已有具名`lifecycle scope refuses preview wrong roles worlds and split screens`测试拒绝这类反向角色，本次无代码改动，不为正常拒绝补虚构缺陷回归。

重试`.local/net02-lifecycle-retry-2026-10-12`有独立85项before/baseline，引用原实际deployment而不冒称重新部署；`p1-net02-lifecycle-retry-prepare-2026-10-12.txt`exit0，新四config显式启用、其余81项及八save不变。夹具仍f849、生产ad9/ZIP不变，无新构建。只开Host22444先等待主持并核MainPlayer，再开Client新窗口加入；避免同时展示两个相同窗口而把实际角色与配置倒置。重试尚无报告/重连证据，收尾恢复必须使用新轮enabled/before。

## 正确角色重试：真实局部通过，重新加入前截止

Host22444实际MainPlayer、Client20848实际Farmhand，均加载春24，首次Ready两端原报告分别WaitingDisconnect/WaitingTitle、Failure=null；`p1-net02-lifecycle-retry-first-ready-2026-10-12.txt`exit0。两份SHA分别`2866e990940510a92a50bce7e5a25431265e63000120ca8c4a2edd8898bf34d3`与`686dcccfef66e306f6c9605d4f71ce14cbe762f4fd6f2aa05080785ab7920ea8`；首次阶段归档两日志/config/85hash，八save及其他81项不变。此阶段只读握手，没有probe/物品操作或保存，不能沿用Basic探针结论当本轮行为。

Client在原标题仍保留进程20848；原报告捕获标题worldgen3/session=null/peers空/queue0，host捕获原worldgen2/session保留、disconnectgen1/peers空。主机首次SaveLoaded22:14:24，五分钟截止22:19:24；下一次读取22:19:43已Status=Failed、Failure为截止，Initial/Gap都留存，Reconnected=null。阶段归档命令`p1-net02-lifecycle-retry-gap-2026-10-12.txt`实际exit1，因host Failed拒绝，未生成gap目录；不能将它标为阶段通过或去掉Failure。客户端原Status=WaitingReconnect/Failure=null，两个端点均无Reconnected。分步回复等待消耗了本轮时限；不是协议bug，不变更已声明上限、不救活失败run。

`p1-net02-lifecycle-retry-timeout-closeout-2026-10-12.txt`exit0：两端返回标题/ClosedGame/Disposing/CIM0，八save原字节不变，六config恢复本轮before禁用，85项等于停止部署，两个唯一报告原字节归档/hash核验后移出Mods，身份预检0。两日志各一Steam启动ERROR，无NRE/Save调用。final host SHA `8ffc78f9623ea98555d90dbe486fe357cf2c7d205f46f23ee2cb1935c3241103`，client `aa50b9dcd0a8505bdca052971f66d7e4aab0c42ae79e608e5dff4990bfcc0960`；原双报告通过公共CLI实际REJECT/1/stdout空，拒绝输出另留after-timeout，不覆盖first-ready。

独立只读检查`p1-net02-lifecycle-retry-actual-partial-gates-2026-10-12.txt`exit0：本地无SDK inspector引用当前实际NetworkLifecycleCheck.dll，从上述真实原报告严格reader读入，执行同一共享HostGap/ClientTitle判据，均true，PairMatches仍false；原两SHA匹配归档。它不修改报告或计入101人工单元测试，源码/命令在`.local/lifecycle-checkpoint-inspector`，数据/编译产物不提交。这里只证明实际空队列局部生命周期，不证明负载旧队列/旧Welcome重放或完成重连。

新batch `.local/net02-lifecycle-batch-2026-10-12`引用retry after-timeout final-audit，以全85项重新before备份；`p1-net02-lifecycle-batch-prepare-2026-10-12.txt`exit0，四config显式启用、八save及其他81项不变、无新构建或部署。先只启动Host核角色，后让新Client一次完成加入/停10秒/标题停5秒/原窗口重新加入/停10秒，无中间等待人工回复；观察器原检查点保留，最终原双报告/日志/外部存档审计统一核验。不提前声称新轮已执行或通过。

文档段落更新脚本曾因插入计划的段落前还有对应设计行、及再次运行时旧前缀已变而assert退出，部分较早文档已更新，后续检查点脚本明确核对已写新段后完成；没有生产源码/保存/config变更。失败未包装成bug回归，最终维护另核。

## Batch实际同进程重连通过与最终收尾

本轮源码/DLL未改：生产ad9、夹具f849，101核心/SDK/CLI构建仍为实现阶段结果。先启动Host36608，22:27:09实际MainPlayer/春24/首次SaveLoaded，原报告WaitingInitialReady/Failure=null；新Client14768随后启动并一次连续完成加入→标题→原窗口重新加入，未等待中间逐步回复。主机未重新加载，客户端两次加载都在同一进程14768、同一HarnessRunId，原Host世界/会话不换。

| 原生日志时点 | 本轮实际输入/观测 |
|---|---|
| 22:28:08 | Client首次Farmhand加载；两端First actual Ready recorded |
| 22:28:26 | Client原标题，夹具记录清会话并保留历史 |
| 22:28:27 | Host peer gap，原世界session保持 |
| 22:28:45 | Client第二次Farmhand加载；两端同ID新连接/实际新握手Completed |
| 22:30:29 | Client收尾原标题，额外ClosingTitle快照 |
| 22:30:37 / 22:30:41 | Client / Host ClosedGame、Disposing，之后CIM0 |

22:28:45早于主机22:32:09截止，没有调整计时或恢复失败Run。中途`p1-net02-lifecycle-batch-gap-2026-10-12.txt`实际exit1，因阶段读取时已Completed而拒绝，未生成gap目录；本轮没有独立first-ready/gap外部报告归档。原唯一最终报告自身保留InitialReady/DisconnectOrTitle/Reconnected三个SDK检查点及实际trace，后续校验绑定同一HarnessRunId/ProcessId/protocol RunId/session/generation/connection，未从旧轮拼接。

`p1-net02-lifecycle-batch-reconnected-2026-10-12.txt`exit0，在reconnected保留原双报告/归属日志/config/85hash。两原Status=Completed/Failure=null，Saved/ProbeSent/Full/OldPacketReplay/LoadedQueue均false；各本端库存值/raw跨重连一致，引用在每load内一致，Host加载计数1/1/1、Client1/1/2。客户端原生重载可能重建Item对象，不能要求不同load引用相同；两个端点VisibleMachineCount均0。

公共严格CLI实际原双报告命令及输出见`.local/p1-net02-lifecycle-batch-real-pair-2026-10-12.txt`，exit0：SameIdRehandshakeObserved=true、HostWorldSessionPreserved=true、Full/OldPacketReplay/LoadedQueue=false。输入Host SHA `d4b302fb38cb0c08e7f16d90699e5e910e5ffbcd4a383f042076ff5ec5bceca4`、Client SHA `51e003c3a75adcfca36cdd75d3a92fda19de8aacf4548a4b0f416d6316d94044`，摘要与归档原字节匹配。新旧peer ID相同、连接/challenge不同、双方实际新Hello/Welcome挑战一致；Host worldgen2及session保持、disconnectgen0→1，Client原标题worldgen3后新Ready worldgen5。整个trace无probe/淘汰/队列拒绝，队列为空，不证明旧带负载回调丢弃。

`p1-net02-lifecycle-batch-closeout-2026-10-12.txt`exit0：两个进程均0，八save原hash仍春24，六config恢复batch before原字节禁用，85项全等于新baseline、生产/ZIP不变、实际禁用身份预检0。原三SDK检查点及其他声明在终止前后逐字段相等；Client仅新增ClosingTitle，worldgen5→6、active/probe=false、session=null/localId0/peers空/queue0，final SHA `182600c1a23e3694a07ad03dd79d9317b9dc0845b28f238a46d823841d12a0b4`。Host最终原字节仍原SHA，但ClosingTitle=null且日志无收尾原标题，因此不计本轮Host标题清理，不用操作者返回标题的描述代替缺失SDK证据。

对after-exit最终双报告再次执行同一公共CLI，exit0，摘要另存final-pair-stdout.json/stderr，不覆盖原通过输入。唯一两原文件在核归档hash后移至after-exit/retired，其他未知文件未删；完整两日志、85项退出snapshot/恢复前后hash、实际preflight/final-audit均在after-exit。两日志各一Steam成就启动ERROR，无NRE或原生Save调用，不能宣称日志零ERROR。原角色反向和超时两轮各自独立收尾，未变为成功。

本次仅一Host与一Client；正常可见gap后同ID重连通过，不覆盖BUG014的两次轮询间无缺席快照、第三位peer保持、旧Welcome真实重放、换世界/带负载队列、16/128预算或强认证/远端提交。Full/P1及P2冻结边界不变。未新增代码或本轮bug，无新单元/构建或远程CI。维护`p1-net02-lifecycle-batch-maintenance-2026-10-12.txt`实际exit0/0提交正文；交付读回`p1-net02-lifecycle-batch-delivery-readback-2026-10-12.txt`实际exit0，再核当前85项/八save/六禁用config/CIM0和原/最终报告哈希，并检查全部12份本轮文档（含未跟踪文件）的UTF-8/空白/冲突标记及tracked差异。无本次commit/push/PR/merge。
