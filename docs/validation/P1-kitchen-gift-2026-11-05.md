# P1 原生厨房入口到成品赠礼

日期：2026-11-05，Asia/Shanghai。新进程49616的一次完整KitchenGiftInteraction通过，补齐真实农舍灶台入口→原版烹饪两份→保留实际成品送Robin。P1整体未验收；本轮没有生产代码/内容扩展或新发行包。

## 基线和范围

分支`feat/p1-game-adapter`，HEAD仍`8f2468d83b43fb805073f73bbc8b989cf716852e`，未提交/推送/PR/合并/远程CI。累计修改保留，86项运行原字节（含此前赠礼原报告）及155份源数据/文档在`.local/p1-kitchen-gift-2026-11-05/{baseline.json,before,before-source}`。

独立测试游戏1.6.15 build24356、SMAPI4.4.0、CP2.9.0，中文1280×720；专用XYSP1UI/8624092802。场景默认禁用，只更新runtime夹具，网络profiles保留旧版/禁用。生产src/catalog/content/tools及开发ZIP不变。

准备会在当前内存调用原生农舍升级方法生成厨房、授予既有菜谱、提供香椿2/豆腐2，使用空选中槽、空冰箱与固定正午。不自动打开菜单，不调用地图Action、购买、烹饪或赠礼API。制作后的两份料理保留同一背包；准备NPC时不覆盖库存或再次创建成品。NPC位置/日程与零好感是内存准备，不能据此声称步行开店、学习菜谱、购买房屋升级、自然寻人或完整获料加工链通过。不保存、不恢复本次内存，必须退出进程。

## 失败与修复

首轮43576已实际打开原版厨房并做两份，但入口判据错误拒绝；完整原失败源码/二进制/报告保留。第二轮3940仅加只读输入采样，旧准入保持不变，最小打开/关闭菜单后重现同一入口拒绝，未进行烹饪断言。两轮独立关闭/恢复配置/存档哈希通过，不与最终成功拼接。

实际Grab=(3,5)、准备灶台=(3,4)，原版会尝试邻近交互格。修复以只读postfix记录实际成功的厨房Action，仍分别核验物理右键/原生菜单/完整扣料与产量；没有改生产配方，也没有放宽为只要看到菜单就通过。根因、具名单位回归、修前证据、风险和回退详见[BUG023](../bugfix/BUG-2026-023-harness-kitchen-action-source.md)。

## 同次修后实机证据

23:18:49原生加载专用单人档，之后由computer-use实际鼠标操作。

| 阶段 | 实际操作与原报告 |
|---|---|
| 原生地图入口 | 右键真实厨房区域，KitchenClicks=1/KitchenMenus=1；原生performAction实际(3,4)、token=kitchen、Succeeded/LocalActor/CurrentLocation均true，Grab仍(3,5) |
| 原版烹饪 | 实际点击菜谱制作两份并放入背包；香椿2→0、豆腐2→0、料理0→2、原生recipesCooked增量2；灰色配方与原料0已实际查看 |
| 保留成品赠礼 | 关闭菜单并核验后，只准备人物；用同一背包料理实际右键Robin，原生感谢对话出现，料理2→1/好感0→45/当天与本周计数1/总赠礼增量1 |

最终`Completed=true/KitchenCompleted=true/Step=1/Failure=null/SavingObserved=false/SavedByHarness=false`，两条Evidence分别为Stage=-1厨房与Stage=0赠礼。本模式不重复此前已通过的同日第二次拒绝；其独立原报告保持原字节。

原报告SHA为`7fee57c886c96bfdbeb01dc5fc526fd3182ee663a212715fe4758d71c77e0c03`。最终报告/退出日志/86项snapshot及停止audit在`after-kitchen-gift`；失败原文分别在`failed-kitchen-input`、`failed-input-observation`。没有保存截图，不提供虚构截图链接。个别烹饪/关闭点击未立即响应，均重新查看现场后重试；以实际库存与原生计数判断，不将工具调用次数当作成功次数。

## 检查和停止审计

```powershell
dotnet run --project tests/MachineCore/MachineCore.csproj -c Release
dotnet build tests/GameHarness/GameHarness.csproj -c Release "-p:GamePath=<独立测试游戏>" "-p:SmapiPath=<独立测试游戏>" --nologo
python tools/check_workflow.py
git diff --check
```

旧坐标假设提取后的具名回归实际非零失败：112既有项后，新fallback用例抛错，见`kitchen-proof-red.txt`。修后`kitchen-proof-green.txt`exit0，全115项通过，含新增三个来源/拒绝场景。SDK最终`build-native-action.txt`exit0、0警告0错误。之前两次构建也通过，失败发生于真实入口判据，不将编译通过当作实机通过。

最终测试窗口X退出，游戏/SMAPI进程0，八受保护XML与本轮before原字节一致，六配置原字节恢复禁用，旧赠礼原报告不变。86项仅build/runtime夹具DLL/PDB共四项变化，profile身份预检exit0；最终日志ERROR行0、无原生Save调用。SavedByHarness=false只表示夹具不调用保存，实际磁盘保护由独立哈希证明。没有标题清理/保存该链/联机验收声明。

本次build/runtime夹具SHA`1fd039890c1ddfe24eebb1006b2421978d9c1cab895704129ded73e46c0901de`；生产DLL仍`f34e86be090967bfa3b5c253b3e2930d88072d8a4ab31ca2bdd1a86f0eed6148`，开发ZIP仍`514839e644dd71c414c3e9374862ca19353d7112a8ef75cb4dea457a09b7a0c1`。不为未改的生产包重新打包，也不重跑无关Python/网络矩阵。

最终workflow.txt实际exit0，维护记录/本地链接通过、0提交正文检查；delivery.txt实际exit0，17项新增/修改公共文件（含未跟踪）的UTF-8/空白/冲突标记、当前86项/厨房报告/CIM0/分支/HEAD通过，生产src/catalog/content/tools/ZIP不变。清单在delivery-audit.json，补充最终结果后的两份文档再次核文本。未执行远程CI。

## 复跑、回退与下一步

模式与操作见[GameHarness说明](../../tests/GameHarness/README.md)。停止进程、备份专用档/配置，并将旧kitchen-gift-interaction-result.json归档；只对独立runtime设置Enabled=true/Scenario=KitchenGiftInteraction/OpenPreview=false/正确XYSP1UI世界ID。自动加载后右键灶台、做两份入包、关闭菜单、中键；再右键Robin、关闭对话、中键。没有把准备或聊天等待算入的用例倒计时，不睡觉/保存，结束关闭整个进程、恢复禁用并核磁盘。

本轮私有脚本均已执行且输出不能覆盖。回退停止后恢复before中的runtime夹具与配置，撤回新增模式/观察器/纯判据即可，不改生产物品或清空存档。下一步是三家商店真实地图柜台入口和完整手工两设备链；不再重复本轮已通过的厨房/赠礼来消耗开发时间。
