# BUG-2026-031 加工按钮过早显示完成

- 状态: 本地验证通过
- 发现/更新日期: 2026-11-17，Asia/Shanghai
- 责任任务/分支: P3 发行画面验收，`codex/first-stable-release`
- 性质: 设备菜单文案状态优先级；修复前画面和回归失败均已保留

## 现象与影响

点击开始后，菜单顶栏显示「加工中」和剩余时间，但灰色操作按钮显示「已完成」。豆坊与发酵缸共用这一渲染路径，玩家可能误以为能立刻领取。批次、背包和计时在实测中保持原规则。

## 环境与前置条件

Windows Stardew Valley 1.6.15.24356、SMAPI 4.4.0、Content Patcher 2.9.0，自动测试使用 .NET SDK 7.0.302。已放置任一模组设备，备足能立即开工的原料。

## 复现步骤

打开豆坊，翻到第 3 页，选择豆干并保留菜单。观察剩余 220 分钟和同一行按钮。旧构建按钮显示「已完成」；原生复验见 `.local/p2-native/p3-release-visual-v2/Mods/GameHarness/06-bean-processing.png` 与 `08-crock-processing.png`。

## 根因分析

菜单会话成功锁阻止同一个菜单再次提交操作。旧 `MachineMenu.draw` 将会话成功锁置于实际机器阶段之前，因此成功提交开工被显示为批次完成。既有核心测试验证锁与批次，没有检查渲染使用的标签。

## 解决方案

将标签选择移到已有 `MenuActionSession`，等待主机和加工中的状态优先于操作成功锁。新开的待领取菜单仍展示「领取成品」。同菜单继续禁止第二次事务。同步更新两种语言的多配方指南标题，内容数据和存档格式保持原值。

## 单元测试

`tests/MachineCore/Program.cs` 的 `processing action label describes the batch even after the start click succeeded` 用 `dotnet run --project tests/MachineCore/MachineCore.csproj -c Release` 运行。旧优先级在完成一次开工后返回 `machine.done`，新测试按预期在 `.local/p3-visual-bug031-red.txt` 失败。新优先级返回 `phase.Processing`，同时核对成功领取后的菜单锁与新开的待领取标签。修后 145 项通过，见 `.local/p3-visual-bug031-green.txt`。

## 验证结果

最终冻结包 `C79B0B67C9D80A61FCC51D962C958720F25C18EAD96A323613F8F6690A3F048E` 在 `XYSP3Visual_620261135` 的 28572 实机产生 11 张实际画面。两台加工菜单的灰色按钮均为「加工中」，等待结束后地图显示两个成品气泡，新的菜单允许领取。111 个现有文件保持一致，零保存零错误，测试退出并禁用。原版新档豆腐链复验通过。见 [发行候选验收](../validation/P3-release-2026-11-16.md)。

## 风险与回滚

标签的改变仅影响展示和翻页指南文案。回退提交或恢复上一个同版完整包可恢复旧标签，无需改写存档。

## 关联记录

关联 [开发计划](../DEVELOPMENT_PLAN.md)、[交接](../HANDOFF.md)和[发行验收](../validation/P3-release-2026-11-16.md)。本地修复提交随发布 PR 交付。
