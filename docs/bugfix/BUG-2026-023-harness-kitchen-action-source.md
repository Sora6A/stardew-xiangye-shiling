# BUG-2026-023：厨房夹具把抓取格当作原生最终交互格

- 状态: 本地验证通过；纯判据先红后绿、完整原生厨房/烹饪/赠礼通过，待审查/提交/合并
- 发现/更新日期: 2026-11-05，Asia/Shanghai
- 责任任务/分支: P1单人厨房地图入口，feat/p1-game-adapter
- 性质: 新开发的本地验证代码缺陷；非线上事故，非生产配方修复

## 现象与影响

实际右键厨房后，原生烹饪菜单正常打开，两份料理也已制作；但中键核验以“Require actual kitchen-tile input and the resulting native cooking menu”失败，KitchenClicks/KitchenMenus均为0。预期应记录真实入口并继续核验原料守恒。错误只阻断验证，生产数据/物品未改；两轮均退出且八份保护XML未变。

## 环境与前置条件

独立游戏1.6.15 build24356、SMAPI4.4.0、CP2.9.0，中文1280×720；XYSP1UI/8624092802专用单人副本，KitchenGiftInteraction启用。内存准备升级农舍、已学菜谱、香椿2/豆腐2及空选中槽；不保存。原生地图准备灶台为(3,4)。

## 复现步骤

1. 使用本轮修前夹具启动专用副本，右键画面中的厨房上部。
2. 原版菜单打开。完整首轮制作两份料理、放入背包并关闭；第二轮最小复现只打开/关闭菜单。
3. 中键核验，均在入口证据检查失败；第二轮尚未进行原料/产量验收，不把它写为两次烹饪通过。

首轮进程43576，原失败报告SHA`dd65174f72def8a3b82f2d3fc19e6aa6139b38f801e691b1e66799c15455d268`；源码、部署DLL/PDB、前后退出日志保存在`.local/p1-kitchen-gift-2026-11-05/failed-kitchen-input`。第二轮进程3940，只加只读采样、旧准入不变；原报告/源码/部署二进制在`failed-input-observation`，真实样本为Configured=(3,4)、Cursor=(3,3)、Grab=(3,5)、ActiveMenu=null、CanMove=true，CursorAction/GrabAction均null，原生菜单实际已打开。

## 根因分析

旧OnButton要求`cursor.GrabTile == KitchenTile`才累计点击，OnMenu又依赖点击已累计，导致真实菜单也被漏记。SMAPI的抓取/指针候选不等于原生最终检查格；本机原版Game1.pressActionButton会按距离/可见指针及相邻候选继续尝试交互。真实采样已证明旧坐标相等假设不成立，不能通过移动鼠标到唯一“夹具喜欢”的坐标掩盖问题。此前Interaction直接创建菜单，不覆盖此类原生地图入口。

## 解决方案

只在KitchenGiftInteraction临时安装只读Harmony postfix，观察原生`GameLocation.performAction(string, Farmer, Location)`成功返回的实际格/token。要求当前Location、本地Farmer、实际灶台格及kitchen token；鼠标输入、原生烹饪菜单和扣料/产量仍分别核验。postfix不调用Action、不改变返回值/库存；完成、失败或回标题时解除，不进入生产ZIP。

共享无SDK判据[KitchenEntryProof](../../tests/GameHarness/KitchenEntryProof.cs)使用Actual格匹配准备灶台，Grab仅保留诊断。SDK生产者独立填真实Action字段，纯判据/人工样本不证明发生了原生调用。修后完整实机仍须取得实际Action、菜单、料理和赠礼证据。

## 单元测试

[KitchenEntryScenarios](../../tests/MachineCore/KitchenEntryScenarios.cs)，命令：`dotnet run --project tests/MachineCore/MachineCore.csproj -c Release`。

- `kitchen native fallback action is accepted despite different grab tile`：真实红的Grab=(3,5)与灶台=(3,4)不同，实际成功厨房Action为(3,4)时应接受。先把旧坐标假设提取到共享判据，新测试实际失败；`kitchen-proof-red.txt`保留非零退出/堆栈及修前源码。它是提取旧假设的纯回归，不冒称旧DLL直接执行新增方法。
- `kitchen configured grab tile cannot stand in for a different actual action`：Grab恰好命中也不能替代不同的Actual格。
- `kitchen source proof rejects failed foreign and non-kitchen native actions`：失败返回、其他Farmer/Location、空token、商店Action均拒绝。

修正共享判据后，`kitchen-proof-green.txt`实际exit0，以上3项及全115规则/提交/诊断场景通过。最终SDK构建`build-native-action.txt`exit0、0警告0错误。没有把纯回归当作原生地图或完整P1验收。

## 验证结果

两轮原游戏失败/独立退出审计均保留。修后新进程49616于23:18:49加载专用单人档，同次完整KitchenGiftInteraction通过：右键/原生厨房菜单各1，真实成功Action为(3,4)，同时Grab仍(3,5)，证明不再受旧坐标假设影响；香椿/豆腐各2→0、料理0→2、原生烹饪增量2，随后保留同一库存送Robin一份、料理2→1/好感+45/计数+1。原报告Completed=true/KitchenCompleted=true/Step=1/Failure=null/SavingObserved=false，SHA为`7fee57c886c96bfdbeb01dc5fc526fd3182ee663a212715fe4758d71c77e0c03`。

原报告/退出日志/86项snapshot/结束audit保存在`.local/p1-kitchen-gift-2026-11-05/after-kitchen-gift`。窗口X退出，进程0/八XML不变/六配置原字节禁用/旧赠礼原报告不变/profile预检0；本次退出日志ERROR行0，无原生保存调用。两轮失败不与该次结果拼接，不视作普通地图行走、正常房屋升级购买或完整P1验收。生产DLL/开发ZIP、两端profile不变；当前状态见[交接](../HANDOFF.md)，无远程CI。

## 风险与回滚

原生重载签名变动会使仅本地场景失败；不能改为无Action来源也通过。只验证指定农舍/灶台，其他厨房、手柄和NPC日程另测。回退移除本轮模式及临时观察器、在停止状态恢复夹具二进制/禁用配置；不改生产包或清除玩家物品，不保存测试内存。纯回归仍保留以防重新引入坐标假设。

## 关联记录

[开发计划](../DEVELOPMENT_PLAN.md)、[单人状态](../P1_SINGLEPLAYER_STATUS.md)、[交接](../HANDOFF.md)、[场景入口](../../tests/GameHarness/GiftInteractionScenario.cs)。尚未提交/推送/PR/合并，修复提交须关联本记录并更新实际验证结果。
