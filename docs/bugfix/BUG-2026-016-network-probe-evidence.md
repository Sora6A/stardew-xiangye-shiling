# BUG-2026-016：只读网络探针可能把迟到拒绝当作当前用例通过

- 状态: 共享纯判据先红后绿，当前 81 项核心回归通过；实际 SMAPI 双端证据与生命周期未执行，待验收/提交/合并
- 发现/更新日期: 发现于 2026-10-01，更新于 2026-10-02，Asia/Shanghai
- 责任任务/分支: `P1-NET-02` 诊断夹具，`feat/p1-game-adapter`
- 性质: 开发中的测试证据关联缺陷；本轮对实际使用的旧判据留红后修复，非历史补录、非已发生的玩家事故

## 现象与影响

诊断夹具原判据只核对记录序号在发送之后、`RejectionAccepted`、RequestId 和拒绝原因，没有绑定世界、连接或消息来源。Malformed 用例的 RequestId 本来就是 null；一条迟到的旧世界 `InvalidRequest(null)` 可以同时满足这些条件，误把当前测试记为通过。同一主机 SessionId 在其他玩家断连时不轮换，单独比较 SessionId 也不能排除连接替换后的证据。

预期是只有本次进程/世界/连接中，发送之后由当前主机读入并接受的对应拒绝才能通过；证据丢失或上下文改变应中止，保留报告。本问题影响测试结论可靠性，不打开写入、不产生成功事务或回执。没有在游戏中执行旧判据，也没有已知玩家数据损失；纯负例不能被写成真实网络事故。

## 环境与前置条件

Windows、.NET SDK 7.0.302、目标 net6.0。MachineCore 直接编译生产 Networking 源码及 GameHarness 的实际共享判据，不依赖游戏/SMAPI，不启动网络。

最小输入：上下文 WorldGeneration=2、DisconnectGeneration=3、主机 ID=10、发送序号=10；之后出现 Sequence=11、WorldGeneration=1、主机 ID=10、`RejectedType`、`RejectionAccepted`、BodyReadAttempted=true、RequestId=null、Reason=InvalidRequest 的旧记录。除世界标记外其他数据与正常输入相同，足以揭示旧判据误判。

## 复现步骤

1. 将当时 NetworkScenario 正在使用的弱判据提取到 `NetworkProbeEvidence.HasRejection`，由实际夹具调用；提取阶段保留原行为，没有先加上下文过滤。
2. 新增 [ProbeEvidenceScenarios.cs](../../tests/MachineCore/ProbeEvidenceScenarios.cs) 第一项回归，构造上述旧世界 null-ID 拒绝，断言不能计入当前 malformed 用例。
3. 修改过滤逻辑前运行：

   ```powershell
   dotnet run --project tests/MachineCore/MachineCore.csproj -c Release
   ```

4. 原有 74 项先通过，新回归实际抛出 `BUG-2026-016: late null-id response from old world was accepted as this malformed probe.`，进程退出 -532462766。红日志为 `.local/bug016-probe-evidence-red-2026-10-01.txt`。
5. 修复共享判据及调用上下文后同命令转绿；补齐诊断观测回归后的最终 81 项通过，日志 `.local/p1-network-probe-core-green-2026-10-02.txt`。

上述红证据来自实际弱判据的纯回归，没有运行旧版双端游戏、模拟 SDK 事件线程或观察玩家复现。完整本机日志留 `.local`，不提交。

## 根因分析

“在发送之后观察到相同拒绝”被当作足够关联条件。序号是本地诊断记录顺序，不证明消息属于当前世界/连接；null RequestId 又无法区别两次格式错误。旧判据没有核对 Processed 来源、SMAPI Type、执行线程、读取尝试、world/disconnect generation、当前主机连接 GUID 或进程 RunId，也没有因队列拒绝/诊断淘汰而停止采用不完整证据。

先前探针计划测试只验证固定请求格式与纯会话拒绝，不检查 GameHarness 如何选择报告记录，因而没有发现夹具把不同上下文拼接的问题。

## 解决方案

[NetworkProbeEvidence.cs](../../tests/GameHarness/NetworkProbeEvidence.cs) 是纯测试与实际夹具共用的唯一判据。首次真实 Ready 快照建立 `ProbeEvidenceContext`，绑定 RunId、SessionId、世界/断连 generation、唯一 Ready 主机 ID/ConnectionId、构造线程 ID 和当时 QueueRejected/Evicted。

- 每帧和匹配拒绝前核对上下文仍活动、客户端角色、未 Faulted、WritesEnabled=false、ProbeAllowed；run/session/generation 与唯一 Ready 主机连接必须仍相同。
- 队列拒绝或 trace 淘汰数量增加时不再接受证据，不能将缺记录当作测试通过。
- 仅选择发送序号之后的 `Processed`、同 generation、当前主机、`XYS/Rejected/1`、当前游戏线程、实际尝试读正文的 `RejectionAccepted`，再核对 RequestId/Reason。
- [NetworkScenario.cs](../../tests/GameHarness/NetworkScenario.cs) 五种用例和 burst 观察均调用此判据；上下文变更中止该轮，不自动接续或拼接连接前后的报告。

这里只约束本地测试证据，不提供网络强认证。一个在回调开始前延迟、且进入当前上下文的格式错误仍没有线上的请求 ID；主机报告与生命周期实测必须独立核对。生产只读拒写路径、设备 schema 和稳定 ID 均不变，无存档迁移。

## 单元测试

命令见复现步骤；测试文件为 ProbeEvidenceScenarios，直接链接实际 GameHarness 判据。

| 测试名称 | 断言 | 修复前/后证据 |
|---|---|---|
| `network probe evidence rejects late null-id response from another generation or connection` | 旧世界 null-ID 拒绝、主机同 session 但连接 GUID 更换均不能通过 | 首个旧世界断言实际红；修复后整项绿，连接断言无独立旧轮结果 |
| `network probe evidence requires actual current host read and subsequent correlated rejection` | 当前对应拒绝可通过；发送前/相同序号、旧断连标记、其他 peer/Type/线程、未读正文、错误观测类型/RequestId 均拒绝 | 修复后绿；未单独执行各负例旧轮 |
| `network probe evidence rejects dropped trace queue faults and another protocol run` | QueueRejected/Evicted 增加、Faulted、开放写入、另一 run/session、失活或断连标记变化均拒绝 | 修复后绿；未单独执行各负例旧轮 |

不要把同项测试中首个断言的真实红，推广为该项其余断言或后两项也有独立红日志。新增四项 DiagnosticScenarios 另验证实际会话观测与委托读取次数、owner 线程和有界快照，不替代本修复的旧判据复现。

## 验证结果

| 检查 | 实际结果 |
|---|---|
| 旧共享判据回归（10-01） | 已执行并失败，退出 -532462766，红日志保留 |
| MachineCore（10-02） | 81 项通过，含本修复三项，退出 0 |
| Python（10-02） | 37 项通过，退出 0；包含配置准备工具 11 项 |
| 生产/夹具 Release（10-02） | 均 0 警告、0 错误，夹具真实调用共享判据，保持原 GetApi 返回类型 |
| 新两端配置/包审计（10-02） | 当前构建两端哈希一致，两端探针及夹具均禁用、独立世界 ID=0；旧 profiles/六存档文件准备前后未变 |
| 真实网络及完整 NET-02 | 未执行；没有新农场或连接报告，未验收 |
| 提交/推送/PR/合并 | 未执行，无修复 SHA/远程结果 |

具体命令、包哈希和证据见 [本轮验证](../validation/P1-network-probe-2026-10-02.md)。本地构建、纯测试与配置准备不能替代两端实际 transport、读取前额度、重连或标题生命周期。

## 风险与回滚

快照绑定依靠正常 SMAPI 路由元数据和本地连接对象，不抗恶意伪造；null-ID 回复没有完整线上的因果关联。burst 客户端收到八条仅是候选观察，必须另核主机八次读正文/一次读取前限额，不能标记完整 NET-02 通过。诊断窗口只保留 256 条，淘汰会导致本轮中止，应归档两端报告后重跑，不放宽条件来取得绿色。

停止时关闭两端 GameHarness.Enabled、OpenPreview 与主模块 EnableReadOnlyNetworkProbes，保留所有报告。代码回滚仅撤回夹具判据/测试，生产仍维持只读拒写；旧判据不得再用作验收依据。无需修改或清空设备/存档，不通过删除记录、原值或断线历史“修复”测试。

## 关联记录

[计划](../DEVELOPMENT_PLAN.md)、[NET-02 准备](../NET02_TEST_SETUP.md)、[主机契约](../P1_HOST_PROTOCOL.md)、[交接](../HANDOFF.md)、[本轮验证](../validation/P1-network-probe-2026-10-02.md)、[BUG-015](BUG-2026-015-multiplayer-profile-preflight.md)、[BUG-014](BUG-2026-014-readonly-reconnect.md)、[贡献规范](../../CONTRIBUTING.md)。

分支 `feat/p1-game-adapter`，基线 `8f2468d83b43fb805073f73bbc8b989cf716852e`；基线不是修复提交。全部改动尚未提交，本轮无 PR/merge；发布后再补真实修复 SHA 与完整提交正文，不能预填。
