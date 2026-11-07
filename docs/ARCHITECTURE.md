# 工程架构与维护入口

11-06新增本地WorldShopInteraction：只准备专用单人资金/空背包/人物时刻与三店定位，实际柜台Action的只读postfix与ShopId/原生库存快照分别留证，关店中键核对付款/原料/配方学习，不调用购买或学习API、不改库存表。真实同次三阶段及停止审计通过；不代表NPC日程或地图步行，生产事务/数据未变，见[三店验证](validation/P1-world-shops-2026-11-06.md)。

11-05厨房接续：GiftInteraction复用可选KitchenGiftInteraction，在实际地图灶台触发后观察原版烹饪/成品赠礼；新增KitchenEntryProof与临时只读Action postfix，使用原生实际格而非SMAPI GrabTile，结束/失败/标题解除。真实同次流程与115核心通过，生产事务/喜好/数据/存档未改，见[BUG023](bugfix/BUG-2026-023-harness-kitchen-action-source.md)及[厨房证据](validation/P1-kitchen-gift-2026-11-05.md)。

11-05增加仅本地GiftInteraction模式：专用单人档内存准备后，由真实鼠标触发原生NPC收礼/同日拒绝，中键只核验；不调用NPC赠礼API、不改生产喜好/事务/存档，保存被观察即失败。实际料理扣一份/好感+45及重复拒绝保持已通过并停止归档，见[赠礼记录](validation/P1-gift-2026-11-05.md)；定位不代表普通地图步行验收。

11-03单人菜单新增两页只读指南/缺料差额，导航先于事务点击处理，保留原交易按钮位置与一次成功锁定。路线名/量/工时读当前Service.Recipes，来源说明只描述既有三家商店；库存预览不替代服务提交核验，无新持久化字段。五项物品描述从catalog生成CP文案，目录范围不变；SDK18与中文查看通过，说明见[玩家指南](PLAYER_GUIDE.md)、[单人状态](P1_SINGLEPLAYER_STATUS.md)及[验证](validation/P1-singleplayer-guide-2026-11-02.md)。

11-02：新增Networking/WelcomeReplayBuffer与MachineService固定旧Welcome诊断API，来源仅本机已发送内存，主线程/专用世界/唯一当前Ready/一次性约束；Reset清空，机器事务和线上格式不变。独立NetworkWelcomeReplayScenario、共享Evidence与无SDK CLI先核实际重连再核发送/读取拒绝/状态保持。112核心和构建/停止部署通过，实机仍待；完整所有权/限制/回退见[本轮记录](validation/P1-welcome-replay-2026-11-02.md)，Full仍未验收。

alpha.2单人P1原型已接入两设备菜单/背包、7物品5配方及有限商店，联机加工/放置拒绝。专用世界准备、真实Basic及独立同进程同ID重新握手/主机原会话保持已完成子集验证；后者由共享NetworkLifecycleEvidence及严格无SDK CLI校验实际原双报告，结束存档/配置/85项恢复通过。机器数均0，旧报文重放/负载队列/强认证和真实远端背包提交不借此通过，Full/P1仍待。当前入口/源码来源/运行状态见[HANDOFF](HANDOFF.md)和[生命周期实测](validation/P1-network-lifecycle-2026-10-12.md)，旧日期记录保留历史。

## 模块与数据流

| 模块 | 输入与职责 | 维护约束 |
|---|---|---|
| data/catalog.json | 40 物品/30 配方冻结源；仅 prototype 激活 | planned 的 null 数值不能运行 |
| tools/catalog.py、prototype.py | 生成 CP 数据/i18n、登记表和机器配方 JSON | --write 更新全部生成物 |
| CP 内容包 | 增量修改 Objects/BigCraftables/CraftingRecipes/CookingRecipes/Shops | 不整表覆盖，不分发原版图像 |
| ModEntry | 配方、补丁、读档/换场景/新增设备初始化、会话重置 | 加载诊断不是完整验收 |
| Machines | 无游戏依赖的规则、模型、schema 1 JSON | Accepted 只意味着计划有效 |
| Networking | 严格请求/可信快照准入/lease与回执，以及只读握手/限流/邮箱 | 无游戏依赖；只读分支不调用准入/账本/提交，不操作库存；见 P1_HOST_PROTOCOL |
| Transactions/CommitCoordinator | 准备、核对、防重入、提交/补偿与隔离 | 不是联机事务协议 |
| GameIntegration/MachineService | 身份/范围检查，真实快照、克隆、库存/modData 适配 | 保留游戏线程/单人限制 |
| GameIntegration/ReadOnlyProtocolService | SMAPI事件→连接/世界标识邮箱→UpdateTicked→定向握手/拒绝 | 只读；元数据不是强认证；128排队/16每帧/32peer/每peer每秒8条 |
| GameIntegration/MachinePatches | 两设备 ID 的菜单、投料、放置和移除路径 | Harmony 来自完整 SMAPI 安装 |
| Menus/MachineMenu | 显示原料/产量/时间，发送绑定 revision 的请求 | 不自行扣料 |
| Menus/MenuActionSession | 绑定一次菜单打开期间的请求和操作完成状态 | 成功后锁定，失败重试保留原请求；重开才产生新意图 |
| MachineCore 与 Python tests | 无游戏规则/补偿/菜单会话、内容与维护检查 | 不能代替实机 |
| tests/GameAdapter | 引用真实生产适配器和本机 SDK 的独立 CLI 回归 | 所有引用不复制；局部 Harmony 测试 finally 撤销，不打开游戏/存档；不加入无游戏 CI |
| tests/GameHarness | 默认禁用的本地集成夹具 | 不进开发ZIP；Memory恢复不保存，Persistence保存独立副本，Interaction/WorldInteraction不保存不恢复；Network只读观察/固定探针，须双开关与专用非零ID/角色；InputObservation限定专用主玩家、只读输入/门禁，不加载/准备/保存/模拟输入/发包，见夹具README |
| tests/GameHarness/InputObservationPolicy | 真实夹具与MachineCore共同编译的纯输入观测门禁 | 非零世界ID/保存目录/主玩家逐次核对，六键白名单、文本输入时省略按键、三分钟/900采样/120事件上限；不是输入恢复验收 |
| tests/GameHarness/NetworkLifecycleScenario、NetworkLifecycleEvidence | 被动同进程同ID重连观测与共享纯判据 | Host一次/Client两次原生加载；跨标题保留历史，实际新挑战/连接、主机原session、每load引用及跨load值/raw保持；五分钟，不发送probe/操控/保存 |
| tools/NetworkLifecycleCheck | 独立无SDK schema1原双报告配对 | 同一实际判据，严格字段/2MiB限制，安全REJECT/1；只核重新握手及host session保持，不接受Full/旧报文/负载队列能力声明 |
| 构建/环境工具 | ZIP、隔离游戏/Mods/存档副本 | .local/artifacts 忽略，第三方 DLL 不分发 |
| tools/configure_network_fixture.py | 实际专用XML身份＋两端包/config→只读plan或四禁用配置绑定 | 源存档/包/profiles只读，不创建世界/启诊断；before/prepared/hash审计，逐文件替换非批事务，不执行旧导出计划 |
| tools/check_network_preparation.py | 原生SaveGame.farmhands直接Farmer名单＋宿主Info＋两文件稳定hash | 只读；复用fixture内部有界XML/路径解析，更新该解析器时同步新测试。空名false占位不计客户端；唯一created角色/不同非零Int64才证明持久化，连接/输入/保存流程/退出/Full另核 |

数据路径：catalog → 生成/检查 → CP 数据＋运行时配方 → 实机。交互路径：菜单 → 真实快照 → 纯规则计划 → GameCommit 准备 → 协调器核验/提交或补偿。

## 不变量与持久化

设备身份、revision、请求 ID 防重放；ActorId 来自真实单人 Farmer，未来联网必须由认证消息上下文取得。每批固定投入明细、产量、品质和 ReadyAt。全批放得下才清空；堆叠资格由游戏判断。

菜单会话最多接受一个成功的 Applied 操作，成功后禁用操作按钮，下一次开工或领取须关闭并重新打开菜单。失败不完成会话，缺料或满包解除后可沿用原 request ID、InstanceId 和 ExpectedRevision 重试；外部状态已推进时仍拒绝旧修订，不能自动绑定更新状态。此约束防止同位置连点把领取解释为下一批开工，见 [BUG-2026-007](bugfix/BUG-2026-007-menu-double-click.md)。

状态存于 modData，严格 schema 1；未知或损坏值保留且拒绝操作。新放置取得新 InstanceId，空闲拆回再放也不复用旧请求身份。没有 schema 迁移器；新增字段前设计旧版本迁移和失败路径，禁止静默重置。

放置补丁必须先验证目标格为空，再确认原生成功后地图中新增了对应 ID 的对象才初始化；原生 `placementAction=true` 不是新增对象的充分证据。拒绝占用格时原设备、JSON、库存和携带物不变，避免覆盖正在加工或待领取的批次，见 [BUG-2026-009](bugfix/BUG-2026-009-occupied-placement.md)。

原生 NPC 清路会把空格对应的 `null` 对象交给移除补丁。`TryGetKind`、`TryRead`、`BlocksRemoval` 接受空目标；空格和其他物品放行原方法，不初始化或写入状态。只有成功分类时对象才保证非空，返回 false 时分类或状态输出不可使用。已识别设备缺失/损坏/未知状态仍保护，加工中和待领取保护保持不变；空目标不是损坏设备，见 [BUG-2026-019](bugfix/BUG-2026-019-null-removal-target.md)。

写前准备克隆和 JSON，写前/写后核对库存、设备与状态。补偿恢复原引用和字符串，失败隔离会话；第三方监听器任意外部副作用不在恢复保证内。具体契约、时间单位与移除入口见 [P1 事务](P1_TRANSACTIONS.md)。

## 联机边界与接续

2026-10-01 [放置与工具链实测](validation/P1-world-2026-10-01.md) 全量 Memory 16 项通过。两设备在实际 Pickaxe.DoFunction 下保护批次与损坏状态，空闲设备只生成一个 Debris，经 updateChunks 自动拾取后以 Utility 消耗真实库存放置，获得新身份；旧请求拒绝且新请求可开工。场地选取须经过原生完整碰撞预检，独立工具夹具保留原设备/家具/掉落并恢复玩家与随机状态。它验证游戏组件 API；物理输入、地图游玩入口、赠礼、保存该链和双客户端仍另行验收。

主机请求纯核心与ReadOnlyProtocolService只读桥已实现，合法Start/Collect始终拒绝写入。历史85核心/77Python与两项Release见 [配对验证](validation/P1-network-pair-2026-10-02.md)，105Python含28身份专项见 [工具验证](validation/P1-network-fixture-2026-10-02.md)。10-02夹具Tick观测增强见 [观测扩展](validation/P1-network-observation-2026-10-02.md)，10-04两个新主机进程已实际验证Scope读前null、正常读取后门禁失败保留及Failure拒绝，见 [失败路径](validation/P1-network-failure-2026-10-04.md)；不是API内部异常、跨Tick或双端验收。生产源码不变，真实专用身份仍禁用绑定，见 [历史合作准备](validation/P1-network-live-2026-10-02.md)、[主机契约](P1_HOST_PROTOCOL.md)。元数据不证明库存可写权；桥不构造HostActorSnapshot或调用Ledger/Commit。world/disconnect generation及连接引用GUID筛旧排队事件，同ID重连须新challenge（BUG-014）。MachineService GetApi的主线程诊断读/固定probe委托不换类型、不调用Execute；256条trace淘汰显式计数，诊断不是成功账本。Network夹具绑定本次run/session/主机连接与读入证据（BUG-016），synthetic意图/八回候选不等于完整NET02或实际机器准入。身份与开关现场统一见 [HANDOFF](HANDOFF.md)，操作边界见 [准备说明](NET02_TEST_SETUP.md)。联机加工/放置门禁保持，有效Idle原生移除可能走原版。

后续 WorldInteraction 由操作者实际鼠标制作/放置/开工/领取/挥镐，以只读 Harmony 记录真正 Pickaxe.DoFunction 命中，避免任意体力下降与忽略的目标点击拼接成成功（BUG-011，2 项纯判据回归，该夹具轮核心合计 28 项通过）。观察掉落后只一次定位到原生墙/地板过滤及四邻连通查询允许的远点（BUG-012）；之后只记录方向输入与逐帧位移，由游戏正常更新吸取，不调用工具或拾取 API。Saved=false 仅表示夹具未调用保存，须独立哈希审计。

2026-10-01 最后同一运行通过豆坊前 7/20 步后中断；末次报告 Step7、Completed=false、Failure=null、方向输入/轨迹零、掉落一份，自然拾取及重放未完成。最新两次本机查询确认游戏/SMAPI 为零，退出动作未观察；Enabled/OpenPreview 已禁用、Scenario=Memory。中断审计已实际确认源/UI 四个存档文件哈希未变及当时既有生产包/build/部署一致。该模式不保存也不恢复内存，Step7 现场随进程消失不能接续，后续必须新鲜进程完整重跑，不能拼接历史通过项或把中断审计当作 20 步验收。完整证据与边界见 [世界输入验证](validation/P1-world-ui-2026-10-01.md)，配置与重跑状态以 [HANDOFF](HANDOFF.md) 为准。

单人原生购买/制作、三状态磁盘重载、跨季节与冷启动已通过，见 [保存验证](validation/P1-persistence-2026-09-28.md)。2026-09-30 的 [Interaction 界面验证](validation/P1-ui-2026-09-30.md) 13 步全部通过，贯通获料、学习、制作、两设备加工、烹饪、食用和皮埃尔售出。首轮曾在前 12 步通过后因误选 Saloon 而售出失败；售出目标改为 SeedShop，原生商店回归先红后绿，完整 UI 重跑通过（[BUG-2026-008](bugfix/BUG-2026-008-ui-sale-shop.md)）。购买菜谱仍在格斯，生产物品数据未因夹具问题修改；12 项 Memory、26 核心场景与 26 项 Python 同日通过。

Interaction 直接打开 ShopMenu/CraftingPage，准备资金、制作材料、设备放置、玩家定位和时钟，不覆盖 NPC 营业日程、步行开店、房屋厨房地图触发、手工放置、赠礼、工具击落拾取、手柄、英文及其他界面尺寸。WorldInteraction 的实际世界输入单独留证，不用旧 UI 结果替代其未完成阶段。仍须实现并测试远端库存所有权、主机序列化确认、重放与断线恢复。P1 总体验收未完成，P2 不开始；场景启用/进程与停止步骤以 HANDOFF 为准。

## 变更检查

规则与提交变化运行 MachineCore；生成器/清单运行 Python；C# 补 Release；游戏行为补 GameHarness 和手工实测。修复关联 [bugfix](bugfix/README.md)，中断更新 HANDOFF，完整提交规范见 [CONTRIBUTING](../CONTRIBUTING.md)。

## 双端报告配对

tools/check_network_reports.py离线读schema2，按两端共享session/fixture/互为player/请求ID配对，各自基线/连接/线程/世代/时钟独立验证。实际LastQuota窗口/计数用于八读一未读，不由时间跨度或少一回复推断；本地诊断非强认证、不接事务。Network active与title分文件，成功只有BasicProbeEvidenceVerified/Full=false。历史40人工报告专项属于105/116 Python；10-12真实原活动双报告配对已核Ready/5probe/8读1未读及各端指纹，断连后主机Failure/双title另归档，最终Failure pair预期拒绝。局部空队列标题清理不代负载旧队列/重连/换世界；准确原SHA及85项收尾见 [实测](validation/P1-network-basic-2026-10-12.md)，操作/契约见NET02_TEST_SETUP。

Tick失败报告仅保留同Tick成功读取的诊断局部值；读取前失败为null，不跨Tick缓存或异常后重读。快照可能早于后续发送或异常，不代表最终状态。Failure仍拒绝配对，报告I/O失败不保证落盘；新增观测不改变schema2、Full=false、标题文件或生产只读门禁。人工拒绝测试不执行实际Tick catch，运行覆盖边界见本轮validation。

身份绑定先读真实专用存档/包/config，默认plan不写，导出仅保存原字节与候选。apply要求调用前实例停止、四配置禁用且角色正确，重新预检后只替换Farm/ID字段，源码不启动进程或创建/保存世界。profiles旧configs哈希保留历史，新的before/after与源XML/包哈希另记；原子替换仅保证单文件，失败可能部分更新且audit本身也可能写失败。不得把configurationApplied或非零ID视作世界独立/可用小屋/runtime/NET通过，下一步仍是实机双端。Windows配置路径按casefold与包分离，BUG-018保留实际红绿。
