# BUG-2026-026 设备菜单缺少键盘和手柄焦点

- 状态: 本地验证通过，待提交
- 发现与更新日期为 2026-11-11，Asia/Shanghai
- 任务分支为 `codex/first-stable-release`

## 现象与影响

进入豆坊或发酵缸后，方向键无法在控件间移动，Enter 无法打开指南或确认加工。使用原版手柄菜单导航时，设备菜单没有供方向键选择的控件。键盘和手柄玩家需要鼠标才能操作配方与指南。

## 环境与前置条件

Windows x64，星露谷 1.6.15 build 24356、SMAPI 4.4.0、Content Patcher 2.9.0。使用独立 `XYSP1Menu` 测试档及 GameHarness 的 Memory 模式。夹具通过实际设备的 `checkForAction` 打开生产菜单。

## 复现步骤

1. 打开豆坊菜单，避免使用鼠标。
2. 按 Enter 打开指南，再用方向键选择翻页按钮。旧菜单没有对这两个动作提供输入处理。
3. 启用原版手柄菜单导航后重新打开设备菜单，用方向键定位控件。旧菜单没有可吸附焦点。修复后可确认控件、翻页、返回并执行一次开工或领取。

## 根因分析

`MachineMenu` 仅提供鼠标命中范围，未注册 `ClickableComponent`。旧版 `receiveKeyPress` 只在指南中识别 Esc。此前的界面回归直接传入鼠标坐标，未覆盖键盘或原版手柄路径。

## 解决方案

设备菜单将已缩放的按钮注册到原版焦点导航，窗口缩放时按控件 ID 保留当前选择。键盘方向键和游戏绑定的移动键切换选择，Enter 与 Space 确认。手柄 A 继续使用原版焦点处点击，B 使用原版取消键，在指南内先返回设备菜单。确认和鼠标共享事务入口。手柄 A 的点击与同帧映射键合并为一次确认，配方仍受同一菜单的成功锁保护。

## 单元测试

使用 `tests/GameHarness/MachineMenuNavigationScenarios.cs` 的实际游戏程序集回归 `machine menu supports keyboard and gamepad navigation without a second transaction`。验证指南翻页与缩放后焦点、无事务副作用、手柄 A 在动作键重绑为 Enter 时的单次确认、取消及完成锁。该测试依赖设备互动、游戏菜单与真实背包，需部署 Release 夹具运行。

## 验证结果

旧生产 DLL 与新增回归夹具在独立进程运行，前 19 项通过，新用例停在 `Keyboard confirmation did not open the guide.`，报告为 `Restored=true`。新生产模块和夹具 Release 构建均为零警告、零错误。首轮修复版后台实测通过键盘与焦点选择断言，但读到的桌面指针在焦点控件之外，停在 `Gamepad cursor is outside the focused control.`。

复跑夹具通过原版菜单移动焦点，核对控件注册与缩放范围，按游戏的吸附坐标将 A 确认传入点击回调，并发送同帧映射键。新进程 26992 的 Memory 回归通过 20 项，`Restored=true`、`Failure=null`，进程已退出。生产 DLL 的 SHA-256 为 `0925D770A70F2C956E6C1D5123A9AB0037D89AED2211B60EE2975687C0663404`。三轮本地报告分别位于 `.local/p1-menu-input/red`、`green`、`green2`。实体手柄与实际桌面指针吸附仍需实测。

## 风险与回滚

不同手柄配置和实际手柄焦点显示仍需验证。可以回退生产菜单和对应回归，存档格式及机器数据没有变化。

## 关联记录

对应 P1 的共享设备菜单与手柄验收。参见 [开发计划](../DEVELOPMENT_PLAN.md)、[开发交接](../HANDOFF.md) 和 [GameHarness 说明](../../tests/GameHarness/README.md)。本次修复待提交。
