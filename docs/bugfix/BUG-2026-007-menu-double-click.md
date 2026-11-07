# BUG-2026-007：领取连点会意外开工下一批

- 状态: 单元与实际菜单回归先红后绿，本地验证通过，待提交/合并
- 发现日期: 2026-09-29；更新日期: 2026-09-30，Asia/Shanghai
- 责任任务/分支: P1 菜单交互，`feat/p1-game-adapter`

## 现象与影响

设备已有待领取批次，背包还有下一批所需原料。用户在“领取成品”按钮连续点击两次，第一次领取后按钮立即变成“开始加工”；第二次点击被解释为新开工，产生用户未明确选择的扣料。不是同一事务重复执行，而是界面重用了按钮位置并创建了不同请求。

## 环境与前置条件

游戏 1.6.15 / SMAPI 4.4.0 / CP 2.9.0 / alpha.2，单人、背包足够容纳产物。无需联机或外部模组。验证仅使用独立 XYSP1UI 副本，不保存。

## 复现步骤

1. 准备黄豆 6，豆坊开工消耗 3，等待第一批完成。
2. 打开菜单，对领取按钮同一坐标连续调用两次真实菜单左键处理（实机夹具回归模拟连点）。
3. 预期豆腐 2、黄豆 3、设备空闲；旧逻辑第二次生成新请求并再次开工。
4. 抽出同等旧行为的 MenuActionSession 并补无游戏单测；成功后再次 Acquire 的负例应揭示该问题，而失败重试必须继续复用原请求。

## 根因分析

MachineMenu 每次点击重新读取阶段，成功后把 PendingRequest 清空。领取把设备转为 Idle，紧随其后的点击因此构造新 ID、新 revision 的合法 Start。规则层防重放无法拒绝这种新的合法事务；约束必须在一次菜单操作意图内实现。

## 解决方案

一次打开的菜单最多成功执行一个操作。成功后锁定会话并禁用动作按钮；需要下一批时重新打开设备。失败保留原请求和 revision，不锁定重试。所有底层库存/状态校验继续执行，不依赖时间防抖，避免机器快慢改变保护效果。

## 单元测试

- `menu failure retains the original request and revision for safe retry`：失败后可重试，仍绑定原请求和 revision。
- `successful menu action cannot acquire another request until reopened`：成功后的任何后续点击都不再生成请求；新菜单会话才使用当前 revision。
- 命令：`dotnet run --project tests/MachineCore/MachineCore.csproj -c Release`。旧逻辑在第二项断言失败；修复后全部 26 场景通过（16 规则、8 提交、2 菜单）。修复前证据 `.local/p1-menu-double-click-before.txt`。
- 游戏组件回归：GameHarness `collection double click cannot start an unintended batch`，对生产 MachineMenu 连续点击并断言设备空闲、黄豆 3、豆腐 2；这是依赖游戏的集成回归，不能冒充无游戏单测。
- 补充游戏组件回归 `full inventory collection retries in the same menu and locks after success`：满包首点拒绝，原 JSON/Ready/每槽引用数量不变；释放最后一格后同一菜单完整领取豆腐 2、黄豆仍 3，剩余木材完整；成功后再点，原 JSON 与全部库存不变。证明保留失败重试，成功才锁定。此补充测试在修复后加入，不伪称它有独立修复前失败日志。

## 验证结果

修复前实机组件回归确实失败于 `Collection double click unexpectedly consumed the next batch`，随后恢复测试背包；证据 `.local/p1-menu-double-click-game-before.json`、同名前缀 `.txt` 日志。修复后 2026-09-29 Memory 10 项通过；2026-09-30 新增满包重试与商店售出后，全部 12 项通过，Failure=null、Restored=true。最新报告 `.local/p1-game-harness-2026-09-30.json`、`.local/p1-game-smapi-2026-09-30.txt`；两条 Error 为明确故障注入，不能写零错误。无游戏 26 场景与 Python 26 项于本日复跑通过，主模块与夹具 Release 0 警告/0 错误。独立 UI 完整 13 步通过，豆坊两次领取后黄豆保持预期剩量、设备空闲，见 [UI 记录](../validation/P1-ui-2026-09-30.md)；不涉及联机保证。

## 风险与回滚

行为变化：成功领取后不能在同一菜单内直接再开一批，需重新打开。无物品 ID/schema/配方变化，无存档迁移。回滚 MenuActionSession、MachineMenu、文案和对应回归为同组修改；回滚后会重新暴露连点误开工。

## 关联记录

[P1 事务](../P1_TRANSACTIONS.md)、[交接](../HANDOFF.md)、tests/GameHarness/ModEntry.cs。本轮未提交、推送、PR 或合并。
