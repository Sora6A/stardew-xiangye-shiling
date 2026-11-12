# BUG-2026-027：普通小麦的库存格指纹不可用

- 状态: 已复现，根因调查中
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

尚未确定具体拒绝条件。SDK CLI 使用受控的工厂和原生序列化器，未加载真实注册表。下一步分别核对小麦的原生类型、库存格读取和原对象与注册表重建对象的序列化差异。

## 解决方案

目前只加入默认关闭的实机回归。待确认差异来源后，修复正常小麦的指纹读取，并保留变体、额外数据和无法重建物品的拒绝断言。

## 单元测试

`GameHarness` 的 `live owner fingerprints survive native Farmer XML` 要求两种配方材料均返回不同且有效的 SHA-256，内存往返前后一致，空格与不可用状态分开。上述 Release 构建通过，原实机回归在小麦处失败。修复后未执行。先前 GameAdapter 的 16 项通过仅覆盖无游戏内容场景。

## 验证结果

2026-11-12 隔离进程 23472 加载一次，普通 Memory 20 项通过，新增检查失败；内存恢复成功，原生保存零次，51 个存档文件哈希一致。进程退出，夹具禁用，原普通游戏仍在。原报告与审计保留在 `.local/p1-net03-fingerprint`，共享启动偏好被游戏改写。

## 风险与回滚

回归夹具不随生产包分发。可在任务分支回退测试提交，无存档迁移。修复后仍需实际双端同步验证。

## 关联记录

见 [P1-NET-03](../DEVELOPMENT_PLAN.md)、[交接](../HANDOFF.md)及[实机回归](../../tests/GameHarness/OwnerInventoryFingerprintScenarios.cs)。本轮为复现检查点，修复提交待定。
