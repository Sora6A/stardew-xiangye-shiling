# 首次完整源码上传：2026-11-07

## 已完成的上传

按用户“先把目前的文件都上传到git仓库”的要求，在原任务分支feat/p1-game-adapter提交并推送175份公共项目文件。首次完整开发基线为`baebea0b57a1edc51e5531bfaa909fb49e844de2`，父提交为原文档基线`8f2468d83b43fb805073f73bbc8b989cf716852e`。没有改写原历史或将累计修复伪造为逐项旧提交。

[PR #1](https://github.com/Sora6A/stardew-xiangye-shiling/pull/1)目标为main，包含源码、内容、目录登记、生成工具、无SDK检查、本地SDK夹具和维护文档。本记录及交接补充作为后续文档提交附入同一PR；本文记录合并前的已核状态，最终合并SHA/状态以PR及交付完整提交附件为准，不预写未发生的结果。

## 文件范围与自审

首批175文件约1.6MB，均为文本。按明确清单暂存并从索引导出独立源码树，检查跨目录引用完整性，未借未上传的本机文件通过。排除了.local、artifacts、bin/obj、游戏程序集、个人存档、完整本机日志和本机配置；没有上传开发ZIP或在本次任务启动游戏/改运行部署。源安装及实机证据原件仍只在本地。

公开文件UTF-8/冲突标记与常见密钥格式检查没有发现待处理项；用户目录示例改为APPDATA变量。最终暂存检查发现的.editorconfig/.gitignore/bugfix索引三处多余EOF空行已清理。提交按仓库既有Sora公开noreply身份生成，未修改全局Git身份。

自审覆盖文件清单/忽略边界、源码依赖、内容生成一致性、导出树可构建性及文档实际边界，没有声称其他维护者已审查。

## 本地与远程验证

以下为11-07在索引导出的源码树实际执行，原输出仅留本地`.local/upload-2026-11-07`：

| 命令/范围 | 实际结果 |
|---|---|
| `python tools/catalog.py` | 40物品/30配方、来源用途闭环、manifest与生成文本一致 |
| `python -m unittest discover -s tests -v` | 116通过 |
| `dotnet run --project tests/MachineCore/MachineCore.csproj -c Release` | 115通过 |
| 两套Network报告CLI Release构建 | 均0警告/0错误 |
| 生产模块及GameHarness Release构建 | 合法本机1.6.15/SMAPI4.4.0引用，0警告/0错误 |
| GameAdapter实际SDK CLI | 13通过，未启动游戏或读写存档 |
| 维护/本地链接、175文件UTF-8/冲突、最终暂存差异与完整commit正文 | 通过；首次提交后范围检查实际检查1个完整提交 |

远程首次提交的PR工作流[42323878067](https://github.com/Sora6A/stardew-xiangye-shiling/actions/runs/42323878067)已实际完成：catalog、machine-core、maintenance、commit-messages四项均success，绑定head为上述baebea0b完整SHA。push工作流的commit-messages按既有条件跳过，PR工作流已实际检查；不能把跳过写成该次push也执行消息校验。后续文档提交和合并的CI应核各自实际head，不能沿用首次绿色结果。

上传前只读查询main的protected=false、分支规则列表为空；未设置或修改远程保护。合并流程仍按本仓库PR及merge commit要求执行。

## 能力边界与回滚

P1尚未总验收、P2冻结，NET03/04未实现，联机写入关闭；BUG024旧输入失败根因仍未定。11-06世界20项/三店和此前厨房赠礼证据保留原日期，本次没有重跑实机，也没有将历史保存结果当作当前DLL的完整保存验收。下一开发任务仍是当前版本原生保存/重载和冷启动防重复领取。

回滚在任务分支git revert，再经PR保留历史。已安装原型先备份，并保留或处理模组物品后再撤对应代码/内容包，不能清空玩家存档或机器状态来回滚。维护入口见[HANDOFF](../HANDOFF.md)、[交付组织](../P1_DELIVERY_UNITS.md)、[缺陷索引](../bugfix/README.md)。

## 首次推送提交的完整信息

下方正文直接从实际Git提交读取，不是标题摘要；本记录自身后续文档提交及最终merge提交在交付附件中提供完整SHA与原正文。

SHA: `baebea0b57a1edc51e5531bfaa909fb49e844de2`

```text
feat(p1): 纳入累计单人机器原型、验证工程与维护基线

## 背景与目的
按2026-11-07用户要求，将此前仅在本地的P0/P1累计实现完整上传仓库。原Git基线只有三份文档，本提交形成可独立检出和构建的175文件开发基线，保留历史提交与已有缺陷证据。

## 变更内容
纳入SMAPI模块、Content Patcher内容包、40物品/30配方登记源及生成工具，当前只激活7物品/5配方。实现两设备共享菜单、真实整批扣料/领取、版本化状态、防重复提交、失败补偿及工具保护，附玩家指南和有限商店来源。保留默认禁用的只读联机握手/诊断、无SDK规则与报告CLI、本地游戏适配/实机夹具、24份缺陷或调查记录及贡献/交接/CI规范。源文件按明确清单导出检查，排除本机运行时、程序集、存档、完整日志和构建产物；使用APPDATA示例并清理三处EOF空行。

## 验证结果
2026-11-07在暂存区独立导出的源码树执行：python tools/catalog.py通过40物品/30配方及生成一致性；python -m unittest discover -s tests -v通过116项；dotnet run --project tests/MachineCore/MachineCore.csproj -c Release通过115场景。两套Network CLI Release构建、带本机1.6.15/SMAPI4.4.0引用的生产/GameHarness Release构建均0警告0错误；GameAdapter实际SDK CLI通过13项，未启动游戏或读写存档。维护/本地链接、UTF-8/冲突标记及最终暂存差异与完整消息校验通过。11-06同进程世界输入20项及存档不变证据保留原日期；本次未重跑实机，提交时远程CI尚未执行。

## 影响与风险
这是0.1.0-alpha.2开发基线，P1尚未总验收，P2冻结。当前生产版本保存/重载与冷启动闭环仍待补验，NET03/04未实现，联机写入禁用；BUG024旧输入失败原因仍调查中。原版占位美术及本机测试组合不代表正式发布兼容承诺。游戏/SMAPI/CP不在Git中，SDK构建需合法本机依赖；无SDK检查可独立运行。未将历史累计修复伪造为逐项新修复提交，也未修改玩家存档或运行部署。

## 回滚方案
在任务分支使用git revert撤回本提交，经PR保留历史后合入main；不要强推或删除玩家数据。已安装原型的使用者先备份存档，并保留或妥善处理模组物品后再撤回对应代码/内容包；不能用清空存档或删机器状态作为回滚。保留历史bugfix和验证原证据以便复查。

## 关联记录
Plan: docs/DEVELOPMENT_PLAN.md
Handoff: docs/HANDOFF.md
Review: docs/P1_DELIVERY_UNITS.md
Validation: docs/validation/P1-world-ui-2026-11-06.md
Bugfix: docs/bugfix/README.md
Investigation: docs/bugfix/BUG-2026-024-world-movement-investigation.md
```
