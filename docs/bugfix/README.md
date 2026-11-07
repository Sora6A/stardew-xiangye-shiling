# Bugfix 记录索引

每个缺陷一份记录：`BUG-YYYY-NNN-topic.md`，编号在年度内递增且不复用。复制 [模板](TEMPLATE.md)，填写根因、复现、解决方案和单元测试，再更新此表。修复代码、对应测试和记录应在同一 `fix` 提交中；完整流程见 [贡献规范](../../CONTRIBUTING.md)。

| ID | 问题 | 状态 | 单元/回归入口 |
|---|---|---|---|
| [BUG-2026-001](BUG-2026-001-smapi-baseline.md) | CP 2.9 与初始 SMAPI 4.1.10 基线不匹配 | 历史修复补录，本地验证通过，待提交/合并 | `CatalogTests.test_smapi_baseline_cannot_regress_in_either_manifest` |
| [BUG-2026-002](BUG-2026-002-invalid-json-fixture.md) | 损坏 JSON 测试的替换未命中，误将合法数据当成负例 | 历史修复补录，本地验证通过，待提交/合并 | MachineCore 的 `corrupt or future save schema is rejected without replacement state` |
| [BUG-2026-003](BUG-2026-003-commit-postcondition.md) | 库存同步回调静默改写后仍报告提交成功 | 纯回归与实机故障注入通过，待提交/合并 | CommitScenarios 的 `silent callback corruption is rejected and compensated` |
| [BUG-2026-004](BUG-2026-004-harness-entrypoints.md) | 实机夹具 API 类型/库存事件入口错误 | 实机 9 项通过，待提交/合并 | GameHarness 的两个库存回调故障场景 |
| [BUG-2026-005](BUG-2026-005-content-module-version.md) | 新内容包仍允许旧主模块版本 | 自动回归及真实旧版拒绝/匹配加载通过，待提交/合并 | CatalogTests.test_content_pack_requires_current_machine_module、隔离启动矩阵 |
| [BUG-2026-006](BUG-2026-006-shop-price-multiplier.md) | 皮埃尔全局倍率重复放大原型价格 | 单元回归与原生购买实机通过，待提交/合并 | CatalogTests.test_shop_entries_preserve_catalog_prices、PersistenceScenario |
| [BUG-2026-007](BUG-2026-007-menu-double-click.md) | 领取连点被解释为下一批开工 | 单元和实际菜单回归通过，待提交/合并 | MenuActionSession、GameHarness 连点场景 |
| [BUG-2026-008](BUG-2026-008-ui-sale-shop.md) | UI 夹具要求在不接受料理的商店售出 | 原生售出回归与完整 UI 通过，待提交/合并 | GameHarness 原生商店售出场景、InteractionScenario |
| [BUG-2026-009](BUG-2026-009-occupied-placement.md) | 原生占用格成功返回误清空既有批次 | 实机先红后绿，16 项完整回归通过，待提交/合并 | PlacementScenarios 两设备四状态占用格回归 |
| [BUG-2026-010](BUG-2026-010-tool-fixture-placement.md) | 工具夹具选入家具碰撞格导致正常放置拒绝 | 现场修正后两设备完整链通过，待提交/合并 | ToolRemovalScenarios 原生放置前置检查与两设备拆回链 |
| [BUG-2026-011](BUG-2026-011-world-tool-proof.md) | 世界输入夹具将无关体力消耗与被忽略的目标点击拼成有效命中 | 原共享判据先红后绿；11-06同一9004完整20项/8工具阶段9原生命中及停止审计通过，待提交/合并 | WorldToolProof与两项纯回归；新世界输入原报告保留，不拼接旧7/20 |
| [BUG-2026-012](BUG-2026-012-world-observation-wall.md) | 世界输入夹具将真实掉落的观察站位选入农舍墙区 | 原生选址回归先红后绿；11-06两段真实方向走近同实体拾取/重放及完整20项、停止审计通过，待提交/合并 | 固定农舍墙负例/有效地板及61/60位移轨迹；不外推其他布局/保存 |
| [BUG-2026-013](BUG-2026-013-protocol-unicode.md) | 严格协议reader对孤立Unicode代理项抛异常而不是拒绝 | 发现时共享源码回归先红后绿、核心45项与生产构建通过，彼时网络入口未接入；实机网络未验收，待提交/合并 | ProtocolScenarios 的 `protocol malformed unicode is rejected without throwing or replacing the request` |
| [BUG-2026-014](BUG-2026-014-readonly-reconnect.md) | 只读会话对同身份重连保留旧握手，未识别两次轮询间的新连接 | 开发审查历史补录，等价旧比较负例失败/当轮68项纯回归通过；10-12一Host/一Client正常有gap重连子集通过；无gap根因/旧包/负载队列/第三peer仍待，未提交/合并 | ReadOnlyTransportScenarios 的 `read-only changed connection invalidates handshake while other peers retain host world session` |
| [BUG-2026-015](BUG-2026-015-multiplayer-profile-preflight.md) | 双端配置工具误拒运行config且客户端预检前可能先更新主机 | 旧工具实际先红后绿，11专项/37Python及新profile实际准备审计通过；双端网络未测，待提交/合并 | test_multiplayer.py的客户端预检/config保留与另外9项工具回归 |
| [BUG-2026-016](BUG-2026-016-network-probe-evidence.md) | 网络夹具把迟到null-ID拒绝误作当前探针证据 | 旧实际共享判据先红后绿，3专项/81核心通过；真实SDK/两端生命周期未测，待提交/合并 | ProbeEvidenceScenarios 与实际共享 NetworkProbeEvidence |
| [BUG-2026-017](BUG-2026-017-network-report-validation.md) | 新配对工具路径/能力/数值及握手/客户端额度证据缺口 | 两阶段37项3红/40项2红后全77绿；10-12真实Basic原双报告配对/最终Failure拒绝通过，完整NET02仍待，未提交/合并 | test_network_reports.py的40人工报告/CLI回归；实际集成见10-12Basic记录 |
| [BUG-2026-018](BUG-2026-018-network-fixture-config-alias.md) | Windows配置大小写别名被误登记为包文件 | 具名1项先红后绿，28专项/105Python通过；真实专用农场apply/网络未执行，待提交/合并 | NetworkFixtureTests.test_case_aliased_config_cannot_be_disguised_as_package_file |
| [BUG-2026-019](BUG-2026-019-null-removal-target.md) | NPC清理空地图格时机器保护对null目标解引用，中断原生更新 | SDK五项先红后绿/修后13通过；10-11用户确认输入正常，144实际样本支持D移动/Esc关闭对话，退出/八save/六config恢复审计通过；旧残余根因/常规菜单/持续移动/客户端完整验收仍待，未提交/合并 | GameAdapter空输入/真实NPC prefix及两设备7状态回归；InputObservation主机短测另记，不代原红或双端验收 |
| [BUG-2026-020](BUG-2026-020-preparation-empty-placeholder.md) | 新准备校验对原生空名/未创建占位错误拒绝 | 开发期具名1红1绿/新模块11/全116通过；春23真实备份负例及春24已创建客户端CLI正例通过，未提交/合并 | NetworkPreparationTests.test_unnamed_uncustomized_native_placeholder_does_not_block_client |
| [BUG-2026-021](BUG-2026-021-lifecycle-cli-invalid-data.md) | 新生命周期CLI未捕获严格读取器InvalidDataException | 核心101/CLI及具名实际1红1绿通过；真实完整原/最终双报告CLI0、失败两轮REJECT/1，未提交/合并 | NetworkLifecycleReaderScenarios缺字段拒绝；实际CLI incomplete_report_returns_one_without_success_or_stack_trace |
| [BUG-2026-022](BUG-2026-022-harness-menu-translation-context.md) | 新菜单组件测试将夹具翻译器传给生产菜单，错误阻断缺料提示回归 | 实际SDK先红后绿；修后18项/库存恢复/八save及禁用收尾通过，待审查/提交/合并 | GameHarness缺料差额/补料重试与指南导航/完成锁场景；真实设备互动取得生产菜单 |
| [BUG-2026-023](BUG-2026-023-harness-kitchen-action-source.md) | 新厨房场景把GrabTile当原生最终Action格，漏记有效灶台入口 | 两轮原生失败/共享旧判据先红后绿，115核心与修后同次厨房烹饪赠礼/停止审计通过，待提交/合并 | KitchenEntryScenarios三项；真实Action(3,4)/Grab(3,5)、菜单/原料守恒与成品赠礼 |
| [BUG-2026-024](BUG-2026-024-world-movement-investigation.md) | 世界输入场景操作者D移动无响应、Esc菜单可用 | 旧根因调查中；新9004人工四方向/菜单正常并完整20项通过、已退出审计；只读诊断不等于移动修复 | 原失败/新输入报告独立保留；根因回归未编写，不将新进程正常回填旧失败 |

状态为“待验证”的记录必须说明缺少什么证据；已合并记录附真实PR/提交，不以计划分支代替。索引保留历史，不将开发审查发现的缺陷包装成线上事故。
