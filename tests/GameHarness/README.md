# 本地游戏集成夹具

2026-11-03单人Memory本轮完整18项通过：在原16项上增加指南导航/成功锁定，以及缺料差额/不创建请求/补料重试。两新场景通过真实设备checkForAction取得生产菜单与正确翻译器，夹具不能传自己的Helper.Translation，见[BUG022](../../docs/bugfix/BUG-2026-022-harness-menu-translation-context.md)。中文预览只用于查看布局，脱离地图对象不能提交；实际交易证据来自临时已放置设备组件。模式/恢复契约未变，原失败9项、修后完整18项及关闭审计分开保留，详见[本轮记录](../../docs/validation/P1-singleplayer-guide-2026-11-02.md)。

此目录单独构建，**不进入开发 ZIP，也不在普通 CI 运行**。需要完整合法的游戏和 SMAPI 安装；仅在有备份的专用测试档启用。默认 Enabled=false，各模式分别核对农场、世界ID及单人/主机/客户端身份，不能混用。

十一种模式有不同的恢复/保存契约：Memory恢复不保存，Persistence保存显式独立ID，Interaction/WorldInteraction/GiftInteraction/KitchenGiftInteraction/WorldShopInteraction不保存不恢复，Network只读观察/固定探针；InputObservation被动读取主玩家输入及门禁；NetworkLifecycle被动记录同进程重连；NetworkWelcomeReplay在重连后仅主机发送一次内存中的真实旧Welcome。三种网络模式均不自动加载/创建/保存/备料，另需主模块开关、XYSNET专用非零世界ID和Host/Client角色；默认所有开关禁用。旧模式通过不能代替本轮网络，完整入口与停止步骤见下文。

## WorldShopInteraction：三家真实地图柜台购买

入口[WorldShopInteractionScenario](WorldShopInteractionScenario.cs)。仅独立XYSP1UI/实际非零SaveFixtureId/单人/Enabled=true/Scenario=WorldShopInteraction/OpenPreview=false。自动加载后只准备空背包、20000g、人物/时刻及三店定位，不创建ShopMenu、不调用地图Action/购买/学习API、不改完整商店库存。既有原型配方必须尚未学习；NPC自然日程/开店时间、从农场步行进店不在此范围。模式只改当前内存，不保存、不恢复，完成必须关闭整个进程。

按顺序实际操作：①皮埃尔柜台购买黄豆6/小麦2入包，关店中键；②罗宾柜台选择“商店”，买豆坊/发酵缸两图纸，关店中键；③格斯柜台买香椿1/香椿拌豆腐菜谱，入包/关店中键。商品保留原版完整列表，可拖动原版滚动条查找。没有包括准备/聊天等待的倒计时；失败停下保留报告，不拼接新进程。

只读Harmony postfix核实际成功的当前地图/本地角色/柜台格/token，MenuChanged另核ShopId并留原生目标商品的Price/Stock/IsRecipe；中键要求真实右键、实际商店/库存、背包完整数量/无额外物品、配方学习及精确余款19590→16590→15490。不能以鼠标候选位置或商品能显示代替真实购买。完成、失败、回标题解除观察器；Saving观察使报告失败。

world-shop-interaction-result.json独立输出SchemaVersion=1/ProcessId/FixtureId/三条Evidence；通过要求Completed=true/Step=3/Failure=null/SavingObserved=false。SavedByHarness=false不代替磁盘审计。11-06同次完整三店通过，结束八XML不变/六配置原字节禁用/旧两报告保持/进程0及身份预检通过，见[原生三店记录](../../docs/validation/P1-world-shops-2026-11-06.md)。不进开发ZIP/无SDK CI。复跑先停止、备份配置/XML/旧报告，使用新证据目录；结束退出、恢复禁用并核原字节，不覆盖旧证据。

## KitchenGiftInteraction：地图厨房到实际成品赠礼

同一GiftInteractionScenario的可选分支，配置Scenario=KitchenGiftInteraction，其他专用XYSP1UI/非零ID/单人/OpenPreview=false门禁相同。内存调用原生农舍升级以准备厨房、授予既有菜谱、提供香椿/豆腐各两份，初始选空槽，冰箱要求为空；不打开菜单、不调用地图Action/烹饪/赠礼API。真实鼠标右键灶台→制作两份入包→关闭菜单→中键核验；之后只准备NPC/站位/零好感，用同一背包成品右键Robin→关闭对话→中键完成。完成后退出整个进程，不保存或继续该内存游玩。没有准备/聊天等待倒计时。

BUG023要求只读Harmony postfix捕获原生performAction实际成功格/token，不能把SMAPI GrabTile当最终Action格。共享KitchenEntryProof另核成功/本地角色/当前位置/准备灶台/厨房token；中键还独立要求真实右键、原生cooking菜单、原料各2→0/成品2/原生烹饪增量2，随后实际成品赠礼。完成、失败和标题时解除观察器；原版返回值与游戏物品不由postfix修改。无SDK三项回归与完整同次原生流程通过，见[BUG023](../../docs/bugfix/BUG-2026-023-harness-kitchen-action-source.md)及[实测](../../docs/validation/P1-kitchen-gift-2026-11-05.md)。

报告单独写kitchen-gift-interaction-result.json，不覆盖gift-interaction-result.json；Stage=-1为厨房，Stage=0为赠礼，Completed要求Step=1/KitchenCompleted=true/Failure=null/SavingObserved=false。不重复同日第二次拒绝；旧GiftInteraction仍是两个赠礼阶段的独立模式。每轮先归档旧报告、备份配置和XML，结束恢复禁用/核进程与磁盘，禁止拼接失败轮次。这是厨房入口/使用子集，升级购买、原料自然获取、学习菜谱、NPC步行寻找和整链保存另验。

## GiftInteraction：成品真实赠礼

11-05新场景[GiftInteractionScenario](GiftInteractionScenario.cs)已完成原生鼠标收礼及同日重复拒绝两阶段，详见[实测](../../docs/validation/P1-gift-2026-11-05.md)。不调用NPC赠礼/互动API，不自行扣料理或在观测中写好感；中键/F8只核验，真实右键和原生对话均需观察到。不把输入被忽略时的无变化当作拒绝成功。

仅支持独立XYSP1UI副本与实际非零SaveFixtureId，`Enabled=true/Scenario=GiftInteraction/OpenPreview=false`，联机拒绝。启动后自动加载；准备两份普通品质料理、Robin零好感/位置/暂停日程、原生地板站位及固定正午。这里有内存准备，不能宣称自然寻人、获料、烹饪或整个世界链通过。不保存、不恢复，完成后关闭整个进程，不能继续该内存现场游玩保存。

步骤：右键Robin→关闭感谢对话→中键核验→再右键Robin→关闭当天已赠送对话→中键核验。没有包括准备/聊天等待的操作倒计时；失败停下保留报告，不能拼接新进程。菜单未关/非指定世界/库存或好感断言失败均拒绝，原生Saving事件也使本轮失败。

`gift-interaction-result.json`记录实际进程/世界、原生taste、两阶段鼠标/对话、料理数量、好感/当天/本周/总赠礼增量，以及Completed/Failure/SavingObserved。成功要求两阶段完整且Failure=null/SavingObserved=false；SavedByHarness=false仅表示夹具未调用保存，结束仍须独立核存档哈希。报告不包含完整个人存档。每轮先归档旧报告、备份配置和XML；结束恢复原配置禁用、核进程0、保存哈希及实际部署文件。不进开发ZIP或无SDK CI。

## NetworkWelcomeReplay：新Ready后的旧Welcome

11-02新增独立主动模式；112核心、SDK及CLI构建/人工正负例和停止部署通过。实际仅首次Ready，客户端原标题前主机截止，未发旧Welcome，原双报告CLI拒绝；已关闭/恢复禁用/八save不变/85项及移报告后预检通过，重放仍待。入口[NetworkWelcomeReplayScenario](NetworkWelcomeReplayScenario.cs)、共享判据[NetworkWelcomeReplayEvidence](NetworkWelcomeReplayEvidence.cs)；完整步骤/命令/回退及证据见[本轮记录](../../docs/validation/P1-welcome-replay-2026-11-02.md)。

显式Enabled=true/Scenario=NetworkWelcomeReplay/OpenPreview=false且两端主模块开关与专用世界身份匹配后，先正常同进程首次加入/客户端原标题/同角色重连。冻结的Lifecycle保留重放前三点；主机新Ready后等两秒再调用固定旧Welcome入口一次，客户端必须先捕获BeforeReplay再实际读取旧challenge并拒绝。五分钟从首次SaveLoaded计，失败不复活。唯一外层报告为network-welcome-replay-RunId.json；该模式不另写一个重放后仍声称ProbeSent=false的被动最终文件。

`dotnet run --project tools/NetworkWelcomeReplayCheck/NetworkWelcomeReplayCheck.csproj -c Release -- <host.json> <client.json>`读取两个归档原报告。单端Completed不是通过；双端共享校验、真实SDK来源/加载次数/DLL/存档/config审计齐备才可计OldWelcomeReplayRejected。广义旧报文/带负载队列/Full仍false，未Ready前只在纯测试覆盖。结束必须关闭、核八save不变、恢复本轮四config原字节并预检；不睡觉保存，不覆盖旧轮证据。

## NetworkLifecycle：同进程同ID重新握手

2026-10-12真实原/最终双报告配对及85项关闭恢复通过，当前默认禁用。该通过仅正常有gap同ID重新握手/host原会话保持，机器数0、Full/旧包/负载队列false；客户端有收尾标题，主机本轮缺此快照。角色反向及超时原失败保留，详见[本轮实测](../../docs/validation/P1-network-lifecycle-2026-10-12.md)。

入口为[NetworkLifecycleScenario](NetworkLifecycleScenario.cs)，判据为[NetworkLifecycleEvidence](NetworkLifecycleEvidence.cs)。这是独立模式，不接续Network Basic失败的episode。两端GameHarness显式`Enabled=true/Scenario=NetworkLifecycle/OpenPreview=false`，绑定同一专用非零世界ID，角色分别Host/Client；主模块只读诊断须同名称/ID显式开启。当前专用世界XYSNET02/450604963、春24、已有客户端XYSNETClient。停止进程并备份存档/config原字节后才配置或部署，禁止在运行中替换DLL。

实机推荐客户端在一次操作中加入→等10秒→原标题停5秒→原进程同角色重新加入→等10秒，再统一读取报告；观测器保留三检查点，不必逐阶段等待操作者回复。先主持/加入，等两端记录InitialReady；建议先只启动Host并主持，核日志MainPlayer及无Failure后再启动Client，加入已有角色；同时打开的相同窗口可能被反向操作。只让客户端返回标题，主机保持原世界，核客户端session=null/peers空/queue0及主机peer gap；然后客户端在**原窗口、原进程**加入同角色。Host只允许一次SaveLoaded，Client恰两次；第2次加载不清掉首次记录，SDK重新构造的客户端Item引用只要求每次加载内部保持，库存值/raw指纹仍跨重连相等。五分钟从各自首次SaveLoaded开始，额外读档、指纹变化、trace淘汰、队列拒绝、保存或超时均失败；失败报告不能重置或拼接新进程。

模式没有自动加载/输入/定位/冻结时钟/保存或TrySendProbe。唯一`network-lifecycle-<HarnessRunId>.json`为独立schema1，含ProcessId、实际加载的生产/夹具DLL SHA、原Ready/标题或gap/新Ready/可选收尾标题。实际Hello/Welcome必须位于相应连接及当前generation的trace，旧新挑战不同，主机原world/session保留；双方GUID/时钟/线程不能跨端直接要求相等。Completed只代表本端观察完成，必须由独立工具核原双报告：

```powershell
dotnet run --project tools/NetworkLifecycleCheck/NetworkLifecycleCheck.csproj -c Release -- <归档host.json> <归档client.json>
```

工具不引用SDK，使用同一实际判据及严格2MiB读取器；字段缺失/重复/别名/未知成员或无效数据安全REJECT/exit1，无成功摘要。通过仅证明SameIdRehandshakeObserved/HostWorldSessionPreserved，OldPacketReplayVerified/LoadedQueueLifecycleVerified/FullNet02Accepted均false。它不能鉴别人工JSON真伪，须另关联当前进程、DLL、日志和磁盘；VisibleMachineCount=0不证明已有机器保护。

结束先客户端后主机返回标题/关闭，不睡觉保存。核CIM0及本轮八save原hash，归档原报告/日志/config，再恢复本轮四角色config原字节为Network/Disabled；runtime两份不改。只有已确认本轮唯一报告可在核归档hash后移出Mods，未知文件不删。最终实际profile预检见[NET02说明](../../docs/NET02_TEST_SETUP.md)，逐阶段状态和结果见[生命周期记录](../../docs/validation/P1-network-lifecycle-2026-10-12.md)。101项核心通过含8共享生命周期和4严格reader场景，人工测试不代实机；异常边界真实红绿见[BUG021](../../docs/bugfix/BUG-2026-021-lifecycle-cli-invalid-data.md)。

## InputObservation：P1输入故障的被动观测

入口为`InputObservationScenario.cs`，纯安全边界为`InputObservationPolicy.cs`，维护记录见 [10-11验证](../../docs/validation/P1-input-observation-2026-10-11.md)。此模式不会自动加载农场，操作者必须从合作菜单主持专用测试档；也允许同一专用档的单人主玩家，但单人结果不能充作合作输入验证。客户端、分屏、非主玩家、OpenPreview=true、农场/非零ID/实际保存目录不匹配均拒绝。农场名须精确匹配配置、以XYSNET开头且长6–20，保存目录须为`FarmName_SaveFixtureId`，防止旧源/UI/Stage3档进入采样。

配置示例使用当前已备份的专用世界，仅临时修改host的GameHarness，生产`EnableReadOnlyNetworkProbes`保持false：

```json
{"Enabled":true,"Scenario":"InputObservation","FarmName":"XYSNET02","SaveFixtureId":450604963,"OpenPreview":false,"NetworkRole":"Host"}
```

NetworkRole在此模式不参与协议或发包，保留原字段方便恢复。先关闭全部游戏/SMAPI进程，备份八个存档文件和六份指定配置原字节，再构建、部署同次夹具；生产包无需重建。不要在游戏运行时替换DLL或配置，不能直接重跑已有一次性审计脚本覆盖证据目录。

首次SaveLoaded通过身份后立即开始三分钟观测，每200ms最多一份、最多900份；另保留最多120次SMAPI ButtonPressed事件的当时读数，以补短按遗漏。仅W/A/S/D/E/Escape，聊天激活或任何文本Subscriber存在时六键全部省略，事件仅累计脱敏数量；不记录聊天、任意键、玩家姓名、完整背包、机器状态或完整日志。持续记录焦点、overlay门禁、键盘归属、菜单/文本Subscriber类型、冻结/暂停/跨日/事件门禁、CanMove、工具/动画/坐卧状态和玩家位置。所有回调先重新核世界与主玩家；离开世界、再读档、返回标题或异常立即停止，原episode不能接续。

键盘列的含义必须保持区分：`MonoGameKeys`是MonoGame轮询结果（不等于操作系统原始输入）；`InputStateKeys`是当前游戏InputState，可能已被SMAPI替换/抑制；`ChatFilteredKeys`是实际Game1.GetKeyboardState过滤结果；`PreviousControlKeys`是Game1.oldKBState（最近控制阶段保存的值，门禁阻止控制更新时可能停滞，不能固定理解为上一帧）；`SmapiDown/SmapiSuppressed`是SMAPI回调时状态。六层按先后读取，并非原子快照；短按、窗口失焦或事件阶段差异可能造成不一致，不能仅凭一帧断定根因。实际按住D两秒、再按Esc，结合连续位置和菜单类型判断。

本地`input-observation-<唯一run>.json`每秒更新，最终Status为Completed仅代表三分钟到期；Stopped/Rejected/Faulted表示相应停止原因。ReportWriteFailed只在日志明确报告，磁盘可能保留旧的Observing/不完整文件，不能当完成。`InputRecoveryVerified`、`FullNet02Accepted`始终false，由人工关联日志、用户结果与磁盘审计另行验收；没有自动“修复成功”判据。

**观察结束不会退出或冻结游戏，原生时钟/昏倒/自动保存仍会推进。** 进入农场后完成按键即关闭测试窗口，不睡觉、不保存，退出无需等三分钟；Stopped报告同样可供分析。确认游戏进程0后归档报告和日志、核八存档哈希，再恢复host配置的备份原字节，其他五份指定配置保持禁用；下一次启动使用新进程/新报告。`SavedByObserver=false`仅表示夹具没有调用保存，不证明原生游戏没写盘。10-11当前保存为春23，不能拿10-05春2当这次的基线。

直接关闭整进程未必触发ReturnedToTitle；10-11真实短测约30秒后关闭，原JSON保持Observing/Failure=null/验收flags=false。须独立用ClosedGame日志、CIM0和磁盘审计证明终止，不修改原文件状态。该次144样本支持D输入/向右42.24023像素和Escape关闭DialogueBox；操作者报告正常，但没有常规GameMenu或持续走出床的采样，不能扩大自动结论。唯一原报告已归档并移出Mods，六指定config恢复禁用、八save不变，详见同日验证记录。

```powershell
dotnet build tests/GameHarness/GameHarness.csproj -c Release -p:GamePath="D:\path\to\test-game"
```

将产物 GameHarness.dll 和此目录 manifest.json 放入测试游戏 Mods/GameHarness；Memory 模式在该文件夹 config.json 明确写 `{"Enabled":true,"Scenario":"Memory","FarmName":"XYS_P0","OpenPreview":true}`，FarmName 必须换成实际测试农场名。先部署同次构建的主模组和 CP 内容包，再启动游戏读入专用农场。一次读档运行一次，result.json 写入夹具文件夹；包含通过的用例名称、异常与是否恢复。完整日志留在本地，不随仓库提交。

默认 Memory 模式暂时替换当前背包、游戏 HHMM 与相邻空格设备，在 finally 恢复原引用并删除临时设备，不调用保存。没有空的相邻格则明确中止。两个错误日志是有意的回调故障注入，必须结合最终报告判断，不能宣称测试期间日志零错误。OpenPreview 仅打开脱离地图的设备菜单供视觉检查，执行器会拒绝其实际加工。

覆盖商店数据/配方名称与产出、两种机器实际扣料与领取、缺第二原料、跨品质、请求重放、满包、工具/爆炸/NPC/位置删除保护、损坏状态保留、空闲拆除和库存回调故障补偿；新增实际 MachineMenu 的领取同坐标双击保护、满包后同菜单重试和原生 ShopMenu 料理售出。2026-09-30 的 12 项已通过，Failure=null、Restored=true。售出回归先在 Saloon 复现拒收，改为 SeedShop 后验证原生接受规则、料理两份整批精确付款、再次点击空槽无重复付款；两个 Error 是预期故障注入。数据级配方检查和方法级商店回归不等于完整购买/售出 UI、原版厨房制作或跨日保存重载通过；这些必须另行实测。完成后关闭游戏，禁用或移出此测试模组。

生产菜单一次打开最多执行一个成功的 Applied 操作，成功后按钮禁用，下一次操作须关闭并重新打开。失败重试保持同一 request ID、InstanceId 和 revision；背包空间解除且设备状态未被外部推进时可以重试，不自动改绑新的设备状态。纯 MenuActionSession 回归已有 2 项通过，见 [BUG-2026-007](../../docs/bugfix/BUG-2026-007-menu-double-click.md)。

### Memory 新增放置与工具链组件

Memory 最新补充（2026-10-01）：全量 16 项通过，详见 [放置与工具链实测](../../docs/validation/P1-world-2026-10-01.md)。PlacementScenarios 的两设备用例各覆盖 4 种占用目标，直接 placementAction 组件回归先红后绿；ToolRemovalScenarios 两设备用真实 Pickaxe.DoFunction、单实体 Debris.updateChunks 自动吸取及 Utility.tryToPlaceItem 消耗真实 carrier 完成重放，再验证新 instance ID、旧请求拒绝和新请求开工。原测试格与床碰撞造成首轮重放失败，后改搜索原生预检允许的空地/相邻站位，不移除家具或原设备。

Memory result.json 的 ToolFixtures 附原请求格家具、选中格、站立格及 NativePrecheck；本日均为 `(10,9)/(F)2048` → 设备 `(8,7)`、站位 `(7,7)`、true。工具场景仅更新本次新增 Debris，完成后按原生移除契约移出集合；不直接 collect。同步测试隔离随机源/HUD，finally 恢复原设备/库存、位置/朝向/索引/工具状态/体力、时钟/菜单、随机源/拾取提示、统计和原掉落，并执行断言。只清理本次新场地，外层原格保持不变；这些 API 回归不代替物理鼠标工具与放置输入或该链保存重载。最终 Failure=null、Restored=true，日志仅两次预期事务故障 Error。旧 12 项与本轮新增 4 项必须同一次读档完整通过，不能拼接报告。

## Persistence 模式：会保存专用副本

入口为 PersistenceScenario.cs。这个模式与 Memory 的恢复契约不同：**会改动并保存测试副本的资金、背包、日期、配方、地图设备与玩家 modData**，随后自动退出标题并从磁盘加载三次。禁止在唯一正式存档上使用。

1. 游戏关闭后，以已有测试档作为源运行 `python tools/prepare_save_copy.py --source "具体测试存档文件夹" --label XYSP1Save`。源文件保持原状，输出独立 game ID 的副本到 .local/save-fixtures。
2. 将新生成的完整文件夹复制到游戏 Saves 目录。保留 .local 原始副本供复跑；不要覆盖既有文件夹。名称必须是 XYSP1Save，不含下划线，因为游戏保存名过滤会去掉下划线。
3. 配置写 `{"Enabled":true,"Scenario":"Persistence","FarmName":"XYSP1Save","SaveFixtureId":8624092801}`；示例 ID 必须换成第 1 步实际生成的 ID。入口同时检查农场名、游戏 ID 与保存名；不匹配立即拒绝，避免写入其他农场。
4. 部署同次主模组/CP 内容包及夹具，启动测试游戏。夹具会在标题初始化后自动加载且仅加载指定副本；测试期间不要手动加载其他农场。结果为 Mods/GameHarness/persistence-result.json。
5. 通过标准为 Completed=true、Stage=3、6 个 Passed、Failure=null；终点夏 1。失败时保存报告/日志，不强行标绿；退出游戏后排查。完成后配置 Enabled=false，避免下次启动自动运行。

场景顺序：给测试资金与原版制作材料 → 真实 ShopMenu 库存/购买逻辑扣款并学习三配方 → 原生 CraftingRecipe 扣料/产出 → placementAction 放置与重新分配实例 ID → 两条原型加工路线 → 构造加工中/待领取/领取后保存矩阵 → SaveGame.Save 写盘/退出标题/SaveGame.Load → 原生 newDayAfterFade 从春 28 到夏 1 并夜间保存 → 领取/再保存/再重载检查不复制。

阶段检查点保存在此副本 player.modData 的 Sora6A.XiangyeShiling/LocalHarnessCheckpoint，包含 Stage、机器位置/原始 JSON、库存摘要和已通过场景。中断后同配置可从已保存阶段接续；要重跑购买等初始阶段，应再生成一个全新 ID 副本，不删旧证据或在正式档清 marker。

购买通过游戏原生私有 tryToPurchaseItem（反射，仅测试夹具）；制作使用游戏配方对象的扣料与产出。它们验证实际学习、扣款、来源和消耗，**不代替步行到商店点击界面、房屋厨房交互、食用/赠礼/售出验收**。时钟/初始日期由夹具准备，跨季节那一步必须走原生夜间流程。此模式不故意注入库存异常，最终正常运行不应有非预期 Error。

## Interaction 模式：实际界面输入，不保存

入口 InteractionScenario.cs。游戏关闭后，从未学习本模组配方的测试源档用 prepare_save_copy.py 创建 `--label XYSP1UI` 的独立副本，复制到 Saves（不要覆盖旧副本），配置 `{"Enabled":true,"Scenario":"Interaction","FarmName":"XYSP1UI","SaveFixtureId":实际独立ID}`。同时核对农场名、保存名和 ID，加载后会打开原生商店。

每一步用实际鼠标/键盘完成界面操作，把手持物品放入背包并关闭菜单，再按 F8 或点击鼠标中键核验并进入下一步。桌面注入的极短 F8 按压可能被游戏输入轮询遗漏，此时使用中键；它不会自动购买或执行生产事务。

顺序为皮埃尔黄豆 9/小麦 2 → 罗宾两设备图纸 → 格斯香椿 3/菜谱 → 制作两设备 → 豆坊两批各开工/领取 → 发酵缸开工/领取 → 烹饪三份香椿拌豆腐 → 吃一份 → 在 SeedShop/皮埃尔售出剩余两份。购买菜谱仍在格斯，售出商店按 BUG-2026-008 改为收购料理的 SeedShop；旧 Saloon 配置会失败，不应作为复跑前置。每阶段核对数量、配方、金币或恢复值，报告写入 `interaction-result.json`。全部 13 步通过时 Completed=true、Step=13、Failure=null。

2026-09-30 的修复后完整重跑 13 步全部通过，Completed=true、Step=13、Failure=null、Saved=false。实际界面执行获料/学习、制作、两设备加工、CookingPage 烹饪三份、食用一份恢复 175 能量/78 生命，以及皮埃尔售出两份各 240g；最终资金 15590g（15110＋480），背包余镐一把、豆腐一份、酱油四份，料理为零，购买原料全部消耗。六次机器动作按钮均以双击输入验证，没有误扣。最终 UI 日志无 ERROR/WARN，Memory 两个故障 Error 为预期注入，分别统计。

首轮历史失败保留：前 12 步通过后在 Saloon 点击料理没有售出，资金保持 15110g、料理保持两份，核验生成 Completed=false 的真实失败报告。按 [BUG-2026-008](../../docs/bugfix/BUG-2026-008-ui-sale-shop.md) 改 SeedShop，原生方法回归先红后绿后，再执行上述 13 步完整界面重跑；生产物品数据未改，购买菜谱仍在格斯。完整证据见 [UI 实测记录](../../docs/validation/P1-ui-2026-09-30.md)。

夹具给予初始 20000g、木 80/石 60/铜锭 1 和镐，直接打开完整原生 ShopMenu/CraftingPage 商店/制作/烹饪菜单，将实际制作的设备原生放置并把玩家移动到相邻格；关闭开工菜单并核验后跳过等待时间，再打开机器菜单。它不调用购买、扣料、开工、领取、食用或出售逻辑。该方法验证界面与生产数据接通，**仍不覆盖 NPC 营业日程、步行开店、房屋厨房地图触发、手工放置、赠礼、工具击落拾取、手柄、英文及其他界面尺寸**。P1 总体验收未完成，联机放置/加工保持禁用，P2 不开始。

本模式只改内存，不调用保存，也不恢复内存。冻结测试时钟防止长时间检查推进到昏倒；完成后退出整个游戏、禁用 Enabled，并比较副本文件 SHA-256 确认未写盘。不能睡觉或手动保存，不能在测试中切换其他农场；后续重跑直接使用未保存的原副本。

09-30 Interaction 那轮测试游戏已退出，配置已禁用；后续 WorldInteraction 的实际进程/配置以 [HANDOFF](../../docs/HANDOFF.md) 为准。接手仍先复核进程与配置，不能盲用旧 PID 或把夹具默认启用。

## WorldInteraction 模式：实际世界输入，不保存

入口 WorldInteractionScenario.cs；用于补足 Interaction 自动放置与 Memory 直接调用原生 API 未覆盖的鼠标输入。配置写 `{"Enabled":true,"Scenario":"WorldInteraction","FarmName":"XYSP1UI","SaveFixtureId":8624092802,"OpenPreview":false}`。此模式核对专用副本的固定 ID、农场名、保存名、单人及主玩家身份，拒绝操作其他世界。上述 ID 是当前本机专用副本；换副本须同步明确身份约束，不能用任意正式档替代。构建/部署后启动会自动加载这一副本，不手工切换农场。

初始准备直接给予蓝图、木 80/石 60/铜锭 1、黄豆 6/小麦 2 和镐，打开 CraftingPage 并安排空地/站位与测试时钟；这不计作购买来源验收。准备前 VerifyWallPreparationRegression 用固定农舍 (8,2) 负例验证原生物品预检允许的墙格仍被 CanStand 拒绝。操作者实际制作每台设备、选择背包手持物并鼠标放置、右键打开设备菜单、开工/领取、选择镐后单击设备足迹格中心。Busy/Ready/损坏保护必须有只读 Harmony 观察的实际 Pickaxe.DoFunction 目标命中，结合鼠标输入和体力变化，再核验对象引用、状态原文、库存和掉落，不能把忽略的目标点击与另一处体力下降拼接成成功（BUG-011）。不要双击挥镐，等待 UsingTool=false 后再关闭菜单或推进。

计划 20 步：两设备各 9 步（制作→放置→开工→Processing 挥镐保护→Ready 挥镐保护→领取→Idle 击落→方向键走近自然拾取→重放新身份），最后各 1 步损坏 JSON 挥镐保护。按 F8/中键只核验并准备下一步，不执行制作、放置、开工、领取、工具或拾取。`world-interaction-result.json` 每阶段保留数量、身份、工具/掉落观测及当前指令，周期刷新坐标供定位；实机结果以 [世界输入实测](../../docs/validation/P1-world-ui-2026-10-01.md) 为准，不能由夹具存在推断已通过。

2026-11-06同一9004已实际完整20/20并退出，原报告/八存档/六配置恢复及88项审计通过，见[完整本轮证据](../../docs/validation/P1-world-ui-2026-11-06.md)。两段方向键走近及一次F8由操作者完成，其他实际鼠标由Computer Use执行；工具汇总8阶段、原生命中9次（发酵缸Ready两次），不声称每阶段恰好一次。10-01旧7/20仍保持原样，不拼接。当前无保存世界链不代当前版本原生保存/冷启动验收。

11-06因操作者报告“D移动无响应、Esc菜单可用”增加只读`ControlState`和最多32条`ControlInputEvents`，调查状态见[BUG024](../../docs/bugfix/BUG-2026-024-world-movement-investigation.md)。记录角色/全局移动门禁、焦点、运行绑定，以及W/A/S/D/Escape的MonoGame/InputState/聊天过滤/旧控制帧；聊天或文本Subscriber存在时键字段null、事件不采。事件容量满后只增加OmittedControlEvents，诊断异常单列ControlObservationFailure。快照依次读取且可能早于本帧原生控制，不把它称作原子快照、强制解锁或修复。Step0也能留短测事件；原StageMovementInputs仍只在阶段7计数，验收条件不变。新字段是否已部署/实际运行以HANDOFF为准，不能回填到旧报告。

原生磁吸最小半径为 128 像素，普通相邻挥镐会很快自动吸回。为了将“真实掉落”和“实际步行吸取”分别观察，Idle 阶段只在游戏已经移除地图对象且产生本次真实 Debris 后，一次性把玩家放到磁吸范围外；报告必须记录这次观察准备。远点排除原生农舍墙区、限定地板并通过四邻连通查询，不能仅凭 CanItemBePlacedHere 选墙后站位（BUG-012）。之后只能用实际方向键走近，报告同时保留输入及位移轨迹，由正常游戏 Tick 驱动吸取，不调用 updateChunks/collect，不再次定位到掉落。这包含一次观察用定位，不能宣称未干预的整段自由游玩；也不等于拆回携带/重放链保存重载通过。

与 Interaction 一样，此模式只改本次内存，不保存且不恢复。固定时钟防止耗时检查昏倒；不能睡觉、手动保存或加载其他存档。失败时保留现场报告/日志，退出整个游戏，Enabled=false、Scenario=Memory、OpenPreview=false，再比对源与 UI 副本主文件/SaveGameInfo 的 SHA-256。只有 Completed=true、完整 20 个 Passed、Failure=null、Saved=false 且磁盘哈希未变才记为全量通过；不能拼接失败轮次与不同进程的结果。夹具不进开发包，源安装不修改，P1 总体验收和剩余地图/赠礼/多语言/联机条件仍独立判定。

## Network 模式

入口 NetworkScenario.cs；由实际SaveLoaded启动观察，UpdateTicked核对，ReturnedToTitle留本地清会话快照。保持GetApi返回MachineService；不会执行Memory/World准备、自动加载、创建/保存、加物品或调用Execute。历史105项Python、85项核心检查属于此前工具/配对阶段。10-02扩展失败观测后Release退出0、0警告/0错误，具名人工失败报告拒绝测试1项通过；10-04接续两个新主机进程，实际Scope读前拒绝为null、读取成功后的probe门禁失败保留Protocol，原报告落盘与第一阶段endpoint因Failure拒绝均已核，见 [真实失败路径](../../docs/validation/P1-network-failure-2026-10-04.md)。六配置恢复禁用/八存档不变，生产与夹具源码本轮未改；真实peer/双端配对仍未执行。当前现场以 [HANDOFF](../../docs/HANDOFF.md) 为准，历史构建见 [观测扩展](../../docs/validation/P1-network-observation-2026-10-02.md)，操作见 [NET02测试说明](../../docs/NET02_TEST_SETUP.md)。

GameHarness配置Enabled默认false，Scenario=Network、FarmName以XYSNET开头且6–20、SaveFixtureId为实际独立非零世界ID，NetworkRole精确Host或Client，OpenPreview=false。缺省NetworkRole=Observe不能通过角色核对；主模块EnableReadOnlyNetworkProbes也须显式true且同世界名称/ID。两端共享正常Saves/日志，主机从合作菜单加载专用农场，客户端加入，不能各自加载同一档。

2026-10-02已核的准备事实：真实主保存和SaveGameInfo核到农场XYSNET02、世界ID450604963、春1年1，主保存含Cabin；两份XML已备份至`.local/net02-live-2026-10-02/farm-preparation`。plan/export和apply已成功把两端四份诊断配置绑定到该身份。旧host PID55272曾合作加载，client PID56168曾停在IP框，之后消失且退出动作未观察；自动type_text两次无文字、手动加入请求未回复，Farmer创建仍未核。10-04两个主机负例进程已用窗口X关闭并确认游戏0；临时夹具启用/Observe误配已恢复原字节，主模块探针/Enabled/OpenPreview全false，runtime仍禁用Memory。实际生成两份失败报告并归档，不是peer/探针/Basic通过，也不是客户端准备保存。不能接续旧窗口，最新现场以 [HANDOFF](../../docs/HANDOFF.md) 为准。

启动后六份config的原字节已变化，不能声称保持原文：四份绑定配置的JSON与apply候选完全相同；runtime主配置仅格式变化，runtime夹具旧五字段值不变并补默认NetworkRole=Observe。这些配置观察不证明实际握手或运行时网络验收。

首次角色准备与正式诊断按以下顺序分开进行：

1. 保持诊断禁用，让客户端实际加入小屋并完成首次角色创建；确认双方进入同一专用世界后完成准备保存，归档新保存和可归属日志。准备保存不属于夹具诊断，不能据此宣称Saved=false或网络验收通过。
2. 退出两端进程，重核XML身份、存档哈希、四份绑定配置与包哈希。配置在Mod Entry读取，不能靠运行中改文件启用。
3. 另行显式开启两端主模块EnableReadOnlyNetworkProbes和GameHarness.Enabled，OpenPreview保持false，启动两个新进程；客户端使用已创建角色，主机加载后尽快加入。启用后的60秒等待从各端SaveLoaded开始，首次角色菜单会占用主机等待时间，不能靠停留菜单延长期限。合作主机无客户端时Context.IsMultiplayer也可为true，但Ready仍需真实Hello/Welcome。
4. 活动世界内等客户端Step=6、BasicProbesCompleted=true、Failure=null及两端QueueCount=0，再等一轮报告刷新并归档两端原字节，运行配对工具。主机Step=0、BasicProbesCompleted=false正常；配对仅可验证基础证据，完整NET02仍待独立矩阵。诊断期间不保存，结束前先归档活动报告，退出后核存档哈希并关闭诊断开关。

真实Ready后核本地背包Item引用/ID/Stack/Quality/modData指纹、可见机器raw指纹及数量；空机器集合如实count0。不公开库存/raw原文，诊断期间指纹变化即Failure。客户端固定五类synthetic意图要求相应拒绝，1300ms隔离发送，10秒响应超时；然后同帧9Start收到8条不同ID拒绝仅为BurstEightResponsesCandidate。主机观察端不自动通过，必须核它实际同窗口八读＋一次RateLimited未读。真实网络跨窗口/丢包不能用少一回复证明限额。

network-probe-result.json包含进程/HarnessRunId/episode/role、生产DLL SHA、指定世界ID、指纹/count、Passed/ProbeCases发送前序号/BurstIDs及生产Protocol快照。共享NetworkProbeEvidence绑定run/session/generation/主机连接/游戏线程/Type/读取尝试/后续拒绝，证据丢弃或连接改变中止（BUG-016）。BasicProbesCompleted只描述候选子集，FullNet02Accepted/ ReconnectVerified恒false；标题清会话只是快照，不验证完整重连/旧队列/16消费/128满额/错版分屏/强认证。不把纯会话的实际委托调用当SDK网络调用。

失败报告的Protocol只保留本次Tick已成功ReadNetworkDiagnostics返回的快照：读取前失败为null，读取后失败保留本Tick观测，不沿用上一Tick，也不在catch再次读取API。快照可能早于本Tick的发送或异常，不代表失败后的最终状态；部分后续发送也可能尚未出现在它的trace中。Failure仍非null，离线配对无论Protocol是否完整都拒绝该报告；SchemaVersion=2与FullNet02Accepted=false不变。报告写盘自身也可能失败，因此保留观测不保证报告已经落盘，须检查实际文件并保留可归属日志。

10-02通过的`test_failed_saved_or_claimed_full_acceptance_reports_are_rejected`使用人工Python报告，不执行C# Tick catch。10-04的 [真实集成验证](../../docs/validation/P1-network-failure-2026-10-04.md) 分别实际运行Scope读前失败与读取后的门禁失败，第一份真实Host报告也被endpoint因Failure拒绝；没有修改源码、玩家事故、修复前红或新BUG。两新进程不验证同进程跨Tick/重连，API读取内部异常、标题与I/O失败仍未覆盖；不扩大成全部异常或网络通过。

trace最多256条，增加Evicted/QueueRejected不能通过当前客户端证据；不要扩宽判据取绿。报告保留在各自Mods/GameHarness，跨轮/两端归档到.local后再重建配置，工具会拒绝报告这类意外包文件，不自动删除。中断关闭两端夹具/主模块探针、查进程/存档hash、更新HANDOFF；Saved=false只声明夹具没调用保存，不能代替文件审计。不借源/UI/Stage3档来方便复跑，不清旧marker。

## Network schema2 与配对

Schema2带首个Ready Baseline/current指纹/引用一致/burst发送序号；每Processed含实际Quota窗口/计数与连接token。network-probe-result.json保存活动世界，标题另写network-probe-title-result.json；不要用标题报告冒充基础证据。两端归档原文后运行tools/check_network_reports.py，详见 [配对命令](../../docs/NET02_TEST_SETUP.md)。工具核共享身份/请求与各端独立上下文，九发八回需主机同窗口实际八读一未读，不能给未读第九条编ID。schema1/Failure非null/连接变化/失活/丢弃/额外流量拒绝；输出仅Basic/Fullfalse。40人工报告测试不证明SMAPI；首次角色准备尚未核完，旧准备进程已消失，当前无实际网络诊断或pair结果，完整NET02与诊断前后保存hash仍待实测。
