# 当前开发交接

## 2026-11-17 最终 1.0.0 包与发布准备

分支 `codex/first-stable-release`，基线 `c143790f01563675a7cc81906b6f1bfeb28f8e37`。旧档、秋 2 冷读和初版候选双人末页已通过。发行画面发现 [BUG-2026-031](bugfix/BUG-2026-031-processing-action-label.md)，先红后绿共 145 项。最终 `.local/p3-release-candidate-final/XiangyeShiling-1.0.0.zip` SHA-256 `C79B0B67C9D80A61FCC51D962C958720F25C18EAD96A323613F8F6690A3F048E`。生产 DLL `0CDA20A8…CF16`，同版候选仅改菜单标签和指南标题。最终包 11 张实际画面 PID 28572、新装创建与同档自然豆腐 PID 30752 已通过。见 [发行验收](validation/P3-release-2026-11-16.md)。

最终包独立双人末页 `.local/p1-net08-normal-lan/p3-late-page-final/audit.json` 主机 24444、农工 16880 通过。实际 220 分钟后收获豆干 2，两个回执与主机背包审计通过，111 个文件保留，游戏和端口清理完成。全部设计矩阵已核对。远程交付按 [贡献规范](../CONTRIBUTING.md) 走任务分支 PR 和 merge commit，验证当次 PR 与 main 的四项检查，再以同一哈希发布 [GitHub Release](https://github.com/Sora6A/stardew-xiangye-shiling/releases/tag/v1.0.0)。`.local/release-1.0.0/operator.ps1` 有各阶段操作和哈希校验，实际交付凭证保存在 `artifacts/1.0.0-delivery.txt` 与 `.local/release-1.0.0`，接续先核对 GitHub 实际状态，避免重复发布。

## 2026-11-16 P3 经济与发行候选

分支 `codex/first-stable-release`，P2 全量提交 `780eeda413023a7caaaa668d73e985114628a88a`。40 物品和 30 配方、P2 原生证据均已提交。P3 新增 [经济核算](P3_ECONOMY.md) 与 [安装说明](INSTALL.md)，保留首轮平衡数值，去掉 P1 小麦临时货架，原版小麦照常夏秋种。两份 manifest 与程序集版本设为 1.0.0。正式格式候选 `.local/p3-release-candidate-v1/XiangyeShiling-1.0.0.zip` SHA-256 `B7D212224E0F60CF9A2BA80787D7AA7A566D5D57C9849B82E61F38D58F380A65`，生产 DLL `5671EFD4…CC44`。ZIP 内容、CRC、稳定版本及隔离文件排除通过，生产与夹具编译零警告零错误。远程 main 只读核实 `e3b2d575d21094750592531c9ee7dc6d100acff8`，无 `v1.0.0` 标签。尚未推送、合并或发布。

其余工作区为 P3 专用夹具和入口。多人末页运行以全量开发包起始，在 XYSNET08 23020/12432 已原生完成加工请求，等待实际工时及领取。负责方清理后，以发行候选独立测试原生物品分类、售价、职业、用餐、赠礼与图像，再推进跨季冷读、旧档及真新档。P3 全量验收矩阵完成后才推送 PR 并发布。

发行打包提交 `9778db8b6baa3c6d32cd9056ad24f1e96539d875`，Python 125 项通过。开发候选末页 23020/12432 已原版 220 分钟自然领取成功，双端配置禁用且退出；`p3-late-page-v2` 审计。发行候选物品首轮 8232 通过物品/纹理门禁和三笔真实价格，因开始食用时进档黑屏未结束而失败。夹具改为等待 `IsPlayerFree` 与黑屏结束，待复验。跨季序列由独立运行器持有游戏，最初部署重复 manifest 已识别并更换。当前候选工作矩阵见 [发行候选验收](validation/P3-release-2026-11-16.md)。

发行物品复验 9256 通过，原版三笔售出 66/126/345g，麻婆豆腐 180 体力/81 生命，罗宾原版赠礼 +45 后同日拒绝，105 文件与原 Mods 一致，退出并禁用。报告 `.local/p2-native/p3-release-itemuse-v2`。原版跨季写入 29856 通过，一次夏末到秋 1 原版存档；证据 `.local/p3-season-persistence/write-arrival-v3/audit-rechecked.json`。冷读由同一执行器继续，先查进程和 `verify-arrival-v3` 结果。真新档 `P3FreshInstall` 已编译未启动，独立 `.local/p3-fresh-install/run.ps1`；正式候选双人末页已准备，待其他游戏退出后以 `.local/p1-net08-normal-lan/run-p3-late-page.ps1` 的 `p3-late-page-release-v1` 运行。候选 ZIP 字节仍为 B7D21222…0A65。当前剩余包含冷读、旧档、新档、发行包双端及菜单图像合成；本地提交或验收材料均未推送。

秋 1 冷读 20428 已原生领取五份并就寝保存到秋 2。再次冷读与旧档执行器负责收尾，短额度接近限制，接续先核实 `verifycollected-arrival-v3` 和 `legacy-arrival-v3` 的审计及进程，再复用已准备真新档和正式双端。当前最晚维护检查覆盖 42 条提交，正式 DLL 的 GameAdapter 26 项全部通过。

## 2026-11-16 P2 全量数据启用

分支 `codex/first-stable-release`，来源提交 `615d4ad396824aed9dd768ca1ba5b401c1c526fd`。登记 40 物品/30 配方全部启用。新增 8 条自建设备规则、原版熏鱼机两条加煤的原生配方、15 条其余厨房菜谱与原创食品/设备图集。Python 125、MachineCore 144、GameAdapter 26 与生产/夹具 Release 通过。验证脚本 `.local/p2-native/run.ps1` 准备独立 Mods 与冻结哈希。工作区包含全量生成数据、三类集成夹具、文档与图像。

冻结 ZIP `A76436B9…AB85`，生产 `D8D12D4A…9C2D2`。`.local/p2-native/recipes-v1` 验证原版设备菜单十条，95 文件一致。`.local/p2-native/smoker-v1` 22736 通过原版三机自然烟熏，`.local/p2-native/kitchen-id-v2` 31228 通过全部 16 条购买和烹饪。厨房首轮 `kitchen-v1` 12480 的蓝图带后缀显示名导致夹具断言失败，修订已核对显示名和内部 ID。采集 `.local/p2-native/forage-full-package-v2` 33588 通过三图四种野菜、原版大葱及三图夏季正向对照。最后两轮各 99 个文件一致，零保存零错误，所有测试进程退出且禁用。较早 `forage-two-springs` 的部署 manifest 已纠正，失败记录保留。见[来源记录](validation/P2-sources-2026-11-16.md)与[设备厨房记录](validation/P2-production-2026-11-16.md)。

额度接近短窗口限制时保存本轮，未推送或发布。下次从 P3 开始。先按 `docs/DEVELOPMENT_PLAN.md` 第 6、7 节建立经济和兼容性矩阵，豆坊当前基础机会利润 24–30g/游戏小时，检查缺季购买、煤炭与烟熏、油价和麻婆豆腐整链、品质及职业增益。用新候选补真实跨季、多人末页加工、全量存读档、赠礼/食用/售出、动静态美术与手柄界面。候选生产包仍标 alpha.2。全部发布验收通过后才造正式 ZIP，经任务分支 PR 合并并上传 GitHub Release。

## 2026-11-16 P2 来源与设备开发

分支 `codex/first-stable-release`，数据生成检查点 `0652e2d0ebd35bc4b35d0e070d4aa411ca5679e4`。现有未提交项为 P2 作物、商店和采集数据、图像及夹具、菜单分页及测试，另有独立熏鱼机生成器。运行时用 `.local/p2-native/run.ps1` 对冻结 ZIP、夹具和专用存档核哈希，启动前后检查游戏，核对全部存档和原 Mods 一致并禁用夹具。

来源冻结 ZIP `73D3553C…F53`，生产 `D8D12D4A…9C2D2`，夹具 `B7527B57…FC02A`。作物季节、两种跨季/秋种收获、原版购买和日库存，主机 21392 的 `.local/p2-native/crops-v1/audit.json` 通过，93 个存档文件一致，零保存、零错误、进程退出。采集首轮 33616 有六次实际拾取，但夏季正向对照撞上原版每周累计上限，审计保留，待使用原版周/季刷新复验。P2 分页 144 核心项与生产、夹具构建通过，实机待新配方。未推送或发布。

菜单已单独提交 `fba112918121fbc4ce26396770fd91f8014b363c`。采集第二轮 11204 已使用原版周/季刷新，三张地图的夏季对照成立并采得原版大葱，当前样本只见竹笋、香椿，后续扩展春季原版采样。来源提交包含独立作物/采集夹具、图像与生成数据，熏鱼机后续条目仍未启用。证据和适用范围见 [P2 来源实测](validation/P2-sources-2026-11-16.md)。

## 2026-11-16 P2 分批启用数据

分支 `codex/first-stable-release`，起点 `03b916c31c8405765d1209fba65430f398a78a35`。额度恢复，P1 公共机制证据已提交，无运行中的游戏。`data/catalog.json` 进入 P2，`active` 条目可按批启用，保持 P1 的 7 个物品、5 个配方和固定 ID。检查出处、活动依赖和数值，明确原版熏鱼机须追加原生规则。此检查点的 CP 数据、运行时配方和中英文文本与上一个提交逐字节相同。Python 122 项、目录生成和差异检查通过。

尚未启用 P2 新物品。接续接入两种原版作物数据、季外限量和格斯常规食材，再验证原版购种、播种、收成与补货。并行工作中的菜单分页源码和图片单独验证与提交，本提交未包含。原生游戏未启动，远程未推送或发布。

## 2026-11-16 普通直连重连通过

分支 `codex/first-stable-release`，起点 `6833c458b692add0103d70cb46ecfc897942f670`，额度已恢复。同一农工进程 27596 断线重连原主机 34200，通过原生未扣料预留恢复、现有回执及后续真实开工领取。配置禁用，81 文件和原 Mods 哈希一致，零保存零错误，两端已停。见 [实测](validation/P1-native-inventory-2026-11-11.md#普通直连同进程重连后的撤销与继续生产)。

重连已提交 `6ac4605ac4b45b2657cbc7d6b0842d13fd3102f5`，33 条提交信息通过。XYSNET09 搬动 Write 首轮 13464/19012，原版工具重放两台设备、农工看到同两台并退标题。主机超时在就寝之前，零保存，83 文件哈希一致，两端已退出。证据 `.local/p1-net09-move-persist/write-v1/audit.json`。待具体诊断离线与农舍进入条件，再重跑 Write 及冷 Verify。

单人普通建材与首次升级 `620261125` 已通过。29640 实际原版柜台交易、冶炼、罗宾升级和三次就寝，磁盘春 4/等级 1。实际用余料制作两设备并从新灶台打开烹饪，已停，87 个文件中只有专用副本变化。首轮 `620261123` 春 2 保留。见[实测](validation/P1-ordinary-play-2026-11-07.md#2026-11-16-原版建材和首次农舍升级)。

XYSNET09 第三轮 Write 25428/28948、冷 Verify 33256/32352 已通过。两设备原版搬动、农工实际复制、普通就寝写盘、独立磁盘核对和冷载待领取均完成。旧两轮阻断为原版可跳过农场剧情，失败审计保留。四个隔离进程退出，冷载 89 文件一致。见[实测](validation/P1-native-inventory-2026-11-11.md#普通直连搬动后写盘与冷载)。

普通菜单 Escape 24288/20916 和主机先开工争用 12484/7248 均通过。原生取消不扣料且同菜单可重试；反向争用实际只有主机扣料，农工收到本地化拒绝。两轮零保存零错误，89 文件一致，退出并禁用。证据 `.local/p1-net08-normal-lan/{menu-escape,host-first}/audit.json`。

首个 Hello 来源缺失故障注入 27520/24676 未通过完整审计。农工报告观测到 Ready 时缺少权限，随后双方获得同一原生能力标记，真实菜单开工、领取及双端库存一致。主机夹具验收反复抛出未确认预期重发错误，缺失主机故障报告，须检查主机记录与计时断言后再验。候选 `.local/p1-net08-normal-lan/candidate-ready-hello`，夹具 `46035778…2FB6`，证据 `.local/p1-net08-normal-lan/ready-hello-retry`。89 个存档文件与常用 Mods 保持一致，两进程退出且配置已禁用。BUG-2026-030 仍待完整原生验收，随后核对 P1 并接续 P2。仍未推送或发布。

菜单提交 `1aa6e391ff2b11855a4ab2038653b4866f7c0632`。故障复验主机 2560、农工 5828，双方确认相同挑战和原生能力标记，接着完成真实菜单开工与领取。证据 `.local/p1-net08-normal-lan/ready-hello-observer/audit.json`。生产包沿用上次，夹具 `5E15DEA3…9F02`，Release 零警告零错误。双进程退出且配置禁用，89 文件与常用 Mods 哈希一致，零保存零错误。首轮失败审计已归档。P1 冻结公共机制已验收，原版环回直连之外的兼容性及 Full 诊断仍有未验证项。见 [开发计划](DEVELOPMENT_PLAN.md)和[故障复验](validation/P1-native-inventory-2026-11-11.md#普通直连首个-hello-来源缺失后的恢复)。

本轮在短窗口接近 90% 时写入检查点，无运行中的游戏或构建，首个 Hello 夹具及验收文档随本次提交。分支仍仅在本机。接续时先核对额度、`git status -sb` 与分支头，再开始 P2。第一项让 `tools/catalog.py`、`tools/prototype.py` 从同一清单按阶段启用已登记的 40 物品和 30 配方，保持现有 ID 及已启用资产，同步生成中英文与内容校验。之后接入原版两种作物和种子、季外有限补货、酒馆猪肉和蚕豆及四种春采集，再接多配方菜单、原版熏鱼机和厨房。最终兼容性、平衡、正式安装包及 GitHub PR/发布仍待 §7 全量验收。

## 2026-11-15 恢复 P1 验收

分支 `codex/first-stable-release`，本次接续基线 `b7ccecc1c7bdbd0c1c2e063cef04b1c31118f9c2`。周额度已恢复。原先普通游戏进程 16668 本轮起始已不存在，确认无 Stardew/SMAPI 进程且直连端口空闲。后续隔离运行核对启动前后均无外部游戏。

普通直连 Ready 后仍无能力时的同挑战刷新已提交 `6833c458b692add0103d70cb46ecfc897942f670`。旧判据复现失败，修后 MachineCore 142 项、GameAdapter 26 项与生产 Release 通过，32 条提交信息通过。故障注入实机待验，见 [BUG-2026-030](bugfix/BUG-2026-030-direct-ip-ready-hello.md)。

同进程重连 XYSNET08 用原冻结 BUG-029 生产。首轮原版进程启动未及时出现；第二、三轮在旧连接先产生正常撤销日志，未形成目标断连窗口。第四轮隔离 24952/29100 用主线程事件次序隔开退标题，主机在农工离线后看到真实 Reserved、黄豆 6、无日志。同一农工在同一进程取得新连接和新挑战；两端观察旧预留 Declined、Cancelled、相同回执退役，修订保持 0。完整夹具在下一笔真实开工阶段超时，已有证据不包含后续开工领取。四轮已退出、配置禁用，77 文件一致。第四轮证据 `.local/p1-net08-normal-lan/live-reconnect-low-exit/audit.json`，下一候选在 `.local/p1-net08-normal-lan/live-reconnect-position-ready`，先等主机观测农工实际站位再放行新开工。

XYSNET09 搬动保存夹具 Release 零警告零错误。初包把合并构建输出的夹具清单用于生产，预检据此拒绝，未启动游戏；修订包从各项目单独取清单并逐文件核验。私有候选 `.local/p1-net09-move-persist/candidate-manifests` 生产 `766598D3…89A4`、ZIP `ADD1E73E…E57B`、夹具 `F0AFB324…C7B3`，Write 和 Verify 配置已准备且禁用，副本只在工作区。待运行原版两台设备搬动、农工实际复制、主机就寝及两个新进程冷载。

普通建材与首次升级 `XYSP1HomeUpgrade_620261123` 首轮 34412 在真实柜台买足矿、煤、木、石及两图，自然冶炼出铜锭，罗宾原版升级扣 10000g 和木 450。第一夜原版就寝写盘后到春 2，升级剩 2 天；第二夜未打开正常睡眠问答，八分钟超时。进程已退出、配置禁用，81 个文件、其他存档零变化。保留该春 2 专用进度及首轮证据 `.local/p1-native-home-upgrade/first/{audit,report}.json`。下一轮先定位第二次原版床铺触发条件，从受控基线重跑或核对春 2 检查点后续跑。三次就寝、两台制作及新厨房仍待全部通过。

末次短额度 84%、周额度 13%，已停止启动新游戏。工作区保留未提交的三组夹具和本交接，各项按实测证据分别提交。先用已禁用的重连站位复验配置完成新开工领取，再安排搬动就寝和建材升级补测。任务分支未推送，正式版尚未发布。

## 2026-11-13 重连候选冻结

分支 `codex/first-stable-release`，当前提交 `b7ccecc1c7bdbd0c1c2e063cef04b1c31118f9c2`，31 条提交信息通过。周额度最近核对 89%，仅冻结私有候选并保留本交接，未启动隔离游戏，原 16668 保留。未推送或发布。

`.local/p1-net08-normal-lan/candidate-live-reconnect` 包含实际通过 SDK 26 项的生产 `D2A8A257…9FD8`，ZIP `7CB131C4…D24D` 已按字节核对 DLL、manifest 及两份当前翻译，沿用已通过的基础 V3 夹具 `505E5C3A…13FA`。现有夹具只能复跑普通菜单和争用，未覆盖真实重连。下一轮先在专用 XYSNET08 添加同进程断开、原主机继续、同农工重连的验收模式，观察准确的前态及旧预留、Declined 与清理回执，再检查新连接新序号可正常开工和领取。保持 [BUG-2026-029](bugfix/BUG-2026-029-live-reconnect-reservation.md) 待实机状态。

## 2026-11-13 专用联机搬动通过

分支 `codex/first-stable-release`，本轮起点 `4a92cfa65dd66a74ddb18132ed87577068108129`，主机搬动已提交 `ba49b48afcc7bd0ea81a9bb1ce838b3bd411780d`。短额度已重置，本轮启动时短窗口 0%、周窗口 73%。专用双端主机 13704、农工 32532 验证既有争用及豆坊、发酵缸原版手持放置、原版镐保护和空闲重放。两端零保存零错误、73 文件及普通 Mods 哈希一致，已退出并禁用。原 16668 保留。见[实测](validation/P1-native-inventory-2026-11-11.md#原生主机搬动两台设备)。

单人 `XYSP1WalkPersist_620261121` 的 Write 33704、冷 Verify 27744 均通过。冻结生产 `401D8F93…835D`、夹具 `C09A5662…5657`。同档三店、自然工时、厨房食用及原版就寝完成，Saving/Saved 各一次。冷载同一两设备和库存，拒绝旧领取与空闲领取，零保存且 75 文件哈希一致；已退出并禁用。证据 `.local/p1-native-walk-persist/timeout-retry/{write,verify}`，首轮超时单独保留。见[实测](validation/P1-ordinary-play-2026-11-07.md#2026-11-13-同档正常就寝与冷重载)。

容量静默问题已提交 `6de17a71f5a143a3b4f6568efc5c9761cf67edc3`。旧判据失败、修后 MachineCore 136 项与隔离 Release 通过，原生容量边界仍待。见 [BUG-2026-028](bugfix/BUG-2026-028-host-owner-intent-capacity.md)。

普通存档直连的实际双端菜单与争用通过。默认关闭的 XYSNET08 实测只开普通直连，实际原生 Hello、两端绑定同一挑战、中文等待及成功反馈、主机拒绝争用、最终修订和回执相符。修正私有包双端 33284/25496、31944/22000 都已退出，零保存零错误、77 文件一致、仅原 16668。首轮私有包旧翻译导致等待提示缺失，保留失败证据。见[普通存档实测](validation/P1-native-inventory-2026-11-11.md#普通存档直连菜单与争用)。

单人冷载已提交 `3a7b95c62265df80fc202e4e49f894b31ed65638`，普通直连入口已提交 `dec82149c01dc611bd10dadbb556a224095950a0`，序号已提交 `b398384071b0c361317c7d170fbbc35361f59033`，范围 30 项通过。直连序号冻结构建 `496E337F…58F7` 的普通 V3 农工菜单 33756/20300、主机争用 16280/26384 及同构建旧专用 V2 26808/26344 都已通过，终态双端一致，77 文件哈希无差异、零保存零错误、退出且配置禁用，仅原 16668。Release 零警告零错误，GameAdapter 25、MachineCore 141（四项序号专项）。见[实测](validation/P1-native-inventory-2026-11-11.md#普通直连使用每连接序号)。

同进程重连预留的本地修复只保留本进程原菜单发起且从未尝试写入的精确意图。同世界、主机、农工的新可信连接核对 Before 和无日志后可原生 Declined。已加入 GameAdapter 和 [BUG-2026-029](bugfix/BUG-2026-029-live-reconnect-reservation.md)，同一 SDK 旧生产 25 通过、1 失败，修后 26 项全过，Release 零警告零错误。证据 `.local/p1-owner-detached-{red,green}/gameadapter.txt`。下一步冻结候选，实测同进程重连与旧预留取消及实际菜单取消。原 16668 保留，没有隔离游戏。

下一轮的 P1 剩余为真实菜单取消、同进程断线重连时核对旧预留、主机先操作时的争用及搬动后联机存读档、普通获取建材和农舍升级的分段实测。首个 Hello 只有只读 Welcome 而能力未建立时要限频重发同一挑战，保持同连接序号。实体手柄和其他传输仅保留待验范围。P2 尚未开放，任务分支未推送，尚无正式发布。

## 2026-11-13 联机菜单与同档冷重载进行中

分支 `codex/first-stable-release`，已提交主机争用 `50e873d72a5f2db9f1459dbe2315daeb777ce735` 和农工菜单 `4a92cfa65dd66a74ddb18132ed87577068108129`，提交范围 25 项通过。本轮起点 `3cf3392788ac3b44114e402c518284318b67d51f`，远程主分支上次核对为 `e3b2d575d21094750592531c9ee7dc6d100acff8`。任务分支未推送，尚无正式 PR 或发布。未提交内容为本交接、单人同档就寝冷载夹具及专用主机放置搬动与对应检查，分开验证。仅原普通游戏 PID 16668 保留，无隔离测试运行。短窗口最后核对 87%，周窗口 72%，在接近 90% 时保留两个冻结候选，不启动新实机。

- 专用 LAN 主机争用通过。隔离 18772/31812 实际见同一农工预留，主机原版交互打开设备菜单并收到拒绝，原设备、预留与双方库存不变；随后同一农工请求开工并领取，修订 2、黄豆 3、豆腐 2。双方已退出、配置禁用、零保存、71 文件哈希一致，原进程保留。见[实测](validation/P1-native-inventory-2026-11-11.md#2026-11-13-原生主机菜单与农工争用)。
- 专用单人 `XYSP1WalkPersist_620261121` 首轮 Write（`401D8F93…835D`/夹具 `7FB57454…4150`）用原版路线 20:00 完成格斯采购、20:30 返回并开工。21:40 只走完 7/24 个自然时钟变化，触发夹具总限时 12 分钟；未就寝，零保存，73 文件哈希一致，PID 25356 已退出且配置禁用。证据 `.local/p1-native-walk-persist/first/write`。下一轮仅扩大持久化 Write 的限时，重新从同一基线走完整链，随后冷 Verify 拒绝空闲领取且零保存。
- 专用 LAN 农工菜单实测通过。4736/26740 用真实菜单开工、领取，等待及成功反馈与原生终态一致，重复点击保留原请求。最终修订 2、黄豆 3、豆腐 2，两个进程已退出且配置禁用，零保存零错误，73 文件一致，仅原 16668。生产 `E08714FB…545C`、夹具 `DA911DA9…0889`，证据 `.local/p1-net05-machine/native-farmhand-menu`。GameAdapter 24、MachineCore 134、Python 119、catalog 和维护检查通过。
- 专用主机放置绑定原版手持设备和实际背包扣除，手工具移除限于实际本地主机和空闲设备。原版签名验证、生产与夹具 Release 零警告零错误，GameAdapter 25 项。候选 `.local/p1-net05-machine/host-movement-candidate` 的生产 `23FA901A…EA4B`、夹具 `F0B98441…FD88`、ZIP `546DB1F8…BBFA`，待农工菜单双端清理后用 `run-host-movement.ps1 -VerifyHostCompetition -VerifyHostMovement` 验证两设备真实手持放置、加工/待领取保护、空闲击落拾取与重放。普通联机入口和 P2 尚未开放。

下一轮先依次完成专用主机搬动及单人 Write/Verify。单人新候选 `.local/p1-native-walk-persist/timeout-retry` 保持生产与原版时钟，将专用 Write 限时改为 17 分钟，夹具 `C09A5662…5657` 已冻结；首轮未改种子，直接从同一副本重跑。两套尚未启动，按现有额度与独占进程审计接续。

普通 LAN 解禁的后续待验证设计为复用只读 Hello 的随机挑战。主机从实际原生连接核验该 Hello，再在原生主机 Farmer 发布绑定世界、角色、会话和挑战的能力。农工先核实际 Lidgren 客户端和当前 Ready 主机的匹配能力再开启写入菜单。每笔请求继续独立核原生来源，原只读握手仍可与其他传输工作。专用会话当前 256 条结果到顶会让新请求没有终态反馈，普通入口开放前须关闭新授权并能明确拒绝未授权的新请求，已有预留继续恢复。

## 2026-11-12 主机设备菜单争用准备

分支 `codex/first-stable-release`，基线 `a3f02e67c64834dbc014a2badd8940e54e51a48f`。在已绑定的 LAN 测试世界让会话就绪的主机打开原有设备菜单并使用自己的库存，执行前复用生产预留锁。放置和普通联机依然关闭。独立 Release 输出 `.local/p1-host-menu-build`，新入口尚未实机。只有原进程 16668。下一步在 XYSNET05 用高优先级夹具记录农工的实际 `Reserved`，同帧触发主机 `checkForAction`、点击真实 `MachineMenu`，核对拒绝、设备原文、预留及双方库存，继续让农工实际开工与领取结束。

## 2026-11-12 真实豆坊已扣料冷恢复试验

分支 `codex/first-stable-release`，基线 `cd47d8b25bfc185d75cbc99107d31e31acc540ac`。XYSNET07 两轮通过。Write 6732/6700 的真实农工扣黄豆 3，夹具暂停主机提交后正常就寝写盘，磁盘为同一 `Reserved`、黄豆 3、匹配日志及修订 0。冷 Verify 31364/24476 由生产在农工离线时推进修订 1，实际重连收尾回执一致，无第二次扣料。冷轮零保存、71 文件哈希一致，测试已退出且配置禁用，仅原 16668。见[冷恢复](validation/P1-native-inventory-2026-11-11.md#2026-11-12-真实豆坊扣料后断线及冷恢复)。

下一步在仍然隔离的世界接入主机和农工实际菜单，验证两名玩家争用同一台设备，以及加工/待领取移除拒绝与空闲移除重放。普通联机入口与正式版仍待，P2 保持冻结。

## 2026-11-12 活跃预留主动撤销检查点

分支 `codex/first-stable-release`，基线 `64223e3068747a002d9272c53e9d4a572bc44ab9`。XYSNET05 双端实际撤销尚未扣料的已授权开工，原生 `Declined` 与终态收据匹配，修订 0、黄豆 6。随后同连接新开工和领取正常结束。主机 31808、农工 27144 已退出，零保存零错误，67 文件无差异，原进程 16668 保留。MachineCore 133、GameAdapter 24、生产与夹具 Release 通过。开发包生产 DLL `8FF96FC08AB80FCB6951D493F8BF3EA9F99F473A4DCE1A97B5D861FF437CA452`。见[双端实测](validation/P1-native-inventory-2026-11-11.md#有预留时主动撤销)。下一步专用 XYSNET07 验证真实已扣料后就寝冷恢复；普通联机仍关闭。

## 2026-11-12 同档三店到加工食用检查点

分支 `codex/first-stable-release`，基线 `95a62b6a4594e0343c9d4c03f5c9e3b44d756b12`。`XYSP1WalkChain_620261119` 首轮实测完成，14 次原版切图、三店六笔 4510g，13:00 原版农舍门，实际购入料开工，自然 120/240 分钟，17:00 灶台制作并食用。最后豆腐 1、酱油 4。夹具补建材与交互站位。PID 30576 已退出、配置禁用，一次加载、零保存零错误，67 文件哈希一致，原 16668 保留。证据 `.local/p1-native-chain-walk/first`。下一步该链正常写盘和重载。见[同档实测](validation/P1-ordinary-play-2026-11-07.md#2026-11-12-同档从三店采购到加工食用)。

独立的专用 LAN 主动撤销开发仍在验证，普通联机入口保持关闭。

## 2026-11-12 真实豆坊未扣料冷恢复检查点

分支 `codex/first-stable-release`，基线 `d08e2141b68f72dbb51b3a67e1381a334bc7780b`。XYSNET06 两轮通过。真实开工授权后农工未扣料即原版断线，主机原版就寝触发 `Saving`/`Saved` 各一次。冷主机在农工加入前读取同一持久化预留和原库存，生产取消后真实重连收尾，修订 0、黄豆 6、豆腐 0。Verify 零保存、65 文件哈希一致，四个测试进程已退出且配置关闭。原进程 16668 保留。见[冷恢复实测](validation/P1-native-inventory-2026-11-11.md#2026-11-12-真实豆坊未扣料断线后的冷恢复)。

独立单人全链 PID 30576 正在专用 `XYSP1WalkChain_620261119` 测试，冻结生产 DLL `AE8EDBDE…F14E4`，夹具 `5696B3D8…C41B`，证据 `.local/p1-native-chain-walk/first`，超时会自动退出及禁用。先核该审计再启动其他实机。普通联机菜单、实际扣料后冷恢复与多人争用仍待。

## 2026-11-12 原生无授权拒绝检查点

分支 `codex/first-stable-release`，基线 `0c8ce8a3753730d584b9fad617f24b61a0fc2a31`。专用 XYSNET05 的未知配方意图得到原生主机的无授权标记，设备、库存、预留和日志保持原状。农工在同连接继续正确开工和领取，最后修订 2、余黄豆 3 和豆腐 2。两端 32264/28680 退出且配置禁用，零保存，63 文件无差异。MachineCore 132 项通过。生产 DLL `AE8EDBDEC4244864C2B612095517B8D4A12D3181D8A26F62921B8F15C70F14E4`，见[实测](validation/P1-native-inventory-2026-11-11.md#无授权拒绝后继续操作)。普通联机菜单与放置仍关闭。当前另有 XYSNET06 的就寝冷载夹具及单人同档全链改动，未提交内容分别保留。

## 2026-11-12 三店连续步行采购检查点

分支 `codex/first-stable-release`，基线 `fd4e4ccad8ac8f0c286e5149ab3aa58de2de993a`。三店原版自然营业采购通过，旧单店同构建复测通过。两次测试均退出且夹具禁用，原 16668 保留。单店审计的两项新增是独立 XYSNET06 副本，已有 61 文件不变。下一步接三店后回程加工与厨房；专用 LAN 无授权和正常就寝恢复另行推进。见[普通路线](validation/P1-ordinary-play-2026-11-07.md#2026-11-12-自然营业时连续采购三店)。

## 2026-11-12 真实 LAN 机器事务检查点

分支 `codex/first-stable-release`，本检查点基线 `e09ed91adade3462050777d488b143298d9130fb`。专用 `XYSNET05_620261116` 的生产入口已实际完成两端豆坊开工和领取。各重复发两份相同意图，实际修订 0→1→2、黄豆 6→3、豆腐 0→2，两端回执一致，预留和农工日志均清除。PID 32080/5212 已退出，零保存，59 个文件哈希一致。SDK 24、MachineCore 129、Python 119、catalog、Release 构建和维护检查通过。开发包生产 DLL `66F5CED694B44794BEB4AB010DD54E5D753440952CCBB7C02E1EE738E0F3BAB6`。原普通进程 16668 保留。见[豆坊双端实测](validation/P1-native-inventory-2026-11-11.md#2026-11-12-真实豆坊开工和领取)。

普通联机菜单和放置仍关闭。下一步覆盖无授权拒绝、活跃请求的可重试和安全撤销、真实机器在正常就寝中断后的重载、两农工争用，再开放正式入口。独立的三店自然路线开发仅修改 `NativeShopWalkScenario.cs`，另保留实机证据。正式版尚未发布。

本检查点已提交 `fd4e4ccad8ac8f0c286e5149ab3aa58de2de993a`。三店自然路线在独立 `XYSP1WalkThree_620261117` 实测通过。10 次原版地图切换、6 个实际地图动作，09:00 皮埃尔、09:30 罗宾、12:20 格斯，六笔共 4510g，三配方已学习。PID 27364 已退出且夹具禁用，一次加载、零保存、61 文件哈希一致，原进程 16668 保留。原版共享偏好写回已留备份。证据 `.local/p1-native-three-walk`。额度达 91%，未提交内容为三店的 `NativeShopWalkScenario.cs` 和本交接。下轮先复验扩展后的默认单店模式并提交三店证据，再继续三店后同档加工与联机失败/重载。

## 2026-11-12 普通步行皮埃尔采购检查点

分支 `codex/first-stable-release`，基线 `7b5259688419eb142b64b1706d2af45cc11a6d8e`。`NativeShopWalk` 在专用 `XYSP1WalkShop_620261115` 实测通过，四次原版切图，09:00 实际进皮埃尔，原版跳过剧情 17 后继续步行到柜台，花 60g 买到一份黄豆。Release 构建零警告零错误，最终 PID 33540 已退出，夹具禁用，一次加载、零保存，57 个文件哈希一致，原进程 16668 保留。历史的提前切图判断和剧情菜单失败单独留证。见[普通路线](validation/P1-ordinary-play-2026-11-07.md#2026-11-12-后台步行采购皮埃尔)。后续扩展罗宾和格斯；同轮生产 LAN 事务为独立开发内容。

## 2026-11-12 主机预留存档适配检查点

分支 `codex/first-stable-release`，基线 `2daefd3a1bdca690e1d4149411196e3514e8984e`。原生主机 Farmer 的预留键已接入保存生命周期及设备移除门禁。GameAdapter 20、MachineCore 128、Python 119、catalog 40/30、维护与 Release 构建通过。专用进程 16468 的 22 项 Memory 全通过，验证预留原文 XML 内存往返、未知值保留、实际原生移除保护和保存期冻结；一次加载、零原生保存、55 文件无变化。夹具禁用，仅保留原进程 16668。报告 `.local/p1-net03-host-store`。开发 ZIP 与实机 DLL 字节一致，SHA-256 `196B5065942F7E0C69A04CF0561F8515E8D95BCB6ADC739DF7743773C3B51A47`。

下一步用已验证的原生 LAN 连接证据接入受限生产授权、农工实际格提交与主机机器写入，再测正常就寝中断、争用、重放和未知数据。普通三店路线可用原版 `IInputSimulator` 驱动实际移动和地图交互，优先验证皮埃尔一段。正式版尚未发布，远程主分支读取恢复，任务分支尚未推送。

## 2026-11-12 联机正常就寝与冷重载

分支 `codex/first-stable-release`，基线 `519b349c9f55cfc96874cab1934549ee768c5aa4`。隔离 `XYSNET04` 在农工原版断线后从原版床铺就寝，各收到一次 `Saving`、`Saved`。冷启动实际重连，农工对同请求重放一次、写入零次，无标小麦的生产指纹跨双端和冷载一致。Write 只改专用副本，Verify 55 个文件哈希一致，四个测试进程退出，原进程 16668 保留。首轮因夹具/生产版本不配未加载，已保留失败证据。见[实测](validation/P1-native-inventory-2026-11-11.md#2026-11-12-原版就寝与普通物品双端指纹)。已重新通过远程 `ls-remote` 读取 `main` 的 `e3b2d575d21094750592531c9ee7dc6d100acff8`，本地任务分支尚未推送。生产适配继续推进，联机写入保持关闭。

## 2026-11-12 原生 LAN 双端来源观测

分支 `codex/first-stable-release`，基线 `82991ec6ebeb524a49d586170abdb0a71111568b`。冻结构建中的实际农工探针与主机原生 Lidgren 连接身份匹配，客户端返回标题后旧观测失效，两端均保持 `WritesDisabled`。实际进程 22572/33080 已退出，隔离配置禁用，53 个文件哈希一致。原进程 16668 保留，原版启动偏好变更在审计留档。见[双端观测](validation/P1-native-inventory-2026-11-11.md#2026-11-12-原生局域网来源观测)。当前独立开发中的是主机存档适配和正常就寝夹具，运行进程以后续检查点为准。

## 2026-11-12 LAN 来源观测编译检查点

分支 `codex/first-stable-release`，基线 `2baec6135d6561bad2f83589925564ecd832cabd`。生产新增默认关闭的原生 Lidgren 连接来源观测，仅支持已核对的 Windows 游戏 1.6.15.24356 和 SMAPI 4.4.0 程序集。冻结源树的生产、SDK 和夹具 Release 构建零警告零错误，GameAdapter 20 项通过。生产仍只读。实际 LAN 夹具、正常就寝夹具和主机预留适配为后续独立检查点。见[联机协议](P1_HOST_PROTOCOL.md#net-0304-所有者提交与恢复决策)。

## 2026-11-12 主机预留记录检查点

分支 `codex/first-stable-release`，基线 `3debbed5bae1b0fa33f26367699a94537806eb95`。主机预留纯记录和严格 v1 编解码完成，MachineCore 128 项通过。每个世界保留一笔待处理或待确认记录，生产联机写入仍关闭。当前并行草稿包含原版 LAN 连接观测、主机存档适配和隔离就寝夹具。阶段证据和下一步见[联机协议](P1_HOST_PROTOCOL.md#net-0304-所有者提交与恢复决策)。原普通进程 16668 保留，实机进程状态以后续检查点为准。

## 2026-11-12 原版小麦指纹修复检查点

分支 `codex/first-stable-release`，基线 `7600b8c47394b69e16bd5dc642768d9850b508cf`。已定位原版构造随机翻转，将普通库存物品的 `flipped` 排除出指纹，保留其余原生字段与原对象。GameAdapter 旧 DLL 具名回归失败，新 DLL 17 项通过。生产及夹具 Release 零警告零错误。修后专用进程 29412 实机 Memory 21 项通过，含两种小麦朝向和 Farmer XML 往返；一次加载、零原生保存、51 个存档文件哈希一致，进程已退出、夹具禁用，原进程 16668 保留。新开发 ZIP 已生成，生产 DLL `A307647D6F9934F5A2BB244483A0B08A40A2D1A7E7EC17886DD2B51C9004D42E`。当前修复证据见 [BUG-2026-027](bugfix/BUG-2026-027-native-wheat-fingerprint.md)。后续验证实际双端指纹、可信 LAN 连接与正常就寝保存。正式联机写入仍关闭。

## 2026-11-12 小麦指纹差异已定位

分支 `codex/first-stable-release`，基线 `7600b8c47394b69e16bd5dc642768d9850b508cf`。相同诊断 DLL 的独立冷启动复现小麦拒绝，原物品与注册表重建对象的 XML 仅 `flipped` 不同。进程 15132 已退出，夹具禁用，原生保存零次，51 个文件无差异。原证据 `.local/p1-net03-fingerprint-repeat/audit.json`。短窗口额度达 90%，仅本交接未提交。下轮核对 `flipped` 的原生语义，补可重复回归后修复并重跑；生产 DLL 未改。[BUG-2026-027](bugfix/BUG-2026-027-native-wheat-fingerprint.md)

## 2026-11-12 小麦指纹诊断检查点

分支 `codex/first-stable-release`，基线 `e3f67a1ead99806a4a82ee28d6b1a2fe80855f72`。实机夹具新增拒绝时的类型与 XML 字段差异诊断。Release 构建零警告零错误，同一生产 DLL 的第二次冷启动 21 项全通过，尚未捕获首轮拒绝原因。隔离进程 31036 已退出，夹具禁用，一次加载零保存，51 个存档哈希一致。原进程 16668 保留。证据在 `.local/p1-net03-fingerprint-diagnose`，与首轮失败独立。下一轮重复运行诊断构建以捕获差异，再接双端指纹和 LAN 连接准入。生产及远程均未变。见 [BUG-2026-027](bugfix/BUG-2026-027-native-wheat-fingerprint.md)。

## 2026-11-12 原版配方物品指纹实机检查点

分支 `codex/first-stable-release`，本轮基线 `dbf815363557bd0547870118662477facee54b57`。新增默认关闭的 GameHarness 实机检查，SDK Release 构建零警告、零错误。专用 `XYSP1Menu` 实际加载一次，普通 Memory 20 项通过；新增检查通过黄豆的原版 Farmer XML 内存往返，在普通小麦初次取指纹失败。生产代码与开发包沿用上轮。见 [BUG-2026-027](bugfix/BUG-2026-027-native-wheat-fingerprint.md)。

测试进程 23472 已退出，夹具禁用，内存恢复，51 个存档文件哈希一致，无原生保存。原进程 16668 保留。原报告、启动配置、偏好备份及审计在 `.local/p1-net03-fingerprint`。下一步定位小麦的原生类型或 XML 重建差异，修复后完整重跑，再验证实际两端指纹及可信 LAN 连接。当前尚无正式版、PR 或新远程操作。

## 2026-11-12 NET03 冷重载试验检查点

分支 `codex/first-stable-release`，本轮起点 `e2dd31c4b2f717dd7b9f5991b2d716e96317ddf9`，库存写集、恢复决策和指纹已提交 `e69c31a0d4a3f055f57a019c494afc063ea7e45f`。本检查点仅包含合作存档复制工具、默认禁用的 GameHarness 恢复夹具及验收文档，最终提交以分支头为准。Python 119 项、MachineCore 123 项、GameAdapter 16 项、catalog 40/30、维护检查通过，生产与夹具 Release 均零警告、零错误。开发 ZIP 已刷新，新 DLL `B44245E9B433D970689B2876D85F816C5E0D8B89D35B866A9BD117B20932F144`；实机旧包与两端报告一同归档。

本轮专用副本 `XYSNET03_620261112` 的 Write 与 Verify 均通过。农工库存和日志同步后无业务确认即原版断线，主机保存一次；冷主机加载离线农工后，实际重连并重发同一请求，农工重放一次、写入零次，库存保持 2。冷轮无保存，原 47 个存档文件哈希相同。四个测试进程已退出，夹具均禁用，仅保留原普通进程 16668。原版共享偏好写回在审计留档。复跑 Write 需用无标记的独立副本。见[双端实测](validation/P1-native-inventory-2026-11-11.md#2026-11-12-无确认断线及冷重载)。GitHub `ls-remote` 两次在 HTTPS ref 响应前超时，远程分支与 PR 尚未更新。

下一步先在真实 LAN 接收作用域中核对原版连接身份、主机线程与断线失效，同时用无标记普通配方物品核两端及冷载指纹；接着接入主机持久化预留与正常睡觉保存，覆盖未扣料断线、部分同步隔离、完整设备提交与争用。GameAdapter CLI 使用原版 XML 和受控工厂，已通过重建与拒绝检查，尚需在游戏中验证实际注册表路径。生产联机仍只读，P2 保持冻结。

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
