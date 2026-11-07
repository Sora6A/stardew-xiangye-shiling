# BUG-2026-022：菜单场景误用夹具模组的翻译上下文

- 状态: 本机SDK组件回归及完整18项通过，待审查/提交/合并
- 发现/更新日期: 2026-11-02发现，2026-11-03更新，Asia/Shanghai
- 责任任务/分支: P1单人制作指南；feat/p1-game-adapter；基线8f2468d83b43fb805073f73bbc8b989cf716852e
- 性质: 新增测试夹具缺陷，开发期实际SDK失败；不是玩家侧缺料或事务故障

## 现象与影响

首次Memory实测通过9项后，新场景`missing ingredient preview reports deficit without request and retries when stock arrives`失败，Failure为`Missing preview omitted the actual deficit or acquired/mutated an operation`，Restored=true。预期提示含实际黄豆名称及差额3；错误夹具传入的翻译上下文不属于生产模组，阻断文本断言及后续集成。此前指南导航/完成锁定场景已通过。

实际八份存档未变化，无原生Save调用；原报告声明库存已恢复，失败及二进制/日志独立保存。不能据此认定玩家正常菜单存在翻译或扣料缺陷。

## 环境与前置条件

Windows x64、游戏1.6.15 build24356、SMAPI4.4.0、CP2.9.0，工作区独立运行时。Memory/XYSP1UI/8624092802，Enabled=true/OpenPreview=true，网络探针关闭。首次PID53352；生产DLL f34e86be090967bfa3b5c253b3e2930d88072d8a4ab31ca2bdd1a86f0eed6148、修前夹具DLL4c5d37516d062cfdff8e895e52f5ce4b7ff10b03b7f92f68df9bb0a6647ec5d4。

## 复现步骤

1. 停止游戏，备份专用档/config，部署匹配生产包与上述修前夹具；启用单人Memory后加载固定XYSP1UI。
2. 新缺料场景清空背包、放置临时豆坊，直接`new MachineMenu(service, machine, this.Helper.Translation)`；测试自身Helper属于GameHarness。
3. 点击开始区域，断言反馈含实际黄豆显示名和差额3、库存/raw不变、未创建Pending。
4. 首次实际得到上述断言失败，原报告Passed=9/Restored=true；finally恢复库存/清除临时设备。关闭且不保存，独立核存档。

## 根因分析

`this.Helper.Translation`归属于夹具自己的mod ID，生产菜单需要主模组的翻译器。新测试绕过MachinePatches.Interact直接构造菜单并传错依赖；正常设备互动入口由生产补丁持有正确Text，不存在这条错误传入。

导航场景只检查状态/请求/库存，没检查文案，因而先通过；编译也无法判断运行时翻译归属。原失败日志未单独输出Feedback文本，不能冒称保留了其原字符串；归因依据两处明确的错误调用以及修后同生产DLL/内容包的具名SDK复验。

## 解决方案

两项新菜单场景清空原活动菜单后调用真实`machine.checkForAction(player)`，从Game1.activeClickableMenu取得生产MachineMenu；缺失则失败。既复用生产翻译器，也覆盖实际Harmony入口，不复制词典或新增测试专用生产API。原活动菜单在finally恢复，库存/设备恢复机制不变。只修源/重建/部署夹具，生产代码与ZIP不变。

## 单元测试

本问题使用**真实SMAPI中的SDK菜单组件回归**，不是无游戏依赖单元；纯MachineCore不能重现不同mod的Helper翻译归属，没有编写仅检查源码字符串的伪单元。文件：[ModEntry.cs](../../tests/GameHarness/ModEntry.cs)。

构建：`dotnet build tests/GameHarness/GameHarness.csproj -c Release -p:GamePath=D:/code/stardew/.local/validation-game -p:BuildProjectReferences=false`。运行：匹配独立运行时启用Memory并单人加载固定档，无控制台或保存。

| 名称 | 关键断言 | 实际结果 |
|---|---|---|
| `missing ingredient preview reports deficit without request and retries when stock arrives` | 提示含物品名/差额3；缺料不创建Pending、不改库存/raw；补齐后同菜单仅开工一次 | 首次实际SDK失败；修后新进程17744整轮18项通过，原报告无Failure/Restored=true |
| `guide navigation never acquires an operation and preserves the completed-menu lock` | 查看/翻页/返回不创建请求或改库存；成功开工后看指南再返回不能执行第二次 | 首次SDK通过；修后同一完整18项再次通过，未拼接旧结果 |

真实修前报告/日志/部署DLL-PDB在`.local/p1-singleplayer-guide-2026-11-02/failed-translation-fixture`，SHA及来源见audit.json。修前源码原字节未单独保存：首次私有归档因SMAPI重排JSON格式被字节断言拒绝，后续先修源并编译；实际修前安装二进制与原失败报告仍保留，after-fix源码副本不能冒充修前原字节。

## 验证结果

修后夹具构建`p1-singleplayer-guide-harness-fixture-fix-2026-11-02.txt`exit0/0警告0错误；停止部署`p1-singleplayer-guide-fixture-fix-deployment-2026-11-02.txt`exit0，恰8项夹具build/部署/profile变化，生产f34e/当时ZIP40f1/文案/八save/六禁用config不变，实际预检0。新夹具SHA744f9090c49479a0a2b1b1717580cb97ea274cb904060b5be3e518f5719b2258。

修后PID17744于11-02 23:54:59实际单人加载，23:55:00完整18项通过，Failure=null/Restored=true。SDK原报告SHA71fd1f7862bd699230bdd3b62ca928811dc0717fc0a2634ff28bd43e812ef306，原报告/日志/退出snapshot在after-memory-green，两场景PASS均归同进程；两个预期事务故障注入ERROR单列，其余ERROR=0，无NRE/保存。中文实际鼠标查看两页指南及黄豆3/小麦2差额，确认正常生产翻译文本，不将脱离地图的预览当真实交易证据。

23:59:55窗口X退出；11-03收尾`p1-singleplayer-guide-sdk-green-closeout-final-2026-11-03.txt`exit0，CIM0/八save不变/六config原字节禁用/85项匹配停止部署/实际预检0。原初稿将单人日志误匹配为Main player导致exit1、尚未创建归档目录；修正为Single-player后通过，初稿失败保留。随后指南路线纯文案改为投入/产出，生产DLL不变，不影响本修复的18项证据；最终包核对见指南验证记录。

首次私有归档exit1且未创建目录；改为核完整JSON语义并记录新build已变化后，`p1-singleplayer-guide-fixture-red-archive-final-2026-11-02.txt`exit0。这不是生产bug或原场景红。前轮纯核心112未因仅改SDK夹具而重复运行。

## 风险与回滚

真实入口未返回菜单、世界/角色不符时必须失败，不能退回手动new绕过入口。修复只影响本地不分发夹具，不改变生产功能、设备schema或存档。回退先关游戏/恢复本轮Memory config原字节，再替换已知夹具DLL及匹配PDB/profile元数据；保留失败/成功报告，不清空或回滚玩家库存，不丢弃累计未提交树。

## 关联记录

[指南验证](../validation/P1-singleplayer-guide-2026-11-02.md)、[GameHarness](../../tests/GameHarness/README.md)、[交接](../HANDOFF.md)、[贡献规范](../../CONTRIBUTING.md)。尚无本次提交SHA/PR/merge，基线HEAD不是修复提交。
