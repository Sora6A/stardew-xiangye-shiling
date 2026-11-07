# BUG-2026-021：生命周期校验CLI未捕获严格读取器的无效数据拒绝

- 状态: 本地回归及实际无效输入CLI先红后绿通过；未提交/合并
- 发现/更新日期: 2026-10-12，Asia/Shanghai
- 责任任务/分支: P1-NET-02生命周期观察 / feat/p1-game-adapter
- 性质: 本轮新离线工具开发期异常边界缺陷，不是游戏事故；实际旧实现回归失败已保存

## 现象与影响

给新CLI两份`{}`不完整JSON时，严格读取器按契约抛InvalidDataException，但入口未将其转换为安全REJECT/exit1，实际进程退出3762504530并打印未捕获异常/仓库路径堆栈。没有成功摘要或输入改动，但违反稳定拒绝与隐私输出契约；工具尚未部署到游戏，不影响生产存档。

## 环境与前置条件

Windows/.NET6目标、标准JSON解析；独立NetworkLifecycleCheck工程不引用游戏/SMAPI。完全人工`{}`输入，无真实角色或保存。源及CLI原始stdout/stderr留`.local/bug021-2026-10-12`，公开仅此脱敏摘要。

## 复现步骤

在修前工具已Release构建后，用两个相同的人工不完整报告路径执行：

```powershell
dotnet tools/NetworkLifecycleCheck/bin/Release/net6.0/NetworkLifecycleCheck.dll <人工host.json> <人工client.json>
```

具名实际CLI回归`incomplete_report_returns_one_without_success_or_stack_trace`要求exit1/无stdout/单行REJECT、不输出栈。`python .local/check-bug021-cli-2026-10-12.py red`实际断言失败、wrapper exit1；真实子进程3762504530/stdoutEmpty=true/safeRejection=false。before-source保留Program/reader/csproj/测试原字节，red-result及两原输出不覆盖。

## 根因分析

reader.Fields用InvalidDataException表示缺失/未知/重复字段；CLI的catch filter只有IOException等类型，实际InvalidDataException未匹配并越过错误边界。首次新增reader负例也遗漏此类型的预期拒绝捕获，Core已有97＋reader成功序列化项后在缺字段用例同样出现未捕获异常。首次97不含读取器四用例，不能写全部已绿。

## 解决方案

在CLI入口及测试负例捕获中明确包含InvalidDataException，继续只返回REJECT/1、无成功摘要，不放宽reader必填/严格字段/大小上限，也不使用catch-all掩盖编程错误。没有存档格式、线上协议或生产可写能力变化。

## 单元测试

[NetworkLifecycleReaderScenarios](../../tests/MachineCore/NetworkLifecycleReaderScenarios.cs)的`lifecycle strict reader refuses missing top-level and nested capability fields`及重复/别名/未知/超大/类型用例检查严格拒绝；修前完整Core在前者出现实际未捕获异常，`p1-net02-lifecycle-core-final-2026-10-12.txt`留红。命令`dotnet run --project tests/MachineCore/MachineCore.csproj -c Release`修后101项通过，四个reader场景含严格拒绝及原输入字节/hash不变。另有上面的实际CLI同输入回归；不是把原生联网或人工成功报告当作缺陷修前红。

## 验证结果

2026-10-12实际正向集成补充：独立NetworkLifecycle在真实同进程重连产生的完整schema1原双报告和退出后最终报告，经本CLI严格reader及共享PairMatches各exit0，源码/DLL不变，原输入SHA与摘要相同、报告未修改；仅重新握手/host会话保持true、Full等false，见[实际双报告/退出审计](../validation/P1-network-lifecycle-2026-10-12.md)。首轮实际角色反向及retry超时的原报告均安全REJECT/1/stdout空，属于按设计拒绝完整失败报告；它们不是本bug的缺字段修前红。原具名不完整输入的实际1红1绿及Core101记录保留，本次不重新生成或覆盖红证据。

修前具名CLI回归已实际失败，源码/原始stdout/stderr/真实退出码均留档；修后Core101实际exit0，日志`p1-bug021-core-green-2026-10-12.txt`；CLI Release0警告0错误/exit0。相同人工输入执行同回归green实际exit0，子进程exit1/stdoutEmpty=true/safeRejection=true，日志`p1-bug021-cli-green-2026-10-12.txt`，red/green原输出及源码不覆盖。首次CLI构建过宽Networking通配符引入未链接事务类型，缩为四必要纯协议源后通过，不是本异常红。SDK夹具构建/停止部署通过，六配置/八save仍禁用春24基线；真实重连验证另见[生命周期记录](../validation/P1-network-lifecycle-2026-10-12.md)，不代本CLI红。

## 风险与回滚

严格解析只验证报告内容，不证明数据来自真实SDK；仍须归属日志、进程/DLL及保存审计。回滚只停用新CLI或恢复已留旧源码；旧入口会再次对无效数据崩溃，不能因此删除字段或手改报告。个人存档/配置不改、不回滚保存。

## 关联记录

[计划](../DEVELOPMENT_PLAN.md)、[HANDOFF](../HANDOFF.md)、[实际CLI](../../tools/NetworkLifecycleCheck/Program.cs)、[严格reader](../../tools/NetworkLifecycleCheck/NetworkLifecycleReportReader.cs)。当前HEAD8f2468d83b43fb805073f73bbc8b989cf716852e是基线，无本轮commit SHA/PR/推送/merge/远程CI；不能冒充修复提交。
