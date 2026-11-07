# NET02真实双端基础探针（2026-10-12）

当前状态：本轮host13864/client20612真实Ready/五probe/八读一未读及原始活动双报告离线配对已通过；同进程双标题清理局部观测、进程0/八save不变/六config原字节禁用及85项收尾审计均已通过。主机在客户端断开后另记超时失败，原文保留并被离线pair预期拒绝，不覆盖先前通过的活动输入。FullNET02/P1仍未验收，P2冻结；首次客户端[准备保存](P1-network-preparation-2026-10-11.md)新春24基线独立保留。

## 环境与边界

分支feat/p1-game-adapter，HEAD8f2468d83b43fb805073f73bbc8b989cf716852e；累计未提交树保留，无本轮commit/push/PR/merge/远程CI。游戏1.6.15 build24356/SMAPI4.4.0/CP2.9.0；生产DLL ad9d96199a86601bdb253eca628e1fe34464c5ee5e979e41cfac0e5176d17cd1、四处夹具2bef01125ade5bc063a38bc8e3478b6f577751ae863b6211eae628ae02c4e0e5，未因启动本轮重建。

本轮不准备材料/设备、改库存/raw/位置/时钟，不自动加载或保存游戏。Production可写能力继续关闭；probe的Accepted只表示固定合成意图传输，不代表扣料、成品或实际机器授权。Basic只验证有限握手/五拒绝及同peer实际额度，不能代生命周期/队列满/恶意身份/联机事务或P1全部验收。

## 启用与运行检查点

停止进程时重新核85项均与准备后基线相同、实际只读profile身份预检通过。新`.local/net02-basic-2026-10-12`保留85项before原字节、baseline、四config候选和enabled-audit；`p1-net02-basic-enable-2026-10-12.txt`实际exit0，恰四角色配置变为主模块probe=true/Network夹具Enabled=true，OpenPreview=false/450604963/Host与Client，其余81项含八save不变，runtime两配置仍禁用。四个单文件替换不是整体事务；该one-shot脚本已执行，禁止覆盖/重跑或边运行边改配置。

两个新可见窗口从隔离runtime以独立--mods-path启动；host13864先到标题/locale，host-startup-SMAPI.txt已归档，再启动client20612。SMAPI普通ErrorLogs中host为SMAPI-latest.txt/client为SMAPI-latest.player-2.txt，归属须核Mods路径/Log started/CIM，不能借前一轮日志。向操作者请求主机加载XYSNET02春24、客户端随即加入127.0.0.1/已有XYSNETClient，保持约20秒、不要操作物品/睡觉/保存。SaveLoaded起等待Ready最多60秒，不由启动时长判失败；失败后保留原报告，新一轮不能拼接通过项。

## 当时的检查与结束步骤

读取两端本轮network-probe-result.json，核不同HarnessRunId/进程/同world与生产hash，保留原Failure/Protocol及所有flags。真实Ready/五probe/九次burst候选出现且主机消费后，归档两份原字节与可归属日志，用`python tools/check_network_reports.py --host <归档host> --client <归档client> --output <新的.local摘要>`独立配对。失败摘要不生成正例结果，不改原报告、不从旧trace找缺项。

归档后请求先client再host关闭、不保存；CIM0后核八save与本轮before、85项除四config无变化，再恢复四config本轮before原字节，其他两份不改。归档唯一已核本轮报告后移出Mods，不删未知文件，恢复disabled后重跑只读profile预检。具体PID/配置/原报告SHA/失败与结果随检查点写HANDOFF，不能仅以Saved=false证明磁盘不变。

## 活动世界真实配对结果

操作者确认两端进入并保持打开；主机00:08:23加载春24、00:08:33批准原客户端，客户端00:08:39记录同world450604963、Farmhand/2在线及实际GetApi读取。两端本轮Schema2/Episode1，角色及ProcessId分别为Host13864和Client20612，独立HarnessRunId/同生产hash/共同session/互为peer/Ready=true，Failure=null、QueueCount/QueueRejected/Evicted=0。Host是观察者，Step0/BasicProbesCompleted=false正常，不能替主机编造客户端六项通过。

客户端Step6/BasicProbesCompleted=true，Passed为Start、Collect、ActorMismatch、WrongSession、Malformed、BurstEightResponsesCandidate；这只是原夹具候选。`p1-net02-basic-active-archive-2026-10-12.txt`exit0将原双报告/归属日志/四配置及snapshot-audit留`.local/net02-basic-2026-10-12/active`，实核八save及所有非角色config路径保持本轮before不变、四config值匹配opt-in。

随后实际执行：

```powershell
python tools/check_network_reports.py --host .local/net02-basic-2026-10-12/active/host-network-probe-result.json --client .local/net02-basic-2026-10-12/active/client-network-probe-result.json --output .local/net02-basic-2026-10-12/active/basic-pair-audit.json
```

`p1-net02-basic-real-pair-2026-10-12.txt`exit0，BasicProbeEvidenceVerified=true/ProbeCasesVerified=5/BurstHostBodyReads=8/BurstHostPreReadQuotaDrops=1/FullNet02Accepted=false。工具核真实Hello发送/接受及Welcome、实际发包/对应拒绝、同peer固定窗口Count1..8八读与一次Count8/Allowedfalse/BodyReadAttemptedfalse限流；包含两端各自库存值/引用和可见机器raw指纹不变。两端VisibleMachineCount=0，只证明空可见集合，不代表实际机器准入/写入验证。

原Host报告SHA `21e64fb6685c0416fa4f392a5db39434211a4eefd5396f24c9ba19f8211413e7`；Client SHA `e20825182179ac190f722f084b78ee09af5f27c12ae2917985d1228dfdf62a26`。原报告及配对摘要不修改flags；随后断连/标题另有活动/title报告，分别归档，不覆盖该已配对输入。返回标题只提供本地失活/session=null/queue0的局部观测，不证明旧队列有负载或重连已验收。其后标题/退出/配置恢复结果见下节。

## 同进程标题观测及最终审计

操作者回复“两端已返回标题并关闭”。原日志client00:10:51 returning to title、00:10:52 ExitedToMainMenu、00:11:11 ClosedGame/Disposing；host00:10:52 Player quit、00:11:01 returning to title、00:11:02 ExitedToMainMenu、00:11:12 ClosedGame/Disposing。最终CIM0，两端全日志无before save/SaveGame.Save called/NullReferenceException，各一条启动Steam成就ERROR。操作与日志支持客户端先返回标题、随后主机，再先client后host关闭；不称日志零ERROR。

两份title各自仍是本轮相同HarnessRunId/ProcessId/Episode1/Protocol.RunId，ReturnedToTitleObserved/TitleSessionCleared=true、Active=false/session=null/queue0/peers空、WorldGeneration2→3；host DisconnectGeneration0→1，client仍0。host的最终活动和title报告Failure=`Timed out waiting for a real connected and ready peer.`，发生在客户端离开后、原Ready peer消失且先前等待deadline已经过去；夹具中止当前观察，不在同一episode自动重连。client最终活动仍为先前相同SHA，title Failure=null。这是保守中止及局部标题观测，**不能写双标题无Failure或完整生命周期通过**；不修改失败状态、不给它新增虚假的修复SHA。

最终host活动SHA `61c9ed57c7e78e441e77649f8832c8a0da0e00d1209bda210a37763316a00bfd`，host title SHA `7a2614525086d812320948e792286a1604f856585ef1cb26852a41d5c3bfabdd`；client title SHA `bd30bffbe45c710b301a73e2caba674ea136cfecaed2482ccda486f784dbbac5`。四份最终/标题原报告、retired原文件、完整日志、85项恢复前snapshot、final-audit/preflight在`.local/net02-basic-2026-10-12/after-exit`；`active`中的已配对输入不改。使用最终host/client活动报告再次执行CLI，`p1-net02-basic-post-disconnect-pair-refusal-2026-10-12.txt`实际exit1/REJECT，未生成post-disconnect-pair.json；这是预期负例，不是本轮成功配对失败或产品新bug回归红。

`p1-net02-basic-closeout-2026-10-12.txt`实际exit0：八save原字节保持春24准备后基线；只恢复本轮四角色config before原字节，其余两份不改，六份均禁用，85项最终全部与before相同。归档且核同进程归属/原hash后，仅四份本轮已知报告移出Mods，未知文件不删；恢复后configure_network_fixture实际只读预检exit0。one-shot enable/archive/closeout脚本已执行，不盲重跑、清空或覆盖目录。没有新增C#改动，未重复编译/SDK13/89核心或历史实机；本轮工具11/全116检查属于准备阶段源码验证，文档维护/差异最后另核。

下一步独立准备同ID断连重连、新连接challenge/不借旧Ready、主机session保持及负载队列/换档等矩阵；现有Network观察episode在连接变化时故意中止，不能以重新加载后新episode的片段拼接证明旧episode通过。完整P1世界动作与正常地图入口仍待，生产联机写入关闭。

文档维护`p1-net02-basic-maintenance-final-2026-10-12.txt`实际exit0/0提交正文检查；tracked差异和13项本轮未跟踪文本空白检查exit0，日志`p1-net02-basic-final-whitespace-2026-10-12.txt`。独立最终readback实际exit0、当前85项一致/CIM0/拒绝无输出；此前内联转义查询失败及修正边界留HANDOFF，不误作实机回归失败。源码检查116/11及本次实机结果没有因后补文案重跑。
