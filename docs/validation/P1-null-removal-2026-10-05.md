# P1 空移除目标回归与修后实机检查点

2026-10-05，Asia/Shanghai。分支 `feat/p1-game-adapter`，HEAD 基线 `8f2468d83b43fb805073f73bbc8b989cf716852e`；无新提交。BUG019五项实际SDK CLI先红后绿，修后补充13项明确产物定位通过；生产构建/核心85/catalog40/30及停止部署审计通过。新鲜主机已加载专用世界，运行中72项检查点通过，用户最新回复修后仍无法移动；NRE消除/时钟恢复与残余键盘故障分开，原因继续调查，完整实机未验收；本轮客户端未启动，客户端持久化/真实probe pair/完整NET02/P1未验收。

本轮生产空目标修复与 [10-04两条夹具失败路径](P1-network-failure-2026-10-04.md) 分开。那两条是预期负例，不能充当本次真实事故的修前红。完整根因、具名断言和回滚见 [BUG-2026-019](../bugfix/BUG-2026-019-null-removal-target.md)，唯一动态现场和后续步骤见 [HANDOFF](../HANDOFF.md)。

## 现场与保存边界

诊断禁用的旧 host9712/client56700 普通合作连接已观察互收SMAPI上下文、世界450604963/2在线。第二次填写后实际创建overlay消失并出现客户端小屋画面，不能再写创建从未观察；实际键盘移动和持久化是另两项。用户报告角色不能移动/键盘无法操控，主机日志TryGetKind→BlocksRemoval→patched checkDestroyItem→NPC寻路空引用。关闭前主机春1/02:00卧床；“春2保存”反馈无磁盘证明，两XML仍春1/年1、无持久化客户端Farmer，八save同baseline。

原始证据在 `.local/bug019-2026-10-05/{before-host-SMAPI.txt,before-client-SMAPI.txt,error-summary.json,save-checkpoint.json,before-source}`；栈帧匹配数47425不等于独立异常数。两故障窗口按client后host关闭，sky无窗口、CIM无游戏/SMAPI，没有ReturnedToTitle或保存成功证据。生产修复/部署在这一停止阶段完成，不修改旧现场日志或旧validation的时间结论。

## 实际 CLI 回归、构建与检查

新 [GameAdapter](../../tests/GameAdapter/README.md) 直接引用实际生产代码和合法SDK/Harmony，在net6 CLI运行，不打开游戏/保存。五项空输入直接验证分类、state、移除保护、实际 private prefix 放行/ref不变，以及实际 prefix注册后的原生 `checkDestroyItem(null, tile, false)` 返回false；后者只用SDK空分支，不初始化世界，finally撤销自己的补丁。

| 检查 | 实际结果/原始日志 |
|---|---|
| 修前空目标5项 | 0通过/5失败，均实际生产空引用，退出1；`.local/bug019-null-object-adapter-red-2026-10-05.txt` |
| 修后相同5项 | 5通过/0失败、退出0；`.local/bug019-null-object-adapter-green-2026-10-05.txt` |
| 生产最终Release | 0警告/0错误、退出0；`.local/p1-bug019-production-final-build-2026-10-05.txt` |
| 纯核心 | 85项通过、退出0；`.local/p1-bug019-core-2026-10-05.txt`；不调用SDK或真实库存 |
| catalog | 40物品/30配方、闭合source/use与登记/本地化通过、退出0；`.local/p1-bug019-catalog-2026-10-05.txt` |
| 补充SDK保护与实际产物定位 | 最终13通过/0失败、退出0（5.431s）；`.local/bug019-null-object-adapter-final13-location-2026-10-05.txt`；显式加载指定生产DLL且循环外核Location，日志SHAad9。包括普通O/BC、两设备Idle/Processing/Ready/Corrupt/Missing/Future/Mismatch与ID/raw/metadata引用不变；这些是修后补充，无各自旧红 |
| 全Python/旧实机矩阵 | 本轮未重跑105全Python、Memory16/Interaction13/Persistence6、World20；旧通过为历史 |
| 维护/差异 | 本轮最终检查待根任务统一执行；不从旧维护通过推导本轮通过 |

CLI复跑入口（证据使用独立新文件名）：

```powershell
dotnet run --project tests/GameAdapter/GameAdapter.csproj -c Release "-p:GamePath=D:\code\stardew\.local\validation-game" "-p:SmapiPath=D:\code\stardew\.local\validation-game" -- --game-path "D:\code\stardew\.local\validation-game" --smapi-path "D:\code\stardew\.local\validation-game" --production "D:\code\stardew\src\XiangyeShiling\bin\Release\net6.0\XiangyeShiling.dll"
python .local/run-network-fixture-check.py p1-bug019-production-final-build-2026-10-05 -- python tools/build.py --game-path .local/validation-game
python .local/run-network-fixture-check.py p1-bug019-core-2026-10-05 -- dotnet run --project tests/MachineCore/MachineCore.csproj -c Release
python .local/run-network-fixture-check.py p1-bug019-catalog-2026-10-05 -- python tools/catalog.py
```

实际红/绿及补充运行使用相同GameAdapter命令，日志独立不覆盖；原五红（9.016s）/五绿（4.003s）、10绿（6.574s）及未核Location的13绿（5.245s）均保留，不以最终日志倒写早期覆盖。具名测试/断言见BUG019及Program；普通/Idle非空只核API/prefix，不运行未初始化地图的原生正文，受保护状态才另核实际注册prefix拒绝。CLI不代完整NPC update或移动。修复为nullable先null返回false/prefix放行空分支；false输出不可消费，无schema/ID/依赖/写权限变更。

## 停止部署、包拓扑与哈希

`.local/bug019-2026-10-05/deployment-audit.json` 保存69项before/after：恰5个登记项变化（runtime/host/client生产DLL、开发ZIP、currentprofiles），其余64相同；六config原字节禁用、八save均保持本轮before，gameProcessesZeroBeforeApply/profilePreflightPassed=true。它不覆盖原列表之外的新文件，不能声称整树仅5项变化。

补充 `.local/bug019-2026-10-05/deployment-topology-audit.json`：原52个包相关文件与更新后的已知审计匹配，当前55个；新增恰为runtime/host/client的生产PDB，与本次build PDB相同，没有移除。合并69已知项与3新增文件为72个当前登记覆盖项，不等于完整个人目录审计。初稿只预期runtime一处，实际发现3处后断言退出1；修正预期的正式`.local/p1-bug019-deployment-topology-final-2026-10-05.txt`退出0。初稿是本地审计遗漏，不是生产缺陷回归红。profiles.files现15包文件（原14加生产PDB），预检旧“14包”标签不外推为当前清单数。

| 产物 | 本次SHA-256 |
|---|---|
| 生产DLL/build/runtime/host/client | `ad9d96199a86601bdb253eca628e1fe34464c5ee5e979e41cfac0e5176d17cd1` |
| 生产PDB/build/runtime/host/client | `64e7d90d4b0a320ab0e94ca1f5a7b499414c822d2ac7d4b9b1119278d669ab94` |
| GameHarness（未改） | `78026dd6a3f703fcb26bef41590289dab8505ae7621ab4813503666787582693` |
| 开发ZIP | `92402877ba5bd1bc3fd24403de41e7137fbeec37be8fd61a541d4201492f54c9` |
| current profiles.json | `99f4d0880bd8838b089b55fbd2ec9b8f30b7857ddee797569bd6b164a79a3815` |

完整before原字节、旧包/配置/保存、源码现场及已执行一次性脚本留本地；不盲重跑覆盖证据。包不含游戏SDK或个人存档；PDB是本次自有生产调试符号，夹具产物未重建。

## 修后游戏现场与未执行矩阵

修后新鲜host PID56016/窗口332594已合作加载XYSNET02/450604963、春1/年1；[main player]画面06:10→13:40→14:40正常推进，玩家仍在床。`.local/bug019-2026-10-05/fixed-host-loaded-checkpoint.json`与fixed-host-loaded-SMAPI.txt、`.local/p1-bug019-fixed-host-checkpoint-2026-10-05.txt`实查72项匹配、六config禁用/八save同baseline、世界一致/无ERROR，userMovementVerified/nativePreparationSavedVerified/actualReportPairChecked/退出验证均false。自动Escape一次无菜单效果（非用户物理Escape），用户已回复修后仍无法移动，残余输入原因继续调查。此运行中审计不代表退出，也不把时间推进/未见旧error写成输入恢复；客户端未启动。

| 能力 | 当前证据与限制 |
|---|---|
| 原生空目标与prefix | SDK CLI真实入口先红后绿；不代完整NPC更新 |
| 修后实际玩家移动 | 用户已回复仍无法移动；残余原因待调查，时钟恢复/NRE消除不证明键盘恢复，完整实机未验收 |
| 修后普通合作/客户端Farmer | 修前普通连接及创建后小屋画面为历史；修后客户端未启动，实际移动/持久化待分别核 |
| 专用准备保存 | 后续主机已原生保存春2，专用两XML变化；无客户端Farmer，包含客户端的准备保存仍未通过，旧六save不变 |
| NET02基础/pair/完整P1 | 未运行探针/pair，Full未验收；普通连接不能替代模组Ready/配对 |
| 生命周期/完整设备矩阵 | 同进程重连/标题、队列/额度/错版/分屏、World20及旧完整机器流程未重跑 |

接续先调查新host输入链/核实际移动与日志；按键绑定、焦点、原生限制/输入屏蔽是未证候选，必要时另立被动SDK观测。通过后再诊断禁用完成首次客户端创建及专用初始保存；退出两端、确认0、留新的保存/配置基线后才另行开启probe。无需重建450604963或使用源/UI/Stage3。未做提交/推送/PR/merge或远程CI，不存在本次修复SHA/完整正文/远程合并结果。

## 物理Escape中断与后续主机保存

用户在恢复窗口、英文输入法、按住D和Esc后仍回复移动和菜单均无响应。之后Computer Use明确报告用户物理Escape停止，本轮不再调用界面工具；没有关闭窗口或进程0证据。子任务已中断，停止前未见InputObservation源文件/入口/构建；观测器仅为下一步计划，不能声称已有运行中按键读数。

后续主机日志10:38:46–48显示原生SaveGame.Save无异常完成、进入春2；两真实XML核春2/年1/450604963，仍无XYSNETClient。最新host-only-spring2目录保存原字节、可归属host日志及audit.json，p1-bug019-interruption-2026-10-05.txt实际exit0：72项仅专用两save变化，旧六save/六config/其余70项不变；host56016仍运行、无ERROR。这是单端主机保存，不是首次客户端持久化或退出审计。此前八save相同记录只适用于当时，不反写旧JSON。

绑定WASD/E/Escape正常、代码没有禁用输入不等于焦点/overlay/聊天/原生门禁正常。下一轮先核最新磁盘/进程并恢复界面操作，再实现被动SDK观测；部署须停止游戏，禁止强置移动字段或注入输入替代真实测试。网络开关继续关闭，完整输入问题尚未解决。
