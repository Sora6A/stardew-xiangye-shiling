# BUG-2026-013：协议请求包含孤立 Unicode 代理项时越过读取失败边界

- 状态: 本地共享源码回归先红后绿通过，待提交/合并；网络入口未接入，实机网络未验收
- 发现/更新日期: 2026-10-01，Asia/Shanghai
- 责任任务/分支: `P1-NET-01` 主机请求核心，`feat/p1-game-adapter`
- 性质: 新增协议 reader 的代码缺陷，开发期负例发现；非历史补录、非线上事故

## 现象与影响

严格 reader 预期对无效 JSON 返回 false/null，不将异常抛给上层。实际请求的 Action 为转义孤立高代理项 `\uD800` 时，System.Text.Json 已解析结构，但 GetString 解码抛 InvalidOperationException，越过只捕获 JsonException 的边界，核心场景进程异常终止。

网络处理器尚未注册，没有此载荷的真实 peer/玩家执行记录，没有已知存档损失。若直接构造的名称带孤立代理项，旧名称校验也可能接受；随后序列化会改变字符，不能保证账本绑定的原意图保持。未知载荷应被拒绝而不是修补字符后执行。

## 环境与前置条件

Windows、SDK 7.0.302、net6.0。MachineCore 直接链接生产 Networking 源码，不加载游戏。合法基准请求为 protocol1、非空三个 GUID、actor10、FarmHouse(8,7)、revision0、Start/tofu；原值只用于纯测试，不关联真实农场。

## 复现步骤

1. 使用修复前 MachineRequestCodec：TryRead 仅 catch JsonException，ValidName 不检查完整 UTF-16。
2. 在 ProtocolScenarios 加入 `protocol malformed unicode is rejected without throwing or replacing the request`，将合法 JSON 的 `"Action":"Start"` 替换为 `"Action":"\uD800"`，断言 TryRead 为 false 且输出 null。
3. 执行 `dotnet run --project tests/MachineCore/MachineCore.csproj -c Release`，在该测试首个负例实际抛 `InvalidOperationException: Cannot read incomplete UTF-16 JSON text as string with missing low surrogate.`，调用栈位于 MachineRequestCodec.TryRead 的 GetString。
4. 有文件重定向的修复前轮实际退出码为 `-532462766`（Windows 未处理 CLR 异常），输出保留 `.local/bug013-malformed-unicode-red-2026-10-01.txt`。此前28项与请求 roundtrip通过；后续协议/账本/串联未执行到，不能把红轮记为全量通过。此前一次无重定向同反例的工具会话回收退出码为1，不用它替换此正式红证据的实际码。
5. 修复后同一个具名回归及完整命令实际通过。当前 reader 已修正，不能将留档旧异常冒充当前失败。

## 根因分析

JsonDocument 对字符串转义的结构解析成功不等于字符串已经能解码。Action/GetString、GUID/GetString 和属性 Name 都可能在读取孤立代理项时抛 InvalidOperationException；只捕获 JsonException 留下了可执行的异常缺口。

名称长度、控制字符和 Trim 检查不能识别缺失代理项对；直接构造对象与 JSON 原文的 UTF-8 转码还可能替换无效 UTF-16，改变作为账本键/意图比较一部分的名称。最初35项只覆盖语法/字段/数字/字节长度，没有包含Unicode解码负例。

## 解决方案

TryRead 在 JSON 处理边界补 InvalidOperationException 捕获，返回 false/null；不吞外围游戏/提交异常。入口在 UTF-8 转码前检查字面 UTF-16 是否完整；ValidName 对直接内存请求应用同一检查，Write 拒绝无效请求。ValidUtf16 允许合法高低代理项对，拒绝孤立高/低代理项，不规范化原文本。

反例同时覆盖 Action、GUID、名称、配方和属性名的转义孤立代理项、原字符串的字面孤立项；合法中文/emoji roundtrip为正控制。没有放宽协议形状、增加持久化字段或改变生产加工/联机权限。

## 单元测试

文件：[ProtocolScenarios](../../tests/MachineCore/ProtocolScenarios.cs)，测试名 `protocol malformed unicode is rejected without throwing or replacing the request`。命令：

```powershell
dotnet run --project tests/MachineCore/MachineCore.csproj -c Release
```

| 断言 | 修复前 | 修复后 |
|---|---|---|
| 转义孤立高/低代理项返回 false/null，不抛异常 | Action首例实际抛异常，后续断言未执行 | 所有字段/属性名负例通过 |
| 原 JSON 字面孤立项拒绝 | 新增补强，红轮首例终止，未执行到 | 通过 |
| 直接构造 LocationName/RecipeId 的孤立项 invalid，Write 抛 ArgumentException | 红轮首例终止，未执行到，不伪称分别复现 | 通过 |
| 合法中文与完整 emoji 代理对 roundtrip保持请求 | 正控制未在红轮执行到 | 通过 |

## 验证结果

2026-10-01 修复后完整核心45项退出0，含8协议/准入、6账本、3内存串联；生产 Release 0警告/0错误。新开发包仅10文件且build/ZIP/部署一致，源/UI四文件哈希不变；真实游戏/SMAPI消息入口未执行也未接入，网络实机保持未验收。实际本地证据见 [主机核心验证](../validation/P1-host-core-2026-10-01.md)。未提交、推送、PR或合并；没有本修复SHA。

## 风险与回滚

此修复只封闭reader异常边界，不认证peer、不限制传输层此前分配、不提供远端背包事务。后续传输仍须限制消息大小/频率，并先核验来源/会话。回滚只撤回新Networking核心和相关测试，保留该记录/红证据；如果保留旧reader，不能接入不可信请求。没有玩家数据迁移，不删除/重置正式存档。

## 关联记录

[P1 计划](../DEVELOPMENT_PLAN.md)、[主机契约](../P1_HOST_PROTOCOL.md)、[交接](../HANDOFF.md)、[MachineRequestCodec](../../src/XiangyeShiling/Networking/MachineRequestCodec.cs)、[回归](../../tests/MachineCore/ProtocolScenarios.cs)、[实际验证](../validation/P1-host-core-2026-10-01.md)。基线 `8f2468d83b43fb805073f73bbc8b989cf716852e` 不是此修复提交；后续fix须关联本记录、揭示测试及完整正文。
