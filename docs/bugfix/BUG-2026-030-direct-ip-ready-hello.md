# BUG-2026-030 直连已就绪但设备权限未建立

- 状态: 本地验证通过
- 发现/更新日期: 2026-11-15 / 2026-11-16，Asia/Shanghai
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

隔离生产 Release 构建 `.local/p1-direct-ip-ready-hello-build` 零警告零错误。首轮实机抑制首个 Hello 原生来源观测，农工在 Ready 但无权限时仍被拒绝，之后获得匹配的能力标记并完成菜单开工和领取。主机夹具未生成预期的重发报告，完整审计失败。复验改由农工记录真实发送间隔，主机核对同挑战与连接绑定。

复验主机 2560、农工 5828。首个只读 Welcome 正常抵达，设备权限未建立期间菜单拒绝；实机累计两次发出 Hello，主机接受同一个挑战两次并在第二次核实原生农工连接。双方能力标记一致，随后用真实菜单完整开工与领取，最终修订 2、农工黄豆 3、豆腐 2。双端零错误零保存，89 个文件及常用 Mods 哈希一致。审计与计时口径见 [直连实测](../validation/P1-native-inventory-2026-11-11.md#普通直连首个-hello-来源缺失后的恢复)。

## 风险与回滚

额外 Hello 仅影响显式启用的当前直连，受原 peer 限流。撤回本提交可恢复原只读握手行为，无需改写存档或删除原生预留。

## 关联记录

关联 [开发计划](../DEVELOPMENT_PLAN.md)、[主机协议](../P1_HOST_PROTOCOL.md) 和 [交接](../HANDOFF.md)。随本地关联提交交付，尚未推送。
