# 开发与启动

11-06完整世界输入链20/20已在同一新进程通过并退出：原生制作/放置、两配方事务、工具保护、实际WASD自然拾取/重放及损坏raw保护，八存档未变/六配置恢复禁用。原报告与只读诊断边界见[完整实测](validation/P1-world-ui-2026-11-06.md)，当前停止基线及下一保存任务见[HANDOFF](HANDOFF.md)。本次接续源码未变，不重跑实现阶段115核心/SDK构建；生产包未变，P1未总验收。

11-06此前三店阶段：三家真实地图柜台/购买通过，只增加默认禁用WorldShopInteraction及文档；SDK构建0警告0错误，实际原料入包/图纸菜谱学习/精确付款及停止审计已核，生产包未改。复跑与内存准备范围见[夹具说明](../tests/GameHarness/README.md)、[三店记录](validation/P1-world-shops-2026-11-06.md)。该阶段之后的完整手工两设备链已见上条，不重复未变化的来源子集。

11-05厨房接续已完成真实地图灶台→原版烹饪两份→同一成品赠礼的实机子集。仅本地KitchenGiftInteraction及BUG023来源判据变更，115核心/SDK构建/停止审计通过，不重建未改的生产包；模式复跑与边界见[夹具说明](../tests/GameHarness/README.md)和[厨房记录](validation/P1-kitchen-gift-2026-11-05.md)。

11-05完成单人成品真实赠礼两阶段：Robin收一份且好感+45，当天重复被拒绝且物品/计数保持。只增加默认禁用的GiftInteraction本地验证及文档，生产包未改；复跑/停止/保存边界见[夹具说明](../tests/GameHarness/README.md)和[实测](validation/P1-gift-2026-11-05.md)。普通地图商店/厨房入口及完整P1仍待。

11-03当前新增单人获取/制作指南和缺料反馈；玩家入口见[PLAYER_GUIDE](PLAYER_GUIDE.md)，实现/验收矩阵见[P1_SINGLEPLAYER_STATUS](P1_SINGLEPLAYER_STATUS.md)。目录16、生产/夹具构建及实际SDK18通过，中文UI已查看，最终路线纯文案包核对通过；本轮没有重复无关112核心/116Python或联机矩阵，证据/源码入口/回退见[本轮验证](validation/P1-singleplayer-guide-2026-11-02.md)。

当前交付为 **P1 单人机器原型 / 0.1.0-alpha.2 开发包**。设计基线仍为 0.4，两者含义不同。
已接入7个物品、5个配方、有限商店、共享菜单与单人事务，以及只读联机握手/拒绝和默认禁用的诊断。种植、采集、远端库存/可写提交未实现；planned不注入，真实双端未验收。

接手前先读 [当前交接](HANDOFF.md)、[开发维护规范](../CONTRIBUTING.md) 和 [架构入口](ARCHITECTURE.md)。本页负责工具链与运行命令；分支/提交/bugfix/中断记录的要求统一放在贡献规范，避免多处版本漂移。

## 目录与职责

| 路径 | 职责 |
|---|---|
| `data/catalog.json` | 冻结范围的唯一登记源；40 个物品、30 个配方，含来源、去向所需关系、图像位置与文本 |
| `content/XiangyeShiling.Content` | Content Patcher 内容包；物品、作物、商店、菜谱、图像和本地化的归属 |
| `src/XiangyeShiling` | SMAPI 模块；P0 只检查加载；P1 承载两种机器的共享菜单与单人真实背包事务 |
| `src/XiangyeShiling/Machines` | 纯事务规则与状态格式；游戏适配位于 GameIntegration，联机禁用 |
| `tools/catalog.py` | 与 prototype.py 生成登记表、CP 数据/中英文文本、运行时机器配方，检查范围、引用、来源闭环及文件一致性 |
| `tools/check_workflow.py` | 校验维护文档、相对链接、完整 commit 正文以及 fix 提交对应的 bugfix 更新 |
| `tools/build.py` | 本地编译、生成开发 ZIP；只写工作区，不自动部署 |
| `tools/check-environment.ps1` | 只读检查实际游戏、SMAPI、Content Patcher 版本 |
| `tools/prepare-test-runtime.py` | 用已有依赖准备工作区内的独立游戏/Mods 副本 |
| `tools/prepare_save_copy.py` | 从既有存档生成带独立农场名称与 ID 的本地测试副本 |
| `tools/prepare_multiplayer.py` | 来源与两端全预检后生成文件哈希一致的Mods/启动脚本；显式--output/可选整理后的harness、保留目标config，schema2审计；不是I/O事务、不启动游戏 |
| `tools/configure_network_fixture.py` | 从真实专用存档读取农场名/世界 ID，重核两端包后默认只读 plan；显式 apply 仅绑定四份禁用配置，备份/候选/哈希留档；不创建农场或启用诊断 |
| `tools/NetworkLifecycleCheck` | 无SDK依赖的只读schema1重连报告CLI；链接真实共享判据，严格完整字段/2MiB上限，输出配对摘要至stdout，不修改输入 |
| `tools/NetworkWelcomeReplayCheck` | 无SDK依赖的独立旧Welcome重放报告CLI；严格校验冻结生命周期及重放前后，输出精确子集/原双SHA，拒绝时stdout空 |
| `tests` / `.github/workflows` | 无游戏依赖的 Python 回归、C# 事务场景与 CI |
| `.local` / `artifacts` | 忽略提交的引用、缓存及构建产物 |

## 工具链

- 目标验证基线：Windows x64、游戏 1.6.15、SMAPI 4.4.0、Content Patcher 2.9.0。
- C# 项目目标 `net6.0` / x64，SDK 本次实际使用 7.0.302；`global.json` 允许较新 SDK。须有 .NET 6 targeting pack。
- Python 3.10+，只用标准库。
- 本工程不使用 NuGet 依赖；直接引用开发者本机游戏和 SMAPI 程序集，`Private=false` 防止分发它们。`NuGet.Config` 清空源，避免离线构建访问包索引。未来引入依赖时应显式更新该配置。
- 实测与未测项目见 [环境记录](ENVIRONMENT.md)；版本最低要求不等于对所有更高版本的兼容承诺。

## 无游戏检查

2026-11-02：112核心（101＋11）通过，新增旧Welcome缓存/接收诊断/共享配对/严格reader场景；生产包、SDK夹具及两个CLI 0警告/0错误。真实CLI人工pair0/四项拒绝1已核，均非SDK证据；停止部署通过，实际重放仍待，见[本轮记录](validation/P1-welcome-replay-2026-11-02.md)。本轮未改公共Python，不重复历史116项。

2026-10-12当前核心101项通过：原89＋8同ID生命周期共享判据＋4严格读取器场景；SDK夹具及独立CLI编译通过，BUG021实际无效输入CLI先红后绿。真实同进程重连原/最终双报告CLI0及关闭恢复审计已完成；Full仍false，人工报告回归不代实机，入口/限制见[生命周期记录](validation/P1-network-lifecycle-2026-10-12.md)。Python116仍为此前准备轮结果，本轮没有公共Python改动；下方旧计数均保留原日期。

在仓库根目录执行：

```powershell
python tools/check_workflow.py
python tools/catalog.py
python -m unittest discover -s tests -v
dotnet run --project tests/MachineCore/MachineCore.csproj -c Release
dotnet build tools/NetworkLifecycleCheck/NetworkLifecycleCheck.csproj -c Release
dotnet build tools/NetworkWelcomeReplayCheck/NetworkWelcomeReplayCheck.csproj -c Release
```

修改清单后执行 `python tools/catalog.py --write`，提交源 JSON、生成的 CP content.json/i18n、src/XiangyeShiling/assets/machine-recipes.json 和 `docs/CONTENT_CATALOG.md`。检查器会拒绝未同步生成文件、重复 JSON 键、超出冻结数量、未知原料及不可获得的生产闭环。
`null` 表示尚未确定的平衡值；带有待定数量或工时的配方不得标为已实现。

C#场景直接编译规则/提交/菜单/世界判据、Networking及GameHarness实际共享网络证据源码，不引用游戏。2026-10-02前轮85项通过（此前81＋4实际quota观测），Python77项通过（此前37＋40人工报告配对）；见 [前轮配对验证](validation/P1-network-pair-2026-10-02.md)。身份绑定工具的专项与本机审计见 [本轮验证](validation/P1-network-fixture-2026-10-02.md)。纯会话接收观测与委托读取测试不等于真实SMAPI事件/双端网络或库存验证。旧45/68和游戏组件/UI结果保留，不作为新DLL实机通过。

游戏适配入口另用 [GameAdapter CLI 回归](../tests/GameAdapter/README.md)：该工程引用实际生产项目和本机 SDK，所有引用 `Private=false`，不是无游戏依赖测试，也不加入普通 CI。它在独立 .NET 6 进程注册实际 NPC 移除 prefix，验证原生 `checkDestroyItem(null, tile, false)`；无需打开存档或游戏窗口。涉及空目标的实际输入契约见 [BUG-2026-019](bugfix/BUG-2026-019-null-removal-target.md)。该入口检查不能代替完整 NPC 更新、角色移动或联网验收。

2026-10-11新增InputObservation的四项纯安全策略场景，共89项MachineCore通过；检查真实共享的世界/所有权/文本隐私/采样容量判据，不加载SDK，不代表实际按键已恢复。SDK夹具构建和部署审计另见 [输入观测记录](validation/P1-input-observation-2026-10-11.md)，配置、六层输入定义、停止与恢复操作见 [GameHarness README](../tests/GameHarness/README.md)。该模式不调用机器API或保存，不冻结原生时钟；真实采样后必须及时关闭游戏并按本轮基线核磁盘。

## 本地构建

客户端准备保存另用只读`tools/check_network_preparation.py`，实际命令与字段契约见 [准备记录](validation/P1-network-preparation-2026-10-11.md)。它核两份原生XML及直接farmhands名单，不启动游戏、不写存档；应在两端退出后运行，纯XML成功不等于真实连接/输入/保存顺序。专项11项及全Python116项通过（`python -m unittest discover -s tests -p test_network_preparation.py -v` / 默认全discover），空占位边界真实1红1绿见 [BUG020](bugfix/BUG-2026-020-preparation-empty-placeholder.md)。该工具复用身份工具内部解析器，改其读数/拒绝契约时同时执行这两套测试。

在已安装正确版本 SMAPI 的游戏上：

```powershell
python tools/build.py --game-path "D:\SteamLibrary\steamapps\common\Stardew Valley"
```

也可单独编译：

```powershell
dotnet build src/XiangyeShiling/XiangyeShiling.csproj -c Release "-p:GamePath=D:\SteamLibrary\steamapps\common\Stardew Valley"
```

或者在忽略提交的根目录 `local.props` 中设置：

```xml
<Project>
  <PropertyGroup>
    <GamePath>D:\SteamLibrary\steamapps\common\Stardew Valley</GamePath>
  </PropertyGroup>
</Project>
```

构建脚本输出 `artifacts/XiangyeShiling-0.1.0-alpha.2-dev.zip`。ZIP 仅包含代码模块、本内容包与开发说明，不包含游戏、SMAPI、Content Patcher 或游戏图片。

需要在保留旧安装的情况下检查编译，可从[官方 SMAPI 4.4.0 安装包](https://github.com/Pathoschild/SMAPI/releases/tag/4.4.0)的 `internal/windows/install.dat`（ZIP）提取完整 Windows payload 到 `.local/smapi-reference`，必须保留 smapi-internal/0Harmony.dll 与 SMAPI.Toolkit.CoreInterfaces.dll，然后执行：

```powershell
python tools/build.py --game-path "D:\SteamLibrary\steamapps\common\Stardew Valley" --smapi-path "D:\code\stardew\.local\smapi-reference"
```

这仅替换编译引用，不升级实际游戏安装，也不能据此运行旧 SMAPI。第一次使用 .NET 可能需要初始化用户级缓存。

## 独立测试环境

关闭测试游戏后，用已有官方依赖和开发包准备工作区副本：

```powershell
python tools/prepare-test-runtime.py --game-path "D:\SteamLibrary\steamapps\common\Stardew Valley" --smapi-installer ".local/SMAPI-4.4.0-installer.zip" --content-patcher ".local/test-mods/ContentPatcher" --bundle "artifacts/XiangyeShiling-0.1.0-alpha.2-dev.zip"
./tools/check-environment.ps1 -GamePath "D:\code\stardew\.local\validation-game" -ModsPath "D:\code\stardew\.local\test-mods"
& ".local/validation-game/StardewModdingAPI.exe" --mods-path "D:\code\stardew\.local\test-mods"
```

脚本复制游戏及 Content，再解包官方 Windows SMAPI payload。游戏和 Mods 位于工作区，**存档、偏好和日志仍使用正常用户目录**，应只操作测试农场。源安装不会被更新，游戏文件也不进入开发 ZIP 或版本控制。

旧档副本先生成在 `.local/save-fixtures`，命令中的源路径须指向一个具体存档文件夹：

```powershell
python tools/prepare_save_copy.py --source "$env:APPDATA\StardewValley\Saves\原农场_编号"
```

副本会改变玩家名、农场名和 `uniqueIDForThisGame`，并生成同名文件夹与主存档。验证 XML 后才写入；不复制旧备份文件，不改变源文件。导入前再次确认它有独立名称/ID，不能直接覆盖原农场。已生成副本不等于已完成游戏迁移验证。

本次测试 CP 为作者 `content-patcher/2.9.0` 标签的本地构建，提交 `807ca18be9291f301119a3fa01f49b6a59ac625f`。Roslyn 4.8 编译成功；SMAPI 构建分析器要求 4.9，产生 CS9057 并未运行。正式发布前仍需用官方发行二进制复测。

当前NET02双人文件准备（停止目标实例、先核验当前source和整理后的夹具）：

```powershell
python tools/prepare_multiplayer.py --source-mods .local/validation-game/Mods --runtime .local/validation-game --output .local/net02-profiles --harness-dir .local/net02-probe-harness
./tools/check-environment.ps1 -GamePath ".local/validation-game" -ModsPath ".local/net02-profiles/host/Mods"
./tools/check-environment.ps1 -GamePath ".local/validation-game" -ModsPath ".local/net02-profiles/client/Mods"
```

新目录已用当前构建实际准备，旧 `.local/multiplayer` 不变。默认source/output仍为旧test-mods/multiplayer，不能省略参数当作当前配置。可选harness只接整理好的manifest/EntryDll/同名deps/runtimeconfig，不接完整bin/PDB/第三方DLL。来源config不复制、目标已知config保留原字节，现有Enabled=true不会被自动禁用；意外文件预检拒绝，不删除。脚本不启动或写存档，完整I/O失败仍可能部分更新，schema2旧记录不能证明失败后的当前一致。两端共享用户存档/日志，仅Mods隔离。专用合作世界、四配置与首次客户端创建步骤见 [NET02准备](NET02_TEST_SETUP.md)；真实450604963已禁用绑定，当前准备阶段及启动后配置变化见 [合作准备](validation/P1-network-live-2026-10-02.md)，不以实例启动代表连接或探针通过。

## NET02 专用世界身份绑定

`tools/configure_network_fixture.py` 为已创建的专用合作存档绑定身份，不生成或安装存档、不启动游戏。当前两份真实XML已核XYSNET02/450604963，实际plan/export/apply成功且仍禁用，见 [合作准备](validation/P1-network-live-2026-10-02.md)。存档目录可用玩家名，不能仅凭目录前缀推断农场。85核心/77Python是历史配对检查，28人工身份专项/全105Python见 [工具验证](validation/P1-network-fixture-2026-10-02.md)，都不能代替实际客户端角色、小屋、探针与生命周期验收。以下命令是通用复跑说明，现有证据目录不得覆盖。

先停止全部使用目标配置的游戏/SMAPI 进程，并保持两端主模块 `EnableReadOnlyNetworkProbes=false`、夹具 `Enabled=false/OpenPreview=false/Scenario=Network`、NetworkRole 分别 Host/Client。下面从仓库根执行，输入的是实际存档文件夹，不是单个 XML 或文件夹标签代替农场名：

```powershell
Get-Process StardewModdingAPI, 'Stardew Valley' -ErrorAction SilentlyContinue
$net02Save = Read-Host '输入已创建的 XYSNET02 专用农场存档文件夹完整路径'
python tools/configure_network_fixture.py --save "$net02Save" --profiles .local/net02-profiles --farm-name XYSNET02
```

默认只打印 plan 概要，不写文件。导出可审查计划与原配置备份时使用新目录，仍不改目标：

```powershell
python tools/configure_network_fixture.py --save "$net02Save" --profiles .local/net02-profiles --farm-name XYSNET02 --output .local/net02-fixture-plan-run01
```

核对 fixture-plan.json、before 原字节及 prepared 候选后，显式 apply 必须另用从未存在的目录：

```powershell
python tools/configure_network_fixture.py --save "$net02Save" --profiles .local/net02-profiles --farm-name XYSNET02 --apply --output .local/net02-fixture-binding-run01
```

run01 是首次使用例；已存在目录换新名称，不能覆盖或复用 export 目录。`--profiles/--output` 限工作区 `.local` 严格后代，输出不得与源存档/profiles/记录的 runtime 交叠；save 与 profiles 也分离。每次重新读输入，旧导出计划不是 apply 的执行来源。名称必须是真实 XYSNET 前缀、6–20 UTF-16 单元的农场名；另选名称时同步三个命令的 `--farm-name`。

工具检查同名主存档和 SaveGameInfo 的直接身份元素，两份农场名一致、主存档世界 ID 非零且不是已登记旧 ID。拒绝 DTD/entity、损坏/重复身份 XML；非零不证明独立创建。两端全部包哈希、manifest/声明版本与 profiles schema2 重核；报告等未知包文件先归档，不自动清理。配置须有效、无重复/大小写别名键或非有限值，且开关显式禁用、模式与角色正确；不把 Enabled=true 自动改回 false。

apply 只更新四份配置的 Farm/ID 字段，保留未知设置值；JSON 会重新序列化，原字节留在 before。源存档、包、profiles.json、runtime 两份配置和旧 profiles 不写。profiles 的旧配置哈希仍属准备时点，本次 beforeSha256/afterSha256 与 fixture-apply.json 记录新绑定。备份/候选/plan 写完才逐个 atomic replace；四份整体不是事务，失败可能部分更新，配置被外部改动时没有强并发保护。

失败保持进程停止，先查 `fixture-apply.json` 的 configurationApplied/replacedConfigs/failure，再逐份比较当前与 before/prepared 哈希；审计 I/O 失败可能连审计文件都没有，不能由失败退出推断目标未改。第三种哈希需先辨认外部编辑，不能盲目覆盖或回放；恢复四份一致状态、重新预检后再用新目录重试。详细目录与接续步骤见 [NET02准备](NET02_TEST_SETUP.md)。

成功绑定仍保持诊断禁用，cabinAvailabilityVerified、independentWorldCreationVerified、runtimeBinaryVersionVerified、networkStarted、fullNet02Accepted 全为 false。小屋、独立创建、运行版本及真实双端另核；完成准备后才另行显式启用双门禁，工具本身不启用。个人存档、完整本机日志与输出证据只留忽略目录，不提交。

已实际完成当前包 14 文件/四禁用角色配置只读预检，以及旧 UI 受保护存档的 apply 预期拒绝（退出 1、没有输出目录）。独立前后审计确认六配置/六存档、旧及当前 profiles、ZIP 和两个 build 不变，见 `.local/p1-network-fixture-local-audit-2026-10-02.json`。这是保护负例与文件审计，不是 bug 修前红、真实专用身份绑定或网络通过。

## P0 加载检查步骤

1. 使用独立测试用游戏/Mods 配置，安装目标 SMAPI 和 Content Patcher；新档与旧档副本分别验证。先备份存档，勿用唯一的正式存档测试。
2. 运行版本检查；有 Issues 时先解决依赖：

   ```powershell
   ./tools/check-environment.ps1 -GamePath "D:\SteamLibrary\steamapps\common\Stardew Valley"
   ```

3. 解压开发包，将 `XiangyeShiling`、`XiangyeShiling.Content` 两个文件夹放入测试配置的 `Mods`。启动 `StardewModdingAPI.exe`。
4. 确认日志出现 P1 single-player prototype 提示。进入新档或读取存档后检查 `P0 check passed` / `P0 检查通过`；这只证明黄豆数据与图标资源存在。代码同时处理新游戏的 `DayStarted` 与旧档的 `SaveLoaded`，每次进入存档只检查一次。
5. 开发时可在 SMAPI 控制台使用 `patch summary` 查看本包状态；用开发物品工具检查 `Sora6A.XiangyeShiling_Soybean` 的名称、说明、背包和地面显示。原版绿豆图标仅是临时占位；不会复制原版图像到仓库。
6. 中英文各检查一次。记录游戏/依赖版本和日志位置；退出后在旧档副本重复。P0 没有正常获取流程，不能以此勾选 P1 验收。
7. 双人测试准备两端同版本的 SMAPI、CP、本代码与内容包，使用测试农场；主机权威与竞争操作由 P1 实现后再测。

## P1 单人测试与接续契约

本轮原型来源为皮埃尔黄豆/小麦、格斯香椿/菜谱、罗宾两设备图纸，具体价格与数量见 P1_TRANSACTIONS。采集和种植保留到 P2。先在专用农场备份后运行 [GameHarness](../tests/GameHarness/README.md)，再做购买、厨房、存读档的手工验证。独立夹具不进入开发 ZIP。

夹具有五种模式，默认Enabled=false。Memory恢复不保存；Persistence保存显式独立ID；Interaction/WorldInteraction不保存不恢复；新增Network不自动加载/创建/保存/备料，仅只读观察及固定探针，另需主模块开关、专用非零世界ID和Host/Client，详见 [夹具](../tests/GameHarness/README.md)。各模式结果不可混用。历史09-30 Interaction13步、Memory12项，后续Memory16项是旧构建实机证据；本轮Network仅纯测试/构建与文件准备，没有实机通过。

09-30 Interaction 那轮游戏已退出并禁用配置；当前是否有新测试运行以 [交接](HANDOFF.md) 和实际进程/配置为准。Interaction 是准备式 UI 测试：直接打开 ShopMenu/CraftingPage，夹具准备资金/材料、放置设备、定位玩家和推进时钟，操作者执行界面交易。它的通过不证明 NPC 日程、步行店铺入口、房屋厨房地图触发、手工放置、赠礼、工具击落拾取、手柄、英文及其他尺寸已验收。

- 代码只服务豆坊与发酵缸；两者复用菜单、输入检查与事务。常规内容交给 CP，以独立 ID 增量追加，不覆盖整表。
- 状态为 `Idle → Processing → Ready → Idle`；当前仅接受单人主玩家的真实背包操作，主机请求纯模块已实现，但联网确认与远端库存尚未接入。工作中与待领取时拒绝换配方和搬动。
- 每次打开菜单最多成功执行一个操作，Applied 后禁用按钮，后续操作须关闭并重开；失败重试保留原 request ID、设备身份和 revision，不自动接受外部更新的设备状态（[BUG-2026-007](bugfix/BUG-2026-007-menu-double-click.md)）。
- 提交前检查全部背包输入；固定配方、扣料明细、产出、开始/结束时间与唯一批次标识。重复请求不能二次扣料。
- 规则核心已按背包槽位顺序消耗同 ID 各品质堆叠，产物品质由固定配方决定；单人游戏背包适配已接入，联机仍禁用。
- 领取先确认整批可放入，成功后才清机器；失败保持 Ready。存档、跨日、断线和双方竞争测试通过前不扩充 P2 内容。
- 状态格式与规则已实现，详见 [P1 事务核心](P1_TRANSACTIONS.md)；modData、移除保护与单人磁盘/换季/冷启动重载已验证，见 [保存实测](validation/P1-persistence-2026-09-28.md)。准备式实际界面 13 步已通过，联机写入适配尚未实现；尚未覆盖的正常入口、输入与联网边界仍须完成，P1 总体验收未完成，P2 不开始。

最新实机补充见 [10-01 放置与拆回验证](validation/P1-world-2026-10-01.md)：Memory 16 项通过，新增两设备四状态占用格与真实 Pickaxe/Debris/Utility 链；末次报告含 ToolFixtures 的选址/原生预检证据。场地先用原生碰撞规则验证，不能把 objects 空格或 placementAction 的 true 当作可放置证明。退出后核对 Enabled=false、存档不变及 ZIP 与部署一致。BUG-005 混装启动用独立 --mods-path，无夹具且不加载农场，不复制其修复前 alpha.2 正例 DLL 作为最终包。

实际世界输入另见 [WorldInteraction 实测与未完成项](validation/P1-world-ui-2026-10-01.md)。该模式固定使用 `XYSP1UI_8624092802` 单人主玩家副本，准备材料、图纸、可达地板及阶段时钟，操作者通过实际鼠标制作、手持放置、菜单加工/领取、挥镐，再用方向键走近真实掉落。夹具不主动调用工具、设备操作或拾取 API；只读 Harmony observer 记录游戏自己完成的目标命中，方向键事件、位置轨迹与原 Debris/物品引用共同证明自然拾取。远离掉落的单次观察定位属于准备动作，必须在报告披露；它不证明没有夹具准备的完整自由游玩流程。

2026-10-01 最后同一进程通过豆坊前 7/20 项，随后在自然拾取阶段中断：末次报告 Step=7、Completed=false、Failure=null、StageMovementInputs=0、MovementTrail=[]、DropCount=1，未观察到自然拾取。工具证明 [BUG-2026-011](bugfix/BUG-2026-011-world-tool-proof.md) 的共享纯判据回归先红后绿，豆坊三个原生工具命中已观察；墙格选址 [BUG-2026-012](bugfix/BUG-2026-012-world-observation-wall.md) 的原生探针已在旧实现失败、修正后通过，观察点位于连通地板。完整 20 项、该输入链保存重载及联机仍未通过。

该历史世界输入阶段结束时，两次进程查询确认 Stardew/SMAPI 均为零，退出动作未经观察，不能记为操作者完成拾取或按计划退出。已保留 `.local/p1-world-ui-interrupted-2026-10-01.json` 与 `.local/p1-world-ui-interrupted-smapi-2026-10-01.txt`；当时配置已恢复 Enabled=false、OpenPreview=false、Scenario=Memory，保留固定测试农场/ID。`python .local/audit-p1-world-ui-interruption-2026-10-01.py` 已实际通过，证据 `.local/p1-world-ui-interruption-audit-2026-10-01.json` 核对源/UI 共 4 个存档文件哈希未变、夹具禁用和中断时既有生产 build/部署/10 文件 ZIP 一致；这是未完成运行的历史审计，不是当前NET02进程状态。此前运行中 checkpoint 审计保留，不能把任一审计写成完整 20 项通过。

WorldInteraction 不保存也不恢复内存，进程消失后 Step7 现场不能接续。后续核对固定磁盘副本、配置与当前构建后，用新鲜进程从第一步完整重跑；先取得真实方向输入、位移及原生自然拾取，再核验重放，不能拼接旧进程的通过项。具体未完成步骤、配置和证据路径以 [交接](HANDOFF.md) 为准；45 项纯测试与历史 Memory 16 项也不能替代完整世界 UI 验收。

主机请求/只读桥契约见 [P1_HOST_PROTOCOL](P1_HOST_PROTOCOL.md)，历史85核心/77Python及两项构建/包审计见 [配对验证](validation/P1-network-pair-2026-10-02.md)，身份工具人工回归见 [工具验证](validation/P1-network-fixture-2026-10-02.md)，真实专用身份绑定见 [合作准备](validation/P1-network-live-2026-10-02.md)。普通已加载多人世界启用只读桥，分屏/单人不发包；额外探针/trace须显式配置。128邮箱/16每帧/32peer/每peer8条每1000ms，probe出站16次/1000ms；合法意图仍WritesDisabled。10-12 Basic实测通过，完整SDK生命周期/预算/满队列仍待，不能取消门禁。复跑顺序仍为禁用阶段首次Farmer/专用准备保存、退出、独立启用诊断和留双端证据，见 [准备说明](NET02_TEST_SETUP.md)；当前已有春24客户端，无需重复创建。不复用源/UI/Stage3身份。

## 参考

- [SMAPI 4.4.0 工程：目标 .NET 6](https://github.com/Pathoschild/SMAPI/blob/4.4.0/src/SMAPI/SMAPI.csproj)
- [Content Patcher 作者指南](https://github.com/Pathoschild/StardewMods/blob/develop/ContentPatcher/docs/author-guide.md)
- [EditData 增量补丁与 i18n](https://github.com/Pathoschild/StardewMods/blob/develop/ContentPatcher/docs/author-guide/action-editdata.md)
- 物品字段与图像尺寸同时核对本机 1.6.15 的 `StardewValley.GameData.xml`，未把上游 develop 文档视为实机验收。

## NET02 报告离线检查

实际运行后先归档两端active的network-probe-result.json，再按NET02_TEST_SETUP使用 tools/check_network_reports.py --host/--client/--output .local/证据摘要.json。schema2需首次Ready基线、current指纹与实际quota/连接trace，旧schema1不验收；标题文件另存、不参与Basic通过。工具不会启动游戏、启用配置或写存档，输出只限工作区.local严格后代且不覆盖源报告。40专项是人工fixture；10-12另有真实活动双报告Basic配对通过、断连后主机Failure最终pair预期拒绝、标题局部清理及85项结束审计，见 [本轮记录](validation/P1-network-basic-2026-10-12.md)。原报告分时点归档，不能以失败后的title替代已配对active；Full矩阵仍待，源码缺陷历史见BUG-017。

2026-10-02的 [失败观测扩展](validation/P1-network-observation-2026-10-02.md) 只改GameHarness：Tick失败保留本Tick成功读取的Protocol，Failure仍拒绝配对，读前失败为null。10-04已用该构建实测两个主机Scope/门禁失败，临时启用与Observe误配在退出后恢复原字节，24项/八save/六config不变，见 [实测复跑步骤](validation/P1-network-failure-2026-10-04.md)；没有重建或运行真实双端。以后刷新夹具先留新基线、停实例，再用上面的prepare_multiplayer显式路径生成当前清单并核文件/配置哈希；不能复用旧表或盲重跑一次性脚本。故意失败负例仅按该验证的独立步骤安排。首次客户端准备仍保持诊断禁用，创建/专用保存并退出后才进入另一次探针运行，最新状态见HANDOFF。
