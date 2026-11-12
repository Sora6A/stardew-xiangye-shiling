# 当前开发交接

## 2026-11-12 NET03 冷重载试验准备

分支 `codex/first-stable-release`，本轮起点 `e2dd31c4b2f717dd7b9f5991b2d716e96317ddf9`。工作区新增纯库存写集与恢复决策、原版库存指纹和测试；合作存档复制工具、默认禁用的 GameHarness 冷重载场景为本轮独立改动。MachineCore 123 项、GameAdapter 16 项通过。原生指纹实际注册表调用须由游戏加载内容后验证；适配 CLI 使用真实对象 XML 和受控的同型对象工厂。生产联机仍只读。

本轮专用副本 `XYSNET03_620261112` 已安装，原 47 个存档文件安装前后相同，原普通进程 16668 保留。试验用两个独立配置启动实际局域网世界，目标是在农工库存与日志同步后故意不作最终确认，农工退出、主机实际保存，再用两个新进程从磁盘重载并重发同一请求。准备及运行证据在 `.local/p1-net03-native` 与 `.local/p1-net03-recovery`，本检查点尚无冷重载结果。GitHub `ls-remote` 重试在 HTTPS ref 响应前超时，远程分支与 PR 尚未更新。

## 2026-11-12 NET03 所有者提交检查点

分支 `codex/first-stable-release`，基线 `887e2c5abc9e4779c71b66ea5b31af2e38b73843`。额度短窗口恢复后自动接续。专用双端夹具增加由农工自己提交测试格的模式，主机重发同一指令后客户端只执行一次，主机通过原版同步读到对应变化及最终清空。Release 构建零警告、零错误，双方报告及退出审计通过，47 个存档文件一致。测试进程已退出且夹具禁用，原普通进程 16668 仍在。见[双端实测](validation/P1-native-inventory-2026-11-11.md#2026-11-12-由库存所有者提交)。

第二轮确认主机能在原版远端 Farmer 上同时读到农工写入的库存结果和 `modData` 提交记录，原报告在 `.local/p1-net03-native/owner-journal`。生产联机事务仍待实现。下一步复制独立双人存档，验证准备后断线、农工已提交但确认丢失及重载恢复，确定主机设备提交与客户端库存证据之间的持久化顺序。两轮只改夹具与记录，生产包沿用上次构建。GitHub 推送重试超时，检查点保存在本地。

## 2026-11-11 正式版持续开发

目标是在 GitHub 交付满足冻结设计的首个正式版。当前任务分支 `codex/first-stable-release`，远程 `main` 核对为 `e3b2d575d21094750592531c9ee7dc6d100acff8`。本地基线提交 `b643bc79cc3fbb38c834b51b8e0ae06855341d8d` 包含环境配置、单人菜单及同档路线实测。116 项 Python、116 项 MachineCore、catalog 和维护检查通过。旧普通游戏进程 16668 仍运行。GitHub 任务分支推送两次遇到 HTTPS 连接中断，尚无本轮远程 PR 或正式发布。

P1 本机双端原版接入已经验证。两端试验表明，主机可读客户端写入的库存，主机修改的远端副本未同步回客户端。两端测试进程已退出，隔离夹具禁用，47 个存档文件哈希一致。原报告与两份共享设置文件的哈希差异记录在 `.local/p1-net03-native/native`。下一步按原版背包所有权设计由物品所有者提交库存、主机提交机器的协议，并实测断线/重试守恒。见[双端实测](validation/P1-native-inventory-2026-11-11.md)。

设备菜单键盘及手柄焦点修复已用旧 DLL 复现、新 DLL 验证。最终后台实机 20 项通过，夹具恢复，47 个存档文件哈希相同，无原生保存。实体手柄和桌面指针吸附仍待验证。开发 ZIP 已刷新，证据在 `.local/p1-menu-input`，最终审计中原版只改写了 `startup_preferences`。所有本轮测试进程已退出，夹具均禁用。见 [BUG-2026-026](bugfix/BUG-2026-026-machine-menu-navigation.md)。

Codex 已建立当前聊天持续目标与每 30 分钟接续开发的定时任务。11-11 检查时 5 小时额度用到 91%，周额度用到 86%，在此提交检查点暂停新增开发。预计短窗口于 11-12 01:18、周窗口于 09:29 重置，续作以实际额度为准。P1 通过后激活冻结的 P2 内容，再执行 P3 平衡、兼容和正式打包。GitHub 目标为 `Sora6A/stardew-xiangye-shiling`，首个正式版需有实际构建、验收矩阵、PR 合并提交和 GitHub Release。

## 2026-11-11 无人值守单人链实测

分支 `codex/p1-automated-route`，基线 `e3b2d575d21094750592531c9ee7dc6d100acff8`。保留前序所有未提交修改，本轮新增默认禁用的 `AutomatedRoute` 夹具、复用的原版商店和制作助手及文档。夹具 Release 构建零警告、零错误。最终进程 28964 同一次加载通过九项检查，完成六笔原版交易、制作放置、24 次自然工时时钟变化、实际灶台烹饪及食用，余豆腐 1、酱油 4。此前夹具在厨房站位、菜单等待和自动入包判定上的失败证据另存。见[无人值守实测](validation/P1-automated-route-2026-11-11.md)。

后台测试进程已退出，各轮夹具禁用。最终审计一次加载、零保存、零错误。五个临时安装副本已清理，工作区保留源副本，原有 47 个存档文件哈希一致。原普通进程 16668 保留，生产 ZIP 与前序一致。最终报告和审计在 `.local/p1-auto-route/complete`。下一步可继续 P1 其余覆盖。原版步行与 NPC 自然营业、手柄和联机验收仍待，各项边界见[单人状态](P1_SINGLEPLAYER_STATUS.md)。

## 2026-11-09 普通游玩接续

分支 `codex/p1-ready-marker`，基线 `e3b2d575d21094750592531c9ee7dc6d100acff8`，保留此前未提交改动。操作者确认可接手专用 `XYSP1Walk`，已交付从原会话步行采购的步骤。16668 仍运行，原生日志仅有该档的一次加载，未见保存。接续前已核 47 个存档文件哈希与上一轮一致，夹具关闭。原会话使用旧生产 DLL `47F32FF54CA3B861C323FDE64319077EEBEB038443A597CFC65484F6C4CB9E20`。Computer Use 两次返回 `failed to activate captured window`，随后画面确认 Windows 锁屏，已请操作者本机解锁。下一步待操作者暂停后核对实际采购、库存、资金和配方，再接制作与加工。

## 2026-11-08 地图成品提示检查点

分支 `codex/p1-ready-marker`，基线 `e3b2d575d21094750592531c9ee7dc6d100acff8`，保留前序全部未提交修改。本轮新增两种设备地图成品提示及文档，未提交或推送。生产 Release 零警告、零错误，GameAdapter 13 项和 catalog 通过。独立预览确认仅待领取设备显示，发酵缸自然到时出现，豆腐 2 和酱油 4 分别通过生产菜单领取后标记消失。首轮预览 API 配置失败与布局调整证据另存，最终实测见 [地图提示记录](validation/P1-ready-marker-2026-11-08.md)。

最终预览 24892 已关闭，辅助开关全部禁用，47 个存档文件哈希不变，最终日志零保存、零错误。新开发包在 `artifacts`，上版包已在 `.local/p1-ready-marker` 归档。同目录下的 `ordinary-mods` 仅有生产模组、内容包和 Content Patcher，可通过隔离运行时的 `--mods-path` 启动。原普通会话 16668 仍载入旧构建，未重启。尚无普通三店采购新证据。下一步由操作者长按方向键完成步行，按[普通流程](validation/P1-ordinary-play-2026-11-07.md)优先罗宾再到镇上两店，回农舍串联制作、加工和厨房。要用新包验证，另开独立副本或先确认旧会话进度再切换，避免同时操作同一存档。

## 2026-11-08 自然工时实测检查点

分支 `codex/p1-natural-clock`，基线 `e3b2d575d21094750592531c9ee7dc6d100acff8`。保留前序所有未提交修改，本轮新增默认禁用的 NaturalClock 夹具、入口与文档，未提交或推送。GameHarness Release 构建零警告、零错误。当前生产包的两台设备在同一专用单人档 06:00 同时开工，经过 24 次原版十分钟变化，08:00 实际领取豆腐 2，10:00 领取酱油 4；到期前拒绝、重复领取拒绝均通过。夹具准备设备和库存、调用生产事务，等待不设置时刻。详见 [自然工时实测](validation/P1-natural-clock-2026-11-08.md)。

测试进程 1696 已关闭，隔离夹具已禁用，47 个存档文件哈希相同，原生日志加载一次、保存和错误均零。原报告与审计保留在 `.local/p1-natural-clock/native`，`python tools/check_workflow.py` 与 `git diff --check` 通过。普通会话 16668 仍用原构建独立运行，原 Mods 哈希不变。本轮没有生产包变化。下一步用独立普通档和当前包，把三家商店的自然日程、地图步行、制作放置和厨房使用串成同一次正常游玩。自然工时子集已通过，无需用预设时间重跑。P1 总体验收及联机仍待。

## 2026-11-08 设备菜单缩放检查点

分支 `fix/p1-machine-menu-fit`，基线 `e3b2d575d21094750592531c9ee7dc6d100acff8`。保留之前的环境、保存验证、普通游玩准备和预计领取时刻改动。本轮新增生产菜单布局、夹具回归和相关文档，尚未提交。旧包在 540×300 逻辑视口实际越界。修复后生产和夹具 Release 为零警告零错误，116 项核心和 19 项游戏内 Memory 检查通过。中文指南实际最大化和还原后保持页码，翻页和关闭可用。见 [BUG-2026-025](bugfix/BUG-2026-025-machine-menu-small-viewport.md)。

两轮 `XYSP1Menu` 预览已退出并禁用夹具，47 个存档文件哈希一致，原报告和审计在 `.local/p1-menu-fit`。开发 ZIP 已重建，旧包已归档。普通游玩进程 16668 与预览隔离，仍运行旧包。下一步用新包启动独立会话，接着核对正常营业时间采购、自然工时和厨房串联。

## 2026-11-07 普通单人链检查点

分支 `test/p1-ordinary-play`，基线 `e3b2d575d21094750592531c9ee7dc6d100acff8`。保留前两轮未提交的开发环境和保存验证记录。已用已有的旧版 P0 专用副本生成新的 `XYSP1Walk` 独立存档，原版起点是第 4 年春 4，农舍已有厨房、资金和部分材料，三份模组配方尚未学习。夹具保持禁用。43 个既有存档和新副本初始文件哈希记录在 `.local/p1-ordinary-play/baseline.json`。

从标题菜单已加载 `XYSP1Walk`，日志和画面均到达第 4 年春 4。操作者完成实体键盘测试，随后画面确认角色已离床。交互进程 16668 在 12:50 打开原版日志暂停。后续从三店采购开始，再制作、自然等待和使用成品。准备路线、箱子和扣款预期见 [普通单人流程记录](validation/P1-ordinary-play-2026-11-07.md)。

已实现设备预计领取时刻与开工后关闭菜单提示，改动在 `Menus/BatchCompletion.cs`、`MachineMenu.cs`、双语文本和 MachineCore。116 项 MachineCore、生产和夹具 Release 构建、18 项游戏内 Memory 检查均通过。中文预览在 06:00 显示发酵缸预计 10:00 可领取。预览进程已关闭，预览夹具已禁用，47 个存档文件哈希匹配。新版开发 ZIP 在 `artifacts`，旧 ZIP 与原始预览报告已归档于 `.local/p1-completion-time`。普通游玩进程仍使用原先加载的 DLL，验证新菜单时需用新开发包另开会话。

启动审计确认 43 个既有文件和 2 个专用副本文件哈希均与准备时一致，一次专用档加载和零次写盘，生产 DLL 哈希匹配，夹具开关为禁用。SMAPI 读取配置时补全了默认字段，启动前后配置字节有差异。启动日志与 `load-audit.json` 已归档，维护检查、catalog 和 `git diff --check` 通过。本轮新增未提交内容为普通流程记录、玩家指南和交接更新。

## 2026-11-07 当前版本保存实测检查点

分支 `test/p1-current-persistence`，基线 `e3b2d575d21094750592531c9ee7dc6d100acff8`。保留环境配置阶段的未提交开发入口和文档。本轮文档改动为 README、开发计划、单人状态、交接与新增实测记录。尚未提交或推送。

首轮 Persistence 六项通过，原生日志确认四次加载与三次保存。另起进程从最终磁盘存档通过 Stage 3 防重复领取检查。只读核对第 1 年夏 1、豆腐 6、酱油 4、四个不同实例及空批次。两轮终态后已停止，游戏进程为零，39 个既有存档及冷启动前后的 4 个专用档文件哈希相同，夹具配置按原字节恢复为禁用。证据为 `.local/p1-current-persistence` 下的两轮原报告、日志和 `audit.json`。详见 [本轮实测](validation/P1-current-persistence-2026-11-07.md)。

收尾检查通过 `python tools/check_workflow.py`、七项维护回归、`git diff --check` 和游戏环境版本预检。保留专用副本及本地归档，复跑完整链时应新建独立 ID。

下一步是 P1 的普通单人获取链。使用独立农场和禁用夹具配置，从农场步行到三家商店，在自然营业时间购买黄豆、小麦、香椿及图纸/菜谱，原版制作并放置两设备，按游戏时钟等待，实际领取豆腐和酱油，再用灶台完成料理并使用成品。此前的实机子集可用于比对失败位置。多人争用依 P1-NET-03/04 单列，P1 整体验收仍待。

## 2026-11-07 新机器接手检查点

本轮目标为配置开发环境与接手，不扩展玩法。新检出基线为远程 main 的 `e3b2d575d21094750592531c9ee7dc6d100acff8`；已通过 GitHub 核对 PR #1 为 merged，旧文档中的“等待合并”属于历史检查点。当前任务分支为 `chore/dev-environment`，没有前人未提交修改；本轮暂不提交、推送或创建 PR。

本机工作区 `G:\codex_project\startdew`，合法 Steam 游戏目录为 `G:\software\Steam\steamapps\common\Stardew Valley`，只读版本为 1.6.15/build24356；源安装没有 SMAPI/Content Patcher，尚未修改。旧机器的 `.local`、存档、实测原报告及运行配置不在此次 clone 内，历史哈希不能作为新机器实测证据。

在项目根目录执行 `. ./tools/enter-dev.ps1` 进入环境。`local.props` 指向本机游戏和 `.local/smapi-reference`。已验证 SDK 7.0.302 / .NET 6 运行时 6.0.36 / 6.0.16 引用包和 Python 3.12.14。首次 C# 还原因缺 6.0.16 引用包失败，补齐 Microsoft 官方包后通过；无需改变 `NuGet.Config`。首轮 Python 使用系统 TEMP 时因 8.3 短路径别名出现 1 failure/1 error，入口改用 `.local/tmp` 后 116 项全部通过。证据见 `.local/setup/python-tests*.log`、`machine-core*.log` 和 `environment.json`。

本机通过 catalog（40/30）、维护检查、MachineCore 115、GameAdapter 13；两套报告 CLI、生产模块及 GameHarness Release 构建均为 0 警告/0 错误。开发 ZIP 为 `artifacts/XiangyeShiling-0.1.0-alpha.2-dev.zip`（10 个文件，仅含本项目 DLL；已核对 ZIP 内、构建和测试副本的 DLL 字节一致）。隔离运行目录为 `.local/validation-game`，Mods 为 `.local/test-mods`；版本预检游戏 1.6.15 / SMAPI 4.4.0 / 作者发行的 Content Patcher 2.9.0，`Issues=[]`。GameHarness 为 `Enabled=false`、`OpenPreview=false`、`Scenario=Memory`。

可以用 `./.local/start-test.ps1` 启动隔离的游戏和 Mods。启动仍使用正常用户的存档与日志目录，本轮未启动游戏、未操作存档，收尾未发现游戏/SMAPI 进程。未提交内容仅为开发入口和开发/环境/交接文档，生产源码和内容包未改。

接手时确定的下一步是当前 DLL 的保存、重载和冷启动闭环，结果已补入上方检查点。P1 未总验收，P2 和联机写入继续冻结。

以下为原机器的上传前后历史记录；当前分支和环境以上方接手检查点为准。历史更新时间：2026-11-07，Asia/Shanghai。11-03 之前原文见[历史](HANDOFF_HISTORY_2026-11-03.md)，中间各轮详见对应 validation 和 bugfix。旧 PID、配置和内存不能接续。

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
