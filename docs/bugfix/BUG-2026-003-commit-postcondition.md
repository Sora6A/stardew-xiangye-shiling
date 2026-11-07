# BUG-2026-003：同步回调静默改写后仍报告事务成功

- 状态: 本地验证通过，待提交/合并
- 发现/更新日期: 2026-09-28，Asia/Shanghai
- 责任任务/分支: P1 单人游戏适配；`feat/p1-game-adapter`
- 性质: 未发布事务提交代码缺陷；本轮先复现再修复，非历史补录

## 现象与影响

当库存或状态 setter 的同步回调静默修改了提交结果，原协调器仍返回 Applied。可能造成投入、产物与机器状态不一致；未观察到玩家正式档损失，本次只在隔离夹具中注入故障。

## 环境与前置条件

Windows x64；.NET SDK 7.0.302 / net6.0；纯测试不需要游戏。实机目标为游戏 1.6.15、SMAPI 4.4.0、CP 2.9.0、本工程 alpha.2 与独立 GameHarness，专用测试农场。

## 复现步骤

1. 在原 CommitCoordinator 中运行 `dotnet run --project tests/MachineCore/MachineCore.csproj -c Release`。
2. `silent callback corruption is rejected and compensated` 用 fake 写入库存 1、状态 1，然后状态回调把库存改为 123，不抛异常。
3. 断言必须 RolledBack 且库存/状态均恢复为 0；旧实现返回 Applied，触发 `Commit boundary assertion failed`。修复前日志保存在忽略提交的 `.local/p1-commit-before.txt`。
4. 当前版本同命令应全部通过。实机夹具向 `OnSlotChanged` 注入一次静默堆叠数量改写，断言原引用、原数量和原始 JSON 都恢复。同容量 OverwriteWith 会逐槽写入，并非整数组换代事件。

## 根因分析

CommitCoordinator 只有写前快照核对与异常补偿，把 setter 正常返回当成成功证据。游戏 Inventory 的替换会同步调用库存监听器；监听器可以无异常改动结果。原 16 个纯规则测试只检查计划，不覆盖真实写入与回调。

## 解决方案

IPreparedCommit 增加 IsApplied；写库存、写状态后检查完整写集，不匹配则进入现有补偿流程。GameCommit 预先保存期望物品引用、数量/品质/堆叠规则/modData、容量、设备位置引用和状态 JSON，提交后逐项核验。两侧恢复均尝试，恢复后再核验；失败隔离会话，直到返回标题重置。日志回调抛错不能阻断补偿。

## 单元测试

- 文件：`tests/MachineCore/CommitScenarios.cs`，命令如上。
- 核心用例：`silent callback corruption is rejected and compensated`；修复前失败，修复后通过。
- 相邻回归：正常只提交一次；快照冲突/准备失败零写入；库存/状态写后抛错；重入拒绝；日志失败；恢复失败继续补偿另一侧并隔离。
- 2026-09-28 实际结果：24 个规则/提交场景全部通过。不能用此结果代替游戏集成验证。

## 验证结果

主工程 Release 0 警告、0 错误。GameHarness 实机 9 项通过，其中两种回调故障均被检测且完整补偿；联机未执行且功能禁用。最终结果见 [P1 实测](../validation/P1-2026-09-28.md)。

## 风险与回滚

补偿只保证已核验的同步单人事务边界；第三方回调若同时修改原物品引用、地图或外部系统，不能承诺任意副作用可恢复。恢复失败提示停止操作且不保存，排查日志。回滚应撤回整个未发布 alpha.2 原型、恢复测试前备份，不单独去掉 IsApplied 校验，不清理正式存档物品。

## 关联记录

`src/XiangyeShiling/Transactions/CommitCoordinator.cs`、`src/XiangyeShiling/GameIntegration/MachineService.cs`、`tests/GameHarness/ModEntry.cs`；[P1 事务](../P1_TRANSACTIONS.md)、[交接](../HANDOFF.md)。Commit/PR 待提交，无远程合并记录。
