# BUG-2026-028 联机农工请求达到容量后持续等待

- 状态: 已修复待验证
- 发现/更新日期: 2026-11-13，Asia/Shanghai
- 责任任务/分支: P1-NET-04，`codex/first-stable-release`
- 性质: 生产代码缺陷，修复前判据失败已保存

## 现象与影响

主机累积 256 个结果后，新认证请求应得到准确的无授权结果。旧判断直接返回，菜单持续重发且没有终态。同一个完成请求的重发也可能同时落入已完成和已拒绝集合，重复计数。此路径没有主机新授权，未观察到物品损失。

## 环境与前置条件

适用已启用专用事务的 Windows 1.6.15.24356、SMAPI 4.4.0 会话。边界回归用 .NET SDK 7.0.302 执行，不启动游戏。

## 复现步骤

修复前将旧分支顺序移入同一纯判据，执行 `dotnet run --project tests/MachineCore/MachineCore.csproj -c Release`。以 255 个历史 ID 加一笔授权达到边界，重放该授权，然后提交新的认证 ID。用例在第一笔应明确拒绝的溢出请求失败，实际为 `Ignore`。原记录 `.local/p1-host-intent-cap-red.txt`。

## 根因分析

`HostOwnerTransaction.ReceiveIntent` 先分别累计 Completed/Refused，再对溢出请求静默返回。旧测试覆盖活跃预留与少量重放，未达到上限。

## 解决方案

用一个有界集合保存不同的农工和请求 ID，优先保留同一活跃授权。冷载授权、尝试授权、拒绝及终态共用同一 ID。达到 256 后，新请求继续得到与实际连接、请求和 nonce 绑定的无授权反馈，无需再占用集合。恢复与已有授权仍能收尾。修改不改变存档结构和协议格式。

## 单元测试

`tests/MachineCore/HostOwnerIntentCapacityScenarios.cs` 的 `host intent capacity refuses fresh requests after the hard limit and still preserves an active grant` 旧判据失败，修后核对活跃授权、20 笔溢出、256 上限、终态与重置。第二项 `host intent capacity counts each actor request once including loaded grants and terminal retries` 核对冷载授权、不同农工同 ID 和重复确认。修后同命令通过 MachineCore 136 项，原记录 `.local/p1-host-intent-cap-green.txt`。

## 验证结果

2026-11-13 生产 Release 隔离构建 `.local/p1-host-intent-cap-build` 零警告零错误。原生双端尚未实跑 256 条和菜单溢出反馈，保留待验状态。

## 风险与回滚

仅影响已鉴别来源的专用 LAN 事务，单人和旧只读协议保持现有行为。普通长期游玩仍需每农工的有界序号或等效机制。撤回关联提交可恢复旧容量行为，现有预留和回执无需迁移或删除。

## 关联记录

关联 [开发计划](../DEVELOPMENT_PLAN.md)、[主机协议](../P1_HOST_PROTOCOL.md) 和 [交接](../HANDOFF.md)。修复随关联提交交付，尚未推送。
