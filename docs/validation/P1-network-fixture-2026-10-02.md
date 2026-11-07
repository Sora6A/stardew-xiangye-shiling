# P1-NET-02 专用世界身份绑定验证

日期：2026-10-02，Asia/Shanghai。分支 `feat/p1-game-adapter`，基线 `8f2468d83b43fb805073f73bbc8b989cf716852e`。本轮修改 Python 工具、专项测试和接续文档，未改生产/夹具 C#、内容范围、设备格式或依赖。未提交、推送、创建 PR 或合并；真实双端与 P1 未验收。

## 行为与所有权

[configure_network_fixture.py](../../tools/configure_network_fixture.py) 默认重新读取实际存档两份 XML 和 schema2 profiles，生成四份禁用配置的计划；不给 `--output` 不写任何文件。指定新输出目录会导出原字节 `before`、候选 `prepared` 和 fixture-plan.json，仍不改两端配置。显式 `--apply` 需新的证据目录，才绑定两个模块的 FarmName/世界 ID；Host/Client、Scenario=Network、Enabled/OpenPreview/EnableReadOnlyNetworkProbes=false 必须原先就正确，不自动关闭活跃测试或转换其他夹具模式。

身份只读取 SaveGame/uniqueIDForThisGame、SaveGame/player/farmName 与 Farmer/farmName 的确切直接节点。世界 ID 必须非零 ulong，农场名匹配显式预期且为 XYSNET 前缀、6–20 UTF-16 单元。拒重复/缺失/嵌套伪身份、错名、已知源/UI/Stage3/旧 P0 ID、损坏 XML 及 UTF-8/UTF-16 的 DTD/entity；每份 XML 有界读取至 64MiB+1。文件夹可能取玩家名，不能凭文件夹标签推断农场名或独立性，也不遍历任意个人存档 XML。

两端当前包文件、manifest/声明依赖/版本重新核对，要求相同构建、正确 GameHarness 与白名单、完整必需文件；不信任 roles.mods 绝对路径选择写入目标。已有报告/额外 DLL 先保留并归档，不删除绕过。配置重复键/大小写别名/非有限数/错类型/开启开关拒绝；未知设置值保留。CLI 的 profiles/output 限本工作区 `.local` 严格后代，输出必须不存在、不交叠存档/两端/runtime；链接/reparse/hardlink 拒绝。

原存档、包、启动脚本、profiles.json 与 runtime 旧配置不写。原 profiles 的 configs hash 只属于其准备时点，绑定后通过新计划/审计的 beforeSha256/afterSha256 说明当前四配置；不把旧清单伪装为新身份审计。新审计带来源 XML/原 profiles/包哈希，仍不证明小屋、独立建档、SDK 版本或网络成功。

## 失败与接续

全预检和 before/prepared/计划导出在首次替换前完成，应用前再读全部输入；每配置用临时文件/原子替换，四文件不是批事务。第二次替换 I/O 失败可能只改第一份；fixture-apply.json 记录 configurationApplied=false、replacedConfigs、failure，未替换文件和全部 before 原字节保留。审计 I/O 再失败可能没有失败审计；根据 before/prepared 哈希逐文件检查，不能只看返回码、无报告或旧 profiles 推断完全未改。

必须在使用这些配置的实例停止时调用；重复读取不是强并发锁。失败不自动回滚其他编辑者、不自动重放旧计划。保持诊断关闭，检查当前四配置/原值/包后，从独立原字节备份选择恢复或另行重新预检；成功状态也只描述本次文件绑定。原配置可能含本机信息，输出只保存在忽略目录，不能提交。

## 当前检查点

语法、`--help`、28项专项和全105项Python通过；核心85/两项Release0警告0错误属于 [前轮配对阶段](P1-network-pair-2026-10-02.md)，本轮C#未变不重跑。测试输入为临时人工XML/包/config，不证明真实农场绑定。首轮专项中的既有输出保留helper曾错断言目录不存在，修正测试helper后通过；这是测试自身断言问题，不冒称工具缺陷红。

| 检查 | 实际结果/证据 |
|---|---|
| `python -m py_compile tools/configure_network_fixture.py` / `--help` | 退出0，语法和CLI可读，不代表实际身份绑定 |
| BUG-018具名单项 | 修前1项1failure/exit1，修后1项OK/exit0，当前Windows实际执行无skip；`.local/bug018-network-fixture-case-red/green-2026-10-02.txt` |
| `python -m unittest discover -s tests -p test_network_fixture.py -v` | 28项OK/exit0；初轮unit及补正常配置别名断言后的unit-final日志均保留，最后`.local/p1-network-fixture-unit-final-2026-10-02.txt` |
| `python -m unittest discover -s tests -v` | 105项OK/exit0，含28专项；修后补正常配置别名断言再整轮重跑，最后`.local/p1-network-fixture-python-final-2026-10-02.txt`，24.659s；初轮105日志另留 |
| `python tools/catalog.py` | 40物品/30配方闭合与生成物通过/exit0；`.local/p1-network-fixture-catalog-2026-10-02.txt` |
| `python tools/check_workflow.py` / 差异 | 结构/本地链接通过/exit0，日志`.local/p1-network-fixture-maintenance-2026-10-02.txt`；0提交正文检查。git diff --check通过，已有DESIGN CRLF归一提示非失败；本轮工具/测试/新验证与bug文档无行尾空白匹配 |

专项检查默认/导出均不改输入、apply只变四禁用config且unknown设置保留；错误client/XML/包/角色/开关全预检拒绝，硬链接/输出交叠/旧证据不能覆盖；人工第二replace失败只改一份、准确failure/replacedConfigs、原字节备份齐全并清本次临时文件。UTF-16普通XML可读、DTD/entity拒、大小写别名配置不得伪装包，JSON与ID类型/范围严格。完整名称与断言见 [test_network_fixture.py](../../tests/test_network_fixture.py)。

[BUG-018](../bugfix/BUG-2026-018-network-fixture-config-alias.md) 的具名红在修复前实际执行：两端主模块改名Config.json并统一原bytes，加入全局/两端包清单，默认plan误接受，`ValueError not raised`。见红后把清单拒绝与实际tree分离两处都按casefold匹配根config，之后单项绿/28专项/105全量。进一步正常“别名配置不登记为包仍可plan且不改字节”断言属修后补充，不冒称原红覆盖；没有对错误清单实际apply或玩家事故证据。

真实本地审计 `.local/p1-network-fixture-local-audit-2026-10-02.json` 已写出：14 包文件和四禁用角色配置预检通过；对受保护旧 UI 存档执行 `--apply` 负例，预期退出1且不创建输出。六份配置、源/UI/Stage3 六份存档、当前/旧 profiles、ZIP、生产/夹具构建前后哈希均不变。这是误用防护检查，既不是工具 bug 红证据，也不是成功绑定。

最终只读审计 `.local/p1-network-fixture-final-audit-2026-10-02.json` 实际退出0，再次核本轮17个受监视文件原字节/哈希均未变、14包/4禁用配置通过，关联初次审计原文SHA。没有把Stage3的本轮前后相等当成它与旧初始基线相等。最后实际查询游戏/SMAPI为0；忽略检查确认审计与ZIP不进入Git跟踪。

本轮没有取得、指定或核验真实专用 XYSNET 世界身份，没有真实成功 apply、游戏启动、网络报告配对或小屋验证。存档文件夹是否带 XYSNET 前缀不能证明它内部的 farmName，本轮未开任意个人农场搜寻。

## 包与运行状态

| 对象 | 本轮实查 SHA-256，未重新构建 |
|---|---|
| 生产 DLL | `5ef0df7577255bbc0132e7879fab4f40fda1bcbcad013a291c906a3dbc4b04ac` |
| 开发 ZIP | `7907c05e9e54c3d95cfb261ba2653ed574c9f9f2a12759c419e622fc93e05096` |
| GameHarness DLL | `d2f054089f1dedf7a31ed63cadd5c49befbdd4feccf7541dbcbc02fec03db345` |
| 当前 profiles.json | `cf540bb6c55ef2bcbf069aeceeab5b470e690d2881b984601e50bfb1a6d54e8c` |

两端仍为 XYSNET02/ID0，主模块探针和夹具 Enabled/OpenPreview 全 false；runtime 原禁用 Memory/XYSP1UI 配置保留。查询游戏/SMAPI 为0。未修改 C#，没有重建/更新 ZIP 或重新部署；上一轮 DLL 的构建通过记录仍有效，但尚未实机启动，旧 Memory/UI/保存通过项保持历史性质。

## 下一步

先在诊断关闭时准备新的独立合作农场和可用小屋，保存并停止准备实例，取得真实 farmName/世界 ID。按 [操作步骤](../NET02_TEST_SETUP.md) plan→审查→新目录 apply，核四配置、来源和包哈希，再另行显式开启。主机加载合作世界，客户端加入；实际 active 报告配对之后仍需额度、重连/标题/换世界、版本/分屏、16帧预算/128邮箱和存档文件核验，不开放多人加工/放置。`configurationApplied=true` 不等于已创建世界或 NET-02 通过。

接手状态见 [HANDOFF](../HANDOFF.md)，本轮无新的 commit SHA/正文、PR 或合并结果，累计工作区仍待按授权审查提交范围。
