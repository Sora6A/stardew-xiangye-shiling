# BUG-2026-017：离线网络报告校验的入口与证据边界缺陷

- 状态: 本地回归及10-12真实Basic双报告正例/最终Failure拒绝集成通过；完整NET02仍待，未提交/合并
- 发现/更新日期: 2026-10-02发现，2026-10-12补核，Asia/Shanghai
- 责任任务/分支: `P1-NET-02` 离线双端报告配对，`feat/p1-game-adapter`
- 性质: 新开发工具的 CLI 与证据校验缺陷；本轮新增回归后实际留红再修复，非历史补录、非游戏中已发生的玩家事故

## 现象与影响

第一阶段首次执行 37 项专项时实际出现三个失败：合法独立临时工作区的 CLI 输出被拒；输入报告声称 `ReconnectVerified=true` 却仍被基础配对接受；未知附加字段中的 `1e999` 转为非有限浮点数后仍被解析接受。

预期是 CLI 使用调用时的工作区边界，只在该工作区 `.local` 内输出且不覆盖源报告；基础探针只能证明当前合成目标的有限证据，拒绝报告自称已验收重连或完整 NET02；任何位置的非有限 JSON 数值都拒绝，不能因该字段暂时未参与判据而绕过入口边界。

工具只离线读取两份报告并生成可选摘要，不操作游戏、网络、配置或存档。上述问题影响报告配对可用性与结论可靠性，没有打开远端库存/机器写入，也没有已知玩家数据损失。人工报告通过不能写成实际双端联网通过。

第一阶段修复后 37 项转绿。随后主任务审查又实际发现两种可被接受的人工负例：客户端缺少 Hello 的 SendAttempt；客户端同一个实际 quota 窗口内八条 Allowed 读取都报 Count=8。第二阶段新增回归后，40项专项实际出现2个failures；修复后最终77项Python全部通过，其中包含当前40项专项。两轮红证据与首次37项绿分别保留，不能混写成同一轮结果。

## 环境与前置条件

本机 Windows、Python 标准库 `unittest`；环境 Python 基线见 [环境记录](../ENVIRONMENT.md)。专项测试为 [test_network_reports.py](../../tests/test_network_reports.py)，直接导入实际 [check_network_reports.py](../../tools/check_network_reports.py)，没有复制一份校验逻辑。

`synthetic_pair()` 手工构造 schema-2 Host/Client 报告，使用固定人工 GUID、独立进程/run 标记、互为 peer、共同 session/fixture、各端本地 generation/线程/连接标记、五项 probe 与九发八回/主机八读一拒的轨迹。它们是测试输入，不是游戏或 SMAPI 实际记录。报告中机器集合可为空，不构成真实设备 raw 保护证明。

CLI 测试使用 `TemporaryDirectory` 建立独立工作区及 `.local`，两份 JSON 输入和摘要均在该沙箱内。通过 `patch.object(reports, "ROOT", self.root)` 和 `sys.argv` 执行实际 `main()`；不会写正式工作区 `.local`、启用夹具、改旧 host/client 配置或读取个人完整存档。工具输入可以位于调用者指定位置；只有可选输出受工作区 `.local` 的严格子路径限制。

## 复现步骤

以下针对第一阶段修改前的实际工具。红日志已保留，当前直接运行会使用更新后的源码，不能重现旧错误后仍声称运行了原版本。

1. 先加入 37 项人工报告及 CLI 专项。其中合法 CLI 正例将导入后的 `ROOT` 指向独立临时工作区，指定沙箱内 `--host`、`--client`、`--output`。预期退出0、写入基础摘要并保持两份源报告字节不变；旧入口实际退出1，打印通用 `REJECT`。
2. 在完整人工报告对中仅将 Host 的 `ReconnectVerified` 改为 true。预期 `validate_pair()` 抛 ValueError；旧实现没有抛出。
3. 在合法 Host JSON 的未知附加字段 `AdditionalSyntheticNumber` 中放入 `1e999`。预期 `read_report()` 抛 ValueError、CLI 退出非零且不创建摘要；旧解析没有抛出。该项也包含 NaN、Infinity、-Infinity，但第一阶段实际失败子项只有 `1e999`。
4. 未修复这三个问题前实际执行：

   ```powershell
   python -m unittest discover -s tests -p test_network_reports.py -v
   ```

   第一阶段结果为 `Ran 37 tests`、`FAILED (failures=3)`，退出1。证据为 `.local/bug017-network-report-red-2026-10-02.txt`；管道 Tee 只负责保存日志，不改变专项命令。
5. 修复三处边界后同命令实际为 37 项全部 OK、退出0，证据 `.local/bug017-network-report-green-2026-10-02.txt`。

第二阶段在首次三处修复后，先补具名 Hello 发送证据与客户端 quota 计数回归，再运行同一专项命令：实际 `Ran 40 tests`、`FAILED (failures=2)`、退出1，证据 `.local/bug017-network-report-second-red-2026-10-02.txt`。两项均为 `ValueError not raised`，不是历史重建旧逻辑或游戏实测。

随后修复第二阶段判据，主任务实际运行全量：

```powershell
python -m unittest discover -s tests -v
```

结果为77项全部OK、退出0，其中当前报告专项40项；日志 `.local/p1-network-pair-python-green-2026-10-02.txt`。首轮37绿是较早一轮，不能替代最终77的证据。

## 根因分析

1. `validate_output(..., root=ROOT)` 的默认参数在模块导入时捕获原 ROOT。`main()` 未显式传 root，调用者或测试把模块 ROOT 切到独立工作区后，路径校验仍使用导入时的旧工作区。这不是当前工作目录识别失败，也不是普通游戏安装目录问题。
2. `endpoint()` 原先没有将 `ReconnectVerified` 纳入基础阶段必须为 false 的能力声明。其余轨迹符合时，工具接受了“已测重连”的输入宣称；基础 probe/配额证据并不能支持重连结论。
3. JSON 的 `parse_constant` 能拒绝 NaN/Infinity 字面量，不能拦截合法数字语法 `1e999` 的浮点转换溢出。未知字段允许保留，之后又没有进入已知整数/证据校验，因此 Infinity 漏过入口。

第二阶段还有两处证据缺口：匹配 challenge 的 WelcomeAccepted 被当作客户端握手证据，却没有要求客户端此前实际尝试发送 Hello；配对代码只要求客户端 quota.Allowed=true，没有重建其本地窗口中每次正文读取应占用的连续计数。主机burst八读一拒成立，仍不能让客户端八条回执读取全部使用同一个Count=8成为可能轨迹。

这是离线工具首次完整专项暴露的问题。既有核心场景验证实际只读会话、配额与夹具判据，不覆盖新 Python CLI 的动态工作区或完整 JSON 文本入口；格式良好的人工报告正例也无法揭示能力声明遗漏。两轮回归逐步补足入口、客户端握手发包与两端接收配额守恒，并未执行旧版或新版双端游戏。

## 解决方案

第一阶段修改实际 [check_network_reports.py](../../tools/check_network_reports.py)：

- `main()` 显式调用 `validate_output(..., root=ROOT)`，在调用时使用当前工作区，保留 `.local` 严格子路径、不可覆盖任一源报告、不可写目录/共享链接目标等约束。
- 两端 `ReconnectVerified` 必须显式为 false，与 Saved、FullNet02Accepted、ReturnedToTitleObserved、TitleSessionCleared 的基础阶段边界一致；缺字段或形似布尔值不能补默认通过。
- `json.loads` 增加 `parse_float`：先转 float，再用 `math.isfinite` 拒绝数值溢出。保留 `parse_constant`、任意层级重复键拒绝、UTF-8 输入和已知字段严格类型校验；允许未知附加字段不等于允许非有限值。

附加防回退措施：读取报告时最多读 `MAX_REPORT_BYTES + 1`（上限2 MiB加1），超过上限即拒绝，避免先无界分配再检查长度；Host Step 必须 `type(value) is int` 且为0，不能让 False 或0.0按相等比较通过。这两处没有独立修复前红日志，不将它们写成另两项先红后绿。

输出仍只能表示 `BasicProbeEvidenceVerified=true`，`FullNet02Accepted` 恒 false；合成目标不证明机器准入、远端背包写权、SDK 生命周期或恶意 peer 认证。校验失败在写摘要前停止，源报告字节保持；不存在摘要不等于源报告缺陷已经修复，须看失败判据。

第二阶段追加两项实际修复：

- 客户端已接受的Welcome之前必须存在 `XYS/Hello/1` SendAttempt，使用本端相同连接、世界、断连generation和owner线程核验；该发送记录不能带入站Observation/Quota。正确challenge本身不能替代实际发送轨迹。
- `validate_local_quota()` 遍历两端完整、未淘汰的本地Processed轨迹，按本地世界/peer/ConnectionId重建窗口：新窗口Allowed=true且Count=1；同窗Allowed计数逐次递增；新窗口起点至少经过1000ms；受限记录Count=8、正文未读、无RequestId/Reason。BodyReadAttempted须与Allowed一致。其他peer断连不重置当前peer额度，因此DisconnectGeneration不作为配额重建key，但证据区间仍受各端baseline上下文约束。

主机固定窗口的八次读取与第九次读取前拒绝、客户端八个实际接受ID和九次唯一发送等原判据继续保留；不读取第九条受限正文来伪造RequestId，也不放宽trace丢失、世界/连接/线程和跨端关联边界。

## 单元测试

第一阶段命令见复现步骤；三个具名失败均在首次完整 37 项执行中实际出现：

| 测试名称 | 关键断言 | 第一阶段修复前 / 后 |
|---|---|---|
| `test_cli_success_writes_limited_result_and_preserves_both_report_bytes` | 实际 main 使用独立临时 ROOT；合法 CLI 退出0，摘要 Basic=true/Full=false，两份源报告字节不变 | 红：`0 != 1`，合法输出被拒；绿：整项通过 |
| `test_failed_saved_or_claimed_full_acceptance_reports_are_rejected` | 报告声称失败、保存、完整验收、重连等时拒绝 | 红子项：`field='ReconnectVerified'`，`ValueError not raised`；绿：整项通过。其余子项没有同轮失败，不扩写为均有独立红 |
| `test_parser_rejects_non_finite_numbers_even_in_ignored_additional_fields` | parser与CLI拒绝未知附加字段非有限数，不创建摘要 | 红子项：`token='1e999'`，`ValueError not raised`；绿：整项通过。NaN/Infinity/-Infinity 不是该轮失败子项 |

其余34个测试在第一阶段红轮已通过，覆盖共享 session/fixture、互为 peer、不同本地标记、trace完整性、基础拒绝关联、显式配额、九发八回与主机未读第九条、输入/输出隔离等，第一阶段绿轮仍通过；没有这34项各自的旧失败证据，不宣称全部都有红转绿。

第二阶段具名结果：

| 测试名称 | 关键断言 | 第二阶段修复前 / 后 |
|---|---|---|
| `test_client_welcome_requires_a_preceding_actual_hello_send_attempt` | 删掉客户端Hello发送轨迹，即使challenge匹配、WelcomeAccepted存在仍拒绝 | 40项红轮：`ValueError not raised`；最终77项中通过 |
| `test_each_local_quota_window_must_consume_allowed_counts_once` | 客户端同窗八条回执读取全Count=8必须拒绝，不能只看Allowed=true | 40项红轮：`ValueError not raised`；最终77项中通过 |

第二红轮其余38项通过，其中新增 `test_extra_burst_response_or_drop_cannot_be_silently_ignored` 验证额外burst回复/受限记录不能被忽略，没有该项独立旧失败证据。超过2 MiB输入的拒绝测试不单独证明旧代码读取分配有界；Host Step严格int也没有独立修前失败记录。实际红证据只按日志列出的失败和子项记录，不扩大到其余断言。

## 验证结果

| 检查 | 实际状态 |
|---|---|
| 第一阶段专项旧实现 | 2026-10-02：37项执行、3 failures、退出1，红日志保留 |
| 第一阶段专项三处修复后 | 同日37项全部OK、退出0，绿日志保留；这是第二阶段修改前结果 |
| 第二阶段专项旧判据 | 同日40项执行、2 failures、退出1，第二红日志保留 |
| 最终全量 Python | `python -m unittest discover -s tests -v`：77项全部OK、退出0，含当前40专项；最终green日志保留 |
| 核心 | 85项通过，`.local/p1-network-pair-core-green-2026-10-02.txt`；不代表游戏库存或transport |
| 生产与夹具 Release | 主任务实际执行，两构建均0警告/0错误；不覆盖真实双端运行 |
| 真实报告配对 / 游戏 / 双端网络 | 10-02当时未执行；10-12真实原活动双报告Basic配对通过，最终断连Failure报告预期拒绝，标题仅局部观测；NET02未总验收 |
| 最终部署、配置、源/副本审计 | 主任务实际执行退出0；10文件包/build/runtime/新两端一致，六配置原文保留且探针/夹具false、独立ID=0；旧profiles及六存档文件未变，证据`.local/p1-network-pair-package-audit-2026-10-02.json` |
| 提交 / 推送 / PR / 合并 | 未执行，无修复提交SHA或远程成功结果 |

完整命令、包审计与未执行边界见 [本轮离线配对验证](../validation/P1-network-pair-2026-10-02.md)。本轮审计显式记录 `actualReportPairChecked=false`、`networkStarted=false`、`newNetworkFarmCreatedOrInstalled=false`、`fullNet02Accepted=false`；包同步或人工报告测试不能代替这些实测。旧probe/host-core验证属于历史范围，本记录不将其中81/37等数量作为本轮新源码结果。

10-12实际集成补充：原活动host/client Schema2/Episode1双报告CLI exit0/Basic=true/五probe/同peer窗口8读1未读/Full=false，各端库存引用值/raw不变，机器数均0；输入原SHA及命令见 [真实Basic记录](../validation/P1-network-basic-2026-10-12.md)。随后客户端离开、host最终报告保留超时Failure，使用最终双活动报告实际CLI exit1/REJECT且未生成输出，不拿原成功摘要覆盖失败；title仅本地session清理，85项收尾/八save不变/六config原字节禁用/CIM0均核过。这是新增真实正例/负例集成，不借当年的人工红证明每个实机错误输入均已覆盖，不新增源码修复或宣告FullNET通过。

## 风险与回滚

报告是本地观测文件，离线一致性校验不能证明文件真实来自网络、不构成密码学认证或完整NET02验收。两端本地时间、generation、ThreadId、ConnectionId不能要求跨端相等；共享session/fixture/player/request关联也不能支持远端写权。机器集合为空时，只能记录空集合保持，不能标记真实设备保护通过；Saved=false还需外部磁盘哈希证明。

重复运行前归档原始两份报告与旧摘要。`--output`必须是当前工作区`.local`内的文件且不等于任一输入；读取失败、超过上限、重复键或证据不完整时不得覆盖旧摘要。工具不启用探针或夹具，不修改host/client根配置；旧Persistence Stage3和其他源存档不能作为报告校验沙箱，更不能为取得绿色而重写其marker。

回滚限定为工具和对应专项测试的撤回，保留输入报告、失败日志和本记录。若回退到旧校验器，必须停止将其结果作为验收依据；不要通过放宽输出目录、忽略声明或删掉拒绝记录来“修复”测试。没有设备schema、稳定ID、配方、依赖或存档迁移，无需清理玩家背包/设备/存档。停止实机工作时仍应关闭两端Harness.Enabled、OpenPreview与主模块EnableReadOnlyNetworkProbes，保留原始证据。

## 关联记录

[实际工具](../../tools/check_network_reports.py)、[专项测试](../../tests/test_network_reports.py)、[本轮离线配对验证](../validation/P1-network-pair-2026-10-02.md)、[主机契约](../P1_HOST_PROTOCOL.md)、[NET02准备](../NET02_TEST_SETUP.md)、[计划](../DEVELOPMENT_PLAN.md)、[交接](../HANDOFF.md)、[贡献规范](../../CONTRIBUTING.md)、[bugfix模板](TEMPLATE.md)、[BUG-016](BUG-2026-016-network-probe-evidence.md)。索引与公共交接由根任务同步，不改其他负责人文件。

实际分支 `feat/p1-game-adapter`，基线 `8f2468d83b43fb805073f73bbc8b989cf716852e`；基线不是本修复提交。全部相关改动未提交、未推送、无PR或合并；后续实际提交/发布后再补真实SHA、完整commit正文和远程结果，不能预填。
