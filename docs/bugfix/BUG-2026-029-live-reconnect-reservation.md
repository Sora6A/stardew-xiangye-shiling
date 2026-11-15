# BUG-2026-029 同进程重连后未扣料预留无法结束

- 状态: 本地验证通过
- 发现/更新日期: 2026-11-13 / 2026-11-16，Asia/Shanghai
- 责任任务/分支: P1-NET-04，`codex/first-stable-release`
- 性质: 生产代码缺陷，保留修复前组件失败记录

## 现象与影响

农工提交开工意图后，在本机尝试扣料前断开连接。主机若继续运行，同一原生预留会阻挡新授权。农工重连后因原待处理请求已清除，无法证明自己能安全撤销。库存未写入时应能在取得可信新连接后结束该笔预留。

## 环境与前置条件

固定 Windows 游戏 1.6.15.24356、SMAPI 4.4.0、普通直连事务开关。组件回归使用已安装 SDK，创建与真实原版断开动作等价的生产意图隔离状态，不加载存档。

## 复现步骤

在组件中登记真实菜单类型的原请求、世界和主机，调用生产连接失效处理，再用同世界、同主机的新连接检查精确旧请求。旧 DLL 能显示未确认，但不保留匹配证据。失败输出 `Same-world reconnect lost exact pending intent after FenceIntent`，记录 `.local/p1-owner-detached-red/result.txt`。

## 根因分析

`FarmhandReservationClient.FenceIntent` 清除旧菜单与意图。新连接不会重新执行旧请求，活跃撤销又依赖本地待处理意图。持续在线的主机无冷载前态证明，不能单靠断线释放预留。

## 解决方案

在当前进程保留实际菜单发起且从未尝试写入的精确请求。只有同一世界、同一主机和农工在新可信连接上读到相同请求，才能走原生 Declined 路径。该路径仍核对日志缺失及精确库存前态，不允许新扣料。终态由原生回执收尾；关闭进程后不推断曾经的本地意图。

## 单元测试

SDK 回归位于 `tests/GameAdapter/Program.cs`，名称为 `same-process reconnect preserves only an exact never-attempted detached intent`。同一构建的 GameAdapter 对旧生产 DLL 运行 25 通过、1 失败，修后 26 项全通过。检查重复隔离保留证据、错误世界或主机、原连接、错误请求与已尝试写入均不符合。完整报告见 `.local/p1-owner-detached-{red,green}/gameadapter.txt`。运行方法见 [开发与启动](../DEVELOPMENT.md) 的 GameAdapter 指令。

## 验证结果

隔离生产 Release 零警告零错误。XYSNET08 隔离双端主机 34200、农工 27596。农工通过真实设备菜单提交，取得 Reserved 后未扣料即原版退出。主机仍在线，离线农工黄豆 6、无日志，同一 Reserved 原文保持。同进程、同农工原版重连取得新连接和挑战，双方观察到 Declined、Cancelled 和同一旧回执，修订保持 0、无扣料。主机核对重新到站位后，农工真实菜单重新开工并领取，双方最终修订 2、黄豆 3、豆腐 2。两端零保存零错误，81 个存档文件和原 Mods 哈希一致，已退出并禁用。见 [实测](../validation/P1-native-inventory-2026-11-11.md#普通直连同进程重连后的撤销与继续生产)。

## 风险与回滚

不改变协议、存档或回执格式。保留旧预留和库存可继续原生冷恢复。撤回该修复会恢复同进程重连无法主动撤销的行为。

## 关联记录

关联修复 `b7ccecc1c7bdbd0c1c2e063cef04b1c31118f9c2)、[开发计划](../DEVELOPMENT_PLAN.md)、[主机协议](../P1_HOST_PROTOCOL.md) 和 [交接](../HANDOFF.md)。本地验证尚未推送。
