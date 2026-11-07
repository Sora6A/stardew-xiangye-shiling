# BUG-2026-009：占用格放置误重置既有设备批次

- 状态: 本地实际游戏回归通过，待提交/合并
- 发现/更新日期: 2026-10-01，Asia/Shanghai
- 责任任务/分支: P1 放置与拆回验收，`feat/p1-game-adapter`
- 性质: 生产适配代码；本轮先增加实际游戏回归，再修改实现，非历史补录

## 现象与影响

直接调用携带设备的 `Object.placementAction`，目标格已有相同 ID 的加工中设备时，旧补丁返回成功且将既有设备重置为空闲；原批次 JSON 被替换，已经扣除的原料对应批次消失。未知/损坏状态同样存在覆盖风险。预期拒绝占用格，原设备引用、原 JSON、背包和携带对象均不变。

这是原生组件/API 边界的实机复现。普通鼠标路线通过 `Utility` 的碰撞预检通常提前拒绝；本轮未用普通鼠标复现覆盖，也没有正式玩家存档损失证据。

## 环境与前置条件

Windows、游戏 1.6.15、SMAPI 4.4.0、CP 2.9.0 固定标签本地构建，.NET SDK 7.0.302/net6.0。双包 `0.1.0-alpha.2`，单人主玩家，独立 `XYSP1UI` 副本；不保存，不加载正式农场。夹具默认禁用，操作说明见 [GameHarness](../../tests/GameHarness/README.md)。

## 复现步骤

1. 保持旧 `BeforePlace`/`AfterPlace`，加入 [PlacementScenarios](../../tests/GameHarness/PlacementScenarios.cs) 并在 Memory 中注册。
2. `dotnet build tests/GameHarness/GameHarness.csproj -c Release -p:GamePath=D:/code/stardew/.local/validation-game`，部署夹具，配置 `Enabled=true`、`Scenario=Memory`、`FarmName=XYSP1UI`、`OpenPreview=false`。
3. 启动测试副本，加载专用农场。夹具在相邻空格原生放置豆坊，背包放黄豆 3，通过真实执行器开工，然后把新建同 ID 设备放入背包，直接对已占用格调用 `placementAction`。
4. 2026-10-01：此前 12 项通过，首个占用格用例失败：`BeanWorkshop/Processing: occupied placement returned success; target JSON unchanged=False`；报告 `Restored=true`。红证据 `.local/p1-placement-before-2026-10-01.json` 和对应 `-smapi-` 日志仅留本机。

## 根因分析

游戏原生大设备放置遇同 ID 的既有对象时不替换地图对象，仍返回 `true`。旧 prefix 只检查携带物是否可移动，没有检查目标格；postfix 把成功布尔值视为新增对象的证据，对目标设备调用 `Initialize(newlyPlaced:true)`，重新生成空闲状态和 instance ID。原回归仅验证空格放置和移除，未验证这种原生成功但不插入的行为。

## 解决方案

[MachinePatches](../../src/XiangyeShiling/GameIntegration/MachinePatches.cs) 的 prefix 在任何写入前拒绝已占用目标；用 Harmony `__state` 记录本次自定义设备通过空格预检。postfix 仅在预检通过、原生返回成功、地图存在相同 ID 的新对象且其引用不同于携带物时初始化。原版设备、拒绝路径和异 ID 对象不写本项目状态。未改变 JSON schema、配方或数量，无数据迁移；已经被旧行为清掉的批次不能凭空重建。

## 单元测试

原生放置依赖游戏程序集与真实 Harmony 补丁，独立 MachineCore 没有该地图 API；没有编写伪造原生成功语义的纯单元测试。新增游戏组件回归两项：

- `bean placement rejects occupied processing ready corrupt and idle targets`
- `crock placement rejects occupied processing ready corrupt and idle targets`

每项覆盖加工、待领取、损坏及空闲目标，断言返回 `false`、目标引用/完整原 JSON 不变、库存全部槽引用/数量及 carrier 的位置、地图引用、owner、health 和 modData 不变；finally 恢复原测试格、库存和时钟。命令与启动步骤同复现；修复前首项实际失败，2026-10-01 修复后两项全通过。

## 验证结果

修复前 GameHarness Release 0 警告/0 错误，实际游戏红证据如上。修复后首次实机占用格两项通过，累计 14 项后因工具夹具选址失败中断（BUG-010）；不能把该轮称为完整通过。选址修正后同一独立农场重新运行，全部 16 项通过，Failure=null、Restored=true，其中两项占用格和两项原生工具/掉落/拾取/重放身份回归均通过。绿证据 `.local/p1-world-game-2026-10-01.json`、`.local/p1-world-smapi-2026-10-01.txt`；两个 Error 为预期事务回调故障注入，无新增非预期错误。

本日 26 核心、26 Python 通过，生产/夹具最终 Release 均 0 警告/0 错误，清单/维护通过。开发 ZIP 10 文件逐字节匹配 build/deployment，最终主 DLL SHA-256 `64c197c5eb8613218f79ac8ac528e349ef03db21893c4cd87744cb84239f8aa5`。原存档/UI 副本磁盘哈希不变，游戏关闭、夹具禁用；审计 `.local/p1-world-final-audit-2026-10-01.json`。本日没有重跑 Persistence/UI 全量矩阵，P1 总体验收继续未完成。

## 风险与回滚

本轮不验证其他第三方放置补丁或双客户端兼容；当前联机放置/加工继续拒绝。回滚代码需保留本回归并重新执行，应避免重新引入占用格初始化。测试结束退出不保存、禁用夹具；正式存档回滚用备份，不删除物品或 JSON。

## 关联记录

[计划](../DEVELOPMENT_PLAN.md)、[事务](../P1_TRANSACTIONS.md)、[本轮实测](../validation/P1-world-2026-10-01.md)、[交接](../HANDOFF.md)。基线 `8f2468d83b43fb805073f73bbc8b989cf716852e` 不是修复提交；本轮未提交、推送、建 PR 或合并，关联提交待授权整理。
