# BUG-2026-020：保存校验将未创建的小屋占位角色当作损坏姓名

- 状态: 本地回归及真实保存CLI集成通过；未提交/合并
- 发现/更新日期: 2026-10-11发现，2026-10-12补核，Asia/Shanghai
- 责任任务/分支: P1-NET-02准备保存校验 / `feat/p1-game-adapter`
- 性质: 本轮新Python只读工具开发期边界缺陷；人工成功夹具加本机原生占位结构发现，不是玩家输入故障或生产写入事故

## 现象与影响

准备校验初稿遍历原生farmhands列表时，对每个name调用通用严格身份文本读取。合法的未命名/未创建小屋占位角色以`<name />`、isCustomized=false保存；人工夹具把已创建客户端与这种占位同时放入直接列表后，旧工具抛`ValueError: Identity must contain plain text.`，错误拒绝本应通过的客户端持久化校验。工具只读，没有修改保存、配置或游戏，也没有因此标记真实玩家准备成功。

## 环境与前置条件

Windows/Python标准库；本机隔离游戏1.6.15的SaveGame/Farmer保存结构。SaveGame源码字段`List<Farmer> farmhands`在加载时进入netWorldState.farmhandData；Farmer的name/isCustomized/UniqueMultiplayerID为原生序列化字段。本机春23专用原XML确有一个空name/isCustomized=false的直接Farmer，仍无XYSNETClient。正例使用完全人工的XYSNETFresh/900001，主机ID10、客户端ID-20，目标已customized=true，另有name空/false/ID0的占位；测试不打开真实游戏或个人保存。

## 复现步骤

修前在新工具源码仍使用`_text(farmer, "name")`时先加入具名回归并执行：

```powershell
python -m unittest discover -s tests -p test_network_preparation.py -k unnamed_uncustomized -v
```

`test_unnamed_uncustomized_native_placeholder_does_not_block_client`首先断言已创建客户端＋空占位组合应返回clientFarmerPersisted=true，后断言只有占位应拒绝、customized=true且name空应拒绝。修前第一断言实际抛上述ValueError，1项error/exit1，`.local/p1-net02-preparation-placeholder-contract-2026-10-11.txt`留实际堆栈。初稿源码原字节未单独备份，不能声称有完整修前源码快照；红日志来自实际初稿、发生于修改守卫之前。

## 根因分析

共享fixture._text用于必须有值的农场/世界身份，合理拒绝element.text=None；ElementTree将`<name />`读为None。初稿把这个契约扩展到尚未创建的所有Farmers，遗漏原生列表可能保留空小屋占位。首批人工用例把占位命名为UnnamedPlaceholder，10项通过未揭示空name。补以实际保存结构为依据的成功组合回归后才暴露边界。

## 解决方案

[check_network_preparation.py](../../tools/check_network_preparation.py)引入`_farmhand_name`：name仍须唯一直接/无嵌套内容，isCustomized必须精确true/false；空名仅在false占位允许，目标匹配仍须非空指定名、customized=true及独立非零Int64 ID。已创建空名、重复目标、别名ID、宿主名冒充、嵌套Cabin/name均拒绝。现有fixture._text/身份工具不改，其他未创建占位可保留ID0，不将其计作目标客户端；不改变任何存档格式或写入权限。

## 单元测试

相同具名命令修后1项OK/exit0，`.local/p1-net02-preparation-placeholder-contract-green-2026-10-11.txt`。完整新模块11项OK/exit0在`p1-net02-preparation-tests-final-2026-10-11.txt`，包含：

| 具名测试后缀（NetworkPreparationTests） | 断言范围 |
|---|---|
| `native_roster_negative_id_and_empty_farm_name_pass_without_writes` | 直接列表的customized客户端、负Int64及空客户端farmName合法；字节不变，其他验收flags=false |
| `host_only_nested_cabin_and_uncustomized_farmer_do_not_count` | 单端/嵌套Cabin/未创建/宿主名字冒充拒绝且字节不变 |
| `duplicate_roster_name_container_and_alias_id_are_rejected` | 同名/重复列表或name/其他角色同ID拒绝 |
| `zero_host_alias_overflow_and_noninteger_client_ids_are_rejected` | 零/宿主ID/越界/非整数/非规范ID拒绝 |
| `save_info_host_identity_and_expected_world_must_match` | Info同宿主名与ID、世界匹配；bool伪整数及空目标拒绝 |
| `invalid_date_favorite_and_customization_are_rejected` | 原生日期/季节/指定favoriteThing/flag严格校验 |
| `utf16_and_placeholder_do_not_prevent_valid_client` | UTF16及其他命名未创建占位不阻止有效客户端 |
| `unnamed_uncustomized_native_placeholder_does_not_block_client` | 本缺陷的1红1绿；空占位组合允许、占位单独或created空名拒绝 |
| `dtd_and_invalid_root_are_rejected_without_writes` | DTD/未知根拒绝，不改输入 |
| `change_during_validation_refuses_stale_snapshot` | 两XML读取期间发生日期变化则拒绝旧快照；故意改动来自测试注入，非校验器写保存 |
| `cli_positive_and_refusal_emit_only_appropriate_result` | 人工成功输出正摘要，缺目标时不输出成功摘要 |

表中均以`test_`为实际名称前缀。其他10项不是逐项修前红，不借本缺陷单项红宣称全部先红后绿。

## 验证结果

本缺陷离线具名1红1绿/新模块11通过已执行。真实春23保存的**备份**调用新CLI实际拒绝“Expected exactly one named client in direct farmhands/Farmer.”/exit1，日志`p1-net02-preparation-before-save-refusal-2026-10-11.txt`；这是目标不存在的预期负例，不是修前缺陷红，不证明后来已创建或保存。完整Python检查及真实双端保存/退出结果另见[准备阶段记录](../validation/P1-network-preparation-2026-10-11.md)，不把配置启动或人工XML当真客户端验收。

完整Python116项/31.388s/exit0；实际春24新保存CLI exit0，直接列表核XYSNETClient/customized=true/favorite tofu/独立非零ID，与双端连接上下文一致。独立关闭审计85项仅专用两XML变化，旧六save/六禁用config及程序包不变，CIM0；日志与原生保存另留.local。此正例证明新工具可读取实际已创建角色，不借人工占位回归宣称真实春24也有空占位，亦不代替NET02诊断或BUG019旧残余输入根因。详见准备记录的10-12补核节。

## 风险与回滚

工具只验证原生1.6直接farmhands结构与固定字段；其他版本或未知/损坏结构拒绝，保留原值，不迁移/修复保存。稳定重复读取不是跨文件锁或成功保存流程证明，必须两端退出后使用，另核日志/哈希/用户输入。回滚仅停用此校验或恢复明确旧源码，不删占位、不重写姓名或客户端ID、不改生产模组/游戏存档；旧工具仍可能错误拒绝合法占位。

## 关联记录

[P1计划](../DEVELOPMENT_PLAN.md)、[NET02指南](../NET02_TEST_SETUP.md)、[HANDOFF](../HANDOFF.md)、[准备验证](../validation/P1-network-preparation-2026-10-11.md)、[新测试](../../tests/test_network_preparation.py)。HEAD基线8f2468d83b43fb805073f73bbc8b989cf716852e；代码/文档未提交，无本次修复SHA/完整commit正文/PR/merge/远程CI。
