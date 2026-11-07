# P1-NET-02 真实合作农场准备与禁用身份绑定

本文件保留旧夹具d2和两准备进程当时的事实。后续复查两PID已消失，本轮新夹具780构建与停止状态部署另见 [观测扩展](P1-network-observation-2026-10-02.md)；最新现场以 [HANDOFF](../HANDOFF.md) 为准，以下运行检查点不代表当前仍有窗口。

日期：2026-10-02，Asia/Shanghai。分支 `feat/p1-game-adapter`，基线 `8f2468d83b43fb805073f73bbc8b989cf716852e`。专用世界两XML已核且四配置禁用绑定完成。主机合作加载450604963，客户端已启动但停在局域网IP框，等待操作者手动加入/创建，尚未实核peer。诊断未启、报告未配对，Basic/NET02/P1未验收。这是双进程运行中的检查点；接手先查 [HANDOFF](../HANDOFF.md) 与实际进程/配置，不盲用旧PID。

本轮没有修改生产/夹具源码、重跑单元测试或构建。85核心/Release、40人工报告专项及后续28身份工具专项/105Python是 [历史配对](P1-network-pair-2026-10-02.md) 与 [历史工具验证](P1-network-fixture-2026-10-02.md) 的证据，不改写成本次实机通过。设备格式、7物品/5配方原型、稳定ID与依赖不变。

交付前独立检查：`.local/p1-network-live-save-checkpoint-2026-10-02.txt`退出0，专用两XML身份及初始SHA均未变，当前包/禁用gates再次只读通过；这时未核客户端入场，不能当作首次客户端准备保存已完成。`.local/p1-network-live-maintenance-2026-10-02.txt`记录check_workflow退出0、文档结构/本地链接通过、0提交正文检查；git diff --check退出0，既有DESIGN CRLF提示非失败。最后CIM仍为host55272/client56168，手动输入请求尚未回复，未关闭两实例、未启诊断、未生成网络报告；这是等待操作者的运行检查点，不是结束审计。

## 现场与阶段

| 阶段 | 已观察事实 | 尚未证明 |
|---|---|---|
| 默认runtime准备实例 | PID22468加载当前模块/内容包/夹具；操作者进入新测试农舍，后由实际窗口关闭按钮退出；配置写入前实查游戏/SMAPI为0 | 该实例没有执行NET02探针或客户端连接 |
| 专用存档落盘 | 两XML严格核farmName=XYSNET02、uniqueIDForThisGame=450604963；春1/年1、主机测试玩家XYSNETHost、建筑含Cabin；两文件已独立备份 | XML含Cabin不证明实际可加入或客户端Farmer已创建 |
| 真实export/apply | 先导出fixture-plan，后在新fixture-binding目录显式apply；退出0、configurationApplied=true、failure=null、四条replacedConfigs；全部诊断/预览仍false | 不是网络连接或探针验收 |
| 主机Hidden启动尝试 | PID45684加载4个mods，但MainWindowHandle=0、sky无游戏窗口；未进入世界时已Stop-Process结束，保留host-preparation-hidden-start.txt | 不计为合作连接，不再把PID45684作为现状 |
| 主机Normal启动/主持 | PID55272、窗口4001946与OS handle对应；先观察中文标题，随后准确点击合作菜单“主持XYSNET02农场”，截图确认[main player]农舍、春1、6:20 | 未实核客户端peer/Ready或报告证据 |
| 客户端Normal启动 | PID56168、窗口2887862；合作→加入局域网→IP框聚焦，两次sky.type_text 127.0.0.1均未显示文字 | 尚未入世界或创建Farmer，不能由进程存在写已连接 |
| Mods与待回复 | 两端CIM command核独立host/client --mods-path；已异步请求操作者手输IP加入空小屋并创建XYSNETClient/favorite tofu，尚未回复；诊断/预览均false | 首次入场、peer、客户端准备保存、探针与pair仍待 |

PID与窗口仅属于这个观察时点。交互游戏需要可见窗口，后台辅助进程仍使用Hidden；不能从无窗口推断没有进程，也不能从模组加载推断已连入世界。

## 身份与初始保存证据

进入实机准备前的基线为 `.local/net02-live-2026-10-02/before-hashes.json`，六份配置原字节在同目录before中，旧完整日志只留本地。专用存档的两份初始文件和 `identity.json` 位于 `.local/net02-live-2026-10-02/farm-preparation`。身份来自确切XML节点，不是目录后缀或玩家名推断。

| 初始文件 | 实际SHA-256 |
|---|---|
| XYSNET02_450604963主存档 | `d459be380b0485b80793556e95a9eb316b2ff2b741344ae5c37e356fce2c4e59` |
| SaveGameInfo | `1fe86e0966e13de68620d4d7a2e6ed36dbcc7242083fe53bdb3b9001e5070ffc` |

`identity.json`记录XYSNET02/450604963、春1/年1、主机测试玩家XYSNETHost及建筑Cabin；它的protectedSaveHashesUnchanged逐份匹配本轮before-hashes里的源/旧UI/Stage3六文件。此次初始保存是专用世界准备动作，不能要求“新建前后专用存档字节不变”。后续首次客户端创建/准备保存还会建立新的磁盘基线，不能沿用此初始hash声称整个加入准备没有保存。

只提交这份脱敏摘要；个人完整XML、路径、截图原件与完整SMAPI日志不进入Git。XML中Cabin存在仅是结构证据，小屋使用与真实客户端身份仍待游戏核验。

## 实际身份绑定

本轮以真实专用存档文件夹为 `--save`，`--profiles .local/net02-profiles --farm-name XYSNET02`。先export到 `.local/net02-live-2026-10-02/fixture-plan`，再以 `--apply --output .local/net02-live-2026-10-02/fixture-binding` 完成绑定。两目录已有before/prepared/fixture-plan证据，binding另有fixture-apply.json；不覆盖重跑，不把旧导出计划当执行来源。一般命令与失败接续见 [NET02指南](../NET02_TEST_SETUP.md)。

fixture-plan schema1的configurationApplied=false，表示导出没有改目标；fixture-apply schema1的configurationApplied=true、failure=null、replacedConfigs完整四条，表示逐份替换及后核完成。网络报告schema2尚未生成，与这两个文件格式不可混用。

| 配置（相对profiles） | beforeSha256 | afterSha256 |
|---|---|---|
| host/Mods/XiangyeShiling/config.json | `43cb3db20eb51d68ccf20056bf0318fa5a0bdecc072a5dbaa74b255fadb20cb9` | `c08b71b1d6f09daad5b285d8a0adc6fc4db558795904d8e1ee5535330944c603` |
| host/Mods/GameHarness/config.json | `a8825871d792c8e09fb04378b8c9548a3fa946c95f1fd37333a7f96f7a059231` | `dd7cf3d7a0d8529efbca6f5f1ca9f8e8dd69d0980388555275bd3ba2dd05b17c` |
| client/Mods/XiangyeShiling/config.json | `43cb3db20eb51d68ccf20056bf0318fa5a0bdecc072a5dbaa74b255fadb20cb9` | `c08b71b1d6f09daad5b285d8a0adc6fc4db558795904d8e1ee5535330944c603` |
| client/Mods/GameHarness/config.json | `22234054f66b10848ca2bf6003c1d0e50cde9f9b22a0857b888c901a82922e26` | `5c466f9b337052cbbfea23aa896fd04b9ecfe27d3203ec70697697287611a053` |

两端主模块NetworkProbeFarmName=XYSNET02/NetworkProbeSaveFixtureId=450604963且EnableReadOnlyNetworkProbes=false。夹具FarmName/SaveFixtureId一致，Network/Host与Client、Enabled/OpenPreview=false。profiles.json不重写，roles.configs仍属文件准备时点；上表afterSha256是apply完成时的候选/后核原文，不是游戏启动后的当前raw hash。启动后值未变、格式hash变化，详见下一节，不能声称六配置原字节全未变。

apply审计的cabinAvailabilityVerified、independentWorldCreationVerified、runtimeBinaryVersionVerified、networkStarted、fullNet02Accepted均为false。实际世界创建与XML身份是外部准备证据，工具不验收小屋使用、SDK二进制或真实连接。四文件逐个atomic replace仍不是整体事务，今后失败须按before/prepared/audit核对，不盲目覆盖外部编辑。

## 未改对象与构建身份

绑定审计重核14包文件及声明版本：CP2.9.0、主模块/内容包0.1.0-alpha.2、夹具0.1.0。绑定工具没有写包/profiles或runtime两配置，但runtime在游戏启动后已发生格式/default写回；它用于默认准备实例，不能代替host/client角色配置。下面runtime两行仅记录启动前基线。

| 对象 | 本轮绑定引用的实际SHA-256 |
|---|---|
| 生产DLL | `5ef0df7577255bbc0132e7879fab4f40fda1bcbcad013a291c906a3dbc4b04ac` |
| GameHarness DLL | `d2f054089f1dedf7a31ed63cadd5c49befbdd4feccf7541dbcbc02fec03db345` |
| profiles.json | `cf540bb6c55ef2bcbf069aeceeab5b470e690d2881b984601e50bfb1a6d54e8c` |
| runtime主模块config基线 | `43cb3db20eb51d68ccf20056bf0318fa5a0bdecc072a5dbaa74b255fadb20cb9` |
| runtime夹具config基线 | `2a7e29d5f3676a47dc40f399078ec0aa0121e1791359d76c6af3048565514c81` |

这些hash说明包身份，不是新构建、SDK二进制版本或结束审计。六旧保存已在初始身份和后续bound审计核不变；两个角色实例仍在准备，客户端准备保存的新基线与最终退出后审计尚未完成。

## 启动后的配置写回与检查点审计

根任务实际完成 `.local/net02-live-2026-10-02/bound-preparation-audit.json`，退出0，并将六份当前原文保存到bound-configs。保留进入准备前before、apply prepared、启动后bound-configs三个时点，审计脚本只运行一次，不重跑覆盖。

四角色完整JSON与apply候选相同；runtime主配置完整JSON值也相同，runtime夹具旧五字段保持并新增默认NetworkRole=Observe（来自ModEntry.cs默认）。六份raw SHA不同，不能要求启动写回后仍等于候选或before原文；相关禁用开关未变。

| 配置 | 启动后实际raw SHA-256 |
|---|---|
| host主模块 | `a52298679734b8140e28bc68008cb26ca63654c93a39dfb5a68f819d3815b5a8` |
| host夹具 | `59a5abd9a5af96c7f9bfd160d3f33f839df2fa40367fb86b795b86de6d2b7c73` |
| client主模块 | `a52298679734b8140e28bc68008cb26ca63654c93a39dfb5a68f819d3815b5a8` |
| client夹具 | `707161569466163c07c3239f3280dd9510f8f1402d7e96a139da05291f251391` |
| runtime主模块 | `bf6542bf5a78970b443d5231ab5c43bfd28168b138fed3e2424690665590fd13` |
| runtime夹具 | `2f2cd534d2d2e79318249dd4df2efeabc15101fb5e881fc060ba959f321846f2` |

此前三次本地审计草稿先后在host原文hash、runtime主配置原文hash、runtime夹具默认字段假设处停止；不是生产bug回归红，失败草稿不标通过，也未覆盖配置来满足原假设。最终JSON的verifiedHashes实际15条：六config原文变化，其他九（六旧保存、两个profiles、ZIP）不变；另重新只读plan核14包/禁用门禁通过。configurationFormatChanges/runtimeAddedDefaultFields和observedProcesses明确录差异与角色。初始专用两XML仍等于初始保存hash，尚无客户端准备保存。

第一次成功stdout固定标签误写“17对象/其他11未变”，不是原JSON数据。已只读核对正确为15/9，根任务把本地脚本print改为实际长度动态计算，未重跑或改原audit（输出有存在保护）；不加构建DLL强凑17，也不列为新增生产缺陷或先红后绿用例。审计结果引用原始15条JSON及独立14包核验，不用旧stdout数量代替数据。

bound审计保留configurationApplied=true、diagnosticsEnabled=false、clientEntryVerified=false、basicProbeEvidenceVerified=false、fullNet02Accepted=false。两CIM命令分别绑定host/client Mods，但进程存在不证明连接、探针或退出；这是进行中检查点审计，不是最终收尾。

## 接续顺序与未执行矩阵

1. 先重查55272/56168与两个实际窗口及禁用配置，等待操作者手动加入回复；client当前IP框需要127.0.0.1，加入空小屋后创建XYSNETClient/favorite tofu。未观察前不写已入场，不重新建另一农场或用旧UI/Stage3。
2. 客户端首次Farmer完成后，实核同世界、角色/peer、Cabin使用和客户端身份；只在450604963完成准备保存，备份加入后的两文件及hash，保留初始备份与bound检查点。
3. 退出两端并查询进程0，留档准备日志、配置与旧受保护文件，再另行显式启用两端探针/Harness；OpenPreview保持false。首次创建须放在诊断前，避免主机60秒Ready等待被角色创建耗尽。
4. 新鲜诊断轮中归档两份active schema2报告原字节，核同世界/session、各端本地基线稳定、五种拒绝与九发八回候选；再执行离线pair。主机八读一读取前拒绝须实际quota证据，不能由客户端少收一条推断。
5. 结束时标题报告另存，不覆盖active；关闭四份配置的探针/Enabled/OpenPreview，确认进程结束，用客户端准备保存后的专用基线及旧六存档/配置/包完成新审计，更新HANDOFF。

| 验收项 | 当前状态 |
|---|---|
| 专用两XML身份/初始备份 | 已核XYSNET02/450604963，初始两文件备份与六旧文件核对完成 |
| 四禁用配置真实apply | 成功，审计true/failure null/四条替换记录；不是网络通过 |
| 合作主机加载 | 已截图确认主持450604963后的[main player]农舍，春1/6:20；不证明客户端连接 |
| 首次客户端Farmer/准备保存 | 进行中：PID56168停IP框，手动输入请求未回复，尚未进入/创建或取得新保存基线 |
| 启动后bound检查点审计 | exit0，六配置raw变化和值/default明确留档、旧保存/profiles/ZIP不变、14包禁用plan通过；两进程仍运行 |
| 两端peer/Hello/Welcome/共同session | 未执行 |
| 五种固定探针/两端库存与可见raw | 未执行；无实际报告，不能猜机器数量或空集合指纹 |
| 九发八回与主机同窗口八读一未读 | 未执行 |
| schema2真实报告pair/Basic结果 | 未执行，无摘要或退出码；FullNet02Accepted不标通过 |
| 未握手/Hello重试、断连同ID重连 | 未执行，须独立episode，不拼接报告 |
| 16每帧/128邮箱、错版/分屏/方向/来源 | 未执行，基础夹具不自动验收 |
| 标题/换世界/旧消息、最终禁用与磁盘审计 | 未执行；当前不是结束状态 |
| 真实设备准入、远端库存和联机提交 | 未实现或未验收；保持NET03/04与生产写入门禁 |
| 远程提交/推送/PR/merge/CI | 未执行，无新SHA或成功结果 |

Saved=false仅说明夹具没有调用保存；准备保存必须独立披露，诊断不保存仍须磁盘hash验证。metadata不是强身份认证，synthetic目标与空可见机器集合都不能证明真实设备授权或保护。此记录没有把进程启动、XML Cabin或configurationApplied=true提升为Basic/完整NET02/P1验收。
