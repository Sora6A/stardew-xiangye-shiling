# BUG-2026-004：实机夹具使用错误的 API 与库存事件入口

- 状态: 本地集成验证通过，待提交/合并
- 发现/更新日期: 2026-09-28，Asia/Shanghai
- 责任任务/分支: P1 集成夹具，`feat/p1-game-adapter`
- 性质: 新测试代码缺陷；两次失败均发生于专用测试档，未进入开发包

## 现象与影响

首轮仅通过商店/配方数据测试后出现 NullReferenceException；修正 API 后通过七项，但故障用例出现 Fault reported success。后者是注入事件没有触发，不能据此推断游戏事务忽略异常。两轮夹具均进入 finally 恢复背包、时间和临时设备，没有保存游戏。

## 环境与前置条件

游戏 1.6.15、SMAPI 4.4.0、CP 2.9.0、alpha.2、本地 GameHarness；专用单人农场 XYS_P0。固定容量背包，无额外库存模组。

## 复现步骤

1. 原夹具使用 `GetApi<object>(Id)`；SMAPI 明确报错泛型类型必须是公开接口，返回空引用，随后执行服务失败。
2. 改用非泛型 GetApi 后，原夹具订阅 OnInventoryReplaced，再执行同容量 OverwriteWith。
3. 数组不换代，监听器不触发，事务正常成功；“必须失败且一次注入生效”的断言失败。
4. 两次失败报告留在 `.local/p1-harness-first-result.json` 与 `.local/p1-harness-callback-before.json`。重跑方式见 [夹具说明](../../tests/GameHarness/README.md)。

## 根因分析

误把泛型 API 映射当成返回任意 class 的转换，并误把 OverwriteWith 的名字理解为数组替换。实际非泛型 GetApi 返回同进程 API 原对象；Inventory.OverwriteWith 调用 NetList.CopyFrom，同容量先逐槽清空再填充，触发 OnSlotChanged。底层扩容换代才可能触发 OnInventoryReplaced。

## 解决方案

非泛型 GetApi 加类型检查，缺少服务时在任何夹具修改前明确报错。故障监听改用 OnSlotChanged，一次注入后立即停用；静默改写等待非空新物品出现，避免测试空槽。finally 解除事件并恢复备份。

## 单元测试

无游戏的单元测试不能复现 SMAPI 代理与 NetList 事件分发，本缺陷不伪造 mock 通过。使用实际游戏组件的集成回归：`real inventory callback failure compensates both sides` 与 `real silent callback change is detected and compensated`；关键断言为一次注入确实触发、操作被拒绝、原物品引用/数量/机器 JSON 恢复。

修复前两项无法通过。纯提交协调器另由 CommitScenarios 的对应故障场景覆盖，但不当作本夹具入口的验证。2026-09-28 实际重跑：全部 9 项通过，两项注入均生效且恢复成功；Failure=null、Restored=true。

## 验证结果

GameHarness 和主工程构建 0 警告/0 错误；修正后的完整实机结果见 [P1 验证](../validation/P1-2026-09-28.md)。本记录不代表正常购买路线、存读档或联机已通过。

## 风险与回滚

游戏 API 或库存事件语义变更时需重跑真实集成用例。夹具独立打包、默认禁用；回滚可禁用或移出测试配置中的 GameHarness，保留失败证据，不修改正式玩家存档。

## 关联记录

`tests/GameHarness/ModEntry.cs`、[BUG-2026-003](BUG-2026-003-commit-postcondition.md)、[交接](../HANDOFF.md)。尚未提交、推送或合并。
