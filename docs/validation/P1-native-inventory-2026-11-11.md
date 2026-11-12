# P1 联机背包归属实测 · 2026-11-11

在 Windows、Stardew Valley 1.6.15、SMAPI 4.4.0 和 Content Patcher 2.9.0 中启动两套隔离 Mods。主机从专用 `XYSNET02` 加载现有世界，客户端经原版局域网接入该世界的已有农工。生产 DLL 为 `BBD9766E5014077C4219A49EDBED6F350A51A6D05F323241960107043B5E27CD`。生产联机加工仍关闭。两端启用同一测试夹具，并互认对方的运行标识。

客户端在自身第 12 格放入带本轮标记的 3 个木材。主机读到相同物品、数量和标记，从实际在线 Farmer 的库存对象将该格替换为同一标记的 2 个木材，并在本地读回。客户端未观察到数量变成 2，双方均在限定时间内失败退出测试。原版连接、客户端写入到主机可见及主机副本可修改均有实测证据。主机写入客户端所有的库存后同步回客户端这一验收项未通过。

原报告及核对结果保存在 `.local/p1-net03-native/native`。两端报告 `Completed=false`、`SavingObserved=false`。退出审计 `IntegrityPassed=true`、`ScenarioPassed=false`，47 个存档文件无差异，隔离夹具已禁用，旧游戏进程仍在。原版进程改写了共享的 `default_options` 和 `startup_preferences`，本轮只有文件哈希的前后证据。下轮隔离测试应在启动前保留原始字节。

后续协议需遵循背包所有者在其本机执行扣料和入包、主机权威更新设备的边界。先用专用双端档验证准备、确认、断线与恢复的守恒，再决定可写实现。单凭握手和主机本地副本写后核对，无法证明双人事务成功。

## 2026-11-12 由库存所有者提交

两端沿用同一专用世界和隔离启动器，在 `NativeInventoryOwnership` 配置中同时设置 `NativeInventoryCommitByOwner=true`，以独立消息类型区分此次验证。客户端先放入带本轮标记的木材 3 个。主机从原版远端 Farmer 读到该格后，两次发出同一提交请求。客户端在本地只提交一次，将原格替换为同一标记的木材 2 个。主机从原版库存读到该变更才发确认。客户端清空测试格，主机在原版库存读到清空后完成验证。

实际主机 PID 21284 与客户端 PID 27892 均报告完成，提交阶段各有一条记录，标记与数量一致。两端未保存，隔离夹具已禁用。退出审计的 `IntegrityPassed` 与 `ScenarioPassed` 均为 true，47 个存档文件哈希一致，原进程 16668 保留。原版写回了共享启动偏好，启动前的字节与审计均留在 `.local/p1-net03-native/owner-commit`。本场景证明在线原版库存同步和重复指令下的本地一次执行，断线、保存恢复和正式机器事务留待 NET03/04。

同日第二轮在农工的 `Farmer.modData` 增加仅本轮使用的提交记录。主机等原版同步同时提供记录和匹配的库存结果才确认，允许两者在不同更新时刻到达。PID 31368 与 6716 的记录、物品标记和数量一致，主机随后观察到记录和测试格均已清空。两端均完成且未保存，47 个存档文件哈希一致。报告、启动偏好备份和退出审计在 `.local/p1-net03-native/owner-journal`。这验证了在线观察持久化候选字段的路径，字段跨存档的存续还需专用保存试验。

## 2026-11-12 无确认断线及冷重载

使用 `python tools/prepare_save_copy.py --source <XYSNET02_450604963> --label XYSNET03 --save-id 620261112 --cooperative` 生成独立副本。工具只在工作区输出，保留主机和农工的原生联机 ID，重新绑定世界名。审查后仅将该副本安装到游戏 Saves。主机和农工分别使用隔离 Mods 的 `NativeInventoryRecovery`，设置专用农场名和世界 ID，显式开启库存试验，关闭生产协议探针。Write 与 Verify 分别启动新进程。

Write 轮主机 PID 18324、农工 PID 7400。农工把测试格中的标记木材由 3 改为 2，在自身 `Farmer.modData` 写同一请求的日志。主机从实际 Farmer 观察到两者后仍不发业务确认。启动器以本轮请求 ID 通知农工调用原版 `ExitToTitle`。主机收到 `PeerDisconnected` 且在线名单中无该农工后，推进一次 `SaveGame.Save()` 迭代至结束。退出审计核对专用主 XML 中该农工第 12 格为木材 `(O)388`、数量 2、品质 0，匹配本轮标记和日志。原 47 个存档文件不变，两套夹具关闭，原普通进程保留。原报告、配置和审计保存在 `.local/p1-net03-native/recovery-write`。

Verify 轮主机 PID 18764 在农工上线前从磁盘加载的离线 Farmer 读到相同槽位及日志。农工 PID 33456 实际重连后，主机重发同一请求；农工报告 `OwnerWrites=0`、`OwnerReplays=1`，主机及农工最终仍见木材 2 个及原日志，两端完成。两轮隔离生产 DLL 为 `E19B50F8ADFFCCFBFDF41B69A4A5C654D296D0C72FCC83C1E4DF98E4A4C7BEAF`，夹具 DLL 为 `E936D2FA68236BC07616F91B734EDDDBC101805EDDEF301EC785BF2706E613DB`。Verify 原报告位于 `.local/p1-net03-native/recovery-verify`。

两轮审计的 `IntegrityPassed`、`ScenarioPassed`、`DiskCommitMatches` 均为 true。Write 轮只更新两个副本主文件并创建两个原版备份，Verify 轮全部 51 个存档文件哈希一致。两轮隔离夹具均禁用，四个测试进程均已退出，原普通进程 16668 保留。游戏原版写回共享启动偏好，前后哈希与启动前字节留在两轮证据中。

Write 轮直接推进原版存档迭代器，`SavingObserved=false`、`SavedObserved=false`。写盘以主 XML、原版备份和下一进程冷重载证明。生产实现仍需在正常就寝保存中验证 SMAPI 生命周期，并覆盖主机预留、部分同步、两人争用及真实设备提交。标记木材仅用于此场景识别同一测试栈，生产指纹实测需使用普通无标记物品。

## 2026-11-12 原生局域网来源观测

在 `XYSNET02` 的隔离主机与实际农工启用 `LanTransportObservation` 及只读探针配置。主机 PID 22572、客户端 PID 33080。农工正常发送固定只读开工探针。主机从原版 Lidgren 已批准的连接识别农工，两次原生派发通过来源核对，其中开工探针与同一 SDK 连接、同一请求的拒绝配对。两端均记录 `WritesDisabled`。客户端实际返回标题后，主机断连代次从 0 递增到 1，旧 `LastConfirmed` 失效。

生产 DLL `1A077F962A9D81BA85395E0FE3AE2C4056FB38D1FFC8F7F192BF699304EFB3D0`，夹具 DLL `E525F06CBD0F03F675DD66CFB70680DD25691ACD87FCD1E3B7939FB45FB1874F`。双方完成，未发生保存，53 个文件哈希一致；关闭两测试进程、禁用配置，保留原普通进程 16668。原版改写了 `startup_preferences`。报告、构建和审计保存在 `.local/p1-net03-lan/native`。本轮范围为指定 Windows 1.6.15.24356、SMAPI 4.4.0 和 LAN 接收来源，Steam/Galaxy 和实际写权限另验。
