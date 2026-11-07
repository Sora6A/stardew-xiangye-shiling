# P1 主机请求核心与只读传输契约

2026-11-02新增独立旧Welcome诊断入口，线上格式和机器写入禁用不变。112核心/构建/人工CLI正负例及停止部署通过，真实重放仍待，见[本轮记录](validation/P1-welcome-replay-2026-11-02.md)。`WelcomeReplayBuffer`只保留本进程当前主机世界已真实发送的相邻两连接Welcome；`TryReplayPreviousNetworkWelcome(expectedConnectionId)`要求专用世界、主线程、唯一新Ready客户端、一次取用及共享探针预算，不接收payload或目标。Reset清内存，无持久化字段。

WelcomeRejected只表示实际读取了合法Welcome后本地未准入，保留其challenge用于观测；不是网络拒绝回复。WelcomeReplayAttempt只表示固定旧载荷尝试，必须紧接当前连接的SendAttempt并由客户端实际读取拒绝配对；新枚举追加，原数值不变。独立schema1重放报告嵌套重放前三检查点，冻结区间的ProbeSent=false不外推到主动阶段。严格CLI只计算OldWelcomeReplayRejected，广义OldPacketReplayVerified/Full仍false。

2026-10-12进展：真实Basic五probe/同窗口8读1未读限流配对及独立同进程同ID重连配对均已完成。后一轮实际新Hello/Welcome/连接/挑战、主机原world/session保持、库存/raw不变已核，关闭CIM0/保存不变/配置禁用/85项及预检通过，见[生命周期实测](validation/P1-network-lifecycle-2026-10-12.md)。线上格式/可写禁用契约不变，schema1只属于独立报告；旧Welcome实际重放/带负载队列/无缺席快照换连接/强认证及远端背包未验收，Full/P1仍false。下方旧日期计数保留原时点，当前环境见[HANDOFF](HANDOFF.md)。

更新时间：2026-10-05；工作包P1-NET-01/02。主机请求纯核心、SMAPI只读桥、默认禁用诊断与新两端文件准备已实现；真实Scope/门禁失败观测已测试，双端/生命周期未执行，不承诺强认证、远端库存事务或联机机器操作已验收。

## 当前能力与入口

`src/XiangyeShiling/Networking` 不引用游戏或 SMAPI，包含请求格式、主机准入、会话账本、有界邮箱和只读会话。`GameIntegration/ReadOnlyProtocolService` 已由生产 `ModEntry` 注册 `ModMessageReceived` 与生命周期/主线程更新事件，只发送握手与拒绝诊断，不持有 `MachineService`，不调用 Gate、Ledger、规则或提交器。格式、会话、声明身份均符合的 Start/Collect 最终仍返回 `WritesDisabled`。

NET-01历史45项及10-01只读桥68项保留；85核心/77Python与构建见 [配对验证](validation/P1-network-pair-2026-10-02.md)，28专项/全105Python见 [工具验证](validation/P1-network-fixture-2026-10-02.md)。450604963真实禁用绑定与旧加载见 [历史合作准备](validation/P1-network-live-2026-10-02.md)，首次客户端Farmer仍未核。10-02夹具观测增强/构建见 [观测扩展](validation/P1-network-observation-2026-10-02.md)，10-04已实测Scope读前拒绝null、读取后门禁失败保留，第一份Host报告被endpoint因Failure拒绝；两新主机进程不验证跨Tick/重连/API内部异常，真实探针与配对待执行，见 [失败验证](validation/P1-network-failure-2026-10-04.md)。生产协议不变，快照不能称异常后最终状态，schema2/Full=false不变，操作见 [NET02准备](NET02_TEST_SETUP.md)。固定synthetic不代表实际设备准入。联机加工/放置门禁保留，有效Idle原生移除不在这些门禁内。

| 文件 | 责任 | 通过的含义 |
|---|---|---|
| MachineRequestEnvelope.cs | 不可变的 Start/Collect 操作意图 | 客户端声明，尚未认证 |
| MachineRequestCodec.cs | 有界、严格版本/字段/Unicode 格式 | 可以读取，不代表有操作权限 |
| HostRequestGate.cs | 可信主机、发送者、库存所有者、地图/范围与状态准入 | Authorized 生成规则请求，不代表有计划或写入 |
| HostRequestLedger.cs | 完整意图绑定、尝试令牌、成功历史回执、隔离 | Started 允许继续准备；Recorded 只接受可信提交器确认 |
| ProtocolInbox.cs | 构造线程消费、任意线程入队的有界 FIFO | 仅排队事件，不解析、认证或提交；满时拒绝新项 |
| ReadOnlyProtocolCodec.cs | 独立握手版本、Hello/Welcome/Rejected 严格字段与拒绝格式 | WritesEnabled 固定 false，没有成功回执 |
| ReadOnlyProtocolSession.cs | 单线程连接表、challenge/会话、消息方向与频率、诊断状态 | 正常路由一致性，不是恶意 peer 认证；合法请求仍拒绝写入 |
| GameIntegration/ReadOnlyProtocolService.cs | SMAPI 事件→邮箱→UpdateTicked→定向诊断发送 | 不持有机器事务服务，不读写远端背包或设备状态 |

设备持久化 schema 仍为 1；协议版本独立，不新增设备 modData，不改变配方、稳定 ID、7/5 激活范围或依赖。所有公开类型是开发中的内部工程契约，尚未成为稳定第三方 API。

## NET-01 请求格式与信任前提

协议版本 1 的字段必须全部存在，大小写固定，不能多、少或重复；Collect 的 RecipeId 必须显式为 null。GUID 使用带连字符的 D 格式。原 JSON 不被改写，读取失败返回 false/null。

| 字段 | JSON 类型 / C# 类型 | 来源与约束 |
|---|---|---|
| ProtocolVersion | Number / Int32 | 必须为 1；未知版本拒绝 |
| SessionId | String / Guid | 主机本次世界会话生成的非空 D 格式 GUID；不能作为长期设备身份 |
| RequestId | String / Guid | 非空 D 格式 GUID；一次操作及其所有重试固定 |
| ActorId | Number / Int64 | 客户端声明的非零 ID；NET-02 仅核对与发送者元数据一致，未来可写 Gate 还须真正可信的发送者身份 |
| LocationName | String / String | 目标地图名称，1–128 UTF-16 单元、无控制字符或首尾空白；未来可写调用链由主机查图，NET-02 不查询设备来准备事务 |
| TileX | Number / Int32 | 目标格 X，必须非负 |
| TileY | Number / Int32 | 目标格 Y，必须非负 |
| InstanceId | String / Guid | 客户端看到的设备身份，非空 D 格式 GUID；重试不得替换成新实例身份 |
| ExpectedRevision | Number / Int64 | 客户端看到的修订，必须非负；失败或重试中不能偷偷替换成当前修订 |
| Action | String / MachineRequestAction | 精确名称 Start 或 Collect，不接受数字/大小写变体 |
| RecipeId | String 或 Null / String? | Start 为1–128 UTF-16单元有效名称，沿用 LocationName 的空白/控制字符/Unicode约束；Collect 必须显式 null。只定位主机配方，不提供原料/产量/时长 |

JSON 上限为 4096 UTF-8 字节，并先检查字符数和完整 UTF-16；原始字符串或转义字符的孤立代理项都拒绝，合法中文和代理项对保持原值。异常边界包含 JsonException 和 JSON 字符解码可能抛出的 InvalidOperationException，见 [BUG-2026-013](bugfix/BUG-2026-013-protocol-unicode.md)。`Write` 同样拒绝无效的直接构造对象，不能靠序列化替换字符后改变已绑定身份。

载荷没有库存、品质、产物、价格、解锁或客户端时间字段；加入这些额外字段直接拒绝。NET-02 外层 SMAPI body 是 JSON 字符串，主线程限流后通过 `ReadAs<string>()` 取回内部原文交给严格 reader，不先 `ReadAs<MachineRequestEnvelope>()` 丢失额外/重复字段证据。4096 字节限制仅约束内部解析入口，不能限制 SMAPI 在回调/读取前已进行的反序列化和分配；邮箱、帧预算及 peer 额度也不是完整网络拒绝服务防护。

`HostActorSnapshot` 和 `HostTargetSnapshot` 是未来可写主机适配器的信任前提：只能从主机实际上下文构造，不能由客户端 JSON 得到。`AuthenticatedActorId` 参数名不表示 SMAPI 自动提供了不可伪造身份，也不能仅把 `FromPlayerID` 复制进去便声称完成认证。NET-01 纯场景预设可信值，只验证给定快照下的准入，不验证它们从网络取得的真实性。Gate 核对：游戏线程、主玩家、已加载世界、当前会话、发送者仍在线、声明 actor 与认证 ID 相同、实际背包所有者相同、玩家没有忙碌动作、目标实际地址、同地图、一格范围与合法设备状态。距离差使用 long，极端 int 坐标不会溢出成近距离。

Gate 保留请求的 InstanceId 和 ExpectedRevision，复制认证发送者 ID 生成规则 Request。具体身份/修订/重复、主机解锁、配方、库存容量及时间仍由 MachineRules 和游戏适配核验。Gate 不锁背包、不核验真实 SObject 引用，也不执行事务。

## NET-02 元数据与握手边界

SMAPI 4.4.0 的 `FromPlayerID`、`FromModID` 是消息序列化元数据，不能单独证明实际网络对端、模组来源或远端 Farmer/背包可写权。连接表、版本、角色和 ActorId 与元数据的一致性只用于正常协议路由；恶意 peer 若使声明字段相互一致，NET-02 也不会因此开放写入。`SessionId` 是当前主机世界生命周期标识，`Challenge` 用于匹配客户端本次连接的 welcome，均不是授权凭证。

桥只在已加载多人世界、非本地分屏、`ScreenId=0` 时建立会话；普通单人不发包。本地玩家 ID 必须非零。每帧用 `GetConnectedPlayers` 刷新 peer：已安装 SMAPI、本模组版本字符串完全相同、非分屏、ID 非零且不是自身；主机只接受非主机 peer，客户端只接受唯一符合条件的远端主机。相同 ID 的重复合格条目排除；最多按 ID 排序保留 32 个，不是已验证的任意规模联机支持。

客户端连接新peer创建随机challenge，立即hello，未Ready每5000ms重试沿用challenge；主机有效Hello即Ready并尝试Welcome、不等送达确认。客户端仅匹配host/challenge/版本/false且未Ready时记录session，重复Welcome不改Ready。连接对象被替换/断连/角色版本变更移除旧状态，本地GUID纳入peer比较，新连接需新challenge。常规客户端自动发Hello；显式开启的本地诊断额外发送固定synthetic探针，不将机器菜单改为远端事务入口。连接标记不进JSON、不构成认证。

## NET-02 消息类型与完整字段

消息接收/发送使用本模组 ID `Sora6A.XiangyeShiling`，SMAPI body 必须是一个字符串，字符串内容才是下述 JSON。所有对象字段必须全部存在、区分大小写、无额外/重复字段；不接受 JSON 数组、默认补字段、字符串代数字、数字枚举或未知版本。GUID 为非空 D 格式，Rejected 的 RequestId 是唯一允许显式 null 的 GUID 字段；ActorId/HostPlayerId 为非零 Int64。三种握手/拒绝对象复用 4096 UTF-8 字节、完整 UTF-16 和最大深度3的严格读取边界，包括 BUG-013 的转义孤立代理项 false/null 行为。直接 `Write` 无效对象抛 ArgumentException，不将 WritesEnabled=true 当作配置开关。

| SMAPI Type | 方向与完整 JSON 字段 | 约束 |
|---|---|---|
| `XYS/Hello/1` | 客户端→主机：`ProtocolVersion: Int32`、`Challenge: Guid`、`ActorId: Int64` | ProtocolVersion=1；Challenge 非空；ActorId 必须与 FromPlayerID 元数据相同 |
| `XYS/Welcome/1` | 主机→客户端：`ProtocolVersion: Int32`、`RequestProtocolVersion: Int32`、`Challenge: Guid`、`SessionId: Guid`、`HostPlayerId: Int64`、`WritesEnabled: Boolean` | 两版本均1；回传 Hello challenge；本主机新世界 SessionId；WritesEnabled 只允许 false |
| `XYS/Request/1` | 客户端→主机：上节全部11个 MachineRequestEnvelope 字段 | 使用 NET-01 严格 request codec；无需查库存/配方/设备，最终合法请求返回 WritesDisabled |
| `XYS/Rejected/1` | 主机→客户端：`ProtocolVersion: Int32`、`SessionId: Guid`、`RequestId: Guid?`、`ActorId: Int64`、`Reason: String` | ProtocolVersion=1；Reason 是下表精确名称；ActorId 是回复对象的元数据 ID；RequestId 字段不可省略，读取失败回复为 null |

Request 协议版本与握手协议版本独立命名，即使目前均1也不能与机器持久化 schema 混用。Welcome 不携带设备快照/库存/产量/价格；Rejected 不携带 AppliedRevision、lease 或成功 receipt。客户端只在已 Ready、SessionId 等于 RemoteSessionId、ActorId 等于本地玩家时记录 `LastRejection`。RequestId 仅是相关性字段，本阶段没有客户端未决机器请求账本，不据此确认成功或改变设备。

## NET-02 限额、错误与生命周期

`ModMessageReceived` 回调检查本模组元数据、四种已知 Type 和主线程原子发布的连接 ID 表；未知表项在入队前丢弃。回调保存事件引用、世界 generation、断连 generation 和本地 ConnectionId，不查询 Context/Game1/SMAPI 连接 API，不调用 ReadAs 或发送函数。发布的连接表替换后不再修改，回调只读该快照。`ProtocolInbox` 默认128项，任意线程可入队、只有构造线程可消费/清空；满时静默拒绝新项，不覆盖旧内容。`UpdateTicked` 先核对多人上下文和实际连接表，再最多取出16项；标记已过期的项也占取出预算但直接跳过，不能在同帧无限清队列。

每个合格 peer 每1000ms固定窗口允许8条角色允许的已知入站消息，超过额度在调用正文读取委托前静默丢弃；已知方向的损坏 body 也消耗额度。未知 peer、错误模组/类型或方向不读取正文、不响应。时间来自本机 Stopwatch 的单调毫秒值；负值/倒退不会读取正文、发包或恢复额度。它没有实现传输层大小上限、全局网络带宽配额或强认证 peer 公平性，不能抗恶意伪造元数据绕过正常归属假设。

| 输入/事件 | 具体行为 |
|---|---|
| 未加载世界、单人、本地分屏或非主屏 | 会话不运行，已排队内容/会话重置，无网络事务 |
| 未知来源/类型、错误角色方向、超额 | 静默丢弃，不读取正文、不发拒绝，避免反馈放大 |
| Hello 无法严格解析、ActorId 与元数据不同 | 已消耗额度；静默丢弃，不标记 Ready、不发 Welcome |
| 已知 Request 的 body 读取失败或严格格式错误 | 定向 `InvalidRequest`，RequestId 显式 null；不回显 raw |
| 已解析 Request.ActorId 与 FromPlayerID 不同 | 定向 `ActorMismatch`，保留解析出的 RequestId |
| actor 一致但 Request.SessionId 不是主机本会话 | 定向 `WrongSession`，不改绑为新会话 |
| actor/session 一致但尚未收到有效 Hello | 定向 `HandshakeRequired` |
| 格式、actor、session、握手都符合 | 定向 `WritesDisabled`；不调用 Gate/Ledger/Rules/Commit、不返回成功 |
| 客户端错误 challenge/host ID、无效 Welcome 或错误/旧 Rejected | 不改变 RemoteSessionId/LastRejection，不回包形成循环 |
| 出站规划后对端断开/版本或角色不匹配 | Send 前复查连接表并丢弃，发送仅定向本模组及该玩家、不发自身 |
| 非内存耗尽的诊断异常 | 清会话/队列，锁定诊断桥到下次 SaveLoaded/ReturnedToTitle；单次类型名称 WARN，无raw或peer姓名 |

同一 Request 同时违背多项时，解析失败优先；成功解析后顺序为 ActorMismatch→WrongSession→HandshakeRequired→WritesDisabled。这些原因全部是精确字符串枚举 `WritesDisabled`、`HandshakeRequired`、`WrongSession`、`ActorMismatch`、`InvalidRequest`，不支持大小写变体/数字/未知原因。`SendMessage` 返回 void，代表发送尝试，不是送达、成功事务或远端已接受确认；Hello/Welcome/Rejected 不执行存档写入。

SaveLoaded、ReturnedToTitle 先递增世界 generation 再清队列/会话/发布连接表；身份或主机角色改变时也重置并重新构造会话。主机新的活动世界生成新 SessionId。已捕获旧世界 generation 的项在消费时拒绝，包括捕获后才与 Clear 并发入队的项；这不证明一个网络包原本属于哪个世界。`PeerDisconnected` 只递增独立断连 generation，不轮换主机世界 SessionId；任何断连保守地使此前排队的所有 peer 消息失效。每帧刷新连接表后还比较本地 ConnectionId，旧连接的排队项不能借用同 ID 新连接或残留 Farmer。只有世界标记、断连标记、连接标记和当前 peer 均符合才交给会话读取正文；之后仍核对 session/challenge。回调开始前已延迟的网络包无法仅凭这些本地标记判定来源，NET-02 的只读拒绝边界始终保留。

## 会话账本与失败行为

账本在游戏主线程构造，所有可变方法/状态读取必须由构造线程执行；跨线程调用抛 InvalidOperationException，不能由网络工作线程直接操作游戏。它按 `(ActorId, RequestId)` 绑定整份不可变请求，SessionId 固定在账本构造时。断线或重连不得清空；返回标题/切换世界后创建新会话和新账本，旧 SessionId 拒绝。

| 事件 | 账本行为 | 调用方义务 |
|---|---|---|
| 首次合法意图 | Started，返回仅账本创建的 lease | 继续读取可信快照与规则计划 |
| 同 ID 改任一意图字段 | IntentMismatch | 不改目标或 revision，不产生新写集 |
| 同意图仍在执行 | InProgress | 不重入，不再扣料 |
| 尚未写入的缺料/满包/规则拒绝 | ReleaseForRetry 保留意图、释放 lease | 必须证明未写入；重试仍用全部原字段 |
| Conflict/PreparationFailed/Reentrant/RolledBack | Complete 返回 Retryable | 仅使用可信协调器结果；下一次尝试取得新 lease |
| Applied 且修订恰为 ExpectedRevision+1 | Recorded，保存原意图与历史修订 | Applied 来自完整写后核验，不能传 Decision.Accepted |
| 成功后再次发同意图 | Replayed，返回原 receipt，无 lease | 返回历史确认，不重新执行，不把历史修订覆盖到当前设备 |
| RecoveryFailed/Quarantined/未知提交结果或矛盾 Applied 数据 | 隔离整个账本 | 保留诊断，停止写入；不能伪装为可重试失败 |
| 旧、外来或已经完成的 lease | InvalidLease | 不能结束当前另一尝试 |
| 容量达到上限 | 新 ID 返回 CapacityExceeded，既有重试/回执仍可访问 | 默认 256 个绑定项，不淘汰成功历史，不靠清账本继续执行 |

成功回执持有整份原请求与 AppliedRevision。设备已经执行另一笔或被重放后，历史成功仍是历史成功；不能用它恢复旧机器或强写旧状态。隔离后不再返回普通成功重放，避免以确认掩盖会话状态未知。所有失败记录也占容量；未来可写传输需要按真正可信 peer 限流和公平额度，当前纯账本不解决拒绝服务。NET-02 的额度只属于独立只读会话，不调用或清理这个成功账本。

会话账本不是磁盘日志。主机进程重启会失去历史回执；MachineState 的 LastRequestId 只保存最近一笔，不能据此宣称所有历史意图在重启后都可准确恢复确认。客户端确认丢失后换新 ID 可能重复执行，因此完整断线/重启的恢复方案尚待设计与实机验证，不开放远端写入。

## 适配顺序与具体下一步

未来**可写**调用链必须顺序执行：严格 reader → 主线程可信 actor/target 快照与 Gate → 账本 TryBegin → 主机查配方/解锁/库存/时钟 → MachineRules → CommitCoordinator 的写前/写后/补偿核验 → Complete → 回执。Replayed 分支在本次规则和提交前返回；未得到 lease 的任何分支都不能准备或写入。NET-02 当前只读桥不接这条调用链，不能把其握手 Ready 当成 Authorized，也不能把拒绝响应当成功账本回执。

`P1-NET-01` 的纯模块和内存集成场景已完成，生产构建通过。后续工作包：

1. `P1-NET-02`：只读桥、默认禁用Network诊断与专用身份绑定工具实现；最后核心85/前轮构建通过，本轮Python105/只读文件审计通过。下一步按NET02_TEST_SETUP建立全新合作世界/小屋和非零ID，在停止实例后plan审查/apply仍禁用、另行启用，执行真实双端握手、拒绝、限额/队列、重连/标题/错版/分屏矩阵；配置绑定、八回候选和标题快照不代替完整验收。保持合法请求拒写。
2. `P1-NET-03`：设计并验证可信发送者边界、真实远端 Farmer 背包所有权、可写权、同步确认和断线窗口。复用单人 OverwriteWith 加本地补偿不代表远端安全；不能直接删 `actor != Game1.player` 门禁或使用FromPlayerID元数据替代信任前提。
3. `P1-NET-04`：串行主机提交、成功确认丢失/重启恢复、双方争用、重放、恢复失败隔离。必须有真实两客户端守恒证据，达到条件后才开放联机开工/领取与放置。

每次接手先读 [HANDOFF](HANDOFF.md)，保留 WorldInteraction 7/20 中断事实和旧 Persistence Stage3 副本。联网工作不替代两设备实际世界输入、携带/重放保存重载及正常地图入口验收，P1 未完成前不进 P2。

## 本地诊断与固定探针

`ProtocolDiagnostics.cs`/`ProtocolProbePlan.cs` 是纯层，`NetworkDiagnosticOptions` 由主模块 ReadConfig 读取。EnableReadOnlyNetworkProbes 默认false；专用农场名以XYSNET开头、长度6–20、独立非零ID必须与实际多人非分屏主屏世界相符。不自动加载/创建/保存。开启探针不会打开事务，普通只读握手不依赖此额外开关。

原 MachineService GetApi 保留，ReadNetworkDiagnostics 返回构造线程复制的peer/会话/trace，不提供授权；未绑定抛错，跨线程拒绝。TrySendNetworkProbe 仅客户端唯一Ready主机、配置合格/未fault下发送固定Start/Collect/ActorMismatch/WrongSession/Malformed计划，16次尝试/1000ms；不接受任意目标/raw，true仅发送尝试。synthetic FarmHouse(0,0)、InstanceId=RequestId、revision0不查真实机器。合法Start/Collect仍WritesDisabled，主机不可通过此入口发送测试意图。

trace构造线程所有，256条满时淘汰旧诊断并计Evicted，与不淘汰历史的成功账本不同。记录本地run/序号/单调时间/线程/world-disconnect generation/固定类型/peer及真实Receive观测；BodyReadAttempted仅表述调用正文读取委托，RateLimited=false表示该委托没有调用，不承诺SMAPI网络层没分配。记录仅显式开关开启时保存，无消息原文/库存/设备raw/姓名/路径；QueueRejected是邮箱满拒绝累计量。Reset保留诊断尾部以观察生命周期，没有清成功账本行为。

GameHarness只消费本地诊断，绑定当前run/session/generation/唯一主机连接/线程/消息type/实际读入与发送序号；QueueRejected或Evicted增加、上下文变化中止，见BUG-016。五项拒绝/九发八回候选需配对主机trace，不证明完整额度、身份或设备事务；FullNet02Accepted恒false。公开API处于开发期，不承诺稳定第三方契约。10-12真实原活动双报告Basic配对核Ready/五拒绝/同窗口八读一未读限流，断连后Failure/标题局部观测与退出另记，见 [本轮实测](validation/P1-network-basic-2026-10-12.md)。实际操作/字段见 [NET02指南](NET02_TEST_SETUP.md)，当前实例/禁用配置/春24基线统一见 [HANDOFF](HANDOFF.md)，Full生命周期仍待。

## Schema2 诊断与离线配对

本轮增补LastQuota实际固定窗口起点/Count/Allowed，只在合格peer读取前额度判断后记录，过滤/倒退时清null；超额Count8/false不读正文。Processed记录消费时核过的本地ConnectionId，SendAttempt仅当前SDK对象引用仍一致才记录token，否则缺证据；这些字段不进线上Hello/Request/Rejected。ProtocolTrace仍256条，离线基础配对要求从序号1完整、QueueRejected/Evicted均0，不用丢失记录验收。

GameHarness报告schema2增加首个Ready Baseline与current库存/raw指纹/引用不变、BurstSentAfterSequence，active与title另存。各端线程/world/disconnect/connection/challenge/Stopwatch不能跨端相等比较：握手以client实际Ready挑战匹配host HelloAccepted和client WelcomeAccepted，而host peer.Challenge是其自行生成值。共享session/fixture/互为player/请求ID及生产DLL用于两端关联。

离线工具检查当前同端连接、请求后接受拒绝及host产生后紧邻SendAttempt；九发的八个实际读ID集合两端一致，host必须同一实际窗口Count1..8后唯一未读RateLimited。全部本端Processed的quota计数也守恒，不能同窗重复Allowed Count8。输入JSON有界/严格、旧schema1不补默认，输出Basic/Fullfalse，不提供认证/完整网络/真实设备或保存保证。40人工报告专项包含在本轮105Python中，最后核心85来自前轮；不是实际联通，命令/字段见NET02_TEST_SETUP，缺陷先红后绿见BUG-017。
