# P1单人获取指南与缺料反馈

开发/验证：2026-11-02至2026-11-03，Asia/Shanghai。分支feat/p1-game-adapter，基线HEAD8f2468d83b43fb805073f73bbc8b989cf716852e；累计来源保留，无新提交/推送/PR/merge。本轮交付的是玩家菜单与上手说明，P1整体/联机/P2状态不变。

## 玩家侧变化

- 两设备菜单增加两页“获取与制作指南”：三家商店材料/图纸/菜谱，以及豆腐、酱油用量工时和厨房料理用途。加工名称、数量、分钟直接读已加载机器配方，不复制数值表；不把planned内容写成已可用。
- 缺料时原料数量/开工按钮变色，点击列出实际差额；不创建Pending或扣料，补齐后可重试。满包领取仍交给原服务拒绝并保留整批，成功锁定语义不变；成功提示改为绿色。
- `data/catalog.json`只改五个既有原型物品的中英描述，写明来源/使用；`python tools/catalog.py --write`同步CP文案。仍40物品/30配方、激活7/5；未改ID/价格/数量/解锁/设备schema/依赖或联机门禁。
- 新[玩家指南](../PLAYER_GUIDE.md)与[单人状态矩阵](../P1_SINGLEPLAYER_STATUS.md)区分现有功能、历史验证与普通世界流程待验收项。

## 代码入口与边界

[MachineMenu](../../src/XiangyeShiling/Menus/MachineMenu.cs)的GuideOpen/GuidePage只属于本次菜单对象，先处理指南导航再处理交易，点击指南正文不会落到背后动作按钮。查看/翻页/返回不调用Acquire/Execute；成功后仍能看指南，但返回不能重置一次成功锁。原交易按钮位置保留，预览库存仅提示，服务仍重读实际世界/角色/背包/状态并决定是否提交。

中英新文案在代码i18n；物品来源文本仍从catalog生成CP i18n，不手改生成物。没有新存档键或指南阅读标志。组件回归复用真实`machine.checkForAction`取得生产菜单，不能将测试模组翻译器传给生产菜单，见[BUG022](../bugfix/BUG-2026-022-harness-menu-translation-context.md)。

## 构建与检查

| 命令/检查 | 实际结果与.local日志 |
|---|---|
| `python tools/catalog.py --write` | 通过；40/30及生成一致，P1激活范围不变 |
| `python -m unittest discover -s tests -p test_catalog.py -v` | 16项/exit0，p1-singleplayer-guide-catalog-tests-2026-11-02.txt |
| `python tools/build.py --game-path .local/validation-game` | 生产Release与开发ZIP通过，0警告0错误；p1-singleplayer-guide-production-build-2026-11-02.txt |
| SDK夹具Release，GamePath=.local/validation-game/BuildProjectReferences=false | 首次及BUG022修后均0警告0错误，p1-singleplayer-guide-harness-build-2026-11-02.txt和...harness-fixture-fix... |
| 停止部署及实际profile预检 | 首次85项恰28项build/包/文案变化，八save/六禁用config不变；p1-singleplayer-guide-deployment-2026-11-02.txt exit0 |
| 修复夹具后单独部署 | 相对首次恰8项夹具build/部署/profile变化，生产/包/文案不变；...fixture-fix-deployment... exit0 |
| 最终文字调整打包 | 11-03 p1-singleplayer-guide-final-wording-package-2026-11-03.txt exit0；同一生产DLL，0警告0错误 |
| 最终文案/包/停止现场核对 | ...final-wording-deployment-2026-11-03.txt exit0；中英键及tokens一致、ZIP等于源、恰8项文案/包/profile变化、CIM0/八save不变/六config禁用/实际预检0 |

本轮不重复与改动无关的112核心或完整116Python；C#规则/网络未改，16项目录与SDK菜单组件是相关验证。CI未在远程运行。最终`p1-singleplayer-guide-maintenance-2026-11-03.txt`实际exit0，维护结构/本地链接通过、0提交正文检查；`p1-singleplayer-guide-delivery-2026-11-03.txt`实际exit0，20项新增/修改公共文件（含未跟踪源码/数据/文档）UTF-8/空白/冲突标记、五项描述限定、生产C#仅菜单、分支/HEAD/diff0/当前85项/CIM0均通过；清单在delivery-text-audit.json。

## 真实SDK回归与界面查看

只启用runtime GameHarness的Memory/XYSP1UI/8624092802/OpenPreview=true；主模块和四角色网络探针保持禁用。使用原游戏鼠标“加载”固定单人副本，不自动创建或保存。原85项及累计147份源/数据/文档原字节在`.local/p1-singleplayer-guide-2026-11-02/{baseline.json,before,before-source}`。

首轮PID53352：原9项通过，缺料提示新场景因夹具翻译上下文错误失败，Restored=true；失败result/日志/实际旧安装DLL-PDB单独留failed-translation-fixture，关闭且核八save不变。归档私有脚本首次因SMAPI仅重排config JSON格式被字节断言拒绝，后以完整语义核对；源码已先修编译，修前源码原字节未单独保存，不能冒称after-fix副本为旧源码。实际修前二进制与原SDK红证据保留，不以人工报告代替。

修后新PID17744：23:54:59原生日志Single-player加载XYSP1UI春1；23:55:00完整18项通过/Failure=null/Restored=true，包含原16项及两个新增组件场景：

1. `guide navigation never acquires an operation and preserves the completed-menu lock`：指南开启/翻页/返回不创建Pending、不改库存/raw；看指南返回后已成功菜单仍不能再提交。
2. `missing ingredient preview reports deficit without request and retries when stock arrives`：缺料反馈含实际黄豆名及差额3、不创建请求/改库存/raw；补齐后同菜单成功一次。

原报告SHA `71fd1f7862bd699230bdd3b62ca928811dc0717fc0a2634ff28bd43e812ef306`；原18项逐一与同进程PASS日志对应，完整报告/日志/config/85退出snapshot在after-memory-green。生产DLL `f34e86be090967bfa3b5c253b3e2930d88072d8a4ab31ca2bdd1a86f0eed6148`、修后夹具DLL `744f9090c49479a0a2b1b1717580cb97ea274cb904060b5be3e518f5719b2258`。原失败9项不拼入18项，不覆盖。

Computer Use实际鼠标查看中文菜单的灰色缺料按钮、指南两页/翻页/返回，以及发酵缸“黄豆×3、小麦×2”的缺料反馈，画面未见遮挡/文字越框。图片只在工具会话中展示，没有编造本地截图文件。预览对象不在地图字典中，不能用它提交交易；真实开工/补料重试的证据来自上述已放置临时对象的SDK组件回归，而非预览点击。

随后将路线箭头改为中英“投入/产出”文字，避免字库将箭头显示成占位符；仅文案和ZIP更新，同生产DLL未变。最终文本未再次启动游戏，已核键/tokens/包原字节一致；不声称保留了最终文字的新截图。英文、手柄、更多UI尺寸尚未人工验收。

## 收尾与当前产物

23:59:55通过窗口X退出，未返回标题、不计标题清理；CIM0。原日志有两条预期事务故障注入ERROR（抛错及静默破坏后补偿），其余ERROR=0，无NRE/原生Save调用。原18结果归档/hash核验后将runtime已知诊断result.json恢复本轮原文件，Memory config恢复原字节；未知文件未删。

私有closeout初稿用联机“Main player”文字匹配单人日志而exit1，尚未创建after-memory-green目录；改为核真实`Single-player.`后，p1-singleplayer-guide-sdk-green-closeout-final-2026-11-03.txt实际exit0。原失败日志不覆盖、不称产品bug。八save全部原字节不变，六config禁用、85项匹配停止部署与实际身份预检通过。

最终alpha2开发ZIP SHA `514839e644dd71c414c3e9374862ca19353d7112a8ef75cb4dea457a09b7a0c1`，文件为`artifacts/XiangyeShiling-0.1.0-alpha.2-dev.zip`。ZIP没有游戏/SMAPI/GameHarness，包含匹配生产DLL和中英源文案。当前85项以final-wording-deployment/audit.json的afterHashes为准，不能再比旧网络版或旧文字包hash。

## 未完成与回退

P1普通地图商店/厨房触发、完整手工两设备20步、真实NPC赠礼、本轮新DLL原生保存链、手柄/更多UI尺寸仍待；不将18个组件或预览当整个P1验收。不开放联机写入，不扩P2。

回退先关游戏、禁用夹具，再按本轮before恢复已知生产/夹具DLL和匹配PDB、文案/profile元数据/开发ZIP并核hash；源码按本轮增量回退，保留累计未提交树。没有schema迁移，不能回滚/清空玩家物品或个人存档。下一步以[当前交接](../HANDOFF.md)及[单人状态](../P1_SINGLEPLAYER_STATUS.md)为准。
