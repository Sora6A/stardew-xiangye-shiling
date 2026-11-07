# P1 共享机器事务与单人适配

11-03新增菜单只读指南与缺料差额；GuideOpen/GuidePage仅为菜单生命周期状态，不创建事务或存档字段。导航点击先处理、禁止落到背后操作按钮，返回后保留Session完成锁；缺料即时预览不获取请求，服务仍在真实提交前核库存与世界。实际SDK完整18项含两新菜单组件通过，当前证据与未测项见[本轮记录](validation/P1-singleplayer-guide-2026-11-02.md)和[单人状态](P1_SINGLEPLAYER_STATUS.md)，玩家用法见[上手指南](PLAYER_GUIDE.md)。

更新至2026-10-05：alpha.2单人菜单/库存/modData/移除保护及只读桥已实现；真实专用世界450604963禁用绑定，首次客户端Farmer仍未核。P1未验收、联机加工/放置拒绝，顺序纯场景不证明双端安全。历史事务/UI与 [人工工具回归](validation/P1-network-fixture-2026-10-02.md) 保留；10-02夹具观测增强见 [观测记录](validation/P1-network-observation-2026-10-02.md)，10-04两个真实Scope/门禁失败分支见 [失败验证](validation/P1-network-failure-2026-10-04.md)。本轮生产事务与源码未改，没有新的事务或完整双端诊断验收。

## 规则层

`Machines` 不引用游戏。Start/Collect 只返回 MachineTransaction（Before/After/SlotChanges），Decision.Accepted 不表示已经扣料。

- 请求绑定设备 InstanceId、ActorId、ExpectedRevision、唯一请求 ID；每次成功开工/领取推进 revision，拒绝重复、旧修订与旧设备请求。
- 完整检查全部输入后才返回扣料计划；按背包槽位顺序消耗各品质堆叠，记录实际投入明细。只接受普通物品 `(O)` 作为原料和输出。
- 批次固定配方、投入、产物数量/品质、开始和结束绝对分钟。之后修改配方不会追溯改变本批次。
- Idle → Processing → Ready → Idle；Processing/Ready 不允许开新批次或搬动。领取必须放下全批，先兼容堆叠再空槽，空间不足时无计划且保持 Ready。
- CanStackOutput 来自游戏 `canStackWith`；不能只按 ID/品质猜测特殊物品可堆叠。

## 游戏调用边界

ModEntry 从清单生成的 assets/machine-recipes.json 读取两种机器配方并安装 Harmony 补丁。菜单显示持有/需要原料、解锁、产量、工时和剩余时间；提交服务重新读取真实状态，不相信菜单显示的旧数量。

MachineMenu 使用 MenuActionSession 保存一次菜单打开期间的操作意图。首次操作绑定 request ID、InstanceId、ExpectedRevision 和 ActorId；失败重试复用这份请求，不随新读到的状态改绑。缺料或满包解除且设备修订未变化时可以在同一菜单重试；外部修订变化则继续拒绝旧请求，操作者须关闭并重新打开。只有 Execute 返回 Applied 才完成会话，随后按钮禁用，同位置再次点击不生成新请求。下一次操作须重开菜单，避免领取成功后的连点意外开启下一批。详见 [BUG-2026-007](bugfix/BUG-2026-007-menu-double-click.md)；这层意图保护独立于规则层的请求重放检查。

MachineService.Execute 只允许入口所在线程、已加载世界、单人主机、Game1.player 的真实背包；拒绝剧情/使用工具中、玩家 ID 不符、不在当前位置字典中的设备及超过一格距离。GetApi 返回本地服务供独立测试夹具使用，**不是网络接口或稳定公共 API**。

1. 从设备读取严格 JSON、可信配方与真实游戏时间；构造产物原型，核对 ItemRegistry 与最大堆叠。
2. 保存完整背包引用、数量、品质、最大堆叠、产物兼容、modData 和容量快照。
3. 规则生成计划，准备变化槽位的克隆与新 JSON；未参与槽位保留原引用，避免重建工具/附魔。
4. CommitCoordinator 防重入，写前核对背包、设备引用和原 JSON；冲突无写入。
5. Inventory.OverwriteWith 写完整库存，再写状态；写后核对期望写集。同步回调不抛异常也不能跳过核验（BUG-2026-003）。
6. 异常/不一致时分别恢复原物品引用与原 JSON，再次核验。恢复失败隔离会话，提示停止操作且不保存；返回标题才重置。日志失败不能阻断恢复。

这是有补偿的单人同步事务，不是底层数据库原子操作，不保证第三方回调任意副作用可逆。暂不支持 Automate/箱子输入，不直接修改远端背包。

## 主机请求核心与只读桥

Networking 已实现 `P1-NET-01`：有界严格请求、可信 sender/库存 owner/主机/地图范围准入、会话意图绑定和历史成功回执。Accepted/Authorized/Started 均不等于 Applied。完整意图的重试保持原 revision；同请求 ID 改字段拒绝；成功重发返回原回执而不是再次提交或回退机器。未写入的规则拒绝可释放尝试，协调器恢复失败或矛盾成功数据隔离；容量满不淘汰成功记录。

NET-01 Gate/Ledger不注册消息，NET-02 ReadOnlyProtocolService只读握手/拒绝不接可写事务链。历史85核心/77Python与Release/profiles见 [配对验证](validation/P1-network-pair-2026-10-02.md)，105Python含28身份人工专项见 [工具验证](validation/P1-network-fixture-2026-10-02.md)。随后真实绑定已完成、两个主机Scope/门禁失败实测已留档，双端及SDK生命周期仍未验收，见 [失败验证](validation/P1-network-failure-2026-10-04.md)。MachineService GetApi保持原类型，主线程ReadNetworkDiagnostics/TrySendNetworkProbe仅测试委托，无Execute调用；fixed synthetic与八回候选不证明真实设备/额度。FromPlayerID/FromModID只是路由元数据；可信身份/远端库存可写权/确认恢复留NET03/04。绑定工具仅改四禁用配置Farm/ID，不启动世界/探针或打开加工；边界见主机契约与 [准备说明](NET02_TEST_SETUP.md)，旧45/68保持历史。

## 持久化、时钟和移除

设备 modData 的 `Sora6A.XiangyeShiling/MachineState` 保存 schema 1 单值 JSON。缺字段/重复字段/未知字段或版本/非法数值拒绝读取，保留原字符串并阻止操作与移动。已有损坏值不能通过 Initialize 清空。缺少状态的设备在读档、进入场景、对象加入时初始化；正常新放置设备取得新实例 ID。

绝对分钟 = `Game1.Date.TotalDays * 1440 + HH * 60 + MM`。夜间 24xx/25xx 对应日尾之后的分钟；次晨自然继续，读档靠固定 ReadyAt 推导 Processing/Ready。2026-09-28 已用真实 SaveGame 写盘/标题重载、原生春夏换季与整进程冷启动验证，见 [保存实测](validation/P1-persistence-2026-09-28.md)；纯 JSON round-trip 本身仍不能代替这些验收。

Harmony 按两个独立设备 ID 拦截 checkForAction（菜单）、performObjectDropInAction（禁止原版投料）、placementAction（实例初始化）、performToolAction/onExplosion、GameLocation.checkDestroyItem（NPC 清路）、removeObject/destroyObject。忙碌或损坏状态阻止这些移除路径；空闲走原版。任意模组直接操作位置字典仍可绕开补丁，不能宣称全模组保护。

放置边界补充（BUG-2026-009）：prefix 对两个设备 ID 检查目标格为空，并保留本次预检通过标志；postfix 只初始化实际新增的匹配 ID 对象。原生对相同 ID 的占用格可能返回成功但不插入，所以不能仅凭返回值覆盖目标状态。占用格拒绝必须保持原设备引用/JSON、携带物和库存不变；无 schema 迁移。组件回归入口为 PlacementScenarios，完整红绿证据见 [缺陷记录](bugfix/BUG-2026-009-occupied-placement.md)。

## 内容与正常来源

唯一登记源为 data/catalog.json；tools/prototype.py 生成 CP 内容与运行时配方，不手写第二份数值。只激活黄豆、香椿、豆腐、酱油、香椿拌豆腐和两设备共 7 物品、5 配方，其余 P2 项保持 planned。

- 皮埃尔：黄豆 60g × 10、小麦 25g × 10；格斯：香椿 100g × 5、菜谱 1000g；罗宾：两张设备图纸各 1500g。
- 豆腐：黄豆 3 → 豆腐 2，120 分钟；酱油：黄豆 3＋小麦 2 → 酱油 4，240 分钟。
- 原版厨房：香椿 1＋豆腐 1 → 香椿拌豆腐 1。两设备使用原版制作界面。
- 限量商店原料为 P1 原型来源，不代表 P2 作物/春季采集完成。价格与图像为原型，临时引用原版素材而不分发。

清单 Price 定义为原型最终购买价，生成库存条目设 `IgnoreShopPriceModifiers=true`，避免 SeedShop 全局倍率再次放大（[BUG-2026-006](bugfix/BUG-2026-006-shop-price-multiplier.md)）。仅约束本模组条目，不改变原版全局规则；第三方覆写价格未验证。

原版配方购买通过产物内部 Name 学习配方；两设备与香椿拌豆腐使用 `_Recipe_...` 作为内部 Name，与 Data/CraftingRecipes、Data/CookingRecipes 的键一致，显示名走 i18n、ItemId 不变。清单测试阻止漂移。

## 测试与下一步

2026-10-01 较早组件轮：26 核心、26 Python、16 Memory 实际游戏回归通过，见 [放置与拆回记录](validation/P1-world-2026-10-01.md)。新增两设备四状态占用格拒绝与两项完整 Pickaxe→单 Debris→updateChunks 自动拾取→Utility 原生背包放置→新实例/旧请求拒绝/新请求开工。BUG-009 先红后绿；BUG-010 工具夹具改为原生可放置场地后同一农场全量通过。没有直接 collect、手动加背包或删家具绕过失败；源/UI 存档不变，夹具已禁用。BUG-005 真实旧模块负例及匹配加载通过，详细范围见缺陷记录。下列 09-28/09-30 证据为前轮结果，本日未重跑其保存/UI矩阵。

2026-09-30 执行 `dotnet run --project tests/MachineCore/MachineCore.csproj -c Release`：26 个场景通过，包含 16 个规则、8 个提交协调器和 2 个菜单会话场景；同日 26 项 Python 回归通过。独立 [GameHarness](../tests/GameHarness/README.md) 默认禁用，不进入开发包；Memory 恢复内存且不保存，Persistence 保存显式指定的独立副本，Interaction 不保存也不恢复本次内存。故障注入的预期异常单列记录。

2026-09-30 的 Memory 12 项实机集成通过，Failure=null、Restored=true。新增实际 MachineMenu 领取同坐标双击不会扣下一批原料、满包领取失败后解除满包并在同菜单沿原请求重试，以及原生 ShopMenu 料理售出回归。售出回归先在 Saloon 复现拒收料理，修正 SeedShop 后验证接受规则、两份整批精确付款、再次点击空槽不重复付款；两个 Error 日志为预期库存回调故障注入，不能据此称正常错误为零，也不能把它们当作失败结果。

2026-09-28 的 Persistence 6 项实机集成已通过：原生购买/学习与总扣款、配方制作/放置身份、两条加工烹饪路线、Processing/Ready/领取后的真实保存重载、原生春夏换季、已领取存档防重复产出。另一次全新游戏进程重跑最终阶段，验证恢复不依赖旧会话内存；不会重新执行前五项。

[Interaction 界面验证](validation/P1-ui-2026-09-30.md) 于 2026-09-30 完整重跑 13 步通过：前三家商店购买/学习、设备制作、豆坊两批开工/领取、发酵缸开工/领取、CookingPage 烹饪三份香椿拌豆腐、食用一份恢复 175 能量/78 生命，以及在 SeedShop/皮埃尔售出两份。菜谱仍从格斯购买。六次机器动作按钮均用双击输入，没有误扣下一批材料。报告 Completed=true、Step=13、Failure=null、Saved=false；资金从 15110g 加两份料理各 240g 后为 15590g，最终背包为镐一把、豆腐一份、酱油四份，料理与购买原料均已消耗，最终 UI 日志无 ERROR/WARN。

首轮历史失败保留：前 12 步通过后，在 Saloon 点击料理没有售出，资金仍为 15110g、料理仍为两份，Advance 生成真实失败报告。根因是夹具误选不收购料理的商店，见 [BUG-2026-008](bugfix/BUG-2026-008-ui-sale-shop.md)；改为 SeedShop 后原生商店回归先红后绿，再完成上述完整 UI 重跑，生产物品数据未改。UI 正常日志与 Memory 的两个预期故障 Error 分别记录，不能混算。

Interaction 直接打开 ShopMenu/CraftingPage，并准备资金、制作材料、设备放置、玩家定位和时钟；交易、加工、烹饪、食用和售出由实际界面输入完成。它不覆盖 NPC 营业日程、步行开店、房屋厨房地图触发、手工放置、赠礼、工具击落拾取、手柄、英文或其他界面尺寸。测试游戏已退出，配置 Enabled=false、Scenario=Memory、OpenPreview=false；13 步通过不代表 P1 全部验收。

P1 剩余验收：正常 NPC 日程与步行开店、房屋厨房地图触发、手工放置、成品赠礼、工具拆回的实际输入与保存重载、英文/手柄/不同界面尺寸。只读桥仍待真实双端验证，随后才接可写主机链，验证远端背包所有权、断线确认与争用。P1 总体验收未完成，全部通过前不扩大 P2。
