# BUG-2026-006：皮埃尔全局倍率重复放大原型食材售价

- 状态: 本地单元回归与原生购买集成通过，待提交/合并
- 发现/更新日期: 2026-09-28，Asia/Shanghai
- 责任任务/分支: P1 正常获取链路，`feat/p1-game-adapter`
- 性质: Content Patcher 商店价格配置；未发布版本实机发现

## 现象与影响

清单标价黄豆 60g、小麦 25g，但加入 SeedShop 后原生价格计算再次应用商店倍率。专用农场以 20000g 购买黄豆 6、小麦 2、香椿 1、两张设备图纸和料理菜谱后，预期余款 15490g，实际 15080g，多扣 410g（两种皮埃尔食材的额外一倍）。单项购买按游戏显示价扣款，问题是显示价与清单承诺不一致；没有物品复制。

## 环境与前置条件

Windows，游戏 1.6.15 / SMAPI 4.4.0 / CP 2.9.0 / alpha.2；独立 XYSP1Save 测试副本；正常价格设置。原测试档与正式农场未保存此修改。

## 复现步骤

1. 在未修复 content.json 上启用 GameHarness 的 Persistence 模式并加载独立副本。
2. ShopMenu 构建真实库存并执行原生 tryToPurchaseItem，图纸/菜谱均成功学习。
3. `native shop purchase debits and learns three recipes` 总额断言失败；失败报告 `.local/p1-persistence-price-before.json`，界面余额 15080g。
4. 新增 Python 回归检查所有生成的原型库存条目显式绕过全局价格倍率；旧生成器缺此字段，先失败，证据 `.local/p1-shop-price-before.txt`。

## 根因分析

tools/prototype.py 把 catalog.prototypeShops.Price 当最终购买价输出，但游戏 ShopBuilder 还会应用 ShopData.PriceModifiers。旧测试只查 JSON Price，未执行商店实际价格计算。SeedShop 的默认倍率使黄豆/小麦翻倍。

## 解决方案

生成的每个本模组原型库存条目设 IgnoreShopPriceModifiers=true。清单价格保持不变，原版商店其他条目和全局规则不改；生成 content.json 后重新部署。这里只定义 P1 原型固定价格，不承诺其他模组改写条目后的价格。

## 单元测试

`tests/test_catalog.py::CatalogTests.test_shop_entries_preserve_catalog_prices`：生成 CP 补丁，逐条断言忽略全局倍率字段为 true。旧实现失败（缺少字段）；修复后 `python -m unittest discover -s tests -v` 全部 26 项通过，其中清单 16、存档副本 3、维护 7。真实扣款另由 PersistenceScenario 总额断言验证，不能用 JSON 单测代替。

## 验证结果

主工程与夹具构建 0 警告/0 错误。修复后原生库存实际单价黄豆 60g、小麦 25g，完整购买后余款为 15490g，三张配方成功学习；包括保存重载在内的 6 项实机集成全部通过（Completed=true、Failure=null）。结果与证据见 [P1 后续验证](../validation/P1-persistence-2026-09-28.md)。商店 UI 手动操作、外部经济模组或全游戏价格平衡未验证，不能因此声明完整玩法验收。

## 风险与回滚

本模组原型条目不会继承商店全局价格修饰；这是明确固定原型标价的选择。若后续需要动态定价，先调整登记语义/说明与原生价格测试，再移除此字段。回滚生成器和生成物应同改；不改物品 ID、机器 schema 或存档。

## 关联记录

tools/prototype.py、tests/test_catalog.py、tests/GameHarness/PersistenceScenario.cs；[交接](../HANDOFF.md)。本轮未提交、推送、PR 或合并。
