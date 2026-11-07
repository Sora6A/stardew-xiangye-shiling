# P1 只读传输开发验证（2026-10-01）

本页保留10-01只读桥68项与当时4513c411…/c73726a2…包证据；不再描述当前构建。10-02诊断/两端文件准备新增后的81核心/37Python及当前hash见 [新验证](P1-network-probe-2026-10-02.md)，真实双端仍未验收。

状态：`P1-NET-02` 代码接线、纯会话/邮箱/格式回归和生产构建通过，**实际双客户端握手/拒绝与 SDK 生命周期尚未执行，任务未验收**。合法 Start/Collect 仍拒绝写入，不接 NET-01 的 Gate/Ledger/Commit 调用链。P1 总体验收未完成，P2 未开始。

任务分支 `feat/p1-game-adapter`，基线 `8f2468d83b43fb805073f73bbc8b989cf716852e`。继续累计未提交开发，没有本轮 commit、push、PR 或 merge；远程 CI/保护未核实。接手记录见 [HANDOFF](../HANDOFF.md)。NET-01 接入前45项/旧包哈希保留在 [历史核心验证](P1-host-core-2026-10-01.md)，没有改写旧世界运行证据。

## 环境与实际命令

Windows、SDK 7.0.302、net6.0/x64、Python 3.10.9。纯测试编译真实生产 Networking/Machines/Transactions 源码，不载入游戏或 SMAPI。生产构建引用测试安装的游戏 1.6.15、SMAPI 4.4.0.0、同安装 Harmony，Private=false；旧 `.local/smapi-reference` 是4.1.10，不能用作本轮4.4行为依据。

```powershell
dotnet run --project tests/MachineCore/MachineCore.csproj -c Release
python tools/catalog.py
python -m unittest discover -s tests -v
python tools/build.py --game-path .local/validation-game
python tools/check_workflow.py
git diff --check
```

核心命令实际退出0、68项通过：历史45＋6邮箱＋5只读格式＋12只读会话/配对测试。输出 `.local/p1-readonly-transport-green-2026-10-01.txt`。目录核对40物品/30配方闭合，26 Python通过；生产Release最终0警告/0错误，日志 `.local/p1-readonly-transport-build-2026-10-01.txt`。初次共享 JSON precheck 提取造成 CS8604，增加 `NotNullWhen(true)` 后编译通过，没有把初稿错误写成实机故障。

## 新增源码与测试断言

| 实际入口 | 断言 | 结果与边界 |
|---|---|---|
| ProtocolInbox / InboxScenarios（6项） | 默认128，FIFO原引用、满时不丢旧项、null拒绝，8并发生产者不越界，非owner不得消费/清空，Clear后复用 | net6通过；邮箱数量上限不等于载荷内存或网络DOS保护 |
| ReadOnlyProtocolCodec / ReadOnlyCodecScenarios（5项） | 三种诊断roundtrip、显式null RequestId、缺多重复字段/类型/枚举大小写/数字/GUID/Unicode/字节上限拒绝、Welcome不能开启写入 | net6通过；外层SMAPI正文读取并未在游戏执行 |
| ReadOnlyProtocolSession / ReadOnlyTransportScenarios（12项） | 实际hello→welcome消息配对，合法Start/Collect一律WritesDisabled；未握手/actor不符/旧会话/损坏正文拒绝 | net6通过；配对是内存实例，没有游戏网络套接字 |
| 同一纯会话测试 | 未知来源/mod/type/自身/角色/版本/分屏在readBody前忽略；每peer第9条超额不读/不回，1000ms恢复，倒退clock不授额 | net6通过；限额不是全网络防滥用/公平性保证 |
| 同一纯会话测试 | welcome核对host/challenge/版本/固定false，重复/旧welcome不改变已绑定session，拒绝只能作本actor/currentSession诊断 | net6通过；没有待请求表或成功回执语义 |
| 同一纯会话测试 | 断连重连新challenge、5秒同challenge重试、连接token变化即使两poll间未见空表也重建握手，其他peer主机世界ID保持 | net6通过；实际SMAPI对象替换/断开回调未实测，BUG-014补录证据见下 |
| 同一纯会话测试 | 32peer上限、重复ID/多个host拒绝、元数据变化删除旧状态、跨线程操作拒绝 | net6通过；SMAPI真实事件线程仍需实机验证 |
| ReadOnlyProtocolService / ModEntry | 事件注册、有界排队→UpdateTicked、原子发布连接表、实际peer引用分配连接ID、世界/断开generation及定向SendMessage | 生产构建通过、独立源码/API审查完成；不冒称该桥在游戏执行通过 |

只读桥不持有 MachineService、不给远端构造可信库存快照、不准备lease或成功receipt、不访问设备raw或修改存档。单人服务/菜单/放置联机门禁保留；有效 Idle 原生移除可走原版，不能概括所有联机世界操作均被禁止。协议字段、拒绝优先级与上限见 [完整契约](../P1_HOST_PROTOCOL.md)。

## SMAPI 依据与身份限制

核对实际运行4.4 DLL/XML及固定标签源码：[事件参数](https://github.com/Pathoschild/SMAPI/blob/4.4.0/src/SMAPI/Events/ModMessageReceivedEventArgs.cs)、[传输实现](https://github.com/Pathoschild/SMAPI/blob/4.4.0/src/SMAPI/Framework/SMultiplayer.cs)。消息 FromPlayerID/FromModID 来自序列化模型，不能单独当作抗恶意peer伪造的可信身份或远端库存可写权；SessionId也只是生命周期标记。NET-02仅检查正常协议的路由一致性，全部有效机器意图继续拒绝。

实际 API 核对还确认 GetConnectedPlayers 返回连接表中的对象引用；client不能仅依赖PeerConnected启动握手，返回标题不能仅靠逐peer断开清理。当前桥刷新连接表，旧peer引用分配的token不借用新连接。回调只读主线程发布的不可变表；表尚未发布时初始hello可被丢弃，未Ready时5秒重试。SendMessage指定本mod及唯一远端playerId以排除自身同步投递；void返回不代表送达。正常接收路径在更新中同步处理，但不据此假设所有回调都保证主线程。

## 快速重连缺陷与补录顺序

[BUG-2026-014](../bugfix/BUG-2026-014-readonly-reconnect.md) 在本轮开发审查发现。初稿只比较ID/角色/版本，快速重连可能保留Ready/challenge。修正早于具名回归写入，**没有原始修复前执行红日志**；已明确补录，不伪称先测试再修或线上事故。

补录的 `.local/reproduce-bug014-old-comparison.py` 在隔离net6项目仅重建“忽略ConnectionId”的旧比较行为，其他源码/回归共享当前文件。实际失败在 `read-only changed connection invalidates handshake while other peers retain host world session`，断言 `BUG-2026-014: changed connection retained Ready instead of issuing a fresh challenge.`；Python返回Windows unsigned exit3762504530。红日志/audit为 `.local/bug014-old-comparison-red-2026-10-01.txt` / `bug014-old-comparison-red-audit-2026-10-01.json`。随后实际生产共享源码68项绿。这证明回归区分旧判据和新判据，不是原初稿二进制或真实断线实测。

## 最终包、本地部署与存档

包构建前两种进程查询均为0，未启动游戏。仅更新停止的 `.local/validation-game/Mods` 中本项目两个包文件，源安装/游戏/SMAPI/CP未改；GameHarness保持Enabled=false、OpenPreview=false、Scenario=Memory。

`.local/deploy-and-audit-readonly-transport-2026-10-01.py` 已实际通过：部署前后源/UI四存档文件等于原SHA基线；ZIP10文件，仅本项目DLL，无游戏/SMAPI/Harmony/夹具；最终build/ZIP/部署一致。结果 `.local/p1-readonly-transport-package-audit-2026-10-01.json`：

- 最终生产DLL SHA-256：`4513c411b3996a15a42d95529666999a712883ff0ad9e28ee481939f5d8413e9`。
- 最终开发ZIP SHA-256：`c73726a2caad66bda47119e92f01feb6f9b38f6c3a5a123f964673cf471b2592`。
- 先前NET-01 ZIP保留 `.local/p1-readonly-transport-before-2026-10-01.zip`，SHA仍`8d432f1084590b00261dba462fd81f88a47105c5aed2a6635485a6a691b08d7b`。

双包版本仍未发布alpha.2，schema/稳定ID/配方/依赖不变，40/30登记及7/5激活不变。当前包从未在游戏启动验证，不能套用旧World/Memory/Persistence的通过报告。完整日志、存档、第三方程序集和本机路径材料不提交。

## 未执行、维护与下一步

未执行真实host/client握手、拒绝收发、实际线程/队列generation、同ID断线重连或返回标题换档；没有开放远端库存、争用、确认丢失/重启恢复。没有更新或启动旧host/client配置。单机68项是规则/内存/调度原语验证，不能代替这些验收。

下一步用两份明确隔离Mods/独立测试农场验证NET-02。先确认实际peer/mod版本与主机ID，观察hello重试与welcome，再从测试发送器发合法/actor不符/旧session/损坏/超额请求，验证只读拒绝与双方库存/raw不变；断线重连与返回标题换档必须确认旧challenge/队列不被消费。SDK FromPlayerID仍不足以建立恶意peer身份保证，NET-03须先确定可信身份及Farmer/背包可写权，NET-04才接完整事务，不可取消门禁后直接发布。旧World20停7/20也仍待新进程整轮验收，不能拼接。

最终文档结构/本地链接与差异检查结果写入 [交接](../HANDOFF.md)，检查不代表远程CI或分支保护。没有提交正文供推送检查；远程动作未执行。
