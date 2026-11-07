# NET02客户端准备与只读保存校验（2026-10-11）

更新至2026-10-12：原XYSNET02/450604963世界的客户端首次创建/原生准备保存与退出审计已通过，新基线春24年1。所有探针/夹具在本轮始终禁用；这不是模组Ready/探针/pair验证。上一轮[输入短测](P1-input-observation-2026-10-11.md)是独立主机D/Esc观测，NET02/P1仍未验收，P2冻结。下方启动及连接检查点保留原时点。

## 新准备基线与启动（历史检查点）

分支feat/p1-game-adapter、HEAD8f2468d83b43fb805073f73bbc8b989cf716852e，保留累计未提交树；无本轮提交/推送/PR/merge。继续前CIM为0，85个非source已知构建/runtime/curated/roles/config/save/ZIP路径与输入关闭后的final-audit.json完全相同；新目录`.local/net02-preparation-2026-10-11/{before,baseline.json,preflight.txt}`保留原字节。`p1-net02-preparation-baseline-2026-10-11.txt`exit0：春23年1、无目标客户端、六指定config禁用、实际只读configure_network_fixture身份预检通过。旧六save/春1春2和输入报告证据未覆盖。

新host30744/client18620分别从隔离runtime以独立--mods-path启动，版本仍游戏1.6.15 build24356/SMAPI4.4.0/CP2.9.0。host-startup-SMAPI.txt保留客户端启动前主机日志；双端实际日志分别为SMAPI-latest.txt及SMAPI-latest.player-2.txt，须按Mods路径/Log started时间/CIM归属，不拿后者当host。当前两端最近日志停标题/locale，尚无本轮连接/创建/保存证据。启动日志可能含Steam成就ERROR，不宣称零ERROR；根因修复/SDK/包没有因本轮工具改动重建。

已向操作者合并请求：主机合作主持XYSNET02；客户端加入127.0.0.1/空小屋，名字XYSNETClient、favorite tofu；确认两端可移动，两人睡觉到次日保存后先client再host关闭。当前会话没有界面控制工具，等待实际操作；不能由请求已发/进程启动计算客户端准备通过，也不能自动把旧创建画面接续成新创建。

## 新只读工具与数据契约

[check_network_preparation.py](../../tools/check_network_preparation.py)读取指定两份原生XML，复用现有fixture的有界/no-DTD/普通非链接输入读取及直接身份唯一性校验；不读取其他个人存档、不写保存/config、不创建或加载游戏。stdout只输出脱离完整XML的摘要，世界ID/宿主name及ID在主保存与Info核一致；名字来自调用者指定，绝不用目录标签代农场或世界ID。

按本机真实1.6 SDK结构，SaveGame.farmhands的直接Farmer在加载时进入netWorldState.farmhandData。仅这份直接名单中的唯一目标名/customized=true/非零signed Int64且与host及其他Farmer不同才证明持久化；空小屋占位false/空name/ID0不计目标但允许存在。客户端自身farmName可能空，不强制等于host farmName，不凭嵌套Cabin或任意name标签证明加入。重复/未知结构、错误角色ID、错误指定favorite、原生日期、读期间变化均拒绝。

工具输出clientFarmerPersisted=true只证明两份稳定XML中对应角色：nativeConnectionVerified/clientInputVerified/nativeSaveSequenceVerified/gameExitVerified/fullNet02Accepted始终false；日志/实际操作/CIM和旧六save审计独立补充，不能将这个摘要交给network报告pair冒充schema2诊断。

两端实际退出后执行：

```powershell
python tools/check_network_preparation.py --save "<当前专用保存目录>" --farm-name XYSNET02 --expected-world-id 450604963 --client-name XYSNETClient --favorite-thing tofu
```

无输出文件参数，记录stdout时使用新的.local摘要文件，不能覆盖旧证据。重复读取发现可观察的并发变化会拒绝，但不是锁或跨文件保存事务保证，因此原生保存完成/进程退出必须另核。后续SDK保存结构改变时同步本文及人工回归，不扩展到未知版本后默认为通过。

## 已执行检查与限制

- 新模块首批10人工检查通过；追加空占位组合的具名回归实际1error/exit1，原因是初稿对所有name使用严格非空文本函数；修后同命令1OK/exit0、新模块11OK/exit0。源码、断言、完整日志和缺失修前字节备份的边界见[BUG020](../bugfix/BUG-2026-020-preparation-empty-placeholder.md)。不是旧输入原因，也不是后来真正客户端保存失败。
- 命令`python -m unittest discover -s tests -p test_network_preparation.py -v`日志`.local/p1-net02-preparation-tests-final-2026-10-11.txt`exit0。测试涵盖直接列表/占位/宿主Info/ID/日期/UTF16/DTD/并发变化/CLI输出；人工成功XML不是真联机证明。
- 新工具对本轮**备份的春23原保存**实际运行CLI，`p1-net02-preparation-before-save-refusal-2026-10-11.txt`exit1且无正摘要，理由直接名单中无目标名。作为原生负例可用，不是BUG020修前红，不改备份或实际Saves。
- 全Python实际`python -m unittest discover -s tests -v`116项OK/31.388s/exit0，日志`.local/p1-net02-preparation-python-full-2026-10-11.txt`（原105＋新11）；包含人工内容/工具/维护/配对等，不是游戏SDK测试。没有改生产C#/目录数据或游戏DLL，不重复89核心/SDK编译/ZIP/历史实机。维护/差异检查在文档收尾执行，不先记通过。

### 连接检查点

两端新日志后来已观察主机starting LAN、客户端连接被批准及farmhand Context同world450604963/春23年1/2在线。客户端加载名最初为`_450604963`，不能据此把名字为空阶段当角色创建完成；操作者的填写及最终XML另核。两进程此时仍30744/18620，诊断禁用、没有probe报告或pair验收。此前“停标题”只描述启动检查点，不能作为当前窗口画面；无需再次启动新实例。

## 后续独立验收与中断

当前保存/进程/操作者状态以[HANDOFF](../HANDOFF.md)实时重核。收到保存/关闭回复后先确认两进程0，归档分别可归属host/client日志，读取新两XML/运行只读工具，核日期推进、ID及真实创建客户端与日志互相支持；保留新原字节/hash，不覆盖旧春23基线，旧三组六save须不变。六指定配置仍禁用，包/profile身份保持本次构建；启动若有配置格式变化按字段和原字节分别记录，不静默接受值变化。

只有实际Farmer和原生保存/退出新基线已核，才进入另起进程的双端诊断；固定probe/synthetic指纹、Basic、quota和full生命周期须分别判定。没有得到操作者回答或文件证据时，保持“准备保存待验证”，不把时间推进或单端保存算双端成功。

## 原生准备保存与关闭审计（10-12补核）

操作者实际回复“两端能移动，已到次日保存并关闭”。10-11主机23:54:54加载春23，23:55:16批准客户端并交换SMAPI上下文；客户端同刻记录同world450604963、Farmhand/2在线。实际Farmer与主机ID分别和原日志一致，完整ID不放公开文档。两端23:56:32 before save、23:56:34 after save春24年1；主机另有SaveGame.Save completed without exceptions。客户端23:56:41 ClosedGame/Disposing，主机23:56:44 ClosedGame/Disposing，CIM独立实查0，没有ReturnedToTitle证据。

只读CLI实际命令与上方相同、指定真实专用Saves文件夹，`.local/p1-net02-preparation-actual-save-2026-10-11.txt`exit0：直接farmhands唯一XYSNETClient、customized=true、favorite tofu、独立非零signed Int64；主机两XML身份一致，日期春24年1。原JSON连接/输入/保存流程/退出/Full flags保持false，补充结论来自独立用户回复/原生日志/CIM及哈希审计，不能修改工具原摘要来扩大其证明范围。

`.local/p1-net02-preparation-closeout-2026-10-12.txt`实际exit0：85项只有专用主XML和SaveGameInfo两处变化；其他83项包含旧三组六save、六指定config原字节/禁用值、生产ad9/四处夹具2bef/profile清单和两个ZIP，均保持准备前。主XML SHA `5f3bcc55f635be4ac3afdd03f8ba72077dd008706623738f2d4a4b0dc130fa70`，Info SHA `0109f8b0e26a556001072c439ba2d8bd06119764e442fdb2faff3b9da84beca0`；实际configure_network_fixture只读预检exit0。双端完整日志各一条Steam成就启动ERROR，无NullReferenceException，不称日志零ERROR或所有玩法已验收。

新85项原字节、两端完整日志、原摘要/preflight/final-audit/prepared-baseline保存在`.local/net02-preparation-2026-10-11/after-save`，旧before春23不覆盖；one-shot关闭脚本已执行，禁止盲重跑。公开只提交此脱敏记录，不提交个人XML/完整日志。当前clientPreparationSavedVerified由独立关闭审计记true，actualDiagnosticPairPassed/fullNet02Accepted仍false。下一轮须新证据目录/四config备份/新进程，选择已有XYSNETClient，诊断期不保存。
