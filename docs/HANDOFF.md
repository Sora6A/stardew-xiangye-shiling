# 当前开发交接

更新时间：2026-11-07，Asia/Shanghai。先读本页；11-03之前原文见[历史](HANDOFF_HISTORY_2026-11-03.md)，中间各轮详见对应validation和bugfix。旧PID、配置和内存不能接续。

## 当前目标与状态

11-07用户明确要求先上传目前文件。175份项目文件已实际提交并推送到feat/p1-game-adapter，首次完整基线为`baebea0b57a1edc51e5531bfaa909fb49e844de2`，已创建[PR #1](https://github.com/Sora6A/stardew-xiangye-shiling/pull/1)指向main，首次PR远程四项CI全部success。catalog40/30、Python116、MachineCore115、两CLI/生产/SDK夹具构建及适配CLI13已在索引独立导出树通过。当前补充上传记录/交接，作为同一PR的文档提交；本文为合并前已核检查点，最终合并状态从PR/Git核对。全部源文件与历史维护记录保留，.local/artifacts/游戏DLL/存档/完整日志未上传，下一保存任务排在上传之后。

本轮单人两设备世界输入链完整20/20已通过，9004已不保存退出、游戏/SMAPI进程0；八份受保护存档未变、六目标配置原字节禁用、88项停止审计及禁用profile预检通过。原报告Step20/Completed=true/Passed20/Failure=null/Saved=false，详见[完整实测](validation/P1-world-ui-2026-11-06.md)。不用旧7/20拼接，不再重复此已通过且未变化的子集。

两设备实际原生制作/放置、真实扣料/领取、加工与Ready挥镐保护、空闲击落、真实WASD走近吸取同一payload、重放新身份及损坏raw保护均通过；最终豆腐2/酱油4，设备各1在地图。材料/图纸/站位/阶段时刻及每次击落后单次移离磁吸属于明确准备，不能称完整普通获取或自然等待。

此前三家真实柜台购买、厨房→实际烹饪→同一料理赠礼及同日拒绝也各有完整子集证据。下一重点转为当前生产DLL的原生保存/重载/冷启动闭环。仍40物品/30配方、激活7物品/5配方，P1整体未验收，P2冻结；联机写入禁用，NET02高级矩阵及未实现NET03/04独立保留。

## 分支与工作区

实际任务分支feat/p1-game-adapter，首次开发提交`baebea0b57a1edc51e5531bfaa909fb49e844de2`已推送，父基线为`8f2468d83b43fb805073f73bbc8b989cf716852e`。此前仅3文档受跟踪的问题已解决：175项目文件进入Git。本次后续文档提交会新增[上传记录](validation/P1-source-upload-2026-11-07.md)并更新交接等说明；自身SHA在提交后交付附件/Git记录读取，避免循环amend。PR未在本检查点合并，不强推、不直推main；合并前核最新head与实际CI。

11-06实现阶段仅给WorldInteraction增加只读ControlState/最多32条有限键事件，并同步BUG024、索引、夹具说明与交付/交接文档；后续20步接续没有改代码。生产src/catalog/content/tools/共享纯判据/ZIP未改，无新增依赖或存档格式。首次盘点172公共文件清单在world-chain/delivery-inventory-before-doc.json，后来新增文档以本轮最终清单复核，不能照旧清单自动提交。

## 已完成与入口

- [WorldInteractionScenario](../tests/GameHarness/WorldInteractionScenario.cs)：同一9004完整20项；Prepare/Advance/RequireToolProof原方法体与诊断前逐段一致。新增ObserveControlInput/CaptureControlState只读、不控制移动、不放宽验收。工具汇总8阶段/原生命中9次，发酵缸Ready实际2次，其余各1；两次自然拾取分别61/60轨迹。
- [BUG024](bugfix/BUG-2026-024-world-movement-investigation.md)：旧36832人工D无移动/菜单可用，根因未定；新9004人工四方向/Esc正常，8条未抑制事件与实际位移已留原报告，随后世界链通过。只读诊断不是输入修复，保留调查状态。
- [WorldShopInteractionScenario](../tests/GameHarness/WorldShopInteractionScenario.cs)：同一30580三柜台Action/ShopId/实际购买学习通过，扣款4510g；[GiftInteractionScenario](../tests/GameHarness/GiftInteractionScenario.cs)：11-05独立赠礼与厨房到同一成品赠礼通过。均已退出、存档不变、配置恢复，保留原日期和准备范围。
- [MachineMenu](../src/XiangyeShiling/Menus/MachineMenu.cs)：指南/缺料预览不替代Service事务；单菜单成功锁、满包保留与失败重试仍在。各模式契约见[GameHarness说明](../tests/GameHarness/README.md)，玩家边界见[单人状态](P1_SINGLEPLAYER_STATUS.md)。

## 验证与限制

11-07上传检查：本地证据在`.local/upload-2026-11-07`，各具名json/log及index-before-tests.txt记录导出索引。完整Python116、核心115、适配CLI13和catalog通过；两CLI/生产与GameHarness构建均0警告0错误。首次提交消息/暂存差异/UTF-8检查通过，提交后范围检查实际1个完整消息通过；push实际exit0，PR四项远程CI绑定baebea0b完整SHA全部success，详见[上传记录](validation/P1-source-upload-2026-11-07.md)。后续文档head须另核，不能复用旧绿。源码/实机运行包未重部署、未启动游戏或保存；11-06实际20项继续保留原日期。

当前最终原报告SHA`ce6a7c6a535d2acea6f526396755682df45bc405eeafef6b1917dccd9ac7bc54`，位于`.local/p1-world-controls-2026-11-06/after-world-20/world-interaction-result.json`；同目录audit.json/SMAPI-latest.txt/profile-preflight.txt为收尾证据。完整阶段顺序/4次放置/2次同实体拾取/8工具阶段及9原生命中计数已核，有限键事件18/省略0/观测异常null。完成瞬间快照00a4328f…另存，不混作退出原文件。

八XML未变、六目标配置恢复before原字节禁用；88路径相对world-controls/baseline仅build/runtime夹具DLL/PDB四项变化。日志ERROR0/无原生保存标记，禁用profiles身份预检exit0；没有重跑网络。临时首次退出审计误要求8次命中，在恢复前停止；对照既有至少一次完整命中判据后逐项核原9条记录，保持源码/报告不变，修正依据在audit.json。

诊断实现阶段SDK Release0警告0错误、115核心通过；实际接续代码未变，不重跑纯检查。当前最终维护exit0/0提交正文，12项新增或修改文档UTF-8/空白/冲突检查通过（含未跟踪）；88运行哈希/新最终报告/源码未再变化/分支HEAD/暂存0/CIM0通过，公开候选文件175份、未纳入.local/artifacts/bin/obj。证据为after-world-20/{workflow.txt,delivery-audit.json}，tracked diff --check exit0，仅CRLF提示；无远程CI。09-28 Persistence6阶段和冷启动仅是历史版本结果，本次无保存世界链不替代当前DLL保存验收。

## 环境与运行状态

当前游戏/SMAPI进程0，测试窗口已实际标题栏X关闭，不算ReturnedToTitle测试。runtime为Enabled=false/Scenario=Memory/OpenPreview=false，主模块探针及host/client四目标配置均禁用。runtime旧world报告原字节已恢复，新完整20报告另行归档；不要读取旧原位报告后误称本轮退回7/20。旧gift/kitchen/shop报告、两端profiles及未知文件未覆盖或删除。

只使用独立`D:\code\stardew\.local\validation-game`，源安装未改。生产DLL SHA`f34e86be090967bfa3b5c253b3e2930d88072d8a4ab31ca2bdd1a86f0eed6148`；build/runtime夹具SHA`c8785359aa902b7cdd6b236e0864d2a4f9403385f008965dfec5b66609f33250`。网络profiles/curated仍旧版`744f9090c49479a0a2b1b1717580cb97ea274cb904060b5be3e518f5719b2258`，未刷新或重跑。开发ZIP SHA仍`514839e644dd71c414c3e9374862ca19353d7112a8ef75cb4dea457a09b7a0c1`，无需为诊断重建玩家包。

本轮快照/部署/closeout私有脚本或单次命令已执行，不能重跑覆盖证据。新88停止基线、最终构建源码及build输出在world-controls；更早world-chain保留161源快照/旧36832失败与收尾，world-shops保留三店原证据。普通SMAPI-latest下次会覆盖，使用归档。

## 下一步与验收

1. 先核PR #1、远程main与当前分支/HEAD，读取[上传记录](validation/P1-source-upload-2026-11-07.md)及Git实际完整提交，完成本轮文档push/当前head CI/PR merge commit流程；已执行部分不重复创建PR或改写历史。再核游戏进程及world-controls/after-world-20/audit.json的88 afterHashes与新原报告；不要重启WorldInteraction。静态维护命令为`python tools/check_workflow.py`。
2. 阅读[保存实测](validation/P1-persistence-2026-09-28.md)、[Persistence源码](../tests/GameHarness/PersistenceScenario.cs)及夹具README，核当前独立XYSP1Save/8624092801磁盘阶段与标记。先只读检查，勿直接重跑旧脚本、重置存档或覆盖已完成阶段；按当前真实状态决定复用或另建明确允许保存的测试副本，再记录新保存基线和配置。禁止在本轮XYSP1UI无保存准备里睡觉。
3. 在当前生产DLL上补开工/完成/领取后的原生保存、标题重载和冷启动防重复领取；阶段时间/库存/raw与磁盘身份分别核。若发现真实缺陷，先补能揭示问题的回归，再修复及写bugfix。不重跑已通过且未变化的三店/厨房/赠礼/20步世界链，不转回网络超时循环。
4. 后续按[P1交付组织](P1_DELIVERY_UNITS.md)在任务分支增量开发、复核完整提交树和受影响检查，使用完整commit正文并经PR merge commit保留历史。实际SHA/完整正文/PR/合并结果随每轮交付；本轮首次push和PR已成功，不沿用旧“没有远程动作”状态。

## 关联记录

[贡献规范](../CONTRIBUTING.md)、[开发计划](DEVELOPMENT_PLAN.md)、[单人状态](P1_SINGLEPLAYER_STATUS.md)、[玩家指南](PLAYER_GUIDE.md)、[完整世界链](validation/P1-world-ui-2026-11-06.md)、[BUG024](bugfix/BUG-2026-024-world-movement-investigation.md)、[三店](validation/P1-world-shops-2026-11-06.md)、[厨房](validation/P1-kitchen-gift-2026-11-05.md)、[独立赠礼](validation/P1-gift-2026-11-05.md)。
