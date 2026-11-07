# P1 保存与获取链路后续验证 · 2026-09-28

本记录接续 [前轮单人验证](P1-2026-09-28.md)。新增 **6 项实机集成通过**：真实购买/学习与扣款、原生配方制作、设备放置/重新放置、三状态磁盘重载、原生春夏跨季节保存、领取后不重复产出。P1 总体仍未验收，联机保持禁用。

## 配置、隔离与方法

游戏 1.6.15 / SMAPI 4.4.0 / CP 2.9.0 / alpha.2；CP 来源沿用 ENVIRONMENT。GameHarness 新增 Persistence 模式，仅作用于独立 XYSP1Save 副本，检查保存名、农场名、game ID 三者匹配。副本由 prepare_save_copy 从已有测试档生成并备份，原 XYS_P0 文件在测试后逐一 SHA-256 相同。

此模式会保存指定副本，不能套用 Memory 模式的“不保存”承诺。完整重建和中断接续步骤见 [GameHarness README](../../tests/GameHarness/README.md)。测试设备/资金/原版制作材料由夹具准备；未把测试初始资源声明为正常游玩获得。

购买使用游戏 ShopMenu 真实库存与 tryToPurchaseItem，制作使用 CraftingRecipe 的原料检查/扣除/产出，机器操作使用生产 MachineService。保存调用 SaveGame.Save，完成后退出标题，再由 SaveGame.Load 从磁盘恢复。季节那一步实际执行 newDayAfterFade 与原生夜间保存，不是只改日期后宣称跨日通过。

## 实际结果

| 用例 | 断言与结果 |
|---|---|
| native shop purchase debits and learns three recipes | 买黄豆 6×60g、小麦 2×25g、香椿 100g、两图纸各 1500g、菜谱 1000g；20000g 剩 15490g，三配方已学会 |
| native crafting consumes materials and placement creates fresh identity | 木材 80、石头 60、铜锭 1 恰好制作两设备；placementAction 真正放置；空闲 removeObject 后重新放置获得新 InstanceId |
| purchased ingredients reach tofu dish and soy sauce through native recipes | 两台机器实际加工与领取，再原生烹饪扣料；最终豆腐 1、香椿拌豆腐 1、酱油 4，购入黄豆/小麦/香椿消耗正确 |
| disk reload preserves processing ready and collected states | 四设备矩阵：加工中两台、待领取一台、已领取一台；第一次写盘/标题重载后完整状态 JSON、库存数量/品质/槽位保持，旧领取请求拒绝 |
| native overnight crosses spring to summer and reload retains output | 春 28 原生日切换到夏 1、夜间保存/退出标题/读档；进行中批次正确待领取，固定产量与 JSON 不漂移 |
| collected disk save cannot regenerate products | 再次领取后保存/退出标题/读档，四台均空闲，再领全部拒绝，库存保持豆腐 6、酱油 4（保存矩阵专用材料） |

最终 persistence-result.json：Completed=true、Stage=3、6 个 Passed、Failure=null。本轮正常保存/购买日志没有非预期 ERROR/WARN；前轮 Memory 的预期故障日志仍单独保留，不混算。

磁盘 XML 另行只读检查：确为夏 1，四台设备实例 ID 不同、Batch=null、Revision>0。画面显示夏季、15490g 与完成提示。

当日 19:18 重新启动整个测试游戏进程，19:19 从磁盘加载夏 1，重新执行 Stage 3 的 `collected disk save cannot regenerate products` 并通过。新进程加载了全新 MachineService，因此该结果不依赖旧会话内存。报告保留前五项检查点记录，并非六项全部冷启动重跑；本次没有再次写盘。完成后已关闭测试游戏，夹具设置 Enabled=false、OpenPreview=false。

## 发现与修复

[BUG-2026-006](../bugfix/BUG-2026-006-shop-price-multiplier.md)：原生 SeedShop 价格修饰使清单标价再翻倍。修复前总款断言失败（余额 15080g），新增 Python 负例也失败；为本模组原型条目增加 IgnoreShopPriceModifiers=true 后，实机总额断言和全部 6 项通过。未更改原版库存条目或其全局价格规则。

Python 回归现为 26 项（16 清单、3 副本、7 维护），主工程/夹具 Release 0 警告/0 错误。纯事务的 24 场景沿用已通过基线，本轮未改变事务代码；不为数据价格修改重复声称新的联机验证。

## 证据与未覆盖

本地保存 `.local/p1-persistence-result-2026-09-28.json`、`.local/p1-persistence-smapi-2026-09-28.txt`，冷启动单独保存 `.local/p1-cold-restart-result-2026-09-28.json`、`.local/p1-cold-restart-smapi-2026-09-28.txt`，以及 BUG-006 的修复前报告。夹具副本和完整日志不提交仓库。

仍未验证：步行到商店的实际界面操作/日程，房屋厨房交互、食用/赠礼/售出，工具击落后的掉落拾取链路（已测的是 removeObject→placementAction），旧档设备迁移、其他语言/手柄/尺寸和外部模组兼容；联机协议/双方争用/断线仍未实现。不能以本轮原生方法集成替代这些界面与网络场景，也不能勾选 P1 总体完成。
