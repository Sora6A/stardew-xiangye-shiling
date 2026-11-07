# NET-02 双端只读诊断准备与执行

11-02新增独立`NetworkWelcomeReplay`模式，部署已完成且默认禁用，112核心/CLI人工正负例不代SDK通过。它在同进程新Ready后一次重发本机旧Welcome，先留Before再读拒绝；配置、五分钟上限、实际双报告CLI/关闭恢复步骤见[本轮记录](validation/P1-welcome-replay-2026-11-02.md)及[夹具说明](../tests/GameHarness/README.md)。身份工具预检仍只对恢复后的Network/Disabled配置执行，不能用它启用主动模式。当前现场以[HANDOFF](HANDOFF.md)为准。

2026-10-12当前：专用XYSNET02/450604963准备保存为春24/已有XYSNETClient，真实Basic五probe及同窗口8读1未读限流已完成。独立NetworkLifecycle原双报告也已通过同进程同ID新握手/主机原会话保持；结束CIM0/八save不变/六config原字节禁用/85项及预检通过，见[生命周期实测](validation/P1-network-lifecycle-2026-10-12.md)。机器数均0，客户端收尾标题有证据、主机本轮缺此快照；旧Welcome重放/负载队列/完整NET02及P1仍待。当前停止现场见[HANDOFF](HANDOFF.md)，下方旧日期状态不作为重建农场或创建角色的依据。

## 独立同ID生命周期流程

首轮两个窗口的实际角色与配置反向，两Scope预期拒绝并安全关闭/恢复，原双报告离线拒绝，不计产品bug或重连通过。重试先只启动Host并核MainPlayer，再开Client；最新PID和启用状态见HANDOFF，下方历史首轮等待状态不能作为当前现场。

NetworkLifecycle不复用Basic episode，不发送probe/旧包、不自动加载/操作/冻结/保存。停止两端后留新的八save/六config及包基线，四角色配置临时Enabled/probe=true、Scenario=NetworkLifecycle、同世界/Host与Client/OpenPreview=false；runtime两配置保持禁用。先主持及已有角色加入，归档两InitialReady，随后仅client返回标题并保持到host观察peer gap，最后**原客户端进程**重加入同角色，归档两Reconnected/原双报告离线核验。Host只能一次SaveLoaded，Client恰两次，五分钟从首次加载开始，失败不得拼接或手改报告。

详细schema1/命令/判据/结束审计见[GameHarness操作](../tests/GameHarness/README.md)和[本轮记录](validation/P1-network-lifecycle-2026-10-12.md)。Completed须双报告核同世界、两进程/DLL、共同session及互为peer、实际旧新Hello/Welcome挑战不同、库存/raw保持；跨端自己的连接GUID/线程/时钟不直接要求相等。只证明重新握手和主机session保持，OldPacketReplayVerified/LoadedQueueLifecycleVerified/FullNet02Accepted保持false。收尾先client后host标题/关闭，核八save不变、四config恢复本轮before原字节为NetworkDisabled，唯一报告归档/hash核验后移出Mods，再做禁用profile预检。

下方10-11及更早的现场均为历史；其中配置展示和身份绑定命令只适用于停止且Network/Disabled的环境，不能在正在启用NetworkLifecycle的进程中应用。

更新时间：2026-10-11，Asia/Shanghai。合作主机只读输入短测已完成，用户确认移动/菜单正常；144采样支持D位移和Esc关闭DialogueBox，普通GameMenu及持续自由移动未另测。已实核ClosedGame/CIM0、六指定config恢复原字节禁用、八save不变，当前专用世界为春23年1/450604963，仍无persisted XYSNETClient。原观测报告保持Observing/false flags并归档，未新增输入修复或确立旧会话根因；生产/包不变。下一步从**诊断禁用条件下首次客户端准备保存**继续，不重建世界、不拿春2做新基线、不用单端短测代NET02。见 [10-11输入记录](validation/P1-input-observation-2026-10-11.md)与 [HANDOFF](HANDOFF.md)。

以下10-05为历史状态，旧PID、仍无响应及春2记录不代表当前会话。

更新时间：2026-10-05，Asia/Shanghai。专用世界XYSNET02/worldID450604963已真实绑定。禁用诊断时两端已原生连接，但 NPC 清路触发 [BUG-2026-019](bugfix/BUG-2026-019-null-removal-target.md)，主机持续异常；用户报告的春2保存未落盘，客户端角色尚未持久化。旧两窗口已实际关闭，空目标回归先红后绿，新模块停止部署通过；修后主机已重新合作加载，用户确认移动和菜单仍无响应；后来主机原生保存春2，无客户端Farmer，物理Escape停止界面操作。六config禁用，旧六save未变、专用两save已变化，真实模组Ready/探针/双端配对未执行，NET-02/P1未验收。本轮证据见 [空目标修复记录](validation/P1-null-removal-2026-10-05.md)，动态进程见 [HANDOFF](HANDOFF.md)。10-04两负例与 [失败路径实测](validation/P1-network-failure-2026-10-04.md)、[10-02观测扩展](validation/P1-network-observation-2026-10-02.md)按历史保留。

## 已有配置与边界

旧实机准备夹具d2记录见 [历史记录](validation/P1-network-live-2026-10-02.md)。10-02观测扩展Release实际退出0、0警告/0错误，GameHarness.dll SHA-256为`78026dd6a3f703fcb26bef41590289dab8505ae7621ab4813503666787582693`，当日停止部署；10-04实际用此构建运行两个受控失败。本轮未改源码或重建，生产DLL仍5ef0df75…，CP2.9/双包alpha.2/夹具0.1.0/schema2不变。完整构建/部署hash见 [观测记录](validation/P1-network-observation-2026-10-02.md)，实测关联与恢复见 [失败记录](validation/P1-network-failure-2026-10-04.md)。当前四角色JSON如下；临时负例配置已恢复原字节，不复用旧清单证明新产物，也不能声称自最初live准备以来原文未变。

主模块两端 `Mods/XiangyeShiling/config.json` 当前均为：

```json
{
  "EnableReadOnlyNetworkProbes": false,
  "NetworkProbeFarmName": "XYSNET02",
  "NetworkProbeSaveFixtureId": 450604963
}
```

GameHarness 两端 `Mods/GameHarness/config.json` 当前为下述配置，客户端的 NetworkRole 为 Client：

```json
{
  "Enabled": false,
  "Scenario": "Network",
  "FarmName": "XYSNET02",
  "SaveFixtureId": 450604963,
  "OpenPreview": false,
  "NetworkRole": "Host"
}
```

两个显式开关关闭、世界 ID450604963 是此次真实 apply 后的配置。此前文件准备的 ID0 见 [历史身份工具验证](validation/P1-network-fixture-2026-10-02.md)，不能当作当前绑定。工具没有自动启用或选择任意存档。NetworkRole 默认 Observe，既不是 Host 也不是 Client，缺省角色不能通过夹具身份核对。关闭探针只关闭额外测试发包/trace；生产普通多人世界的只读 Hello/Welcome/拒绝桥仍工作，机器加工/放置写入门禁不变。有效 Idle 的原生移除仍可能走游戏路径，不能声称所有世界写入都被禁止。

游戏和 Mods 位于工作区，**Saves、偏好与完整日志仍使用正常用户目录**。这不是两个用户目录沙箱。原源农场、旧XYSP1UI与XYSP1Save/Stage3不用于此次Network；旧六save与专用两XML均有独立基线。旧准备55272/56168后来消失，退出动作未观察；本轮52332/39000则实际由窗口X关闭并核CIM0，八save未变。两种退出证据分开记录，停止后的审计使用新证据，不把运行中的旧检查点当最终收尾。

## 重建文件配置

所有使用目标配置的进程应停止。一般变更先构建当前生产和夹具，部署两个自有包到停止的 `.local/validation-game/Mods` 并核对 ZIP/build/部署一致；本轮生产DLL不变，只刷新自有夹具产物与角色清单，保留450604963的四份禁用绑定及runtime禁用Memory配置。整理 `.local/net02-probe-harness`：只放 GameHarness 的 manifest.json、GameHarness.dll、同名可选 deps.json/runtimeconfig.json。不要传完整 bin/Release，因为 PDB 或其他 DLL 会被白名单拒绝；夹具不进开发 ZIP。部署前备份、部署后分别核包和配置原字节，不能用新build哈希外推目标已刷新。

```powershell
python tools/prepare_multiplayer.py --source-mods .local/validation-game/Mods --runtime .local/validation-game --output .local/net02-profiles --harness-dir .local/net02-probe-harness
```

必须显式使用当前已核验 source；CLI 的默认 source 仍是旧 `.local/test-mods`，默认输出仍为旧 `.local/multiplayer`。CLI 输出只允许工作区 `.local` 的严格后代，不能选根或外部路径。工具先检查全部来源、manifest/依赖/链接与两端意外文件再复制，保留已知模组根目录的目标 config 原字节，不继承来源 config。已存在 Enabled=true 的用户配置也会原文保留并如实报告，工具不会替运行者禁用；本轮新配置实际全为 false。

成功后 profiles.json 的 roles.files/configs/versions 来自真实目标，两个角色包文件必须一致，角色配置可不同。清单检查不验证实际 SDK/游戏 DLL 版本，runtimeBinaryVersionVerified=false；按 DEVELOPMENT 的环境检查另核基线。全预检不是完整 I/O 事务：复制中断/权限失败仍可能单侧更新，旧 profiles.json 只能描述上一轮成功，不能代替当前哈希核对，见 [BUG-015](bugfix/BUG-2026-015-multiplayer-profile-preflight.md)。

真实运行会产生 network-probe-result.json 等证据；这些是包白名单外文件，重建前必须先归档到独立本地证据目录，确认保存后再明确处理目标残留，不能自动删除来绕过预检。不要重新生成旧配置、删除未知文件或清空旧存档 marker。

## 建立独立世界

本轮已从两份真实 XML 核到农场 XYSNET02、uniqueIDForThisGame=450604963，主机测试玩家 XYSNETHost，初始春1/年1，建筑含 Cabin；两份初始文件及 identity.json 已备份到 `.local/net02-live-2026-10-02/farm-preparation`。XML 含 Cabin 不证明客户端可加入，小屋使用仍待实际核验。存档文件夹通常采用玩家名，目录标签不能作为身份来源。

以后重新准备时使用全新的合作测试农场，名称以 XYSNET 开头、长度 6–20 个 UTF-16 单元，并准备至少一间可用农场帮手小屋。只在该专用世界做初始保存，取得真实且非零的 uniqueIDForThisGame，保留身份与初始存档哈希。不能仅改文件夹名、复用旧 ID 或让两端各自加载同一农场；主机合作界面加载，客户端合作界面加入。当前继续已有450604963，不应因旧“未创建”记录再创建第二个世界。

第一次准备农场时保持两个开关关闭。如需测试已有设备 raw，设备/原料必须在单独准备阶段建立并披露，诊断开始后不注入设备或改变背包/raw。当前固定 probe 指向 synthetic FarmHouse(0,0)/revision0/InstanceId=RequestId，不证明某台实际机器满足可写条件；没有设备时报告 BaselineVisibleMachineCount=0，仅证明空可见集合的指纹不变。

## 在禁用状态绑定真实身份

`tools/configure_network_fixture.py` 读取已创建的专用主机存档文件夹并重新核验 `.local/net02-profiles`，不会创建、安装或加载农场。使用它前停止所有读取目标配置的游戏/SMAPI 进程；下面先查询现场，不能把没有工具进程锁误认为可以边运行边改配置。

```powershell
Get-Process StardewModdingAPI, 'Stardew Valley' -ErrorAction SilentlyContinue
$net02Save = Read-Host '输入已创建的 XYSNET02 专用农场存档文件夹完整路径'
python tools/configure_network_fixture.py --save "$net02Save" --profiles .local/net02-profiles --farm-name XYSNET02
```

`--save` 必须是包含同名主存档与 SaveGameInfo 的具体文件夹。默认只有标准输出 plan 概要，`configurationApplied=false`，不创建证据目录、不改目标。`--farm-name` 应与真实农场名称一致，不能用文件夹名或玩家名替代；若另选符合约束的名称，三个命令都改为该名称。

要保留可审查的备份和候选，先使用一个从未存在的独立输出目录：

```powershell
python tools/configure_network_fixture.py --save "$net02Save" --profiles .local/net02-profiles --farm-name XYSNET02 --output .local/net02-fixture-plan-run01
```

此次 export 写入 `before/{host|client}/Mods/{XiangyeShiling|GameHarness}/config.json` 原字节、对应 `prepared/...` 候选及 `fixture-plan.json`，四个目标配置仍不变。导出计划只是本次输入证据；工具不会从旧 plan 执行修改，每次调用都重新读当前输入。

核对候选后，用另一个从未存在的证据目录显式 apply：

```powershell
python tools/configure_network_fixture.py --save "$net02Save" --profiles .local/net02-profiles --farm-name XYSNET02 --apply --output .local/net02-fixture-binding-run01
```

命令中的 run01 是首次使用示例；目录已存在时换独立名称，不覆盖或清空旧证据，也不能把 export 目录复用给 apply。CLI 的 `--profiles` 和 `--output` 都只能是工作区 `.local` 的严格后代；输出与源存档、profiles、清单记录的 runtime 不能互相包含。源存档与 profiles 也须分离。链接、reparse 路径及硬链接输入拒绝。

预检读两份真实 XML：主文件须为 SaveGame，恰好一个直接 `player/farmName` 与 `uniqueIDForThisGame`；SaveGameInfo 须为 Farmer，恰好一个直接 `farmName`。两个名称必须与指定名称相同，世界 ID 为非零 uint64 且不在已登记的旧身份集合。DTD/entity、损坏 XML、重复身份元素及不合名称长度的输入拒绝；XML 每文件读取上限 64MiB。非零 ID 和旧 ID 拒绝只排除已知误用，不能证明世界确实独立新建。

工具还核对 schema2 profiles.json、当前两端全部包文件哈希、manifest/声明版本及依赖，要求两端文件与记录相符。未知包文件或 network-probe-result.json 等报告会被拒绝；先归档证据，再由运行者明确处理目标残留，不自动删除。四份 JSON 必须有效且无重复或大小写别名键、无非有限数，读取上限各 2MiB。主模块必须 `EnableReadOnlyNetworkProbes=false`；夹具必须 `Enabled=false/OpenPreview=false/Scenario=Network`，角色恰为 Host/Client。启用或角色不符会拒绝，不替运行者修开关。

apply仅把主模块NetworkProbeFarmName/NetworkProbeSaveFixtureId和夹具FarmName/SaveFixtureId绑定真实身份，未知值保留但JSON重新序列化，原字节在before。工具不写源存档、包、runtime两配置、旧.local/multiplayer或profiles.json。profiles里的roles.configs是生成时历史hash，fixture-plan/apply的before/after描述绑定时点；游戏启动后还会发生配置写回，当前原文hash须另审计，不能直接沿用apply候选hash。

备份、候选和 plan 全部写好后才逐一替换四个配置；每次单文件替换是 atomic replace，四个文件整体不是事务。普通输入变化会再次核对，但没有强并发保护。失败可出现部分更新：若成功留档，`fixture-apply.json` 为 `configurationApplied=false`，用 replacedConfigs/failure 定位；审计 I/O 再失败时也可能没有该文件，退出失败不证明目标全未改。保持进程停止，按 plan 的 before/prepared 哈希逐份核对当前文件，保留故障现场；出现第三种哈希先查外部修改，不盲目回放、覆盖或恢复并发编辑。明确恢复到同一套四配置、重新预检后，才用新目录重试。

成功 apply 只证明本次四配置写入与随后文件核对完成。`cabinAvailabilityVerified/independentWorldCreationVerified/runtimeBinaryVersionVerified/networkStarted/fullNet02Accepted` 均保持 false；小屋、真实双端、运行时二进制版本与网络验收仍需独立验证。本轮在准备实例 PID22468 退出且配置写入前实查进程为0后，真实 export 至 `.local/net02-live-2026-10-02/fixture-plan`，再用新目录 `.local/net02-live-2026-10-02/fixture-binding` 显式 apply，退出0、configurationApplied=true、failure=null，四配置绑定450604963且开关仍关闭。两目录是已存在证据，不可覆盖重跑；准确 before/after 见 [实机记录](validation/P1-network-live-2026-10-02.md)。

此前身份工具阶段实际只读重核 14 个包文件、四份禁用角色配置通过；对已知受保护旧 UI 存档执行 `--apply` 负例，预期拒绝、退出 1 且未生成输出。该阶段独立审计确认六份配置、六个旧存档文件、旧/当前 profiles、ZIP 和生产/夹具 build 均未变，证据为 `.local/p1-network-fixture-local-audit-2026-10-02.json`，专项结果见 [历史验证](validation/P1-network-fixture-2026-10-02.md)。这项负例不是 bug 的修前红，也不是此次成功 apply；真实绑定后四角色配置哈希已变，不能沿用该旧审计声称六份配置全部未变。

## 禁用阶段完成首次客户端创建

真实身份绑定后先保持两端主模块探针、夹具 Enabled/OpenPreview 全 false。主机合作界面加载此专用农场，客户端合作界面加入并完成首次 Farmer 创建；独立核对主机/客户端实际角色、同一世界、可用 Cabin 与客户端身份。只在这个准备阶段对专用世界保存，保留新增客户端后的两份存档与哈希，作为后续诊断的磁盘基线。

首次角色创建和准备保存完成后退出两端，实查进程结束；归档准备日志、配置、专用存档及旧受保护hash，再另行启用诊断。SaveLoaded后Ready默认等待最多60秒，首次创建可能耗尽主机等待，不能用开启的诊断轮代替角色准备或把准备保存计入探针不变性。10-11操作者已完成本轮两端移动/睡觉保存并关闭，10-12日志/实际XML/CIM与85项审计核过：春24年1、直接farmhands中的XYSNETClient已创建、favorite tofu，与连接ID一致，新基线在.local独立归档。详见 [准备记录](validation/P1-network-preparation-2026-10-11.md)。下一轮选择已有角色，不重复创建、睡觉或覆盖旧准备证据。

## 显式启用与连接

完成身份绑定、首次客户端创建/专用准备保存以及独立环境/小屋核对，停止两端后，另行显式设置两端主模块 `EnableReadOnlyNetworkProbes=true`、夹具 `Enabled=true`；OpenPreview 保持 false。绑定工具不做这一步。核对四份配置、实际包哈希与留档证据后再启动；未知 ID、名称不匹配、普通单人/分屏/非主屏、错角色、主模块未开启或诊断 Faulted 均不能继续探针。

```powershell
& .local/net02-profiles/host/start.ps1
& .local/net02-profiles/client/start.ps1
```

运行者在各自终端启动；后台helper用Hidden，交互游戏用可见窗口。旧准备轮Hidden主机无游戏窗口并已结束；后来Normal主机PID55272准确点击“主持XYSNET02农场”，截图确认[main player]农舍春1/6:20，Normal客户端PID56168停在IP框，两CIM命令的独立host/client --mods-path已核。这些是旧夹具d2构建的准备观察，不证明peer连接；本轮已核两个PID消失，退出动作未观察，详见 [旧实机记录](validation/P1-network-live-2026-10-02.md) 与 [最新交接](HANDOFF.md)。Network不自动加载或接其他模式备料/保存入口；启用诊断后等待真正SaveLoaded/Ready，默认60秒，错误保留报告，不打开任意农场重试。

旧启动后的bound-preparation-audit.json已实际退出0：四角色完整JSON仍等于apply候选，但raw格式hash变化；runtime主config也仅格式变化，runtime夹具旧五字段保留并新增默认NetworkRole=Observe，相关开关全false。此前三次草稿在不同原文/default假设处停止，不是生产bug回归红，不标通过。该时点六份原字节另存bound-configs，保留before/apply prepared/启动后三时点。JSON实际核15对象：六配置变化，其他九（旧六保存/新旧profiles/ZIP）不变，另只读plan核14包/禁用门禁通过。首次成功stdout17/11是本地标签错计，原JSON为准；脚本已改动态计数但未重跑/覆盖证据，不列生产缺陷或红绿用例。审计属于旧双进程运行中的检查点，不是本轮退出、部署或网络收尾。

## 夹具与证据判据

MachineService 的本地 GetApi 类型保持不变。ReadNetworkDiagnostics 只在游戏构造线程读取复制的状态/peer/trace；TrySendNetworkProbe 只在配置匹配的客户端、唯一 Ready 主机下发送固定五种意图，每 1000ms 最多 16 次发送尝试。true 只是 SendMessage 尝试，不等于送达。该入口不接受任意 JSON/目标/actor，不调用 Execute/Gate/Ledger/Commit。

两端 `Mods/GameHarness/network-probe-result.json` 含独立 HarnessRunId/进程 ID/episode、角色、生产 DLL hash、指定世界 ID、库存与可见 raw 指纹、可见设备数量和 Protocol 快照。Protocol 含本进程 RunId、peer 本地连接 GUID/challenge/Ready、SessionId、世界/断连 generation、QueueCount/QueueRejected、序号/Evicted 及最多 256 条 trace。Processed 观测明确 BodyReadAttempted、Kind、RequestId/Reason；这是桥调用正文读取委托的观测，不是 SMAPI 回调前网络层分配统计。trace 无 raw/body/姓名/路径，完整报告仍只留 `.local`。

失败报告的Protocol只保留当次Tick已成功ReadNetworkDiagnostics返回的快照。读取前失败为null，读取后失败保留本Tick观测，不借上一Tick或旧连接的缓存，也不在catch再次读取API。快照可能早于随后发送和异常，不能作为失败后的最终状态；本Tick后续发送也可能尚未进入该快照trace。Failure保持非null，离线配对无论Protocol是否完整都拒绝；SchemaVersion=2、FullNet02Accepted=false不变。若报告写盘自身失败，保留观测也不保证报告已落盘，须核实际文件并留可归属日志，不能把旧文件当本次失败报告。

10-02具名人工Python拒绝测试1项OK不执行C# Tick。10-04已实际验证Scope读前拒绝为null、正常读取后门禁拒绝保留Protocol，第一份实际Host失败报告被endpoint因Failure拒绝；复跑步骤与两份原SHA见 [失败路径验证](validation/P1-network-failure-2026-10-04.md)。这是观测增强的集成验证，没有新玩家事故/源码修复/修前红，不新增BUG ID。API内部异常、同进程跨Tick/重连、标题/I/O失败仍未覆盖。报告没有GameHarness DLL哈希字段，须以独立部署/文件审计关联；故意失败验证不代表Basic或完整NET02。

客户端首次 Ready 后绑定进程/session/generation/唯一主机连接与线程；每个发送按发送前序号关联后续 Processed/RejectedType/实际读入且接受的拒绝。连接/世界改变或队列丢弃/trace 淘汰会中止该轮，不拼接。BUG-016 的旧 null-ID 误判已先红后绿；格式错误回复仍没有线上的完整请求关联，必须结合独立主机报告。两端各自核对本地 Item 引用/值及可见设备 raw 指纹，主机只是观察者，不会自动宣告用例通过。

客户端顺序发 Start、Collect、ActorMismatch、WrongSession、Malformed，每项前隔 1300ms，回执超时 10 秒，分别要求 WritesDisabled、WritesDisabled、ActorMismatch、WrongSession、InvalidRequest(null)。然后同客户端帧九次 Start 发送，等待后看到八条不同 RequestId 的 WritesDisabled 仅记录 BurstEightResponsesCandidate。真实网络可能跨主机时间窗口；必须独立核对主机 trace 同一 peer/窗口八次 BodyReadAttempted=true、一次 RateLimited/false，不能由少收到一条推断第九条没读。

BasicProbesCompleted 只描述上述候选。FullNet02Accepted 恒 false、ReconnectVerified 恒 false；TitleSessionCleared 仅观察本地标题快照 Active=false/SessionId=null/QueueCount=0，不等于完整旧队列或重连验收。trace 溢出、诊断失败、世界变化需保留本轮证据并新轮重跑。

## 待执行矩阵与结束

2026-10-11新增只读准备保存核验：两端退出后执行下面命令，摘要只证明宿主两XML及直接farmhands中的已创建目标，仍须独立连接/输入/原生保存与退出审计。未命名空小屋占位允许保留，不要删除占位或手工修XML来让检查通过；详见 [准备验证](validation/P1-network-preparation-2026-10-11.md)和 [BUG020](bugfix/BUG-2026-020-preparation-empty-placeholder.md)。

```powershell
python tools/check_network_preparation.py --save "<专用保存目录>" --farm-name XYSNET02 --expected-world-id 450604963 --client-name XYSNETClient --favorite-thing tofu
```

| 阶段 | 必须核对的独立证据 | 当前状态 |
|---|---|---|
| 真实专用身份绑定 | 两份真实 XML 身份/源哈希、四份 before/prepared/当前哈希及成功 apply 审计；独立创建、小屋、运行版本另核 | 已核身份450604963并成功apply；小屋使用/运行版本不由工具验收 |
| 禁用首次客户端创建 | 实际加入同一世界、Farmer创建、专用准备保存与新磁盘基线、退出两端 | 10-11完成、10-12补核通过：两端移动人工确认、春24已创建客户端/真实CLI、新两XML基线、双端原生save/先client后host退出及85项审计；六config/旧六save/包不变 |
| Tick失败观测 | 本Tick读取成功后的保留、读取前null、无旧Tick借用、Failure拒绝及实际文件落盘 | 10-04两负例已核；10-12客户端离开后主机超时保留Protocol、最终实际pair预期拒绝，标题仍保留Failure。API内部异常/同进程重连/I/O失败未测 |
| 匹配版本实际连接 | 主机/客户端实际 peer、Hello challenge 与对应 Welcome、共同 session、WritesEnabled=false | 10-12实际原活动双报告配对通过 |
| 五种固定探针 | 两端 RequestId/null、真实拒绝路径、本地库存/可见 raw 不变、角色/构建相符 | 10-12实际5项及各端引用/值/raw通过；VisibleMachineCount均0，不代真实机器授权 |
| 读取前额度 | 主机同一 1000ms 窗口八读一次 RateLimited/未读，不只核客户端八回 | 10-12实际host trace/原双报告pair核8读1未读通过 |
| 16 帧预算/128 邮箱 | 实际事件入队/取出/拒绝/旧队列轨迹；不能编造诊断中未实现的每帧聚合字段 | 未执行，基础夹具不自动验收 |
| 未握手/Hello 重试 | HandshakeRequired、5 秒沿用 challenge；需要独立受控场景，基础夹具等 Ready 后才发送 | 未执行 |
| 断连/同 ID 重连 | 新连接 GUID/challenge、不借用旧 Ready，主机世界 session 不因普通断连轮换 | 未执行，另立 episode，不拼报告 |
| 标题/换世界/旧队列 | 新 generation/session、原队列清除、旧标记项拒绝、两端原值保留 | 10-12同进程标题Active=false/session=null/queue0/peers空/generation+1局部已核；host离开后Failure保留，负载旧队列/换世界/重连仍未测 |
| 错版/分屏/方向/来源 | 不读错误方向/来源，不形成响应循环，不接事务；独立负例配置保留匹配正例 | 未执行 |
| 收尾完整性 | 两端结束、开关关闭、报告归档、专用农场和旧三组存档/包哈希核对 | 10-12 Basic收尾通过：先client后host返回标题/关闭、CIM0、八save保持春24基线、六config原字节禁用、85项一致，原活动/最终失败/标题报告分别归档 |

停止/中断前归档两端各自报告、配置 hash、可归属日志与进程查询，关闭两端主模块探针和 GameHarness.Enabled/OpenPreview，确认进程结束；不要保存诊断中的任何临时世界变化。Saved=false 仅表示夹具没调用保存，独立文件审计才证明保存未变。对网络前已延迟/伪造元数据的情况，本地标记不构成强认证，生产写入继续关闭。

不把不同构建/进程/连接的通过项合并为完整矩阵，不删除失败报告。最后更新 HANDOFF 具体执行阶段、开关、进程和下一步；完整个人存档与日志不提交。10-12已有实际原活动双报告Basic配对及独立最终失败拒绝/标题局部观测/收尾，见 [本轮记录](validation/P1-network-basic-2026-10-12.md)；没有本轮提交、推送、PR或合并。

## Schema2 双端报告配对命令

当前夹具报告schema2包含Baseline（首次真实Ready的本地run/session/world/disconnect/localID/owner线程/peer/序号/丢弃量）、CurrentInventorySha256/CurrentVisibleMachineRawSha256/InventoryReferencesUnchanged及BurstSentAfterSequence。Trace包含本地ConnectionId、Quota(StartMilliseconds/Count/Allowed)，由实际TakeQuota产生，不从事件时间跨度猜测。标题写network-probe-title-result.json，保留active的network-probe-result.json；在活动世界等待客户端Basic候选、主机消息消费完毕后归档两端原字节，再结束/保存标题证据。

```powershell
python tools/check_network_reports.py --host .local/net02-evidence/host-network-probe-result.json --client .local/net02-evidence/client-network-probe-result.json --output .local/net02-evidence/basic-pair-audit.json
```

上述路径是归档示例；10-12实际输入在`.local/net02-basic-2026-10-12/active`，原双报告pair通过，确切命令/原SHA见本轮记录。工具只接受schema2活动报告、两端无Failure/写入/保存/越界能力声明且当前queue0；要求trace序号1起完整、Evicted/QueueRejected=0。旧schema1、标题/失活、换连接、丢失trace或附加无关流量均拒，不补默认。各端库存/可见raw与各自基线相等，不能要求两端hash/机器数相同；count0仅证明空集合。

两端必须不同进程/run、同生产DLL/独立fixture/session、互为peer；本地generation/线程/时钟/ConnectionId不跨端比较。核对真实Hello SendAttempt、host HelloAccepted/client WelcomeAccepted challenge，五项对应拒绝及紧邻发包。Malformed null-ID只能该用例区段的唯一拒绝，不从任意迟到记录挑选。当前host peer.Challenge与client challenge不同是正常，不拿两端peer.Challenge相等作要求。

burst核九次实际client ProbeAttempt+SendAttempt、八条独特对应拒绝；host同窗口实际Count1..8八读、唯一Count8/Allowedfalse/BodyReadAttemptedfalse/nullID和reason的RateLimited，且窗口没有额外读/drop。第九条未读，不给它编RequestId。工具同时检查各本端全部quota计数，不能重复/跳数；窗口状态key不含全局disconnect标记，因为另一个peer断连不重置当前peer额度。

输入只读，最大2MiB且读入有界，重复JSON键/非有限值（含1e999）拒；确切enum名称或C#ordinal可读，bool不当int。输出仅.local严格后代、禁止覆盖输入，包含两份原文SHA。通过仅BasicProbeEvidenceVerified=true、FullNet02Accepted=false，不修改夹具候选或计划总验收。输入无效退出1且不写新摘要；旧摘要仅属于它的输入SHA，I/O失败不承诺自动回滚。SDK送达/恶意身份认证、16预算/128满邮箱、重试/重连/标题/换世界、版本分屏、真实机器及磁盘哈希仍单独验证。历史40专项是人工报告，85核心/77Python属于 [前轮配对验证](validation/P1-network-pair-2026-10-02.md)；28身份专项/105Python见 [工具验证](validation/P1-network-fixture-2026-10-02.md)，10-02夹具构建/单项检查见 [观测增强](validation/P1-network-observation-2026-10-02.md)。10-04实际失败验证仅执行第一份Host的endpoint拒绝，没有双端pair，见 [失败记录](validation/P1-network-failure-2026-10-04.md)；各阶段不能拼成完整双端实机通过。
