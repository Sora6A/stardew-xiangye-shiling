# P1输入观测实现与测试记录（2026-10-11）

目的：定位用户重复报告的“角色不能移动、Esc菜单也无响应”。[BUG019](../bugfix/BUG-2026-019-null-removal-target.md)已经修复NPC空目标异常，但完整输入恢复仍未验收。本轮实现被动观测，不虚构根因，不新增已修复BUG或强制设置CanMove。

## 基线与保护范围

分支`feat/p1-game-adapter`，HEAD `8f2468d83b43fb805073f73bbc8b989cf716852e`；保留此前累计未提交源码/文档。本轮未提交、推送、创建PR或合并。测试SDK为游戏1.6.15 build24356 / SMAPI4.4.0 / CP2.9.0，net6.0/x64、SDK7.0.302；本轮不跟随启动中的更新提示改变依赖。

重新核CIM为游戏/SMAPI进程0；旧host56016已不存在，退出动作未观察。真实专用XML为XYSNET02/450604963、春23年1，无persisted XYSNETClient。此前留在运行中的原生日志记录后续多次保存，不能继续说磁盘春2或全部八save等于旧基线。旧源/UI/Stage3三组六个文件仍等于10-05备份；两份专用春23XML成为本轮基线，旧春1/春2证据保留，未回滚或修改保存。

原字节及路径/哈希在本地`.local/input-observation-2026-10-11/before`、`baseline.json`；最初92项含runtime/roles/curated/build/source/八save/两个ZIP。配置计数初稿纳入三份ContentPatcher而显示sixConfigsDisabled=false，不代表任一诊断已启用；supplement严格核GameHarness/XiangyeShiling六份指定配置且原字节一致/开关禁用，并补生产build（总93个已知路径）。首次补录脚本在已备份ZIP上拒绝覆盖、未变生产/保存；改为核相同原字节后跳过，独立`baseline-supplement.json`成功，原初稿保留。这些是本地证据脚本的范围/重复保护问题，不算生产回归红。

## 实现与不变量

[InputObservationScenario](../../tests/GameHarness/InputObservationScenario.cs)只由显式Enabled=true/Scenario=InputObservation启动，独立入口return防止进入Memory准备。首次SaveLoaded身份通过才采样；主玩家、非分屏/screen0、非零ID、实际XYSNET农场和保存目录精确匹配、OpenPreview=false，每次Tick/输入事件重新验证。换世界/再次SaveLoaded/ReturnedToTitle/异常停止，同进程不拼接episode。它不调用加载、保存、机器API、发包、输入模拟、Suppress或IgnoreKeys，也不设置时钟/玩家/菜单/背包。

三分钟/200ms/最多900份采样，另最多120条六键事件；W/A/S/D/E/Escape以外不采，聊天或任何文本Subscriber存在时键字段null、事件不记键名。读焦点/overlay/KeyboardFocus、菜单和Subscriber类型、冻结/暂停/跨日/事件、CanMove/工具/动画/坐卧、坐标/方向。输入六层定义见 [夹具说明](../../tests/GameHarness/README.md)：MonoGame、当前InputState、实际聊天过滤、旧控制帧、SMAPI Down与Suppressed，依次读取不等于原子快照，更不等于操作系统原始输入。

独有run文件每秒刷新，Completed只说明时长到期；Stopped/Rejected/Faulted保留原因，写盘失败停止并写日志，已有文件可能不完整。InputRecoveryVerified/FullNet02Accepted始终false。SavedByObserver=false不证明原生未保存；结束不冻结/退出游戏，需要操作者及时关闭并以本轮八save哈希审计。

## 本轮已执行检查

| 命令/具名检查 | 结果与边界 |
|---|---|
| `dotnet run --project tests/MachineCore/MachineCore.csproj -c Release` | 89通过、退出0，日志`.local/p1-input-core-2026-10-11.txt`；旧85＋四项实际共享安全判据，不是SDK实机 |
| `input observation refuses unrelated saves clients split screens and preview` | 主玩家正确世界允许；未ready/客户端/分屏/预览/null/零ID/错ID/错农场/旧目录/大小写均拒绝 |
| `input observation omits text and arbitrary keys` | 聊天/Subscriber任一存在不记录；允许六键，其余字符、文本、鼠标、F8拒绝 |
| `input observation sample schedule has monotonic duration and capacity bounds` | 首帧及200ms边界、回退/负时间、180000ms结束和900容量断言通过 |
| `input observation limits short key event capture independently` | 179999ms/第120条允许，超时/第121条/负时间拒绝 |
| `dotnet build tests/GameHarness/GameHarness.csproj -c Release -p:GamePath=<隔离测试游戏> --nologo` | 退出0、0警告/0错误；日志`.local/p1-input-harness-build-2026-10-11.txt`。原MSBuild输出使用本机编码，UTF8尾输出中文乱码不改原日志；成功以实际退出码和原输出计数核 |
| `python tools/configure_network_fixture.py --save <专用保存目录> --profiles .local/net02-profiles --farm-name XYSNET02` | 部署前后均只读预检通过，configurationApplied/networkStarted/fullNet02Accepted=false |
| `python tools/prepare_multiplayer.py --source-mods .local/validation-game/Mods --runtime .local/validation-game --output .local/net02-profiles --harness-dir .local/net02-probe-harness` | 停止状态正式刷新两端包，未改变任何配置，不能将配置禁用预检拿到稍后启用阶段冒充检查 |

没有修改生产源码、数据或Python工具，不重跑全105Python、生产ZIP、GameAdapter13、Memory16/UI13/保存/世界20/双端NET02；已有历史结果不被新夹具编译替代。首次部署检查点尚无真实输入采样；随后同进程的实际结果及限制见下节。

## 部署与启动检查点

停止部署的93已知路径审计为`.local/input-observation-2026-10-11/deployment-audit.json`，日志`p1-input-deployment-2026-10-11.txt`退出0：八save、六指定config原字节、生产DLL/两个ZIP不变。四处夹具DLL与本次build相同，SHA-256 `2bef01125ade5bc063a38bc8e3478b6f577751ae863b6211eae628ae02c4e0e5`；runtime PDB也为同次build，roles不额外放SDK/PDB。12变项含build/runtime/curated/roles夹具、profiles元数据及当时四份源/交接文件；这是93已知路径审计，不声称全工作区树绝无新增文件。

随后仅host GameHarness临时改Enabled=true/Scenario=InputObservation/OpenPreview=false，保留XYSNET02/450604963/NetworkRole=Host；其他五份指定config/八save再次核不变，主模块探针false。证据`enabled-host.json`、`p1-input-enable-host-2026-10-11.txt`退出0。正式profile元数据描述的是此前禁用状态，不能说当前config哈希仍与元数据一致。

新host进程PID17644，独立host Mods、client未启动；启动日志已到titleScreenGameMode，尚无SaveLoaded/观测报告。启动有一条Steam未加载的成就ERROR及更新提示，不能说全部日志零ERROR；未见旧NPC空目标异常。不能由进程启动推断界面可见、农场已加载或输入可用。已请求操作者合作主持XYSNET02、点击画面/英文输入法/按住D两秒/Esc，完成即关闭，不睡觉或保存。

运行中标题检查点`running-title-checkpoint.json`/`running-title-SMAPI.txt`已落盘，`p1-input-title-checkpoint-final-2026-10-11.txt`实际exit0：唯一17644、报告0、八save/其他五指定config原字节不变。host ReadConfig后原文重新格式化，SHA由启动前`3d04063b…`变`91717076…`，完整六字段值与opt-in完全相同；原字节另存`running-title-host-config.json`。初稿按启动前SHA不变断言退出1，未落最终审计；之后核实际JSON/原字节并独立final通过，不伪称原文不变。这是运行中检查点，不是关闭审计或完整输入结果。

维护结构/本地链接检查实际通过（0提交正文，未提交）。差异检查包含tracked及12项baseline/new-file：tracked exit0；`--no-index --check`隐含内容差异exit1但无空白诊断，CRLF提示不算失败，final包装检查exit0。首版包装把正常差异1错误判失败，`diff-check.txt`与失败日志保留，`diff-check-final.txt`/`p1-input-diff-final-2026-10-11.txt`核后通过；没有改源码以掩盖差异。文档收尾后的维护结果另补交接。

## 实际主机按键短测与关闭审计

操作者直接确认“移动和菜单都正常，已关闭”。日志证明同PID17644在23:24:45合作主持已绑定世界，春23年1/main player/1在线；23:25:15停止LAN/Disconnected: ClosedGame/Disposing，随后CIM游戏/SMAPI为0，未出现ReturnedToTitle。加载后的约30秒不见NPC空目标NRE，仅保留启动时Steam成就ERROR；全日志仍不能标零ERROR。

唯一原始文件`input-observation-5f5b2f1d443a428886a8638211053a15.json`，SHA-256 `22c1f3e98e0cc34c7dc900b8572d7fcb316786e3738b41337a460cbe54c8f0a1`，最后写盘ElapsedMs=29473，144份采样、2条事件（D/Escape），Failure=null、RedactedEvents/DroppedEvents=0。原文件Status仍Observing：整进程退出没有触发ReturnedToTitle或三分钟deadline，所以它是被ClosedGame截断的有效观测，**不能改成Completed或借此声称完整测试通过**。InputRecoveryVerified/FullNet02Accepted等原flags全部保持false，人工结论写在独立evidence.json，不改原JSON。

| 实际链路 | 观测及判定边界 |
|---|---|
| D键输入及移动 | 15597ms事件未抑制；3份D采样中MonoGame/InputState/ChatFiltered/oldKBState/SMAPI Down均有D，Suppressed均空。15646ms的方向为[1]、CanMove=true、无菜单，X=597.1201；初始X=576，随后X=618.24023，Y始终608，实际向右位移42.24023像素 |
| 原生对话限制 | 15863ms起MenuType=DialogueBox、CanMove=false（共46份），三层焦点仍true、FreezeControls=false，不能把对话期间正常禁止移动解释为输入丢弃。所有样本InBed=true；没有记录对话文字，因此不把对话具体问题写成已证事实 |
| Escape键响应 | 25187ms Escape事件未抑制，回调时还是DialogueBox；25211ms六层有Escape；25422ms采样菜单变null、CanMove恢复true。支持Esc关闭原生对话框，不支持“打开了常规GameMenu”的自动证据 |
| 焦点与过滤 | 全144份Active/ActiveWithoutOverlay/KeyboardFocus=true，无聊天/文本Subscriber/键屏蔽/冻结控制/新一天/事件/全局淡出；这些只描述本次运行，不能倒推出10-05旧会话状态 |

由用户反馈与独立输入/位移/对话关闭证据，本次合作主机短测可以继续后续准备。常规GameMenu打开/关闭、走出床或农舍后的持续自由移动、客户端输入仍未实测；历史残余输入原因未建立因果，不新增BUG020、不称只读观测器修复了玩家输入。BUG019的确定根因及五项先红后绿保持原记录，不能把本次新证据回填为历史失败场景的测量。

一次性关闭脚本`.local/closeout-input-observation-2026-10-11.py`执行前重核进程0、身份/报告/八save/五配置、原opt-in值、构建/包身份；先归档原报告、日志及当前八save/六config原字节，之后只恢复自己的host config，并把这一份已核自有报告移入独立after目录，不删未知报告。`p1-input-closeout-2026-10-11.txt`实际exit0，证据和最终审计在`.local/input-observation-2026-10-11/after-input-run/{evidence.json,final-audit.json}`：

- 八save与本轮春23基线原字节相同，没有本次原生保存；当前仍无XYSNETClient持久化角色，未回滚存档。
- 六份指定config全恢复before原字节/禁用，host恢复Network/Host/450604963、Enabled=false/OpenPreview=false；其他五份未改，生产probe=false。
- 93个已知路径相对停止部署的非source项全部相同；生产ad9、四处夹具2bef、两个ZIP及profiles元数据未改，不声称检查整个工作区树。
- 原报告已留副本和retired原文件，两者SHA一致，Mods无本轮观测报告，便于下一轮严格包准备；旧报告、原草稿和历史证据未覆盖。
- 恢复后实际`configure_network_fixture`只读预检exit0，disabled-profile-preflight.txt保留；游戏进程0。这次是关闭审计，取代启动检查点作为当前环境状态。

代码未变，本次答复只执行观测关闭/审计和文档检查，不重跑89核心/SDK编译；上述89及Release是本日实现阶段的已通过结果。没有新提交、推送、PR或合并。下一步仍是诊断禁用条件下首次客户端创建/包含Farmer的专用准备保存，另立新基线并退出两端后才开启双端只读探针；NET02/P1仍未验收。

本次关闭后的文档维护`p1-input-user-closeout-maintenance-final-2026-10-11.txt`实际exit0/0提交正文，tracked `git diff --check` exit0/仅CRLF提示。首稿新增最新交接模板与旧历史七个小节重名，被检查器拒绝退出1；保留旧文字并将历史小节降为三级后final通过，原失败日志留存，不当作生产缺陷或回归红。

## 接手、失败与回滚

当前动态PID/报告/配置以 [HANDOFF](../HANDOFF.md)重核。观察报告在host GameHarness目录，完整日志/个人保存不提交；后续新报告按run名归档，不覆盖旧日志。若窗口还在标题，可继续加载同一专用农场；若首次加载已Rejected/Completed/Stopped，不能在当前进程重试拼接，需要关闭后新进程。

游戏退出后核CIM0，把本轮报告/SMAPI原日志存入新的after目录，比较八save及六指定config；只将host config从`before/profiles/host/Mods/GameHarness/config.json`恢复原字节，其他配置无需改。如果本轮原生保存有变化，保留新原字节与身份/日期另记，不能覆盖或拿旧春2复原掩盖。关闭后的具体恢复命令与结果补入HANDOFF。

夹具回滚需停止全部进程，再用before的runtime/curated/role夹具和profiles元数据整套恢复；生产包未改不用回退。不要撤回已修BUG019守卫，不用强置CanMove/取消冻结字段“修复”尚未定位的输入原因。真正修复须先保留可揭示根因的失败/回归，再按bugfix规范记录原因、最小复现、解决方案、单元及实机结果。
