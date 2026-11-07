# P1 三家真实地图柜台与购买

日期：2026-11-06，Asia/Shanghai。本轮同一新进程30580完成皮埃尔、罗宾、格斯三家柜台入口与实际购买。生产内容未改，补齐原型正常来源的地图触发/付款/学习证据；P1整体未验收，P2仍冻结。

## 基线与变更

分支`feat/p1-game-adapter`，HEAD仍`8f2468d83b43fb805073f73bbc8b989cf716852e`。累计未提交/未跟踪树保留，本轮没有提交、推送、PR、合并或远程CI。新增[WorldShopInteractionScenario](../../tests/GameHarness/WorldShopInteractionScenario.cs)及[ModEntry路由](../../tests/GameHarness/ModEntry.cs)，不改生产src/catalog/content/tools、数量、ID、价格、配方或依赖。

本轮87项运行原字节（含旧赠礼和厨房原报告）、159份源数据/文档在`.local/p1-world-shops-2026-11-06/{baseline.json,before,before-source}`。独立测试游戏1.6.15 build24356、SMAPI4.4.0、CP2.9.0，中文1280×720，专用单人XYSP1UI/8624092802。只更新runtime夹具，两端profile/curated夹具保留旧版且禁用；源安装未升级或覆盖。

## 实际准备与边界

夹具只准备本次内存的空背包、20000g、人物位置/暂停日程，以及三店定位/营业时刻900、1000、1200。不创建ShopMenu、不调用地图Action/购买/学习API、不改商店数据或库存、不代替鼠标选择罗宾“商店”选项。原有完整商品列表保留，使用原版滚动条查找新增物品。

准备会定位玩家到柜台前、将原版店主放到原生允许的位置：Pierre位于(4,17)、Robin在柜台附近、Gus在原生吧台范围。这里不验从农场步行进店、NPC自然营业日程、店门营业时间或其他日期/季节。不保存、不恢复本次内存，完成必须退出进程；没有把准备或聊天等待算入的操作倒计时。

## 同次真实结果

08:32:02原生加载专用单人档后，由computer-use进行实际鼠标右键/选择商店/购买/入包/关闭操作，中键只核验并准备下一店。

| 阶段 | 真实地图Action/商店 | 实际取得与付款 | 阶段余款 |
|---|---|---|---:|
| 皮埃尔 | SeedShop的(4,18)，Buy General；ShopId=SeedShop | 黄豆6×60g、小麦2×25g，实际入包；三张原型配方仍未学习 | 19590g |
| 罗宾 | ScienceHouse的(8,19)，Carpenter；实际选择“商店”，ShopId=Carpenter | 豆坊、发酵缸图纸各1500g；两个制作键变为已学，不直接获得设备，已购原料保留 | 16590g |
| 格斯 | Saloon的(10,19)，Saloon；ShopId=Saloon | 香椿1×100g、香椿拌豆腐菜谱1000g；料理制作键变为已学，不直接获得料理；先前原料和图纸保持 | 15490g |

各阶段实际右键1、对应原生商店菜单1、成功的本地Farmer地图Action1。只读Harmony postfix记录实际格与成功返回，不使用鼠标候选格替代Action。MenuChanged单独核ShopId并快照目标商品的原生价格/库存/IsRecipe；中键另核完整背包、已学键、精确余款及无额外物品，不能仅凭商品能显示或已知价格认定购买。

原生商品快照分别为：黄豆60g/stock10、小麦25g/stock10；两张设备图纸各1500g/stock1/IsRecipe=true；香椿100g/stock5、料理菜谱1000g/stock1/IsRecipe=true。最终背包只有黄豆6、小麦2、香椿1，三个配方键均已学，累计扣款4510g，未赠送额外设备/料理。

最终原报告`Completed=true/Step=3/Failure=null/SavingObserved=false/SavedByHarness=false/AwaitingWarp=false`，完整Evidence三条，没有拼接不同进程。报告SHA为`394e48319109242c9e0cdd18e273573afb9992d4844332cba988c7752cecb507`。部分首次点击/中键未立即响应，均重新观察后才重试；以真实Action、菜单和付款/数量核验，不把工具调用次数当作游戏接受次数。界面实际查看，未保存截图或编造截图路径。

## 检查与收尾

```powershell
dotnet build tests/GameHarness/GameHarness.csproj -c Release "-p:GamePath=<独立测试游戏>" "-p:SmapiPath=<独立测试游戏>" --nologo
python tools/check_workflow.py
git diff --check
```

本轮`build.txt`实际exit0，Release0警告0错误。新场景首轮完整实机即通过，没有生产缺陷或修复前红，不新增虚构bugfix；未改变纯规则/已有共享判据/Python工具，不重跑无关115核心、Python或网络矩阵。C#变化由SDK构建及真实三阶段鼠标流程验证，不另写复制实现的字符串断言冒充单元回归。

原报告、退出日志、87项运行snapshot和结束audit在`.local/p1-world-shops-2026-11-06/after-world-shops`。标题栏X退出，游戏/SMAPI进程0，八受保护XML与before原字节一致，六配置原字节恢复且全部禁用，旧赠礼/厨房原报告不变，联机profile身份预检exit0；87项仅build/runtime夹具DLL/PDB共四项变化。最终日志ERROR行0，无原生Save调用，不计为返回标题清理或整链保存验收。

本次build/runtime夹具DLL SHA为`03a3196abc4283b2bdb301a5cb947fc62572df6ff58de00a9776afba42b41c68`；生产DLL仍`f34e86be090967bfa3b5c253b3e2930d88072d8a4ab31ca2bdd1a86f0eed6148`，开发ZIP仍`514839e644dd71c414c3e9374862ca19353d7112a8ef75cb4dea457a09b7a0c1`。生产包未改，不为该验证重新打包；模式不进开发ZIP或无SDK CI。

最终workflow.txt实际exit0，维护记录/本地链接通过、0提交正文检查；delivery.txt实际exit0，11项新增/修改公共文件（含未跟踪）的UTF-8/空白/冲突标记、当前87项/新报告/CIM0/分支/HEAD通过，生产src/catalog/content/tools/core/ZIP不变。清单在delivery-audit.json；补充最终结果后的两份文档再次核文本。不将CI配置当作远程执行结果。

## 复跑、回退和下一步

详见[夹具说明](../../tests/GameHarness/README.md)。停止游戏、备份专用档/配置，以新证据目录归档已有world-shop-interaction-result.json；独立runtime设置Enabled=true/Scenario=WorldShopInteraction/OpenPreview=false/正确XYSP1UI世界ID。自动加载后按各阶段指令从柜台购买，商品在原版完整列表中；关店后中键核验。不要睡觉或保存；完成后关闭整个进程、恢复禁用、核保存哈希与原报告。

私有snapshot/deploy/closeout脚本均已执行，不能重跑覆盖输出。回退只撤回新增场景/路由，并在停止状态恢复before中的runtime夹具/配置，不删除玩家物品或更改生产包/存档。下一步集中完整手工两设备链及保存闭环，三店/厨房/赠礼已通过的未变化子集不反复重跑。
