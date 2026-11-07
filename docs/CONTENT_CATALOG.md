# 内容登记表

由 `data/catalog.json` 生成；修改清单后运行 `python tools/catalog.py --write`。

设计基线 0.4；40 个物品 ID、30 个配方 ID。清单是预留范围，不代表已实现。
P1 激活 7 个物品与 5 个配方，仅用于两条单人原型流程；其他 planned 项不注入游戏。`null` 表示待定，不能作为运行时数据。
料理和熟食可作为终点。煤炭与设备制作材料为待实机核对的暂定方案。

## ID 与本地化

物品：`Sora6A.XiangyeShiling_<Key>`；配方：`Sora6A.XiangyeShiling_Recipe_<Key>`。
普通物品使用 `(O)` 前缀，设备使用 `(BC)`；数据表键不带此前缀。显示名不参与 ID。
文本键为 `item.<Key>.name` / `item.<Key>.description`，英文默认与中文已登记。

## 物品

| Key | 中文 | 类别 | 来源 | 去向 | 图像位置 | 状态 |
|---|---|---|---|---|---|---|
| `BambooShoots` | 竹笋 | forage | 春季 Forest, Mountain, BusStop（地图待验） | 清炒竹笋、竹笋炒腊肉、油焖笋 | Objects #0，待绘制 | planned |
| `WildScallion` | 野葱 | forage | 春季 Forest, Mountain, BusStop（地图待验） | 野葱煎蛋、野葱炒熏干 | Objects #1，待绘制 | planned |
| `ChineseToon` | 香椿 | forage | 春季 Forest, Mountain, BusStop（地图待验） | 香椿炒蛋、香椿拌豆腐 | Objects #2，待绘制 | prototype |
| `Mugwort` | 艾草 | forage | 春季 Forest, Mountain, BusStop（地图待验） | 艾草米糕 | Objects #3，待绘制 | planned |
| `Soybean` | 黄豆 | crop | 黄豆种子；summer, fall；SeedShop 季外限量 | 豆浆、豆花、豆腐、油豆皮、千张、豆干、酱油、黄豆酱 | Objects #4，待绘制 | prototype |
| `NapaCabbage` | 大白菜 | crop | 大白菜种子；fall；SeedShop 季外限量 | 酸菜 | Objects #5，待绘制 | planned |
| `Pork` | 猪肉 | shop_ingredient | Saloon 商店 | 腊肉、麻婆豆腐、千张肉卷 | Objects #6，待绘制 | planned |
| `BroadBeans` | 蚕豆 | shop_ingredient | Saloon 商店 | 川式豆瓣 | Objects #7，待绘制 | planned |
| `SoyMilk` | 豆浆 | bean_product | Recipe_SoyMilk | eat、gift、sell | Objects #8，待绘制 | planned |
| `TofuPudding` | 豆花 | bean_product | Recipe_TofuPudding | eat、gift、sell | Objects #9，待绘制 | planned |
| `Tofu` | 豆腐 | bean_product | Recipe_Tofu | 油豆腐、香椿拌豆腐、酸菜炖豆腐、麻婆豆腐、eat、gift、sell | Objects #10，待绘制 | prototype |
| `Yuba` | 油豆皮 | bean_product | Recipe_Yuba | 豆皮汤、eat、gift、sell | Objects #11，待绘制 | planned |
| `TofuSheets` | 千张 | bean_product | Recipe_TofuSheets | 素鸡、千张肉卷、eat、gift、sell | Objects #12，待绘制 | planned |
| `DriedTofu` | 豆干 | bean_product | Recipe_DriedTofu | 熏豆干、兰花干、酱香豆干、eat、gift、sell | Objects #13，待绘制 | planned |
| `VegetarianChicken` | 素鸡 | prepared_soy | Recipe_VegetarianChicken | eat、gift、sell | Objects #14，待绘制 | planned |
| `FriedTofu` | 油豆腐 | prepared_soy | Recipe_FriedTofu | eat、gift、sell | Objects #15，待绘制 | planned |
| `OrchidTofu` | 兰花干 | prepared_soy | Recipe_OrchidTofu | eat、gift、sell | Objects #16，待绘制 | planned |
| `SoySauce` | 酱油 | fermented | Recipe_SoySauce | 素鸡、兰花干、油焖笋、千张肉卷、eat、gift、sell | Objects #17，待绘制 | prototype |
| `SoybeanPaste` | 黄豆酱 | fermented | Recipe_SoybeanPaste | 酱香豆干、eat、gift、sell | Objects #18，待绘制 | planned |
| `SichuanDouban` | 川式豆瓣 | fermented | Recipe_SichuanDouban | 麻婆豆腐、eat、gift、sell | Objects #19，待绘制 | planned |
| `SourCabbage` | 酸菜 | fermented | Recipe_SourCabbage | 酸菜炖豆腐、eat、gift、sell | Objects #20，待绘制 | planned |
| `SmokedPork` | 腊肉 | smoked | Recipe_SmokedPork | 竹笋炒腊肉、eat、gift、sell | Objects #21，待绘制 | planned |
| `SmokedTofu` | 熏豆干 | smoked | Recipe_SmokedTofu | 野葱炒熏干、eat、gift、sell | Objects #22，待绘制 | planned |
| `StirFriedBamboo` | 清炒竹笋 | dish | Recipe_StirFriedBamboo | eat、gift、sell | Objects #23，待绘制 | planned |
| `ScallionEggs` | 野葱煎蛋 | dish | Recipe_ScallionEggs | eat、gift、sell | Objects #24，待绘制 | planned |
| `ToonEggs` | 香椿炒蛋 | dish | Recipe_ToonEggs | eat、gift、sell | Objects #25，待绘制 | planned |
| `MugwortRiceCake` | 艾草米糕 | dish | Recipe_MugwortRiceCake | eat、gift、sell | Objects #26，待绘制 | planned |
| `ToonTofu` | 香椿拌豆腐 | dish | Recipe_ToonTofu | eat、gift、sell | Objects #27，待绘制 | prototype |
| `BambooSmokedPork` | 竹笋炒腊肉 | dish | Recipe_BambooSmokedPork | eat、gift、sell | Objects #28，待绘制 | planned |
| `ScallionSmokedTofu` | 野葱炒熏干 | dish | Recipe_ScallionSmokedTofu | eat、gift、sell | Objects #29，待绘制 | planned |
| `SourCabbageTofu` | 酸菜炖豆腐 | dish | Recipe_SourCabbageTofu | eat、gift、sell | Objects #30，待绘制 | planned |
| `BraisedBamboo` | 油焖笋 | dish | Recipe_BraisedBamboo | eat、gift、sell | Objects #31，待绘制 | planned |
| `SaucedTofu` | 酱香豆干 | dish | Recipe_SaucedTofu | eat、gift、sell | Objects #32，待绘制 | planned |
| `MapoTofu` | 麻婆豆腐 | dish | Recipe_MapoTofu | eat、gift、sell | Objects #33，待绘制 | planned |
| `TofuPorkRoll` | 千张肉卷 | dish | Recipe_TofuPorkRoll | eat、gift、sell | Objects #34，待绘制 | planned |
| `YubaSoup` | 豆皮汤 | dish | Recipe_YubaSoup | eat、gift、sell | Objects #35，待绘制 | planned |
| `SoybeanSeeds` | 黄豆种子 | seed | SeedShop 商店 | 种植 黄豆 | Objects #36，待绘制 | planned |
| `NapaCabbageSeeds` | 大白菜种子 | seed | SeedShop 商店 | 种植 大白菜 | Objects #37，待绘制 | planned |
| `BeanWorkshop` | 豆坊 | machine | Recipe_BeanWorkshop | 加工 豆浆、加工 豆花、加工 豆腐、加工 油豆皮、加工 千张、加工 豆干 | Machines #0，待绘制 | prototype |
| `FermentationCrock` | 发酵缸 | machine | Recipe_FermentationCrock | 加工 酱油、加工 黄豆酱、加工 川式豆瓣、加工 酸菜 | Machines #1，待绘制 | prototype |

## 配方

prototype 项已有暂定数量与工时（见 JSON）；其余 planned 项待平衡。机器菜单配方随设备可用，图纸/菜谱由商店学习。

| 配方 ID 后缀 | 设施 | 输入 | 输出 | 解锁 |
|---|---|---|---|---|
| `SoyMilk` | bean_workshop | 黄豆 | 豆浆 | 随设备可用 |
| `TofuPudding` | bean_workshop | 黄豆 | 豆花 | 随设备可用 |
| `Tofu` | bean_workshop | 黄豆 | 豆腐 | 随设备可用 |
| `Yuba` | bean_workshop | 黄豆 | 油豆皮 | 随设备可用 |
| `TofuSheets` | bean_workshop | 黄豆 | 千张 | 随设备可用 |
| `DriedTofu` | bean_workshop | 黄豆 | 豆干 | 随设备可用 |
| `SoySauce` | fermentation_crock | 黄豆＋Wheat | 酱油 | 随设备可用 |
| `SoybeanPaste` | fermentation_crock | 黄豆 | 黄豆酱 | 随设备可用 |
| `SichuanDouban` | fermentation_crock | 蚕豆＋HotPepper | 川式豆瓣 | 随设备可用 |
| `SourCabbage` | fermentation_crock | 大白菜 | 酸菜 | 随设备可用 |
| `SmokedPork` | fish_smoker | 猪肉＋Coal | 腊肉 | 随设备可用 |
| `SmokedTofu` | fish_smoker | 豆干＋Coal | 熏豆干 | 随设备可用 |
| `VegetarianChicken` | kitchen | 千张＋酱油＋Oil | 素鸡 | Saloon |
| `FriedTofu` | kitchen | 豆腐＋Oil | 油豆腐 | Saloon |
| `OrchidTofu` | kitchen | 豆干＋酱油＋Oil | 兰花干 | Saloon |
| `StirFriedBamboo` | kitchen | 竹笋＋Oil | 清炒竹笋 | Saloon |
| `ScallionEggs` | kitchen | 野葱＋Egg | 野葱煎蛋 | Saloon |
| `ToonEggs` | kitchen | 香椿＋Egg | 香椿炒蛋 | Saloon |
| `MugwortRiceCake` | kitchen | 艾草＋Rice＋Sugar | 艾草米糕 | Saloon |
| `ToonTofu` | kitchen | 香椿＋豆腐 | 香椿拌豆腐 | Saloon |
| `BambooSmokedPork` | kitchen | 竹笋＋腊肉 | 竹笋炒腊肉 | Saloon |
| `ScallionSmokedTofu` | kitchen | 野葱＋熏豆干 | 野葱炒熏干 | Saloon |
| `SourCabbageTofu` | kitchen | 酸菜＋豆腐 | 酸菜炖豆腐 | Saloon |
| `BraisedBamboo` | kitchen | 竹笋＋酱油＋Oil | 油焖笋 | Saloon |
| `SaucedTofu` | kitchen | 豆干＋黄豆酱 | 酱香豆干 | Saloon |
| `MapoTofu` | kitchen | 豆腐＋川式豆瓣＋猪肉 | 麻婆豆腐 | Saloon |
| `TofuPorkRoll` | kitchen | 千张＋猪肉＋酱油 | 千张肉卷 | Saloon |
| `YubaSoup` | kitchen | 油豆皮＋Egg | 豆皮汤 | Saloon |
| `BeanWorkshop` | crafting | Wood＋Stone＋CopperBar | 豆坊 | Carpenter |
| `FermentationCrock` | crafting | Wood＋Stone | 发酵缸 | Carpenter |

## 未锁定项

- 新美术、作物阶段、机器状态图；P0 黄豆暂用原版绿豆图标（仅资源引用，不分发原版图片）。
- 分类/职业加成、赠礼、收集与完成度行为须在 P3 核查；P0 黄豆暂不计入出货收集。
- 生长天数、补货数量、价格、食用恢复量、每批产出与耗时。
- 鸡蛋暂以原版普通白鸡蛋 ID 登记，P1 核查大小/颜色替代规则。
- 熏鱼机煤炭耗用、正常鱼类加工回归；实际补丁只能追加规则。

## P1 暂定供给

皮埃尔全年限量供应黄豆/小麦，格斯限量供应香椿并售香椿拌豆腐菜谱，罗宾售两台设备图纸。
这些库存为原型补给，不代表 P2 的作物、春季采集和季外补货规则已经实现。数值见 catalog.prototypeShops。
