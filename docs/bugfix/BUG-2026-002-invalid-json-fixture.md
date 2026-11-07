# BUG-2026-002：损坏 JSON 测试输入未实际改变

- 状态: 本地验证通过，待提交/合并
- 发现/更新日期: 2026-09-25，Asia/Shanghai
- 责任任务/分支: P1 事务核心回归；本次补录在 `docs/development-workflow`
- 性质: 测试夹具错误；历史修复补录，未影响游戏运行行为

## 现象与影响

扩充严格状态格式测试时，MachineCore 的损坏 JSON 场景报 `Corrupt save was accepted.`。其中一个所谓“损坏数据”实际与合法保存结果相同，因而错误指责读取器接受了损坏数据。

影响是测试误报，不能由此推断存档读写器有漏洞；该规则层当时还未接入真实存档。没有本缺陷造成玩家数据变化的证据。

## 环境与前置条件

本地 SDK 7.0.302、目标 .NET 6；`MachineStateCodec.Write` 按记录字段顺序序列化。`ConsumedStack.Quality` 是对象最后一个字段，其后是 `}`，不是逗号。

## 复现步骤

旧测试使用 `saved.Replace("\"Quality\":2,", "")`。以下最小 C# 片段可独立说明失败条件：

```csharp
string saved = "{\"Slot\":0,\"ItemId\":\"(O)sample\",\"Quantity\":2,\"Quality\":2}";
string invalid = saved.Replace("\"Quality\":2,", "");
Console.WriteLine(invalid == saved); // True：根本没有生成负例。
```

在旧测试中，未变的字符串随后传给读取器并被正常接受，导致“损坏数据应该拒绝”的断言失败。历史执行确实出现上述失败文本，但未将完整失败控制台日志持久保存；这里保留确定的触发机制与可重建输入。

## 根因分析

测试用字符串替换模拟缺失字段，却假定字段后有逗号。`Replace` 未命中时不抛异常，原测试也没断言夹具已改变，因此合法输入混入负例集合。缺陷在夹具构造，不在读取器对该输入的处理。

## 解决方案

替换前置逗号与字段 `,\"Quality\":2`，正确删除对象最后一个属性。每个负例调用读取器前增加 `invalid != saved` 断言，使“夹具没改成”与“读取器错误接受”产生不同失败信息。原来的严格字段和重复键校验保留。

## 单元测试

- 文件：[tests/MachineCore/Program.cs](../../tests/MachineCore/Program.cs)。
- 场景：`corrupt or future save schema is rejected without replacement state`。
- 第一层断言：每个负例必须不同于原 JSON；第二层断言：`TryRead` 为 false 且输出状态为 null。
- 覆盖：无效 JSON、缺少设备类别、缺少品质字段、未知/重复字段、未来 schema、非法数量与结束时间。
- 修复前观察到 `Corrupt save was accepted.`；修复后该场景与其余 15 个规则场景通过。详细失败全文未保存，不附虚构日志。

```powershell
dotnet run --project tests/MachineCore/MachineCore.csproj -c Release
```

## 验证结果

2026-09-25：上方命令通过 16 个场景。仅验证规则层；没有因此重新启动游戏或宣称双人存档验证通过。本次规范补录只为断言添加缺陷关联注释，不改变逻辑；最终检查记录见 [HANDOFF](../HANDOFF.md)。

## 风险与回滚

无生产存档格式改变。未来更改序列化字段顺序时仍可能使字符串夹具失效，但新增的第一层断言会明确报出原因；可进一步用结构化 JSON 操作生成缺失字段，而不是删除这层校验。回退此测试修复会恢复误报，不应作为读取器修复手段。

## 关联记录

- [事务核心](../P1_TRANSACTIONS.md)、[开发计划 P1](../DEVELOPMENT_PLAN.md)、[交接](../HANDOFF.md)。
- Commit/PR：尚未提交或推送；首次提交时引用本记录，合并后补真实关联。
