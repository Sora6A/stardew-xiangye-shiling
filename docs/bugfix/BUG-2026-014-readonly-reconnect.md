# BUG-2026-014：只读会话在同身份重连时保留旧握手

- 状态: 纯回归及一Host/一Client正常有gap同ID重新握手实测子集通过；无缺席快照换连接/旧Welcome实际重放/负载队列及第三peer仍待，未提交/合并
- 发现/更新日期: 2026-10-01 / 2026-10-12，Asia/Shanghai
- 责任任务/分支: `P1-NET-02` 只读握手与主线程传输，`feat/p1-game-adapter`
- 性质: 开发审查发现的会话生命周期缺陷；历史补录，修正先于具名回归，没有保留原始修复前可执行版本的运行红证据；非线上事故

## 现象与影响

初始只读会话按玩家 ID、主机角色、模组版本是否匹配及分屏标志比较连接快照。如果同一玩家在两次 `SynchronizePeers` 轮询之间断开并重连，新快照的这些值仍可能完全相同；旧 `Ready`、客户端 challenge 和远端会话因此没有被作废。预期是每次新连接都重新握手，旧连接的 welcome 不能建立新连接会话，其他仍连接的玩家则保留主机世界会话。

该缺陷在开发审查中发现，主任务先补连接标识，再补具名回归。本记录没有旧真实双客户端现场，也没有原始修复前运行日志。随后以当前实际源码重建“忽略连接标识”的旧比较语义，独立纯项目已实际复现明确断言失败；这份补充红证据不是原始旧版程序或游戏网络实测。

目前只读协议即使收到合法 Start/Collect，也只回复 `WritesDisabled`；没有接入设备事务、远端背包写入或成功回执。缺陷影响只读握手及诊断消息生命周期，没有已知玩家存档损失。不能以其修复或纯测试通过宣称联机加工、背包所有权或实际网络重连已验收。

## 环境与前置条件

纯回归环境为 Windows、.NET SDK 7.0.302、net6.0，命令在仓库根目录运行。[MachineCore](../../tests/MachineCore/MachineCore.csproj) 直接链接生产 Networking 源码，不加载游戏。场景使用主机 ID 10、客户端 ID 20、另一个客户端 ID 21，以及新生成的 GUID；这些只是纯场景身份，不指向真实农场。

游戏适配目标仍为游戏 1.6.15、SMAPI 4.4.0、CP 2.9.0 固定标签本地构建、双包未发布的 `0.1.0-alpha.2`。SMAPI 4.4.0 接口和连接对象引用行为已静态核对；最终生产Release与开发包已通过本地构建/部署/四存档哈希审计，见 [只读传输验证](../validation/P1-readonly-transport-2026-10-01.md)。实际双客户端发送/接收、回调线程与队列生命周期尚未运行，不能用 SDK 静态核对或编译替代。

## 复现步骤

以下是**历史语义重建负例**，不是原始修复前版本，也不要求对正式农场注入网络载荷：

1. 阅读本机 `.local/reproduce-bug014-old-comparison.py`。它在 `.local/bug014-old-comparison` 建立隔离 net6.0 项目，链接当前实际 MachineCore 测试及生产规则/协议源码；只对 `ReadOnlyProtocolSession.cs` 的一处比较生成本地副本，不修改生产文件。
2. 隔离副本将 `peer != oldSnapshot` 改为先把新 peer 的 `ConnectionId` 替换成旧值再比较，等价于初始只比较身份/角色/版本/分屏元数据。当前其他实现和具名回归保持原样；它不是恢复完整旧历史代码。
3. 运行 `python .local/reproduce-bug014-old-comparison.py`。脚本实际执行的子命令是 `dotnet run --project .local/bug014-old-comparison/Negative.csproj -c Release`，并要求子进程非零退出及日志包含本 bug 的明确断言。
4. `read-only changed connection invalidates handshake while other peers retain host world session` 在没有中间空快照的条件下，将主机连接标识改为新 GUID。旧比较保留 `Ready`，因此没有发出新 hello，实际失败为 `BUG-2026-014: changed connection retained Ready instead of issuing a fresh challenge.`。断言直接要求返回一条新 hello，不再用 `.Single()` 的普通空序列异常作为揭示证据。
5. 主任务已保存 `.local/bug014-old-comparison-red-2026-10-01.txt` 和 `.local/bug014-old-comparison-red-audit-2026-10-01.json`；子进程实际 Windows 无符号退出码为 `3762504530`。审计标明 `retrospective equivalent old comparator, not original pre-fix executable`、`onlyConnectionIdentityIgnored=true`。不得改写为原始修复前游戏失败。
6. 使用当前实际生产源码执行 `dotnet run --project tests/MachineCore/MachineCore.csproj -c Release`，完整 68 项通过，日志为 `.local/p1-readonly-transport-green-2026-10-01.txt`。当前修正已存在，直接运行该命令预期绿色；隔离旧判据才是上述负例。

脚本、完整本机日志和编译产物均留在 `.local`，不提交游戏程序集、绝对个人路径或完整日志。

## 根因分析

初始 `ProtocolPeer` 元数据描述“这是哪个玩家以及当前是否兼容”，没有描述“这是哪次连接”。`ReadOnlyProtocolSession.SynchronizePeers` 将相同元数据视为同一个 `PeerState`，只有看见玩家缺席或元数据变化才删除旧握手。断开和重连完全发生在轮询间隔内时，没有缺席快照可触发删除。

旧的显式断开场景会先同步空连接表，再重新添加玩家，能够重置状态，却不能揭示“不曾观察到缺席”的重连。针对该边界的具名回归是在修正后补入，原始红证据缺失；因此本记录明确保留历史补录性质，而不是把后补负例包装成原先的先测后修。

适配层还必须区分已经排队的旧回调与当前连接：仅在消费时用玩家 ID 查新 peer，会让旧 ID 对应的新连接误收旧工作。只清空邮箱也不充分，清空之后仍可能有延迟回调入队。世界代次、断开代次和连接标识必须在队列消费前共同核验。

## 解决方案

- [ProtocolPeer 与会话](../../src/XiangyeShiling/Networking/ReadOnlyProtocolSession.cs) 增加内部 `ConnectionId`，快照比较包含该标识。同 ID/角色/版本的新连接删除旧 `PeerState`，客户端清空远端会话和拒绝诊断，重新产生 challenge；主机世界 `SessionId` 不因某一个 peer 重连而轮换，其他玩家的握手保留。
- [SMAPI 只读桥](../../src/XiangyeShiling/GameIntegration/ReadOnlyProtocolService.cs) 在主线程读取实际连接表，保存 SMAPI peer 对象引用；同 ID 的连接引用变化时生成新 token。主任务对本机 SMAPI 4.4.0 实现的静态核对确认连接表返回其保存的对象，重连创建新对象；这项版本依据仍须用真实双客户端验证，不能外推为任意版本接口保证。
- 主线程原子发布不可再修改的“peer ID → token”字典；消息回调只读取已发布表、保留事件引用并入有界邮箱，不查询游戏状态或立即读取正文。每项携带连接 token、世界 `Generation` 和 `ConnectionGeneration`。
- 返回标题、读档或本地角色变化递增世界代次并清旧会话/队列；断开事件递增全局断开代次。消费前检查两种代次、当前连接存在及 token 相等，失效项在读取正文前丢弃。全局断开代次会保守丢弃其他 peer 已排队的旧工作，但不轮换主机世界会话；诊断握手可按既有重试规则恢复。
- 桥接保持纯诊断：有界邮箱、每帧消费上限及每 peer 的读取前限额都不提供网络认证，也不能限制 SMAPI 在进入回调前的反序列化内存。实际 Farmer/背包认证、事务和成功确认仍属于后续 `P1-NET-03/04`，生产菜单/放置/加工的联机门禁继续保留。

`ConnectionId` 是进程内连接标识，不写入协议 JSON、机器 schema 或存档；无物品 ID、配方、存档迁移和远端库存修改。本次修正不开放设备写入。

## 单元测试

测试文件：[ReadOnlyTransportScenarios.cs](../../tests/MachineCore/ReadOnlyTransportScenarios.cs)。标准命令：

```powershell
dotnet run --project tests/MachineCore/MachineCore.csproj -c Release
```

| 场景名称 | 关键断言 | 重建旧判据 | 当前实现 |
|---|---|---|---|
| `read-only changed connection invalidates handshake while other peers retain host world session` | 没有空快照时，新 token 必须产生一条新 hello/new challenge、清旧客户端会话；旧 welcome 被拒；主机要求重连 peer 重新握手，另一个 peer 仍用原世界会话得到 WritesDisabled；新握手成功 | 实际在 BUG-014 专用断言失败，非零退出 | 通过 |
| `read-only reconnect uses fresh challenge and rejects old connection welcome` | 显式断开清远端会话/诊断；重连 challenge 不复用，旧 welcome 不建立连接，未握手请求拒绝 | 该用例在补充红轮通过，说明它单独无法揭示两次轮询间的重连 | 通过 |
| `read-only duplicate welcome cannot replace established session or clear rejection` | 已建立会话不能被重复 welcome 替换或清诊断 | 补充红轮通过 | 通过 |
| `read-only session rejects foreign-thread peer synchronization reception and count` | 外线程访问在改 peer 或读取正文前拒绝 | 红轮于前一个重连断言停止，未执行到 | 通过 |
| `read-only peer capacity duplicates ambiguous hosts and changed metadata fail closed` | 上限/重复/多主机/版本变化不建立错误会话 | 同上，未执行到 | 通过 |

完整绿色是 68 项，其中 12 项只读会话/路由场景、5 项只读 codec、6 项邮箱场景。邮箱并发与清空测试验证通用容器，不覆盖 SMAPI 实际事件是否按预期到达、peer 引用在真实重连中的变化或代次/token 对真实旧回调的丢弃。补充红轮在该具名场景停止，不能标为完整核心通过。

## 验证结果

2026-10-12补充集成：生产DLL及原修复源码未改，一Host/一Client在同一世界各自同进程进行正常显式标题/gap/同ID重新加入，实际新连接/新challenge/Hello与Welcome及主机原session保持由schema1原双报告和共享严格CLI核通过；完整保存/config/85项关闭恢复也通过。入口命令`dotnet tools/NetworkLifecycleCheck/bin/Release/net6.0/NetworkLifecycleCheck.dll <已归档host.json> <已归档client.json>`，原/最终输入各exit0；三SDK检查点、具体SHA/原生日志时点见[本轮实测](../validation/P1-network-lifecycle-2026-10-12.md)。Core101及本bug具名纯回归为此前源码结果，本轮无改码、不重跑；没有新的修复前SDK红。这个有gap场景旧实现也可能通过，不能揭示本bug最初“两次轮询之间看不到缺席”的根因。因此实际无gap换连接、旧Welcome重放、第三peer会话保持及带负载旧回调仍未执行，不标整个BUG014/NET02实机验收。下表保留2026-10-01当时结果。

| 检查 | 2026-10-01 实际结果与边界 |
|---|---|
| 原始修复前程序/真实双客户端红证据 | 未保留、未执行；开发审查修正先于具名回归，历史补录 |
| 等价旧比较隔离负例 | 已实际执行；子进程退出 `3762504530`，日志为 BUG-014 具名断言，不是空序列异常 |
| 当前精确源码 net6.0 完整核心 | 已执行，68 项通过，绿色日志已保存；不含游戏或真实 SDK 队列运行 |
| SMAPI 4.4.0 接口与 peer 引用依据 | 主任务已静态核对、初次生产编译通过；不能代替真实重连验证 |
| 最终注释更新后的生产构建/开发包 | 已执行 `python tools/build.py --game-path .local/validation-game`，Release0警告/0错误；10文件ZIP/build/本地部署一致、夹具禁用、源/UI四文件哈希未变；没有启动游戏 |
| 双客户端 hello/welcome、同 ID 重连和旧队列回调丢弃 | 未执行，保持待实机验证 |
| 真实远端背包提交、丢确认补发、竞争、断线事务恢复 | 未执行；只读桥不接入设备提交，联机操作仍拒绝 |
| 设备/保存迁移 | 未引入；未运行真实游戏/保存来验证本 bug |
| 提交、推送、PR、合并 | 未执行，无本修复 SHA、PR 或 merge 结果 |

P1 总体验收未完成，P2/P3 不开始。纯会话绿色不表示 SDK 回调生命周期已验收；后续实际网络验证须单独留报告，不能拼接历史单人 Memory、Interaction 或 Persistence 结果。

## 风险与回滚

剩余风险是 SDK 事件时序、对象引用的真实连接寿命、回调发布表与断开通知的交错，以及世界切换时队列丢弃行为。只有双客户端才能验证这些输入来源；普通纯场景的连接 token 由测试构造，不能证明 SDK 必然正确提供对应生命周期。诊断异常应停用该会话并保留门禁，不能以取消检查来通过联机测试。

回滚应撤回只读桥的注册和关联连接身份逻辑、暂停该诊断入口，保留本记录及负例证据；若退回旧比较，不再宣称同 ID 重连正确。不要关闭生产单人/主机/背包门禁、清空设备 modData 或删除正式存档。没有持久化迁移，不需要以修改玩家保存文件回滚。

## 关联记录

[P1-NET-02 计划](../DEVELOPMENT_PLAN.md)、[架构](../ARCHITECTURE.md)、[交接](../HANDOFF.md)、[开发命令](../DEVELOPMENT.md)、[贡献规范](../../CONTRIBUTING.md)、[只读会话](../../src/XiangyeShiling/Networking/ReadOnlyProtocolSession.cs)、[只读 SMAPI 桥](../../src/XiangyeShiling/GameIntegration/ReadOnlyProtocolService.cs)、[邮箱契约](../../src/XiangyeShiling/Networking/ProtocolInbox.cs)、[具名回归](../../tests/MachineCore/ReadOnlyTransportScenarios.cs)、[BUG-013 协议 Unicode](BUG-2026-013-protocol-unicode.md)。本机重建负例脚本、红日志、红审计和绿色日志路径见上文，均不提交。

任务分支 `feat/p1-game-adapter`，基线 `8f2468d83b43fb805073f73bbc8b989cf716852e`；该基线不是本修复提交。本轮未提交、推送、建 PR 或合并。将来实际 `fix` 提交须关联本记录、修正和具名回归，附完整提交正文及真实验证，提交后再补真实 SHA/PR/合并结果。
