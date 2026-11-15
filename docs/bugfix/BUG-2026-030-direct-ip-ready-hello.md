# BUG-2026-030 直连已就绪但设备权限未建立

- 状态: 已修复待验证
- 发现/更新日期: 2026-11-15，Asia/Shanghai
- 责任任务/分支: P1-NET-03，`codex/first-stable-release`
- 性质: 生产代码缺陷，修复前组件失败记录已保存

## 现象与影响

如果首个 Hello 的只读处理成功，原生连接来源核对却未通过，农工会收到 Welcome 并转为 Ready。此时设备权限标记尚未建立，开工和领取一直不可用。没有设备或库存写入。

## 环境与前置条件

Windows 游戏 1.6.15.24356、SMAPI 4.4.0。主机与农工都开启普通直连设备操作。最小组件回归运行于 .NET SDK 7.0.302，无需加载游戏。

## 复现步骤

建立同一 SDK 连接后接收本地挑战匹配的只读 Welcome。模拟原生能力标记仍缺失，等待五秒并请求直连握手刷新。旧处理在 Ready 后保持静默，无法发回同一挑战。运行 `dotnet run --project tests/MachineCore/MachineCore.csproj -c Release`，旧谓词回归失败，见 `.local/p1-direct-ip-ready-hello-red.txt`。

## 根因分析

`ReadOnlyProtocolSession.SynchronizePeers` 只为未 Ready 的 peer 重发 Hello，而普通直连权限独立依赖原生来源及复制标记。既有只读回归只要求 Ready 后保持静默，未覆盖等待直连权限的情况。

## 解决方案

普通直连仅在原生连接与 SDK 主机都仍匹配且能力标记未建立时刷新原挑战。每次间隔至少五秒，每连接最多三次。已存在的同挑战主机记录保留原来的 V3 序号跟踪。没有修改消息或存档格式。

## 单元测试

`tests/MachineCore/ReadOnlyTransportScenarios.cs` 的 `direct IP ready hello refresh retries the same challenge after a read-only welcome` 用相同命令检查连接绑定、相同挑战、间隔、次数和无意变更会话结果。旧谓词失败，修后 MachineCore 142 项全通过，含原有只读静默场景。见 `.local/p1-direct-ip-ready-hello-green.txt`。

## 验证结果

隔离生产 Release 构建 `.local/p1-direct-ip-ready-hello-build` 零警告零错误。实机尚未注入首轮原生来源缺失并验证第二轮恢复，保持待验。

## 风险与回滚

额外 Hello 仅影响显式启用的当前直连，受原 peer 限流。撤回本提交可恢复原只读握手行为，无需改写存档或删除原生预留。

## 关联记录

关联 [开发计划](../DEVELOPMENT_PLAN.md)、[主机协议](../P1_HOST_PROTOCOL.md) 和 [交接](../HANDOFF.md)。随本地关联提交交付，尚未推送。
