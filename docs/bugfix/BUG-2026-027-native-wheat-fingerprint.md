# BUG-2026-027：普通小麦的库存格指纹不可用

- 状态: 本地验证通过，双端同步待验
- 发现/更新日期: 2026-11-12，Asia/Shanghai
- 责任任务/分支: P1-NET-03，`codex/first-stable-release`
- 性质: 游戏适配开发期缺陷

## 现象与影响

已加载游戏内容后，`OwnerInventoryFingerprint.TryReadSlot` 未能为原版注册表创建的小麦提供预期指纹。该物品是发酵缸的正常原料。生产联机仍只读，现有存档及单人玩法未受影响。

## 环境与前置条件

Windows，Stardew Valley 1.6.15，SMAPI 4.4.0，Content Patcher 2.9.0。生产 DLL `B44245E9B433D970689B2876D85F816C5E0D8B89D35B866A9BD117B20932F144`。使用独立单人 `XYSP1Menu` 副本及隔离 Mods。

## 复现步骤

1. 执行 `dotnet build tests/GameHarness/GameHarness.csproj -c Release`，使用本机 `GamePath` 和 `SmapiPath`。
2. 以 `Enabled=true`、`Scenario=Memory`、`OpenPreview=false`、`VerifyOwnerInventoryFingerprints=true` 启用该副本。配置农场名 `XYSP1Menu`、世界 ID `1288856040646931861`，从标题界面实际加载副本。
3. 检查 `result.json`。注册表创建品质 2 的黄豆和普通品质的小麦，并在原生 Farmer XML 内存往返前后检查。预期两种物品各有稳定指纹，实际先通过黄豆检查，后在小麦初次取指纹时报 `Live material was unavailable or did not have a unique lowercase SHA-256: (O)262`。

## 根因分析

同一生产与诊断构建在独立冷启动中再次拒绝小麦，两个原版 `Object` 的 XML 仅 `flipped` 不同。已核对安装版 1.6.15.24356 的 `ObjectDataDefinition.CreateItem` 和 `Object(string,int,bool,int,int)`，小麦的数字 ID 262 满足构造时随机水平翻转的条件。普通 `Object` 的该字段用于地图绘制和临时动画。旧指纹比较完整 XML，随机翻转不同时错误拒绝。初次 SDK CLI 使用默认朝向的受控工厂，未覆盖这个差异。

## 解决方案

仅在普通库存物品的指纹序列化器中用 `XmlAttributeOverrides` 排除 `Object.flipped`。保持原对象及原版存档字段，继续比较其余原生字段，并拒绝额外元数据、变体或无法重建的物品。构造注册表对照物品仍会推进原版随机数；联机接入时需控制读取频率。

## 单元测试

`GameAdapter` 的 `ordinary inventory fingerprint ignores visual flip without changing either object` 强制小麦与对照物的朝向相反，要求指纹一致、对象原样，改价仍被拒绝。按 [SDK CLI 命令](../../tests/GameAdapter/README.md)执行，旧 DLL 16 项通过、此项失败，新 DLL 17 项全部通过。GameHarness 的 `live owner fingerprints survive native Farmer XML` 另覆盖真实注册表黄豆与小麦、原版 Farmer XML 往返、两种朝向和空格/不可用区别。

## 验证结果

2026-11-12 隔离进程 23472 加载一次，普通 Memory 20 项通过，新增检查失败；内存恢复成功，原生保存零次，51 个存档文件哈希一致。进程退出，夹具禁用，原普通游戏仍在。原报告与审计保留在 `.local/p1-net03-fingerprint`，共享启动偏好被游戏改写。

同日第二个隔离进程 31036 用同一生产 DLL 和诊断夹具完成全部 21 项。实际加载一次、原生保存零次、内存恢复且 51 个存档文件哈希一致。进程已退出、夹具禁用。原报告在 `.local/p1-net03-fingerprint-diagnose`，保留首轮失败及本轮通过的独立证据，根因未验收。

第三个进程 15132 在同一诊断构建中重现失败，报告唯一差异字段为 `flipped`。修复后的生产 DLL `A307647D6F9934F5A2BB244483A0B08A40A2D1A7E7EC17886DD2B51C9004D42E` 在新进程 29412 通过全部 21 项，包含两种小麦朝向。两轮实际加载均一次、原生保存零次、51 个存档文件哈希一致，测试进程均已退出，夹具禁用。原报告在 `.local/p1-net03-fingerprint-repeat` 和 `.local/p1-net03-fingerprint-fixed`。双端与磁盘同步指纹待验。

## 风险与回滚

回归夹具不随生产包分发。在任务分支回退本修复提交会恢复原指纹行为，无存档迁移。修复后仍需实际双端同步验证。

## 关联记录

见 [P1-NET-03](../DEVELOPMENT_PLAN.md)、[交接](../HANDOFF.md)及[实机回归](../../tests/GameHarness/OwnerInventoryFingerprintScenarios.cs)。独立复现检查点 `e3f67a1ead99806a4a82ee28d6b1a2fe80855f72`，诊断检查点 `7600b8c47394b69e16bd5dc642768d9850b508cf`，修复与本记录同一提交。
