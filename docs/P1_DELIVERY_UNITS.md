# P1 累计实现的审查与提交组织

更新：2026-11-06，Asia/Shanghai。本页整理累计本地实现的审查入口和提交依赖，不执行暂存、提交、推送或合并。当前实际状态见[交接](HANDOFF.md)，能力边界见[单人状态](P1_SINGLEPLAYER_STATUS.md)。

11-07执行说明：用户已明确授权上传目前文件，本页保留11-06盘点及依赖分析作为首次完整基线的组织依据；实际暂存、提交、推送和PR进度以HANDOFF及Git记录为准。原盘点172份与后来175份范围不同，不能将旧数字当作最终提交清单。

## 当前基线与来源

任务分支为`feat/p1-game-adapter`，HEAD为`8f2468d83b43fb805073f73bbc8b989cf716852e`。Git当前只跟踪README、DESIGN和DEVELOPMENT_PLAN三个文件；工程、内容、工具、测试和维护记录仍是累计未跟踪树。不能把`git diff`的少量输出当作全部变更，也不能将本地未提交成果描述成远程已经交付。

11-06首次盘点包含172个公共候选文件，暂存区为空；逐文件SHA、分类清单在本地`.local/p1-world-chain-2026-11-06/delivery-inventory-before-doc.json`，不包含本页后来新增的文件。只记录非忽略源文件，没有纳入.local、artifacts、游戏程序集、个人存档、完整机器日志或构建目录。本地清单只证明盘点时的内容，提交前要重新核对新增文件和源数据，不照旧清单盲目暂存。

## 审查单元

下列是审查分组，不代表各目录能在Git中机械拆成独立、可编译的提交。

| 单元 | 重点入口 | 要核对的行为与边界 |
|---|---|---|
| 登记源与内容包 | [catalog](../data/catalog.json)、[生成工具](../tools/catalog.py)、[原型生成](../tools/prototype.py)、[CP包](../content/XiangyeShiling.Content/content.json) | 冻结40物品/30配方，仅7物品/5配方激活；ID、原生制作键、商店图纸/菜谱身份一致；生成物不能手改；P2 planned不注入 |
| 单人规则与真实事务 | [规则](../src/XiangyeShiling/Machines/MachineRules.cs)、[状态](../src/XiangyeShiling/Machines/MachineStateCodec.cs)、[协调器](../src/XiangyeShiling/Transactions/CommitCoordinator.cs)、[适配器](../src/XiangyeShiling/GameIntegration/MachineService.cs) | Accepted只表示规则计划；真实扣料/整批领取、修订防重放、失败补偿、损坏/未知raw保留、单人所有权门禁必须一起看 |
| 玩家入口 | [补丁](../src/XiangyeShiling/GameIntegration/MachinePatches.cs)、[菜单](../src/XiangyeShiling/Menus/MachineMenu.cs)、[菜单会话](../src/XiangyeShiling/Menus/MenuActionSession.cs) | 放置占用格与工具保护、一菜单一成功、满包可重试、指南/缺料提示不创建事务；不要只看菜单点击成功 |
| 只读联机边界 | [会话](../src/XiangyeShiling/Networking/ReadOnlyProtocolSession.cs)、[只读桥](../src/XiangyeShiling/GameIntegration/ReadOnlyProtocolService.cs)、[协议说明](P1_HOST_PROTOCOL.md) | 默认禁用诊断、受限握手/拒绝/限额与旧会话隔离；NET03/04未实现，元数据不能作为远端背包可写证明，不能开放机器写入 |
| 无SDK检查和本地集成 | [MachineCore项目](../tests/MachineCore/MachineCore.csproj)、[GameHarness说明](../tests/GameHarness/README.md)、[GameAdapter说明](../tests/GameAdapter/README.md)、[Python测试](../tests/test_catalog.py) | 纯判据、人工报告、SDK组件和实际UI/原生保存分别陈述。严格保留红报告与失败范围，不拼接不同进程，不将夹具代码打包给玩家 |
| 工程与维护 | [构建](../tools/build.py)、[环境](ENVIRONMENT.md)、[工作流](../.github/workflows/validate.yml)、[维护检查](../tools/check_workflow.py)、[规范](../CONTRIBUTING.md) | 本地SDK依赖不分发；检查与完整commit正文对应；CI配置不等于远程已执行或分支保护已开启 |

## 不能直接按目录拆提交的原因

生产ModEntry和MachineService已经引用只读协议桥/Networking。只提交Machines、菜单或GameIntegration，遗漏同次引用的Networking，会得到不完整的工程。内容登记、生成CP数据、机器配方JSON和中英文本也必须作为同一一致性边界审查。

MachineCore直接编译生产Machines/Transactions/Networking/MenuActionSession，还链接GameHarness内的WorldToolProof、KitchenEntryProof、网络/输入判据和两CLI读取器；它不是完全独立的测试目录。将SDK夹具整目录留到后续而遗漏共享纯文件，或遗漏tools内的严格读取器，会使无SDK CI构建失败。反过来，不能为了满足引用就把本地游戏DLL/PDB、运行config、存档和日志暂存进去。

维护检查要求文档必备文件、bugfix索引与本地链接存在。只提交贡献规范却缺对应源码/验证记录，会让新克隆的维护检查失败；当前工作区文件存在不证明提交的树完整。

## 形成可审查提交的顺序

1. 先冻结待交付源树，用上述分组逐一审查并明确文件清单；保护已有未提交来源，确认忽略边界和稳定ID。未验收的单人世界链及NET范围必须留在文档中，不能为提交把状态改绿。
2. 首次可构建的P1开发基线须同时包含互相引用的生产源码、登记/生成内容、必要工具、共享无SDK测试文件及必备维护记录。若要拆得更细，应先设计可独立编译的边界并重新验证，不靠临时缺文件的提交凑历史。禁止直接使用本页作为`git add .`的授权或执行清单。
3. 对这个明确的提交树运行catalog、Python、MachineCore、两CLI和维护检查；有游戏依赖的构建/实机记录另外附环境与日期。已有历史结果是复核依据，不冒称冻结树当日全部重跑。若仅文档变化则按贡献规范缩小检查，不为文案重启游戏。
4. 首轮盘点的23份已有bugfix记录应随涉及的首次源码基线保留；后续新增调查也按实际状态列入，例如[BUG024](bugfix/BUG-2026-024-world-movement-investigation.md)尚未修复，不能混为已验收修复。没有独立历史提交时如实说明累计实现，不伪造逐项修复提交或历史SHA。后续新的fix提交再独立附能揭示问题的回归、原因/复现/解决方案及本次实际结果，并在同一提交更新bug文档。
5. 使用完整commit模板写实质正文，检查消息和明确暂存范围后才提交；检查新克隆或独立导出的提交树，而不只是在含未跟踪文件的工作区运行检查。
6. 按任务实际授权推送任务分支、创建PR到main，核真实远程检查/保护，再以merge commit保留历史。交付完整SHA/完整正文/PR/合并结果；未执行明确写未执行，不以审查分组或计划替代远程事实。

## 第一批提交正文应说明什么

背景写清这是从文档仓库补入累计P1开发原型；变更说明两设备加工、设备制作和厨房料理的五项激活配方、来源及只读联机边界。验证列实际命令、日期、环境和未测项，不能把所有历史数字写成该提交当天全部通过。风险要保留P1未总验收、P2冻结、联机写入关闭、原版占位美术与本地验证范围。

回滚以撤回代码/内容包、备份及保留玩家模组物品为前提，不把删除存档当作回滚。关联记录须包含开发计划、交接、对应验证与bugfix路径。模板正文通过结构检查后仍需人工核实其事实；当前没有实际新提交，所以没有可报告的本次SHA或PR。
