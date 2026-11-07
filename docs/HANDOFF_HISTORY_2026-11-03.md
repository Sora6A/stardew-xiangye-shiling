# 历史交接快照

归档于2026-11-03。本文件保留旧交接原文；全部PID、配置开关、检查点及下一步只代表当时状态。当前任务只以[HANDOFF](HANDOFF.md)为准，不直接照历史步骤运行。

---

# 当前开发交接

更新时间：2026-11-02，Asia/Shanghai（推进顺序纠偏：优先P1单人可玩交付；旧Welcome实测待办独立保留，联机写入继续禁用）。最新现场以本段为准，下方旧检查点保留原时点。

## 当前目标与状态

11-03单人指南最终运行检查点：同一生产f34e实际Memory完整18项通过/Failure=null/Restored=true，含两新菜单组件回归；中文菜单、指南两页、返回及黄豆3/小麦2差额已鼠标查看。17744已X退出/CIM0，八save未变、六config原字节禁用，原18结果/日志/退出snapshot在after-memory-green；旧诊断result已归档后恢复原文件，未覆盖失败9项原报告。只在此后将路线符号改为中英投入/产出文字，生产DLL未变，最终包/本地副本键与tokens/ZIP内容/配置/存档及预检通过，完整SHA和85 afterHashes以final-wording-deployment/audit.json为准。最后文案未再次开游戏，不冒称新截图；下一步整理本轮验证/玩家指南/BUG022与P1单人实现状态、检查文档后交付开发包，不再启动游戏或重复联机测试。

11-02单人夹具修复检查点：首次实际Memory到9项通过，新缺料回归因测试直接new生产菜单却传入GameHarness自己的翻译器而失败（BUG022），指南导航/完成锁已通过；实际原报告Restored=true。53352已窗口X关闭/CIM0，失败原报告/日志/实际修前安装DLL-PDB归档failed-translation-fixture。原归档脚本先因SMAPI重排JSON格式的字节断言拒绝，随后源码已修并编译，不能冒称保存了修前源码原字节；实际修前部署二进制与原失败日志仍保存。现已恢复原report/config，八save/六禁用config及83运行包路径不变，仅新build夹具DLL/PDB待部署。修复两新场景改从真实machine.checkForAction取得生产菜单与生产翻译器，不改生产功能/DLL/包。夹具修后构建0警告0错误，待停止部署并复跑SDK回归。

11-02单人指南实现检查点：指南两页/真实配方用量工时、只读缺料差额/补料重试、完成提示着色、五项原型物品来源说明已实现；目录仍40/30、激活7/5。catalog16及生产/夹具Release各0警告0错误通过；停止部署85项恰28项构建/包/文案变化，八save/六禁用config不变，实际预检0。当前仅runtime GameHarness显式启用Memory/XYSP1UI/8624092802/OpenPreview=true，before原字节在本轮memory-enabled/before.json；网络探针和四角色config仍禁用，尚未启动游戏。接下来只加载该专用单人档运行现有集成与两项新菜单场景、查看中文指南；不睡觉保存，不重做联机矩阵。当前DLL/ZIP完整SHA在本轮deployment/audit.json，旧网络版已保存before。

11-02单人开发检查点：源码审计确认两条加工/厨房及三家商店来源已实现，当前新增游戏内制作指南与缺料预览，改机器菜单与既有7个原型物品的来源说明，不扩P2。原当前85项/CIM0已核，`.local/p1-singleplayer-guide-2026-11-02/{baseline.json,before,before-source}`保存本轮原字节与累计来源；六配置仍禁用、八save不变。将保持菜单原交易按钮位置及一次成功锁定/满包重试语义，指南不获取事务请求或写库存。实现/构建/本轮界面验证尚待，不复用网络测试结果验收新UI。

11-02用户反馈近期长期停留测试、玩法进展不足。本轮审计确认最近增量主要是联机观测/校验基础设施；当前激活仍7物品/5配方，NET03/04远端背包及主机事务未实现，112核心不等于新增112项玩法。当前优先转回P1单人可玩版本的实现差距/正常世界完整流程；NET02未通过项保留为联机开放前门槛，停止重复同一自动输入失败的限时实测。P1整体仍未验收、P2冻结，库存/损坏存档/保存保护不可降低。运行环境仍按下面最终停止审计，不启动新窗口。本轮没有提交/推送/PR/merge。

11-02最终现场：旧Welcome实现与本地检查完成；真实Host18276/Client17048只到首次Ready，自动键盘未能打开客户端菜单，未观察到原标题/重连，Host23:08:26五分钟截止。未发旧Welcome，实际原双报告CLI拒绝1，重放/Full不通过。两个窗口已鼠标X退出、CIM0、八save不变、六config原字节禁用、85项等于新deployment，原报告/日志/config/snapshot及retired独立保存。首次收尾预检因报告尚在Mods拒绝的原exit1保留；移出后补核实际预检0，最新依据after-exit/after-retirement-audit.json。下面运行/实现段均为历史检查点，不表示当前仍开窗口。

11-02运行检查点：四角色config已显式开启NetworkWelcomeReplay（本轮enabled/before保留原字节），runtime两份仍禁用。主机3232刚启动，独立host mods-path已由CIM/日志核对，尚未SaveLoaded；客户端未启动。Computer Use的sky.list_windows暂未返回游戏窗口，先等待启动，不向其他窗口输入。中断后先核PID/日志，不能按下方“CIM0/禁用”旧检查点直接替换DLL；无保存/实际重放结论。

11-02实现检查点：独立旧Welcome重放、受控内存API、真实旧挑战拒绝诊断、共享双端判据和严格CLI已实现。112核心/生产包/SDK夹具/两CLI构建/人工CLI一正四负/停止部署通过。当前八save原字节不变、六config禁用、CIM0，无本轮SDK报告；OldWelcomeReplayRejected及Full/P1尚未验收。下面“恢复开发”段是本轮开始时快照，当前实现结果以本段为准。

11-02当前：继续同一任务分支与HEAD，核上轮85项/八save/六禁用config均未漂移、CIM游戏进程0；恢复日志`p1-net02-replay-resume-readback-2026-11-02.txt`实际0。新一轮停止基线及累计源码原字节在`.local/net02-welcome-replay-2026-11-02/{baseline.json,before,before-source}`，准备日志实际0。正在实现独立NetworkWelcomeReplay模式：只允许主机重发本进程在前一连接已生成并发送的Welcome，拒绝任意JSON/目标；客户端记录实际读入的旧挑战及拒绝，旧被动模式不发包。生产联机写入仍关闭，旧Welcome/Full尚未通过，本轮无提交/推送/PR/merge。代码/构建/实测的后续结果须在检查点更新，不能复用旧DLL结果。

当前P1-NET-02进展：batch实际Host36608/Client14768在同一世界、各自同一进程完成首次Ready→客户端原标题及主机gap→同一Farmer重新加入。原双报告Completed/Failure=null，独立严格CLI实际exit0，确认新连接/新挑战/实际新Hello与Welcome、原host world/session保持、本地库存值/raw和每load引用保持；两端VisibleMachineCount均0。终止后原三检查点未改，最终双报告再次CLI0。现已CIM0、八save春24不变、六config恢复原字节禁用、85项匹配batch before、实际身份预检通过。Full/旧Welcome重放/带负载旧队列仍false，生产写入关闭、P1未总验收、P2冻结。此前角色反向及超时失败独立留存，没有用成功覆盖失败。

此前角色拒绝/重试准备检查点：当前推进P1-NET-02同ID断连重连。NetworkLifecycle实现/101核心/SDK及CLI编译、BUG021异常实际红绿仍为此前通过结果，代码/DLL未改变。首轮host配置25468实际Farmhand，client配置35192实际MainPlayer，两Scope实际拒绝、Initial/Gap/Reconnected均null，失败原双报告被CLI安全拒绝1；不是夹具缺陷或重连通过。已归档/移出唯一报告，CIM0/八save不变/六config原字节禁用/85项匹配停止部署及身份预检通过。另建retry85项新基线并显式启用四config，仅新主机22444已启动到标题，客户端未启动，等待操作者主持后核MainPlayer。Full/P1未验收，生产写入关闭、P2冻结。

10-11首次客户端准备保存已核春24及已创建XYSNETClient/连接ID一致，85项仅专用两XML变化，独立归档不覆盖春23。10-12另起host13864/client20612实际Ready/五probe/同窗口8读1未读限流，原活动双报告pair exit0/Basic=true；随后两端返回标题并关闭、CIM0，八save不变/六config原字节禁用/85项一致。本地标题失活/session=null/queue0/generation+1局部已核，host客户端断开后的超时Failure独立保留且最终pair预期拒绝。FullNET02/P1未验收，P2冻结，生产联机写入关闭；旧残余输入根因不能倒推。

## 分支与工作区

11-02增量：新增WelcomeReplayBuffer、NetworkWelcomeReplayScenario/Evidence、NetworkWelcomeReplayCheck工程和11项WelcomeReplayScenarios；修改生产ReadOnlyProtocolSession/Service/Diagnostics、MachineService仅诊断API，以及夹具入口/观察器复用开关、MachineCore链接与文档。未改目录/线上格式/设备schema/依赖或生产写入门禁。分支/HEAD仍同下方原基线；所有累计来源保留，无本轮远程动作。

本次继续同一feat/p1-game-adapter/原HEAD，无提交/推送/PR/merge。新增GameHarness的NetworkLifecycleScenario/Evidence、MachineCore的Lifecycle/Reader场景及tools/NetworkLifecycleCheck工程，修改ModEntry独立入口/Core链接；新增BUG021和实测文档。生产C#/线上协议/目录数据不变，先前累计树来源保留。源入口原字节及85项基线在`.local/net02-lifecycle-2026-10-12/before`。

分支`feat/p1-game-adapter`，HEAD仍`8f2468d83b43fb805073f73bbc8b989cf716852e`；保留既有累计未提交树。本次新增只读`tools/check_network_preparation.py`/`tests/test_network_preparation.py`及BUG020/准备验证文档，修正空name/未创建原生占位的校验边界。具名1红1绿、新模块11及完整116 Python通过；非游戏适配/数据改动，不改生产SDK/包。无新提交、推送、PR、merge或远程CI；不把HEAD基线充当修复SHA。

## 已完成与入口

11-02入口、命令、边界及日志见[旧Welcome记录](validation/P1-welcome-replay-2026-11-02.md)；新一轮before/deployment/cli-synthetic在`.local/net02-welcome-replay-2026-11-02`，两个one-shot snapshot/deploy均已执行，不覆盖重跑。新生产DLL d715、夹具72da、alpha2 ZIP2fd9（完整SHA见记录）。生产/夹具/两端DLL/PDB同步，85项恰16项预期构建部署变化，八save/六禁用config不变，实际profile预检0。旧ad9/f849及ZIP已保留before，不再是当前安装版本。

最新成功原双报告在`.local/net02-lifecycle-batch-2026-10-12/reconnected`：Host SHA `d4b302fb38cb0c08e7f16d90699e5e910e5ffbcd4a383f042076ff5ec5bceca4`，Client SHA `51e003c3a75adcfca36cdd75d3a92fda19de8aacf4548a4b0f416d6316d94044`。`p1-net02-lifecycle-batch-reconnected-2026-10-12.txt`及`p1-net02-lifecycle-batch-real-pair-2026-10-12.txt`实际exit0；严格CLI成功摘要仅重新握手/host会话保持true，Full/旧报文/负载队列false。最终报告与两日志/85 snapshot/原文件retired/实际preflight/final-pair在after-exit，客户端收尾标题使final SHA变为`182600c1a23e3694a07ad03dd79d9317b9dc0845b28f238a46d823841d12a0b4`，原成功输入不覆盖。主机最终SHA仍同原报告；详见[本轮实测](validation/P1-network-lifecycle-2026-10-12.md)。

本轮源码入口为`tests/GameHarness/NetworkLifecycleScenario.cs`/`NetworkLifecycleEvidence.cs`，独立CLI与严格reader在`tools/NetworkLifecycleCheck`，8＋4纯场景在`tests/MachineCore/NetworkLifecycle*Scenarios.cs`。夹具schema1报告与Basic schema2分离，唯一run名按ProcessId绑定；本次batch没有独立首次Ready/间隙外部归档，原报告自身保留三个检查点，最终归档及严格校验均已完成。流程与此前startup历史状态见[生命周期记录](validation/P1-network-lifecycle-2026-10-12.md)。

最新真实Basic原双报告与日志/config/snapshot-audit/basic-pair-audit在`.local/net02-basic-2026-10-12/active`；Host SHA `21e64fb6685c0416fa4f392a5db39434211a4eefd5396f24c9ba19f8211413e7`、Client SHA `e20825182179ac190f722f084b78ee09af5f27c12ae2917985d1228dfdf62a26`。`p1-net02-basic-active-archive-2026-10-12.txt`和`p1-net02-basic-real-pair-2026-10-12.txt`实际exit0；两端各自库存引用/值/raw指纹不变，VisibleMachineCount均0。Host原Step0/Basic=false是观察角色，Client Step6/Basic=true，Full均false。随后报告变化须另归档，不覆盖该pair的输入。详见[Basic本轮记录](validation/P1-network-basic-2026-10-12.md)。

InputObservation已经实现/编译/部署，入口`tests/GameHarness/ModEntry.cs`、实际观测`InputObservationScenario.cs`、纯判据`InputObservationPolicy.cs`/四项MachineCore场景。原唯一run文件`input-observation-5f5b2f1d443a428886a8638211053a15.json`的SHA `22c1f3e98e0cc34c7dc900b8572d7fcb316786e3738b41337a460cbe54c8f0a1`；最后ElapsedMs=29473/144样本/2事件/Failure=null。整进程ClosedGame截断，未ReturnedToTitle/未到三分钟，因此原Status=Observing、InputRecoveryVerified/FullNet02Accepted等始终false；独立evidence.json记录人工及测量结论，不修改原flags。

原报告/retired原文件/SMAPI原日志、八save/六config关闭前原字节与evidence.json/final-audit.json在`.local/input-observation-2026-10-11/after-input-run`，唯一已核报告已移出Mods，不删其他文件。`.local/closeout-input-observation-2026-10-11.py`已执行，不盲重跑；baseline/supplement/deployment/旧草稿及春1/春2仍保留。详见[输入实测](validation/P1-input-observation-2026-10-11.md)、[夹具操作](../tests/GameHarness/README.md)、[BUG019](bugfix/BUG-2026-019-null-removal-target.md)。

## 验证与限制

11-02交付检查：`p1-net02-welcome-replay-maintenance-2026-11-02.txt`exit0，维护结构/链接通过、0提交正文检查；`p1-net02-welcome-replay-delivery-2026-11-02.txt`exit0，26项本轮新增/修改源码/文档/CI（含未跟踪）UTF-8/空白/冲突标记、tracked diff0、当前85项/CIM0/原分支及HEAD均通过。清单在本轮delivery-text-audit.json。112核心/构建后未改行为，无需因收尾文档重跑；下面“维护仍待”是末次检查前快照。未执行远程CI或发布。

11-02最终补核：`p1-net02-welcome-replay-closeout-after-retirement-2026-11-02.txt`exit0，CIM0/85项/八save/六禁用config/原报告hash/拒绝空stdout及实际预检均通过；此前`...closeout...`exit1只因私有流程先预检后移报告，原失败final-audit/preflight仍保留。实际报告不完整、无重放，CLI安全REJECT1。两日志各一load、0标题、0Save/NRE/ERROR行且ClosedGame；不计标题清理/重连通过。首次隐藏启动无SaveLoaded的原日志在startup-hidden。公共源码自112核心及构建后未再改行为，仅增加CI的两CLI构建命令和补文档；维护/本轮文本差异仍待末次检查。

11-02最后通过：core112、production/SDK/两CLI构建各0警告0错误；人工CLI完整pair0，四负例REJECT1且stdout空/原人工输入未改；停止部署0。首次core编译因测试引用名称笔误失败，修正后通过，原日志保留，不当作产品bug修前红。公共Python未改，116是历史结果未重跑；本轮SDK重放/人工界面/源码文档最终维护检查仍待，不能用纯测试验收新DLL。旧协议行为未发现新缺陷，不新增虚构bugfix。

最新收尾`p1-net02-lifecycle-batch-closeout-2026-10-12.txt`exit0：原/最终两pair均0、八save/生产/ZIP不变、六config原字节禁用、85项匹配batch基线、CIM0、身份预检0。客户端收尾标题active=false/session=null/localId0/peers空/queue0/worldgen5→6；主机缺收尾标题证据，原成功三检查点不因后续退出改写。中途gap独立归档请求实际exit1，因为读到Completed；没有gap目录，最终原报告自身保留三个SDK检查点，未拼接。纯核心101/SDK夹具/CLI和Python116为此前源码阶段结果，本轮未改C#/公共Python，不重复构建或单元。维护`p1-net02-lifecycle-batch-maintenance-2026-10-12.txt`实际exit0/0提交正文；交付读回`p1-net02-lifecycle-batch-delivery-readback-2026-10-12.txt`实际exit0，核当前85项/CIM0/六禁用config、原及最终报告哈希归属、全部12份本轮文档（含未跟踪文件）的UTF-8/空白/冲突标记及tracked差异。BUG014正常有gap子集实测通过，缺席未被轮询看见的根因边界/另一个仍在线peer/旧报文与负载队列不借此验收。

历史batch单主机标题审计通过：PID36608/独立host Mods，LoadedSave和新报告当时均0；八save与其他81项不变，四config完整候选一致，普通主机日志/config/85hash在batch/host-title。此前维护`p1-net02-lifecycle-timeout-maintenance-2026-10-12.txt`exit0/0提交正文，tracked差异0。原Core101/SDK/CLI为此前源码通过结果，本次只改运行流程与记录，不重复编译或单元、不冒称远程CI。

Lifecycle本轮维护`p1-net02-lifecycle-maintenance-2026-10-12.txt`exit0/0提交正文检查；`p1-net02-lifecycle-handoff-text-check-final-2026-10-12.txt`exit0，tracked差异及21个本轮源码/文档UTF-8/空白检查通过，85非source路径只有四启用config不同于停止部署基线，八save与其他81项不变，实际PID25468/35192，报告0份。首版私有文本检查误把计划既有Markdown两空格换行当错误退出1，已保持原文并识别合法换行后另留final；非产品修复。`handoff-checkpoint-audit.json`留当前hash/PID/config状态，不能重复覆盖。两次文档patch上下文不匹配均未应用，后续成功版本另核；不将工具拒绝冒充产品红。源码/SDK产物未在运行期间改动。

最近源码实现阶段检查（实测前）：core最初97（89＋8）；reader负例因遗漏InvalidDataException捕获实际红，修后101/CLI0警告0错误及具名实际CLI red1/green0已留BUG021。SDK夹具Release（跳过生产项目重建）exit0/0警告0错误。部署审计`p1-net02-lifecycle-deployment-2026-10-12.txt`exit0：85项只有8个预期build/部署/profile变化，六config/八save/生产/ZIP不变，新DLL f849四部署+build一致、禁用profile预检通过。CLI首版过宽源通配符编译失败已缩为四必要纯协议源；重复对Program做delete/add的patch验证拒绝未应用，重新按update后成功，均非生产BUG红。完整Python116为上一轮结果，本次无公共Python改动不重跑。该检查点当时尚未执行实机；后续batch真实API/重连原报告的结果见本节首段，人工报告不代SDK。

此前Basic轮最终文档/差异：`p1-net02-basic-maintenance-final-2026-10-12.txt`实际exit0/0提交正文检查；`p1-net02-basic-final-whitespace-2026-10-12.txt`实际exit0，tracked diff --check0及13项本轮未跟踪源码/文档检查无空白诊断。最后一次内联PowerShell/Python快速查询因转义错误exit1、没有执行新hash/CIM审计，未改文件；换独立脚本后`p1-net02-basic-final-readback-2026-10-12.txt`实际exit0，当前85项仍匹配final-audit/CIM0/最新交接和实测文本差异干净/拒绝无摘要。原内联失败工具输出未单独留.local，不编造日志文件或产品BUG。新工具11/全116及实际保存正负例已经通过，文档后补不重复测试或构建。

此前Basic收尾`p1-net02-basic-closeout-2026-10-12.txt`实际exit0，双进程0/85项最终完全等于before/八save保持春24/六config原字节禁用，四本轮active/title报告归档后移出Mods、只读profile预检通过。真实原活动pair exit0，最终断连后host Failure报告另做CLI预期exit1/REJECT，未生成post-disconnect-pair.json，日志`p1-net02-basic-post-disconnect-pair-refusal-2026-10-12.txt`；该失败不覆盖已通过的active原输入，不标产品新BUG或修前红。双title本地清理true但host仍保留超时Failure，client null；负载旧队列/同ID重连/换世界/预算/错误来源等Full仍未测。两端全日志各一Steam启动ERROR，无NRE/原生Save调用；真实机器数量均0，不代表实际机器可写权。

最新准备轮实际CLI `p1-net02-preparation-actual-save-2026-10-11.txt`exit0，仅证明XML，保留其连接/输入/退出/Full flags=false。独立`p1-net02-preparation-closeout-2026-10-12.txt`exit0：85项仅专用两XML变化，旧三组六save、六指定config原字节/禁用值及其余83项均不变；实际profile身份预检通过。新主XML SHA `5f3bcc55f635be4ac3afdd03f8ba72077dd008706623738f2d4a4b0dc130fa70`、Info SHA `0109f8b0e26a556001072c439ba2d8bd06119764e442fdb2faff3b9da84beca0`。双端完整日志各一条启动Steam成就ERROR，无NullReferenceException；不声称零ERROR、诊断已通过或输入旧故障已确定原因。完整Python116/31.388s/exit0和11专项、BUG020实际红绿日志均留.local；本轮C#未改，不重复89核心/SDK/ZIP。准备收尾维护`p1-net02-preparation-maintenance-2026-10-12.txt`实际exit0/0提交正文；tracked差异exit0仅CRLF提示，README后补摘要后需最后差异复核。

关闭审计`p1-input-closeout-2026-10-11.txt`实际exit0：八save与本轮春23基线完全相同；六指定config恢复before原字节且Disabled，host回到Network/Host/450604963、Enabled=false/OpenPreview=false；其他五份未改，生产probe=false。93已知路径的非source项与停止部署后全部相同，生产ad9、四处夹具2bef、两个ZIP及profiles元数据不变，恢复后的实际只读身份预检exit0。CIM游戏/SMAPI为0。client-absence-check.json还独立核所有name元素均无XYSNETClient。

本日实现阶段的89核心、SDK夹具Release0警告/0错误、部署预检均通过；收到用户回报后没有改代码，所以不重跑89/编译/全105Python/SDK13或历史完整实机场景。文档收尾维护`p1-input-user-closeout-maintenance-final-2026-10-11.txt`实际exit0/0提交正文，tracked差异exit0/仅CRLF提示。首次收尾检查因新旧交接七个模板小节重名退出1，历史小节降为三级后final通过，原失败日志保留，非产品修复。日志有启动Steam成就ERROR，试运行未见NPC NRE/原生Save完成；不称所有日志零ERROR。菜单自动证据限于DialogueBox关闭；所有样本InBed=true，CanMove=false的46份与对话共现，不能倒推历史故障或“已走出床”。

## 环境与运行状态

11-02最终停止：18276/17048与早先3232均已结束，CIM0；没有测试窗口，旧异步请求的PID已失效，不再操作。四角色config恢复本轮enabled/before原字节，连同runtime两份共六配置禁用；八save仍春24；当前生产d715/夹具72da/ZIP2fd9，85路径须比after-retirement-audit.afterHashes。两个原报告已核hash归档并移出Mods，未知文件未删。该轮one-shot不可重跑；首次closeout失败和后续补充通过分别保留。

11-02当前停止现场：游戏/SMAPI进程0，六config原字节禁用、八save保持春24；生产/夹具/ZIP已经替换为本轮d715/72da/2fd9，当前85路径以新deployment/audit.json的afterHashes为准，不能再用10-12 audit要求旧DLL相等。完整旧原字节及累计源在新before/before-source；旧实测成功与失败报告保留原归档。

当前CIM游戏/SMAPI进程0，没有运行中的测试窗口。Host36608日志22:30:41 ClosedGame/Disposing，Client14768日志22:30:37 ClosedGame/Disposing；客户端另有22:28:26中途及22:30:29收尾原标题，主机本轮无returning-to-title日志且ClosingTitle=null，因此不计主机收尾标题清理。四角色config已恢复batch enabled/before原字节，六指定config均禁用/角色Network Host与Client/450604963，runtime两份未改；八save仍春24、生产ad9/夹具f849/ZIP不变。batch的before/baseline/enabled、host-title、reconnected和after-exit分别保留准备、唯一原双报告/日志/config/85hash及最终snapshot/retired报告/final-audit/preflight。closeout及此前one-shot已成功执行，不覆盖重跑；Mods里本轮两报告已核归档hash后移出，未知文件未删。

此前22:13准备：Lifecycle重试当时只有host22444，独立host Mods/可见Normal/title，尚无新报告；客户端未启动。四角色config已显式启用NetworkLifecycle/Host与Client/450604963/OpenPreview=false，runtime两份仍禁用，生产写入关闭。新85项before/baseline及四config原字节/候选/启用审计在`.local/net02-lifecycle-retry-2026-10-12/{baseline.json,before,enabled,host-pid.txt}`，不重新构建/部署。旧首轮25468/35192已返回标题/ClosedGame/CIM0，原报告/日志/85项退出snapshot/恢复hash/实际拒绝stdout/stderr/preflight在`.local/net02-lifecycle-2026-10-12/after-reversed-role`；首轮before/deployment/enabled/startup保留。生产ad9/夹具f849/ZIP与八save春24不变。closeout/prepare one-shot已执行，不重跑覆盖；普通日志已被新主机覆盖，旧归档才是证据。

10-12 Basic结束现场（历史）：host13864/client20612先后返回标题/ClosedGame并CIM0，没有运行中的游戏或测试会话。四角色config已经恢复本轮before原字节，六指定开关均禁用/Host与Client Network绑定450604963，runtime保持禁用，生产可写关闭。`.local/net02-basic-2026-10-12`的before/baseline/prepared/enabled-audit保存启用来源；active保存唯一已配对原输入/摘要；after-exit保存四最终/title报告/retired原文件、日志、85项恢复前snapshot、final-audit/preflight。三个one-shot enable/archive/closeout脚本均成功执行，不盲重跑/删除/覆盖。当前Mods无这四份报告，未删未知文件；普通ErrorLogs可能被下轮覆盖，归档才是本轮证据。

游戏1.6.15 build24356/SMAPI4.4.0/CP2.9.0；host30744/client18620均已ClosedGame退出，当前游戏/SMAPI进程0。准备前春23/85项原字节在`.local/net02-preparation-2026-10-11/{baseline.json,before}`；新春24基线及85项原字节、两端原日志/独立审计/只读工具原摘要在`after-save/{prepared-baseline.json,snapshot,host-SMAPI.txt,client-SMAPI.txt,final-audit.json,native-save-summary.json}`。`.local/closeout-net02-preparation-2026-10-12.py`已成功执行，不盲重跑或覆盖。六指定config仍禁用、独立Host/Client Network绑定450604963；生产ad9/四处夹具2bef/profile清单/ZIP均与准备前相同。普通ErrorLogs中的日志可能被下次启动覆盖，归档文件才是本轮证据；个人XML与角色ID仅留.local。

## 下一步与验收

11-02最新推进顺序（替代下方“继续旧Welcome”的旧优先级）：先逐项对照P1代码与正常玩法，形成单人可玩版本的已实现/未实现/仅待验证清单，优先补正常获取→两设备加工→烹饪/赠礼→保存重载的具体缺口；不扩充P2内容。整理累计本地改动为可审查的功能单元/提交清单，避免一直积累为一个大包；提交及远程动作按任务实际授权执行。每个阶段以玩家行为、必要验证和剩余风险交付，测试工具单列，不以测试数量代替功能完成。NET02旧Welcome/队列等独立待办仍未验收，NET03/04未实现，联机写入维持禁用；再次实测前先单独确认菜单输入通路可用，将准备/人工等待与正式用例计时分开设计并记录契约调整，禁止直接重用失败Run或单纯反复拉长超时。以上是P1内部排序调整，不改变整体P1验收/P2冻结要求。

11-02下次具体步骤：先核分支/原HEAD/累计增量、CIM0及after-retirement-audit的85 afterHashes/禁用预检；另起新一轮目录/备份，不重用本轮失败Run。先解决自动原生菜单输入或协调一次连续人工客户端原标题/重连动作，再按本轮记录从首次加载开始完成新Ready后的旧Welcome发送/实际读入拒绝及状态保持。不要在键盘仍无法打开菜单时耗尽五分钟或延长/重置判据。10-12正常重连不能代新DLL，112人工测试不能代SDK重放；P1/P2边界及写入关闭保留。无提交/推送/PR/merge，旧请求中的游戏均已关闭。

11-02下一步：先核本轮deployment85项/CIM0，再留四config原字节并显式启用NetworkWelcomeReplay；先启动/主持主机并核MainPlayer，后启动客户端加入已有XYSNETClient。客户端首次加入等10秒→原标题停5秒→同进程重新加入等10秒，主机保持原世界，全程不保存。现已读Computer Use技能及guidance/api/confirmations，node_repl已初始化@oai/sky、list_windows成功且当前无游戏，允许用该技能操作测试UI；尚未启动游戏。完成后归档真实原双报告/日志/config并用新CLI配对，关闭/八save/恢复六禁用/85项审计与预检。失败照原保存，不能重置或拼接。下面“当前下一步”是10-12旧阶段快照，已由本段替代。

当前下一步：先核feat/p1-game-adapter/原HEAD/累计树来源、CIM0及batch after-exit/final-audit.json的85 afterHashes，实际禁用profile预检；保留春24，不重建农场或再次创建角色。下一个独立NET02任务是同ID新连接下旧Welcome实际重放拒绝：先准备默认禁用的测试入口及能揭示错误准入的回归，绑定旧/新challenge和同进程来源，旧报文发送不混入被动NetworkLifecycle模式。判定必须有当前新握手成立、旧报文读取后的明确拒绝及库存/raw/连接状态不变，原报告不可手改。随后再核换世界/带负载旧队列、每Tick16/邮箱128、错来源/方向/版本/分屏、异常/I/O及无缺席快照换连接等矩阵；每轮另留before/禁用部署/显式启用/原报告/关闭恢复。NET03/04真实远端背包与提交不开放，WorldInteraction完整20步仍待。无本次提交/推送/PR/merge。

此前准备步骤：只让新主机22444主持XYSNET02，先核日志MainPlayer及报告WaitingInitialReady/Failure=null，再启动client独立Mods并记新PID，只让新窗口加入已有XYSNETClient。两实际InitialReady归档后才让client标题并保持到host Gap，再同一client进程重新加入同角色；新CLI只配对新轮归档原双报告。五分钟从首次SaveLoaded开始；超时/错角色世界/数据变化均保留失败，不能拼接。结束先client后host标题/关闭，核CIM0/八save不变，四config恢复retry enabled/before原字节为NetworkDisabled，唯一报告归档后移出、实际身份预检。首次失败已保留，不放宽角色或ready门禁。

以下四项是旧Basic后的步骤快照，当前具体下一步以本节首段为准。

1. 历史接手步骤核分支/HEAD/累计未提交来源、CIM0及85项与`.local/net02-basic-2026-10-12/after-exit/final-audit.json`的afterHashes，实际只读configure_network_fixture预检；不要回滚春24保存、重建世界或重复创建客户端。
2. 下一个P1-NET-02任务为独立同ID断连重连/新challenge与旧Ready拒绝：先读ReadOnlyProtocolService/ReadOnlyProtocolSession/NET02矩阵，准备只读生命周期观测及纯判据回归，不直接复用Network Basic episode（它在peer消失/上下文变化后中止）。新观测必须绑定同进程身份并保存旧/新连接与session，不能拼接报告；未实现部分不提前写完成。
3. 之后再核换世界/负载旧队列、16每Tick/128满邮箱、未握手/重试、错误来源/方向/版本/分屏及API内部异常/I/O等独立场景。每轮先留新before、停止配置/部署、再启用/实测/归档/禁用恢复，保护八save；Full不由当前Basic和空队列标题通过。
4. WorldInteraction完整20步/正常地图入口/赠礼及P1剩余项仍待，生产联机放置/加工不能开放。发布按实际授权审查累计树/分组完整commit/任务分支PR main merge，当前无本轮远程动作。

## 关联记录

[贡献规范](../CONTRIBUTING.md)、[计划](DEVELOPMENT_PLAN.md)、[本轮生命周期记录](validation/P1-network-lifecycle-2026-10-12.md)、[BUG021实际CLI红绿](bugfix/BUG-2026-021-lifecycle-cli-invalid-data.md)、[NET02操作](NET02_TEST_SETUP.md)、[10-12真实Basic/最终Failure拒绝/标题及85项关闭审计](validation/P1-network-basic-2026-10-12.md)、[10-11准备保存](validation/P1-network-preparation-2026-10-11.md)、[BUG020与11项工具回归](bugfix/BUG-2026-020-preparation-empty-placeholder.md)、[BUG019及原红绿](bugfix/BUG-2026-019-null-removal-target.md)。后续发布仍需完整commit模板、fix关联文档、任务分支PR merge；本次无远程发布动作。

## 10-11启动与标题交接（历史）

更新时间：2026-10-11，Asia/Shanghai。当前任务仍是P1键盘移动/Esc无响应的定位，不扩充P2。新现场已核游戏/SMAPI进程0（旧56016已不存在，退出动作未观察）；专用XYSNET02/450604963两XML已到春23/年1，仍无persisted XYSNETClient，不能沿用旧春2基线。旧三组六save与10-05备份一致。源、部署、配置、八save等92个文件已在`.local/input-observation-2026-10-11/before`留原字节和baseline.json；原春1/春2证据保留，不还原存档。

当前检查点：分支仍`feat/p1-game-adapter`、HEAD `8f2468d83b43fb805073f73bbc8b989cf716852e`；累计未提交树属于既有开发，不清理或混入无关提交。InputObservation已在`tests/GameHarness/ModEntry.cs`独立入口接入；Scenario逐次世界/所有权门禁，只读六层限定输入/焦点/菜单/玩家移动门禁与轨迹；Policy由MachineCore链接真实源码，四安全策略加旧85共89通过。SDK夹具Release退出0、0警告/0错误，四处DLL部署为`2bef01125ade5bc063a38bc8e3478b6f577751ae863b6211eae628ae02c4e0e5`；生产ad9/两个ZIP未改，无P2/网络写入。详见[本轮记录](validation/P1-input-observation-2026-10-11.md)与[操作说明](../tests/GameHarness/README.md)。无本次提交、推送、PR或合并。下方10-05内容为历史时点，旧PID/句柄/日期不作当前现场。

停止部署93已知路径的`deployment-audit.json`实际exit0，八save/六指定config原字节/生产DLL与包不变，四处新夹具匹配；`baseline-supplement.json`纠正初稿把三份CP也纳入配置计数的范围，补prod-build并保留原草稿。随后仅host夹具临时Enabled=true/Scenario=InputObservation，其他五份指定config不变/禁用、生产probe=false；`enabled-host.json`与`p1-input-enable-host-2026-10-11.txt`实际通过。profile元数据还是此前禁用状态，不能将其配置SHA等同当前启用字节。

运行现场：新host PID17644，独立host Mods、client未启动；日志到titleScreenGameMode，尚无SaveLoaded/观测报告。`running-title-checkpoint.json`/`p1-input-title-checkpoint-final-2026-10-11.txt`实际exit0，唯一主机/八save不变/其他五指定config原字节不变。host启动后原文格式化为91717076…，值与临时opt-in完全相同，原文留`running-title-host-config.json`；首次SHA不变断言失败不能当通过，修正实值核对后另留final，不把配置格式变化误记生产事故。启动有Steam未加载的成就ERROR及更新提示，未见BUG019的NPC NRE；不声称日志零ERROR或已看见可操控画面。本会话没有界面控制工具，已请求操作者合作主持XYSNET02、英文输入法/按住D两秒/Esc并即关闭，不睡觉/保存。观察三分钟自动停止采样，但不会退出或冻结游戏，不能留窗口长时间运行。

最后通过的检查：89核心、SDK夹具Release、停止部署与身份预检、only-host启用审计、运行标题检查点；文档收尾维护`p1-input-maintenance-final-2026-10-11.txt`exit0/0提交正文，tracked差异exit0/仅CRLF提示，差异final另包含12项未跟踪源码/文档baseline检查，无空白诊断。首版包装误把no-index正常内容差异exit1当失败，原日志保留，final纠正而不改源掩盖。当前已知限制：一条Steam成就ERROR、无真实输入报告/关闭审计/配置恢复；未重跑全Python/历史SDK13/各完整实机场景。六层输入与文本隐私/时长/容量边界已详细记录，单元/编译不代实机。

下一步：读取host Mods/GameHarness的`input-observation-*.json`，保留唯一run名/SMAPI原日志到新的after目录；关联用户实际结果、焦点与六层键、连续位置/菜单门禁定位。未知根因不强置CanMove、不宣称恢复。游戏退出后实核CIM0、八save/六指定config；将host config恢复`.local/input-observation-2026-10-11/before/profiles/host/Mods/GameHarness/config.json`原字节，其他五份无需改。保存若有原生变化先留新备份，不回滚掩盖；同进程Rejected/Completed/Stopped不可接续。正式NET02仍须包含XYSNETClient Farmer的准备保存/新基线；探针/pair/P1未验收。无本次远程动作，后续提交仍按完整正文/PR merge规范执行。

## 10-05交接（历史）

更新时间：2026-10-05，Asia/Shanghai。用户确认修后移动和Esc菜单都无响应，物理Escape已停止Computer Use；本轮不再调用界面工具，不通过终止进程绕过停止。host56016最近CIM仍运行，client未启动，窗口332594的显示/最小化状态须新现场重核。空目标异常BUG019已有5项真实SDK先红后绿、修后13项及核心85/Release通过，生产ad9已部署，完整输入实机未验收。最新日志与XML核主机原生保存春2；客户端仍未持久化、NET02/pair/P1未验收。

### 当前目标与状态

中断收尾检查：`python tools/check_workflow.py`实际通过，日志`.local/p1-bug019-workflow-final-2026-10-05.txt`，0提交正文检查（无本次提交）。`git diff --check`退出0，既有DESIGN的CRLF提示不是失败；累计未提交树未清理。输入观测源文件/入口最终复查仍未出现，未构建/部署。此文保存后不再进行界面操作，当前主机仍运行，接手重核现场。

生产修复只给TryGetKind空目标返回false及nullable成功契约，NPC prefix保留原方法；普通/Idle放行与两设备保护由实际SDK13项核，追加不借首次5项的旧红。Release0警告/0错误、核心85/catalog40/30通过。停止部署69已知项5变、三处新增生产PDB同build，profiles15包；夹具780未改。见[BUG019](bugfix/BUG-2026-019-null-removal-target.md)与[本轮验证](validation/P1-null-removal-2026-10-05.md)。

修后用户在恢复窗口/英文输入法/按住D和Esc后仍报告移动及菜单无响应。存档/startup_preferences的WASD/E/Escape绑定正常；生产不写玩家移动/冻结字段，Enabled=false夹具不进入任何Scenario。焦点/overlay、游戏原始与过滤按键、聊天Subscriber、freezeControls/CanMove等尚未取得运行中读数，不能确定残余原因，也不能强置CanMove掩盖。原计划被动InputObservation尚未落地：停止前未观察到新源文件/入口/构建，子任务已中断；不声称已经部署或采样。

此前fixed-host-loaded/handoff两72项运行检查点八save不变为当时事实。随后10:38:46–48主机日志SaveGame.Save完成/after save春2；真实两XML核春2/年1/world450604963，无XYSNETClient。最新`.local/bug019-2026-10-05/host-only-spring2/audit.json`与`p1-bug019-interruption-2026-10-05.txt`实际exit0：72项仅专用两save变化，旧六save/六config/其余70项不变，无ERROR，host56016仍运行。这是主机单端原生保存，不是包含首次客户端的准备保存，也不是退出审计；旧春1备份与新春2原字节/日志均保留。

| 现场项 | 最近已核事实 / 未完成 |
|---|---|
| 运行进程 | host56016，独立host Mods；client未启动。无当前窗口关闭/ReturnedToTitle/进程0证据；不得盲用旧句柄 |
| 界面授权状态 | 本轮物理Escape停止Computer Use，未再操作或关闭窗口；下一轮恢复界面操作后先重查window/CIM，当前用户反馈不视作已恢复输入 |
| 生产/夹具 | 生产ad9d96199…，GameHarness78026dd6…；SDK5项红绿/追加13、核心85、catalog、生产Release通过。InputObservation未实施/构建/部署，后续不得借Network模式代替 |
| 配置 | 六config原字节禁用；角色Network/Host与Client，绑定450604963。runtime禁用Memory/XYSP1UI；生产联机写入关闭 |
| 磁盘 | 最新专用两XML春2/年1，host-only-spring2留新基线；旧三组六save保持before。没有客户端Farmer，clientPreparationSavedVerified=false |
| 输入 | 用户回复移动/Esc皆无响应；自动键盘此前也无效，时钟与保存能推进不证明键盘恢复。实际原始/过滤键和门禁尚未读到 |
| 未完成 | 被动输入观测、原因定位/修复/实际移动；首次客户端持久化；模组Ready/探针/pair/FullNET02/P1 |
| 远程 | 分支feat/p1-game-adapter，HEAD仍8f2468d83b43fb805073f73bbc8b989cf716852e基线；累计未提交树未清理，无本次提交/推送/PR/merge/远程CI |

下一步先重新查进程与专用春2磁盘，不把旧八save相同检查点当当前值；恢复界面操作后核唯一正确测试窗口。准备只读InputObservation：仅显式启用、限定XYSNET02/450604963，采样焦点/限定WASD/Escape等按键/原生门禁/连续位置，不记录聊天文本、不输入模拟、不改玩家或保存。任何部署前先实际退出并核进程0，留当前夹具/config/包/春2存档原字节；不能运行中覆DLL。定位和移动核过后才继续客户端创建及含Farmer的专用保存/新基线，退出两端后另启probe；不重建世界或使用源/UI/Stage3。

### 10-05修前故障现场与退出（历史）

进入故障前24项before备份在`.local/net02-client-2026-10-05`，预检/启动前复核及running-before-user-checkpoint通过、四诊断禁用。旧host9712合作加载450604963，client56700自动IP两次无效后手动加入；日志互收SMAPI上下文/同世界2在线，归档connected-before-character。最早创建截图名字/favorite为空，第二次填写后实际getState核overlay消失/客户端小屋画面；不能写创建从未观察。用户随后称春2保存但不能移动，关闭前主机春1/02:00卧床、XML春1/年1无persisted客户端、八save同基线。创建画面不代键盘移动/成功持久化，磁盘无Farmer也不证明当时内存一定没有。

host真实NRE调用链为TryGetKind→BlocksRemoval→patched checkDestroyItem→NPC寻路；原现场`.local/bug019-2026-10-05/{before-host-SMAPI.txt,before-client-SMAPI.txt,save-checkpoint.json,error-summary.json,before-source}`。error-summary的47425是栈帧匹配数，不是独立异常次数。两故障窗口按client后host关闭，sky无游戏窗口/CIM0，没有ReturnedToTitle，不计保存成功。CLI修前红在源码修复前留存，停止后新部署另有69项/拓扑审计；现在运行的56016不是旧进程复用。

### 10-04两条真实失败路径与结束审计（历史）

当时PID52332的Host/probe=false/夹具=true负例成功保留ReadNetworkDiagnostics返回的Protocol，另一个新进程PID39000故意以Observe误配在Scope拒绝前保持Protocol=null。这两个Failure是预期负例，不是生产事故或新增BUG修复；均窗口X退出/CIM0，无ReturnedToTitle。该轮host配置恢复before原字节、其他配置不改，最终24项全部相同/14包身份预检通过。见 [历史失败路径](validation/P1-network-failure-2026-10-04.md)，不作为现在9712/56700已退出的证据。

该轮`.local/net02-failure-2026-10-04/final-audit.json`与`.local/p1-network-failure-final-audit-2026-10-04.txt`实际exit0：24项before/after全部相同、changedKeys为空，覆盖源码/build/四处夹具DLL、新旧profiles/ZIP/六config/八save；allSixConfigBytesRestoredDisabled/allEightSaveFilesUnchanged/profilePreflightPassed均true。两份active原报告、active-check与SMAPI日志各自归档after-read/before-read，最后host报告留存后Move到before-read/retired-network-probe-result.json；Mods无报告，不删除未知文件。before-hashes.json与原字节备份保留，各脚本拒绝覆盖已有证据目录或审计文件，不盲重跑。Saved=false只声明夹具未调用保存，存档不变由该轮八文件审计独立证明。

第一阶段PID52332以Network/Host加载专用合作世界，主模块探针false、host夹具临时Enabled=true。Scope通过、GetApi/ReadNetworkDiagnostics成功返回后门禁抛错，活动报告Protocol非null、Failure为`Main module probe gate is closed or diagnostic session is faulted.`。原报告SHA-256为`b757f8d86b6fa5157fca22c458f96f3e6f2f51a80a5a01564f55b398ed2db703`；对该实际报告调用`endpoint(actual, "Host")`确实抛ValueError `Failed or non-synthetic report`，未执行双端pair。读取的Protocol是本Tick检查前观测，不代表异常后最终状态。

第二阶段新进程PID39000保持同专用合作世界，故意将NetworkRole设Observe；这是角色误配负例，不是真实Client。Scope拒绝在GetApi读取之前，活动报告Protocol=null、Failure为`Network probe requires the configured independent XYSNET world and host/client role.`，原报告SHA-256为`731c967dfde0f4f9d5cd722b2067fa8d85b75dd8e5c63e01166ece079aaaff6e`。该阶段没有执行endpoint，不能将active-check的endpointRejectedForFailure=false写成校验器接受了报告。

实际命令`python .local/check-net02-failure-report-2026-10-04.py after-read --pid 52332`与`python .local/check-net02-failure-report-2026-10-04.py before-read --pid 39000`均退出0，对应`.local/p1-network-failure-after-read-2026-10-04.txt`和`.local/p1-network-failure-before-read-2026-10-04.txt`日志。两阶段均Schema2/Step0、BasicProbesCompleted=false/FullNet02Accepted=false/Saved=false，没有进入探针发送或库存hash阶段；BaselineVisibleMachineCount=0是默认未量测，不证明真实设备不变。Escape两次无菜单效果，均通过窗口X关闭并CIM核0，没有ReturnedToTitle；分别新进程不证明同进程跨Tick重入/重连或ReadNetworkDiagnostics内部异常。第二阶段归档并退出后恢复host夹具原字节，其他配置不改。

### 此前真实农场准备过程（历史）

进入准备前保存 `.local/net02-live-2026-10-02/before-hashes.json`、六份config原字节与旧SMAPI日志；一次性脚本不重跑覆盖。默认runtime的准备实例PID22468/窗口4722850加载当前模块与内容包/夹具，创建界面已观察初始小屋数量1；自动键盘输入未生效，操作者报告按XYSNETHost/XYSNET02/favorite tofu/跳过介绍进入农舍。随后computer-use因物理Escape停止，当时没有观察退出，不能倒推那时游戏为0。后续继续授权已恢复，根任务重新观察该农舍，再通过窗口关闭按钮退出；真实配置写入前实查进程0。

随后严格读取两份专用XML，取得450604963、XYSNET02、春1/年1和Cabin，两文件已备份到farm-preparation，identity.json核旧六保存与进入准备前相同。真实export审查和新目录apply成功，四配置仍禁用，before/prepared/plan/apply在fixture-plan与fixture-binding。主机Hidden尝试PID45684加载4mods但MainWindowHandle=0且无sky游戏窗口，未进入世界时已结束；host-preparation-hidden-start.txt保留。改Normal后得到主机PID55272/窗口4001946，准确点击合作菜单“主持XYSNET02农场”，截图确认[main player]农舍春1/6:20。客户端Normal启动PID56168/窗口2887862，合作→加入局域网→IP框聚焦；两次自动输入均无文字，手动创建请求未回复。两端CIM曾核独立host/client --mods-path；这些旧d2构建的窗口与进程现在均不作为现场。

启动后审计草稿先后在host主配置原sha、runtime主配置原sha、runtime夹具默认字段处停止，三次草稿不是生产bug回归红，不标通过。随后bound-preparation-audit.json实际exit0：四角色完整JSON同apply候选，runtime主配置仅格式变化，runtime夹具新增默认NetworkRole=Observe且旧五字段相同，相关开关false；六原文hash变化及该时点原字节另存bound-configs。原JSON实际15对象：六config变化、其余九（旧六保存/新旧profiles/ZIP）不变，另只读plan核14包/禁用门禁通过，两专用初始XML仍相同。首次成功stdout标签误写17/11，已按JSON核正15/9；本地脚本print改动态计数，未重跑或改原audit，不属新增生产缺陷/红绿用例。绑定工具不写runtime是apply时点事实，不能外推启动后原文不变。这个运行中的审计不覆盖本轮新部署；客户端准备保存仍未核。

旧轮交付时最后CIM仍为55272/56168，`.local/p1-network-live-save-checkpoint-2026-10-02.txt`退出0、两XML身份及SHA同初始备份、14包/禁用gates通过；没有计作客户端准备保存完成。`.local/p1-network-live-maintenance-2026-10-02.txt`记录check_workflow结构/链接通过、0提交正文检查，git diff --check退出0，既有DESIGN CRLF提示非失败。该时点未修改C#或重跑构建/单元；本轮进程消失与后续新构建不得反写到这些旧证据。

### 10-02观测扩展与部署（历史）

10-02已实现NetworkScenario.Tick局部observed，保留本Tick成功ReadNetworkDiagnostics返回的快照：读取前失败为null，不借上一Tick或旧连接，不在catch再次读API。快照可能早于本Tick随后发送/异常，不是失败后的最终状态；Failure保持非null、schema2/FullNet02Accepted=false不变，离线配对仍拒绝。报告写盘也可能失败，保留观测不保证文件已经落盘。这是P1-NET-02此前明示缺失的观测能力扩展，没有实际玩家事故或修复前红，不新增BUG ID。

10-02的GameHarness Release实际exit0、0警告/0错误，日志`.local/p1-network-observation-harness-build-2026-10-02.txt`。具名`test_failed_saved_or_claimed_full_acceptance_reports_are_rejected`当时实际1项OK，日志`.local/p1-network-observation-failure-gate-2026-10-02.txt`；该人工Python测试不覆盖C# Tick catch或SDK。彼时新夹具在游戏停止状态下完成部署但尚未启动、无实际失败报告/pair，85核心和105全Python未重跑。这些是10-02原时点事实；本轮10-04/10-05两条真实失败路径另记顶部，不反写旧validation。

10-02源码前基线及24项实际source/build/config/profile/八save哈希与相关原字节备份在`.local/net02-observation-2026-10-02/before-hashes.json`及同目录备份，完整before/after与7项预期变化在deployment-audit.json，`.local/p1-network-observation-deployment-2026-10-02.txt`exit0；其余17项不变，旧.multiplayer完整树/runtime四旧报告亦未变。正式prepare_multiplayer显式使用`.local/validation-game/Mods`、`.local/validation-game`、`.local/net02-profiles`、`.local/net02-probe-harness`，保留六配置原字节和450604963禁用绑定；新profiles的roles.configs描述该时点目标配置，不再沿用live初建旧哈希。runtime PDB与新build匹配、旧PDB单独备份，不入profiles/ZIP。一次性备份/刷新脚本及单次标记不盲重跑；后续需要新产物时先留独立证据，不能删除标记来覆盖旧审计。旧准备PID55272/56168在该次复查已消失、退出动作未观察、手动创建未回复，仅属历史。

### 进入实机准备前的工具阶段（历史）

身份绑定工具已实现：tools/configure_network_fixture.py默认只读计划，导出/显式apply需新证据目录；只更新四份禁用配置Farm/ID，原存档/包/profiles.json不改。该工具阶段28专项与105Python通过，BUG-018的Windows Config.json配置伪装包回归实际先红后绿。彼时14包/4禁用config预检与旧UI --apply预期拒绝通过，退出1且无输出，六configs/六保存/包及新旧profiles未变，审计p1-network-fixture-local-audit-2026-10-02.json；彼时还没有真实绑定或游戏启动。见 [历史工具验证](validation/P1-network-fixture-2026-10-02.md)。此历史不能覆盖后续真实apply/双进程准备或本轮停止状态部署，最后85核心与生产Release仍属前轮依据。

前轮双端报告配对已完成：新增 tools/check_network_reports.py、40项人工报告回归和4项实际quota观测。生产 trace带实际quota窗口/连接标记，Network报告schema2带首次Ready基线/当前指纹/burst发送序号，标题另存。BUG-017两阶段先红后绿：37项3failures→37绿；追加后40项2failures→彼时全77绿。它只核基础探针，不是真实SDK/身份认证，实际报告配对未执行。

前轮85核心/77Python与生产/夹具Release0警告0错误，10文件ZIP/build/runtime/新两端自有文件一致，见 [配对阶段验证](validation/P1-network-pair-2026-10-02.md)。随后工具阶段Python增至105，彼时六配置禁用原字节（角色XYSNET02/ID0）、旧profiles/六存档均未变，结束查询游戏0，尚未创建本次NET农场。ID0/游戏0仅是该阶段结束记录，后续真实绑定和运行状态统一见顶部。此前81/37及881d6bbf…仅属 [更早准备](validation/P1-network-probe-2026-10-02.md)。7/5、schema1、联机加工/放置拒绝不变；有效Idle原生移除不在这些门禁内。

BUG-015 实际旧工具先红再修，两端只读预检、config 原字节保留、显式 --output/可选整理后的夹具及 schema2 审计已完成；I/O 故障仍可能部分更新，不保证完整事务。BUG-016 实际弱证据判据提取后先红再修，共享纯回归/夹具调用绑定当前 run/session/generation/主机连接/线程/Type/读入与发送序号，不拼迟到 null-ID 拒绝。

角色文件准备阶段，`.local/net02-profiles/host`、client用当时构建实际准备后重跑核对config保留，Scenario=Network/Host与Client、开关false、Farm=XYSNET02/ID0；脚本本身没有自动建档/安装农场或启动游戏。旧.local/multiplayer和源/旧UI/Stage3六存档前后不变。此处只记录当时工具行为；后续真实创建/绑定属live历史，当前已停止的新部署与450604963配置见顶部。

Network 模式不自动加载、创建、保存、备料或操作设备，固定 synthetic 目标不是实际机器准入证明。五项探针与九发八回仅有候选标记；主机观察端不自动通过、FullNet02Accepted 恒 false。两端各自本地库存/可见 raw 指纹仍需实际运行验证，空设备集合不证明已有设备保留。SMAPI 元数据只是正常路由一致性，NET-03/04 可信身份/远端背包/提交未实现。

WorldInteraction历史修正后只通过豆坊前7/20步，在Step7自然拾取前中断，Completed=false、Failure=null、输入/轨迹0、一份掉落未入包。原进程已消失且退出动作未观察；中断后runtime夹具禁用、Scenario=Memory/FarmName=XYSP1UI/ID8624092802/OpenPreview=false，原字节保留。本次NET02使用独立角色Mods，不代表旧Step7现场恢复。Step7内存不能从磁盘接续，须新鲜进程整轮重跑。正常NPC/地图厨房入口、赠礼、完整拆回保存、英文/手柄/尺寸及主机权威双端仍待，P2/P3不开始。

### 分支与工作区

- 分支 `feat/p1-game-adapter`，HEAD `8f2468d83b43fb805073f73bbc8b989cf716852e`，标题 `docs: publish frozen v0.4 design and phased development plan`。此原基线不是本轮修复 SHA。
- 从 docs/development-workflow 延续累计未提交工作。已跟踪修改为 README、DESIGN、DEVELOPMENT_PLAN；src/content/data/tools/tests、维护规范/CI/模板与多数 docs 未跟踪。保留原有工作，不 reset/clean/丢弃或盲目 git add .。
- 当前入口为docs/validation/P1-null-removal-2026-10-05.md、BUG019及.local/bug019-2026-10-05真实事故/源码before/69项部署与新增PDB拓扑审计；本轮改生产MachineService/MachinePatches空目标边界、新增tests/GameAdapter SDK CLI及相关记录，夹具未改。docs/validation/P1-network-failure-2026-10-04.md及failure目录24项/两实际报告保持历史；10-02observation与live保留夹具实现/部署和初始身份/真实apply/旧d2准备。工具入口为configure_network_fixture.py、test_network_fixture.py、BUG018及NET02_TEST_SETUP/DEVELOPMENT；单文件替换不保证四文件事务，部分失败须读before/prepared/audit且保持关闭，不执行旧导出计划。前轮report checker、quota/连接trace与GameHarness schema2源码及累计生产/夹具/工具/规范修改保留。
- 前轮世界判据/墙区准备、NET-01 严格 codec/Gate/Ledger 及 NET-02 只读 mailbox/codec/session/桥累计源码仍未提交；设备 schema1、catalog 40/30、7/5 激活、双包 alpha.2 和依赖不变。
- 远程 `https://github.com/Sora6A/stardew-xiangye-shiling.git`，未来 PR 目标 `main`。本轮未 fetch、提交、推送、建 PR 或 merge，无新 SHA/完整正文/PR/合并结果；远程 CI/保护未核实。后续发布按当前任务授权判断，提交范围须显式审查。
- `.local`、artifacts、bin/obj、游戏程序集/个人存档/完整本机日志只作忽略证据，不提交。

### 已完成与入口

1. 生产单人事务：真实快照/克隆、写前后核对、补偿/隔离；菜单一次打开最多 Applied 一次，失败保留 request/instance/revision。数据仅 prototype，planned 不注入。
2. 原生放置保护与 Memory 工具链历史通过；BUG-011 共享命中判据先红后绿，BUG-012 原生墙格回归先红后绿。World20 仍只完成历史 7/20，不借用 Memory API 结果替代实际方向输入。
3. NET-01 纯意图、可信快照准入与 lease/history；NET-02 回调只排队，UpdateTicked 最多16，邮箱128、peer32、每peer每1000ms最多8次正文读取。世界/断连 generation 和实际连接引用 GUID 排除旧排队事件；合法意图仍 WritesDisabled，不接 Gate/Ledger/Commit。
4. 只读诊断代码已实现trace256/序号/Evicted、真实Receive结果/BodyReadAttempted、peer/session快照与固定探针16次出站尝试/1000ms。仅显式开启主模块诊断才记录trace；本地GetApi类型保持MachineService。10-04负例临时Enabled后已恢复false；本轮六config禁用，普通只读桥不等于所有网络关闭。
5. Network夹具代码在首次Ready绑定证据，核本地Item引用/值与可见raw指纹，连接/证据变化中止，不恢复不同轮次。Host只观察、Client五拒绝＋九发八回仅候选，真实host quota仍需独立核对。10-02补本Tick失败观测；10-04实际验证读后门禁保留Protocol/读前Scope为null，未运行Ready/指纹/发包或pair；本轮BUG019普通合作事故不扩充这些通过项。
6. 双端工具全预检/config保留/白名单/路径边界及可选夹具已实现。10-02角色包14文件与14包身份预检为历史；本轮停止刷新后production PDB加入roles清单，profiles15包，六config/八save保持部署before。工具不启动游戏或写存档，运行后的保存须另审计。操作见 [NET02_TEST_SETUP](NET02_TEST_SETUP.md)。
7. 身份工具读确切XML、排旧ID、核包/manifest/角色/禁用开关；默认不写，导出原字节，apply保持关闭。28人工专项含部分替换故障/Windows别名回归；本次真实450604963绑定已完成，apply时before/after与启动后格式写回须分开记录。
8. BUG019生产空目标防护已实现：nullable/NotNullWhen成功契约、null先false，NPC prefix放行/保留ref，SDK空原生分支false。5项真实先红后绿、追加最终13明确ad9产物通过；普通/Idle不执行未初始化非空原生正文，受保护态实际注册prefix拒绝/raw引用不变。false kind/state不可消费、schema/权限不变；运行中72项检查点通过，实际移动/准备保存待核。

### 验证与限制

以下以10-05 BUG019的CLI/构建/停止部署为最新结果，10-04两实际失败路径/恢复审计和10-02完整离线测试另标历史。旧bound格式/default变化、10-02部署7变化、10-04的24恢复相同与10-05的69已知5变化/新增三PDB属于不同时点。修后实际移动/客户端准备保存与NET基础pair仍未核，不能用包一致替代。

| 检查 | 最后实际结果与证据 |
|---|---|
| 核心（本轮） | 85实际通过、退出0，`.local/p1-bug019-core-2026-10-05.txt`；此纯检查不调用SDK，GameAdapter单独列；10-02原85日志保留 |
| Python | 最后完整105（身份工具阶段）：前轮77＋28人工存档/配置专项；`.local/p1-network-fixture-python-final-2026-10-02.txt`全部OK/exit0/24.659s，专项p1-network-fixture-unit-final亦28OK；修后补正常配置别名断言重跑，初轮日志保留，人工正例不算真实绑定或联网 |
| BUG019 SDK CLI | 5项旧0/5exit1→5/0exit0，red/green日志；最终bug019-null-object-adapter-final13-location-2026-10-05.txt实际13/0exit0、5.431s/Location核指定产物/打印ad9；10绿及未Location13绿保留，追加无各自旧红，不代完整NPC/移动 |
| 生产构建（本轮）/夹具（历史） | p1-bug019-production-final-build-2026-10-05.txt实际Release0警告/0错误exit0、生产ad9；夹具780未改未重建，10-02observation构建0/0保留 |
| BUG019部署/拓扑 | deployment-audit.json69已知项恰5变、六config/八save不变且禁用；deployment-topology-audit.json原52包文件匹配/当前55、三新增生产PDB同build/无删除，final拓扑日志exit0。合计72当前登记覆盖，不冒称整目录仅5变化；profiles15包 |
| BUG019修后运行检查点 | .local/bug019-2026-10-05/fixed-host-loaded-checkpoint.json/fixed-host-loaded-SMAPI.txt及p1-bug019-fixed-host-checkpoint-2026-10-05.txt实际通过，72项相同/六config禁用/八save相同/同世界无ERROR；host56016运行，移动/准备保存/pair/退出验证false，不是最终退出审计 |
| catalog（本轮） | p1-bug019-catalog-2026-10-05.txt退出0，40物品/30配方闭合及生成物通过 |
| 本轮维护/差异 | 最终维护/差异待根任务统一执行；此前结构/链接通过不代本轮；无新提交正文或远程CI |
| 10-04读后门禁失败（历史） | 实际PID52332 Host、Scope/GetApi/Read返回后门禁失败、Protocol非null；after-read/active-check.json及p1-network-failure-after-read-2026-10-04.txt断言exit0。实际endpoint报告因Failure抛ValueError，未执行双端pair |
| 10-04读前Scope失败（历史） | 实际PID39000故意Observe误配、同专用合作世界、Scope拒绝/Protocol=null；before-read/active-check.json及p1-network-failure-before-read-2026-10-04.txt断言exit0。不是真实Client，该阶段endpoint未执行 |
| 10-04恢复/最终审计（历史） | .local/net02-failure-2026-10-04/final-audit.json及p1-network-failure-final-audit-2026-10-04.txt实际exit0；24项全部same，六config禁用原字节恢复、八save不变、14包正式fixture预检通过，报告归档/Mods无报告；彼时game0、actualReportPairChecked/fullNet02Accepted=false |
| 10-04维护/差异（历史） | `python tools/check_workflow.py`实际退出0、结构/本地链接通过、0提交正文，p1-network-failure-maintenance-2026-10-04.txt；git diff --check退出0，DESIGN CRLF提示非失败；彼时CIM0/基线分支不变，无远程CI |
| 10-04独立复核（历史） | 原报告/active-check/审计两份SHA及分支一致；当时直接重算24项匹配before/after、六config恢复Host/Client且禁用/八save不变、14包preflight/Mods无报告。未编辑/重跑脚本或启游戏 |
| 10-02 Failure拒绝单项（历史） | `test_failed_saved_or_claimed_full_acceptance_reports_are_rejected`当时实际1项OK，p1-network-observation-failure-gate-2026-10-02.txt；人工Python报告不覆盖C# Tick catch/SDK，不作为105全量重跑，本轮两份实际报告另列 |
| 10-02只读复核（历史） | 当时独立复核Tick局部值/Failure/schema2/标题分文件，以及24项部署变化、两端各14包/各3配置实际哈希一致；复核本身未修改、构建或启动游戏 |
| 10-02维护/差异（历史） | `python tools/check_workflow.py`当时退出0，结构/本地链接通过、0提交正文检查；p1-network-observation-maintenance-2026-10-02.txt及maintenance-final均保留。`git diff --check`两次退出0，既有DESIGN CRLF提示非失败；彼时最终CIM无游戏/SMAPI，HEAD/分支仍为本页基线 |
| 10-02停止部署/保存（历史） | p1-network-observation-deployment-2026-10-02.txt及observation/deployment-audit.json实际exit0；24项恰7变化/17不变，八save/六config原字节保持该时点before、旧profiles完整树/四旧报告不变、14包与roleConfig核对、新runtime PDB匹配。彼时新夹具未启动/实际失败未验；本轮新证据不反写这些false字段 |
| 包/两端/保存（历史配对阶段） | 实际refresh审计退出0；`.local/p1-network-pair-package-audit-2026-10-02.json`：彼时包/build/runtime/两端一致，六configs原文禁用、旧profiles/六保存未变；actualReportPairChecked=false，不是网络验收 |
| BUG-015 红/绿 | 旧工具两项 exit1，1failure/1error；修复后11通过。`.local/bug015-profiles-red/green-2026-10-01.txt` |
| BUG-016 红/绿 | 旧共享判据旧世界null-ID断言红/当时81绿是前轮结果；当前85覆盖其回归，未运行旧游戏 |
| BUG-017 红/绿 | 第一轮37专项3failures(exit1)/37绿；第二轮40专项2failures(exit1)/最终77全绿。红日志bug017-network-report-red/second-red-2026-10-02.txt，初轮green保留 |
| BUG-018 红/绿 | Windows主模块Config.json改名并声明为包文件，默认plan误接受：具名1项1failure(exit1)→修后1绿；随后28专项与105全量绿。bug018-network-fixture-case-red/green-2026-10-02.txt，Windows实际未跳过；错误别名输入未实测apply/SDK，后续正常身份apply见本轮live验证 |
| 身份工具阶段只读审计（历史） | p1-network-fixture-local-audit-2026-10-02.json：14包/4禁用config通过，旧UI --apply预期exit1无输出；p1-network-fixture-final-audit-2026-10-02.json又核17受监视文件，彼时六config/六存档/旧及新profiles/ZIP/两build未变，exit0；不是此次真实绑定后或运行后的审计 |
| 实机bound检查点 | .local/net02-live-2026-10-02/bound-preparation-audit.json实际exit0；JSON15对象，6config raw变化和值/default明确录、其余9不变，另14包plan通过；首轮stdout17/11为本地计数标签错误，原JSON保留；审计时两进程运行、clientEntry/Basic/Full=false，不是退出审计 |
| catalog/维护/差异（工具阶段） | catalog40/30闭合与生成物通过，日志p1-network-fixture-catalog-2026-10-02.txt；check_workflow结构/本地链接及git diff --check通过，0提交正文检查，p1-network-fixture-maintenance-2026-10-02.txt；已有DESIGN CRLF提示非失败，该阶段相关源码/两新文档无行尾空白；旧pair日志保留 |
| NET-01/旧只读桥 | 10-01 的45/68项及旧hash是历史记录，见各 validation；BUG-014历史补录没有原始修前红，隔离旧比较重建失败不能冒称实机先红 |
| World UI | 历史7/20中断，输入/拾取/重放未完成；原报告/audit保留，旧现场已消失；NET02准备和新部署不恢复旧Step7 |
| Memory/Interaction/Persistence | 历史16/13/6及冷启动通过，见旧validation；本轮新DLL没有重新运行它们 |
| 真实双端与生命周期 | 10-04两个独立主机负例；10-05修前普通连接真实、修后host56016移动待核/客户端未启动。Farmer持久化/模组Ready/探针pair、同进程跨Tick重入/重连、API内部异常、ReturnedToTitle/旧队列/额度/分屏/错版与两端库存仍待；不拼旧版本或窗口X证据 |
| 远程 | 未执行提交/推送/PR/合并/CI，维护结构检查不是保护设置核验 |

10-04负例的Saved=false仅表示夹具未调用保存，八文件审计另证那轮磁盘未变；未进入库存hash阶段、count0未量测。10-05BUG019的保存不变由故障及停止部署审计另证，不借旧report的Saved=false。正常指纹仅覆盖本地背包/可见对象，不证外部副作用；九发八回不证明第九条没读，16预算/128队列仍需真实SDK场景。失败Protocol只是本Tick已读观测，可能早于发送/异常，Failure非null始终拒绝，写盘失败不保证落盘。10-04只证明两特定分支，不证明所有异常/同进程跨Tick/重连，见对应历史validation及BUG015/016。

### 环境与运行状态

- 工作区 D:/code/stardew；源安装 D:/SteamLibrary/steamapps/common/Stardew Valley 保持原状（旧 SMAPI3.18.4）。测试 `.local/validation-game`：游戏1.6.15、SMAPI4.4.0、CP2.9.0固定标签构建；SDK7.0.302、net6.0/x64、Python3.10.9。旧 `.local/smapi-reference` 为4.1.10，不作当前SDK行为依据。
- 当前生产DLL SHA `ad9d96199a86601bdb253eca628e1fe34464c5ee5e979e41cfac0e5176d17cd1`；ZIP `92402877ba5bd1bc3fd24403de41e7137fbeec37be8fd61a541d4201492f54c9`；夹具DLL `78026dd6a3f703fcb26bef41590289dab8505ae7621ab4813503666787582693`未变；profiles.json `99f4d0880bd8838b089b55fbd2ec9b8f30b7857ddee797569bd6b164a79a3815`。本轮生产源码/build/包已改，三处新生产PDB同build `64e7d90d4b0a320ab0e94ca1f5a7b499414c822d2ac7d4b9b1119278d669ab94`；不含游戏/SDK/个人保存。旧5ef/7907/9a4及d2/cf5均保留历史，当前进程只见顶部。
- 夹具整理源.local/net02-probe-harness仅manifest/dll/deps；角色启动路径.local/net02-profiles/{host,client}，profiles现15包文件，六config/八save部署时不变。两端共享普通Saves/偏好/日志，只有Mods隔离，不能各自加载同一档。过程/PID见顶部，不重复动态状态；新保存/运行变化须留独立审计。
- 原源XYSP0_449937634；旧UI XYSP1UI_8624092802副本.local/save-fixtures，上次磁盘基线为500g/未学图纸；p1-source-hashes.json、p1-ui-source-hashes.json和save-backups/p1-2026-09-28保留，不能当当前专用世界。
- Stage3 XYSP1Save_8624092801原基线夏1、15490g、4空闲设备、豆腐6/酱油4及checkpoint marker；不用其ID继续网络/新保存。两文件在旧工具/初始身份阶段及本轮八save最终审计中前后相等，不声称与09-28原始基线相同；未来诊断轮收尾另审计。
- 配对阶段实际执行.local/refresh-network-pair-2026-10-02.py通过，旧probe ZIP备份p1-network-pair-before-2026-10-02.zip为26556d39…；881d6bbf…/81项、c73726a2…/4513c411…/68项与NET01 f9866d…/45项均留作历史。一次性脚本不盲目重跑，正式准备按当前工具/配置核验。
- 世界中断：`.local/p1-world-ui-interrupted-2026-10-01.json`、对应 smapi txt/audit；旧 in-progress/checkpoint、墙区前/后 probe、BUG-011红、前轮组件、混装正负例日志均留 `.local`。最后 Step7、Tile(3,6)/Position(192,400)仅是消失现场，不能复用 PID13880 或要求继续旧窗口。
- 运行游戏以顶部修后新鲜host现场为准；旧两个故障进程已关闭不代当前0，本轮生产重建/核心85/SDK CLI已执行、全Python/旧完整实机未重跑。runtime仍禁用Memory/XYSP1UI，角色450604963仍禁用；10-04临时Enabled/Observe恢复属于历史。新增生产PDB进roles清单，runtime夹具PDB仍旧780匹配；不混淆两种符号或旧live格式变化。09-30 UI/Memory和09-28 Persistence不冒充本次重跑。
- 人工专项/全Python final在旧实机准备前完成并停止；check-network-fixture/verify-network-fixture-final-2026-10-02.py已运行，彼时17文件hash未变、角色ID0且禁用。真实plan/apply另存live目录，本轮失败active原报告各自留在failure/after-read与before-read；仍未取得成功的双端探针报告。旧证据不覆盖，新检查使用独立证据名。

### 下一步与验收

1. 用户已回复修后host56016仍无法移动，继续调查输入绑定/焦点/原生限制/输入屏蔽，必要时独立被动SDK观测，不先断言根因；时钟恢复/无ERROR/CLI13通过不代键盘恢复或完整实机验收。五红/五绿/10绿/两13绿独立保留，最终Location日志是当前SDK依据。构建/停止部署及运行中72项检查点已完成，保持六诊断禁用，不在运行中覆盖DLL；下次收尾先Get-Process与CIM复核，不盲重跑一次性审计。
2. 修后主机实机观察通过后，保持诊断禁用再用host/client的start.ps1继续450604963客户端Farmer创建，实际核可操控角色与同世界；用户春2反馈不代替落盘。只在专用世界完成准备保存，备份新增客户端后的两文件/hash/身份，退出两端并CIM0，显式录配置格式/default变化、旧六save/新旧profiles/ZIP/包。不得重建第二个世界或复用源/UI/Stage3，不清marker，不覆写旧证据。
3. 完成准备与退出后另行开启两端主模块探针/Harness.Enabled，OpenPreview保持false，留新四配置hash；重启诊断轮，归档两份active schema2，执行check_network_reports --host/--client/--output .local/独立摘要。主机同窗口八读一未读、两端指纹和文件hash独立核；Basic摘要仍Full=false，标题报告另存不代基础结果，失败保留本轮两端证据、不拼接。
4. 独立执行未握手/重试、错版/分屏、16帧预算/128队列、同ID重连、新challenge/连接GUID、标题/换世界旧消息；基础夹具Full=false，完整矩阵另验。结束关开关、查进程、归档并审计新旧存档。NET03/04完成前保持加工/放置拒绝，不把metadata当库存授权。
5. World20须新鲜进程从第一步重跑：真实方向输入>0、MovementTrail/Tile变化、原Debris/chunks自然结束和原payload入真实背包；不定位到掉落/collect/updateChunks/注入设备。再实际手持重放核carrier消耗/新instance，第二设备与两种损坏raw保护全做；20Passed/Step20/Completed=true后退出禁用与新审计，不能接续旧Step7或拼旧项。
6. World完成后另立新Persistence ID与携带态checkpoint，验证拆回/重放保存，保留Stage3；补正常NPC/步行商店/厨房/赠礼及英文/手柄/尺寸，冻结7/5不进P2。
7. 远程尚未执行。按发布授权审查累计改动、显式分组文件与完整正文校验，fix同提交更新bug；任务分支推送→PR main→merge commit核实际结果。交付完整SHA/正文/PR/merge，不用原基线当修复提交。

### 关联记录

[贡献规范](../CONTRIBUTING.md)、[计划](DEVELOPMENT_PLAN.md)、[架构](ARCHITECTURE.md)、[命令](DEVELOPMENT.md)、[环境](ENVIRONMENT.md)、[NET02准备](NET02_TEST_SETUP.md)、[本轮BUG019验证](validation/P1-null-removal-2026-10-05.md)、[BUG-019](bugfix/BUG-2026-019-null-removal-target.md)、[SDK CLI](../tests/GameAdapter/README.md)、[10-04真实失败路径](validation/P1-network-failure-2026-10-04.md)、[10-02观测扩展与部署](validation/P1-network-observation-2026-10-02.md)、[历史实机准备](validation/P1-network-live-2026-10-02.md)、[历史身份工具验证](validation/P1-network-fixture-2026-10-02.md)、[BUG-018](bugfix/BUG-2026-018-network-fixture-config-alias.md)、[配对阶段验证](validation/P1-network-pair-2026-10-02.md)、[BUG-017](bugfix/BUG-2026-017-network-report-validation.md)、[BUG-015](bugfix/BUG-2026-015-multiplayer-profile-preflight.md)、[BUG-016](bugfix/BUG-2026-016-network-probe-evidence.md)、[主机契约](P1_HOST_PROTOCOL.md)、[历史只读桥](validation/P1-readonly-transport-2026-10-01.md)、[历史NET-01](validation/P1-host-core-2026-10-01.md)、[BUG-014](bugfix/BUG-2026-014-readonly-reconnect.md)、[世界输入](validation/P1-world-ui-2026-10-01.md)、[前轮组件](validation/P1-world-2026-10-01.md)、[历史UI](validation/P1-ui-2026-09-30.md)、[历史保存](validation/P1-persistence-2026-09-28.md)、[bugfix索引](bugfix/README.md)。本轮无新commit/推送/PR/merge SHA。

## 配对工具接手注意

相关记录：[BUG-017](bugfix/BUG-2026-017-network-report-validation.md)、[历史配对验证](validation/P1-network-pair-2026-10-02.md)、[历史实机准备](validation/P1-network-live-2026-10-02.md)、[10-02观测扩展](validation/P1-network-observation-2026-10-02.md)、[本轮真实失败路径](validation/P1-network-failure-2026-10-04.md)。各端RunId/ConnectionId/线程/generation/单调毫秒不跨端相等比较，共享session/fixture/互为player/request才关联。报告归档原字节，摘要带两输入SHA；六配置相对本轮failure before已恢复原字节，不能外推回最早live准备时点。本轮after-read的实际endpoint确实因Failure拒绝；before-read未运行endpoint，不将两个独立主机负例拼成Client或pair。保留Protocol不抵消Failure，也不能称异常后最终状态。实际pair/窗口/重连仍待；旧摘要不代表当前失败输入，I/O失败不承诺回滚。
