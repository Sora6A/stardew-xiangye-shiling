# BUG-2026-018：Windows 配置大小写别名被误纳入固定包清单

- 状态: 已修复待验证；单项回归已先红后绿，最终28专项与全105Python通过、维护结构/本地链接通过；真实专用世界绑定和双端未执行，待提交/合并
- 发现/更新日期: 2026-10-02，Asia/Shanghai
- 责任任务/分支: `P1-NET-02` 专用网络农场身份绑定，`feat/p1-game-adapter`
- 性质: 新开发工具的文件所有权预检缺陷；本轮新增 Windows 专属回归后实际留红再修复，非历史补录、非已发生的玩家存档或配置事故

## 现象与影响

在 Windows 的人工临时双端配置中，把主模块根目录的 `config.json` 改名为 `Config.json`，再把这个文件作为固定包 payload 登记到 profiles.json 的全局及两端 files 清单，旧 `configure_network_fixture.py` 默认 plan 竟然接受。预期是配置始终归可写配置所有，不得靠大小写改为固定包文件；错误清单应在导出备份或修改四份配置之前被拒绝。

实际复现仅执行默认只读 plan，没有执行该输入的 apply，没有启动游戏、SDK 或网络，也没有损坏真实农场或 host/client 配置。人工包 DLL 与 XML 都是测试字节，不是可运行游戏或实际存档。缺陷影响包与配置边界的可靠性，不改变生产模块的网络写入门禁。

推断的后续风险：如果配置被错误登记为固定包，apply 改农场名/ID 后，其包清单哈希可能漂移并造成后核失败；由于四份配置并非整体事务，失败现场也可能包含已替换配置。这条 apply 链未实际复现，不能写成已有部分更新事故。

## 环境与前置条件

本轮在 Windows、Python 标准库 unittest 中实际执行；Python 基线见 [环境记录](../ENVIRONMENT.md)。测试 [test_network_fixture.py](../../tests/test_network_fixture.py) 直接调用实际 [configure_network_fixture.py](../../tools/configure_network_fixture.py)，没有复制一份预检逻辑。

`NetworkFixtureTests.setUp()` 使用 TemporaryDirectory 建立独立人工 XML、manifest、包字节、runtime 占位文件及 `.local/profiles`。人工存档目录/玩家名与农场名不同；测试世界名称 `XYSNETFresh`、ID=900001 只是最小输入，不证明真实世界独立创建、可用小屋或运行版本。两端主模块与夹具均禁用，夹具 Scenario=Network、角色 Host/Client；额外用户设置用于验证值的保留。

回归使用 `@unittest.skipUnless(os.name == "nt", ...)`，因为该复现依赖 Windows 中大小写不改变同一文件的所有权与路径访问。本机本轮实际执行，没有跳过；其他平台跳过此项不构成 Windows 回归通过。所有改名与清单编辑都在人工沙箱内，不读取个人完整存档或现有 profiles。

## 复现步骤

以下是修复前实际执行的回归。当前源码已修复，直接运行同命令会验证拒绝路径，不应将当前结果冒充旧实现。

1. 构造合法人工双端配置，取得 host 主模块配置原字节，并将 host/client 两份配置统一为该字节，确保这个额外“包文件”在两端具有相同 SHA256。
2. 经临时中间文件改名，把两端 `XiangyeShiling/config.json` 改为 `XiangyeShiling/Config.json`。Windows 下读取规范 `config.json` 路径仍访问同一配置。
3. 在 profiles.json 顶层 files 与 host/client 的 roles.files 中加入 `XiangyeShiling/Config.json` 及其真实 SHA256。其余合法包、manifest、版本和禁用配置保持不变。
4. 保存沙箱字节快照，预期默认 `configure_fixture()` 抛 ValueError；旧实现返回 plan，`assertRaises(ValueError)` 实际失败。
5. 修复前实际命令为：

   ```powershell
   python -m unittest discover -s tests -p test_network_fixture.py -k test_case_aliased_config_cannot_be_disguised_as_package_file -v
   ```

   结果 `Ran 1 test`、`FAILED (failures=1)`、退出1，断言 `ValueError not raised`。原始本机日志为 `.local/bug018-network-fixture-case-red-2026-10-02.txt`。

## 根因分析

`_relative()` 原先仅以精确字符串排除登记清单中的已知模组根 `config.json`，因此 `XiangyeShiling/Config.json` 不在排除集合。`inspect_profiles()` 遍历 `packages._tree()` 的实际路径时，也仅按精确字符串跳过根配置；改名后的 Config.json 因而被计算为包文件，并与两端一致的错误登记相匹配。

后续四份配置读取仍使用规范的 `config.json` 路径。在 Windows 上这没有产生另一个文件，所以同一配置同时被错误认定为固定包与可写配置。两端哈希一致只能证明字节一致，不能修正这种所有权冲突。

这是文件名大小写边界，区别于 JSON 属性重复或大小写别名；原 JSON 键拒绝规则没有覆盖文件路径。既有人工包正例与规范小写 config 排除检查不能揭示该入口。新增回归同时改变磁盘名称和全局/两端清单，保留其他有效前提，实际隔离出这一缺口。

## 解决方案

根任务在看到单项红后修改实际工具的两个位置：

- `_relative()` 对登记路径与已知模组根配置集合都使用 casefold 比较，拒绝任何大小写形式的配置被声明为固定包。
- `inspect_profiles()` 对遍历所得相对路径采用同一 casefold 配置匹配，将根配置与包集合分离。正常配置不会因目录遍历显示大小写变化而被改判为包。

修复仅统一配置所有权识别，不把所有包路径宽松归一，不允许新增包文件绕过清单或哈希检查。四份配置仍独立校验显式禁用开关、Network 模式、Host/Client 角色及字段类型；启用配置继续拒绝，未知设置值保留。

错误清单在输出或 apply 前拒绝，输入原字节保留。源 save 和原 profiles.json 始终只读；没有设备 schema、稳定 ID、配方或依赖迁移，也不启用探针或夹具。四配置逐文件替换的非整体事务边界未改变，详见 [准备说明](../NET02_TEST_SETUP.md)。

## 单元测试

具名回归为 `NetworkFixtureTests.test_case_aliased_config_cannot_be_disguised_as_package_file`，文件 [test_network_fixture.py](../../tests/test_network_fixture.py)。它直接构造上述 Windows 别名输入，调用真实工具默认 plan，并断言：

1. 即使两端包哈希和声明一致，也必须抛 ValueError；配置不能被登记为包文件。
2. 拒绝后沙箱完整文件字节快照等于调用前，不能修改原 XML、清单、包或配置。

红命令和断言见复现步骤。根任务已在修复后实际执行同一单项命令，1项通过、退出0；正式日志 `.local/bug018-network-fixture-case-green-2026-10-02.txt` 已保存并核对，记录 `Ran 1 test`、`OK`，没有跳过。该项只覆盖只读 plan 中的所有权拒绝，没有给错误输入执行 apply，也不能证明所有 apply 故障补偿。

完整专项入口为：

```powershell
python -m unittest discover -s tests -p test_network_fixture.py -v
```

完整专项日志 `.local/p1-network-fixture-unit-2026-10-02.txt` 已实际保存并核对，记录 `Ran 28 tests`、`OK`。修后在既有别名用例补正常配置路径：尚未把Config.json登记为包时，inspect/defaultplan应成功、四配置原hash与沙箱snapshot不变；此断言覆盖实际tree分离，不能一概拒绝大写配置掩盖错误。新增断言属于修后补充，不冒称原始红轮覆盖。最终专项 `.local/p1-network-fixture-unit-final-2026-10-02.txt` 又执行28项全绿/exit0。其余27项没有本缺陷对应的独立修前红证据，不扩写成每个断言都先红后绿。

根任务随后实际执行全量命令：

```powershell
python -m unittest discover -s tests -v
```

初轮105项全部OK、退出0，其中含28项身份绑定专项，日志 `.local/p1-network-fixture-python-2026-10-02.txt`，25.254s。补正常配置别名断言后再次实际完整执行，最终 `.local/p1-network-fixture-python-final-2026-10-02.txt` 105项OK/exit0/24.659s。维护结构/本地链接已独立运行通过，不由Python结果推断。

另记测试编写阶段调整，避免把测试错误归给工具：初轮24专项的 `test_apply_requires_output_and_existing_evidence_is_not_overwritten` 已先创建输出目录、要求拒绝且保留旧证据；通用 `rejected_before_writes` 却仍无条件检查目录不存在，发生 `AssertionError: True is not false`。正确输出是保留目录及完整原字节。helper改为 `rejected_before_writes(self, *, output_absent=True)`，仅在该参数为true时检查目录不存在，既有目录用例传false；完整字节快照断言保留。该轮原始输出只在当时工具结果中，未单独保存修前日志；此记录按开发者说明补录，不能补造附件或视为工具的第二条先红证据。随后同专项命令28项/最终105全量通过覆盖此断言，没有修改工具来迎合错误预期。

## 验证结果

| 检查 | 实际状态与限制 |
|---|---|
| Windows 单项修前 | 2026-10-02，1项实际执行、1 failure、退出1；ValueError not raised，未跳过 |
| Windows 单项修后 | 根任务实际执行1项通过、退出0；正式绿日志已核对，未跳过 |
| 完整身份绑定专项 | 已实际执行28项全部OK，专项日志已核对；全部为临时人工输入 |
| 全 Python | 根任务实际执行105项全部OK、退出0，含28项专项；本轮正式日志已核对，前轮77项仅属历史 |
| catalog | 根任务实际执行通过40 items/30 recipes、来源去向闭环、manifest与生成翻译，日志`.local/p1-network-fixture-catalog-2026-10-02.txt`已核对 |
| 维护检查 | check_workflow实际通过结构/本地链接，退出0，日志`.local/p1-network-fixture-maintenance-2026-10-02.txt`，0提交正文检查；不代表提交校验、远程CI/保护 |
| 本机包/受保护旧 UI 负例审计 | 已实际只读重核14包文件/四禁用角色配置；旧 UI apply 预期拒绝、退出1、无输出；六配置/六旧存档、旧与当前profiles、ZIP及两build前后未变，见`.local/p1-network-fixture-local-audit-2026-10-02.json`。这是独立预期拒绝，不是本缺陷红轮或成功绑定 |
| 真实专用农场 plan/export/apply | 未执行；本轮尚未取得、指定或核验真实专用世界身份 |
| 游戏 / SDK / 实际双端网络 | 未执行，未启动游戏或诊断，NET02/P1未验收 |
| 提交 / 推送 / PR / 合并 / 远程CI | 未执行，无修复SHA或远程成功结果 |

本轮验证入口为 [身份绑定验证](../validation/P1-network-fixture-2026-10-02.md)。未用目录名称推断用户全部存档中不存在网络农场；真实世界身份须由明确指定的两份 XML 与后续游戏核对建立。

## 风险与回滚

修复要求错误清单拒绝，不会自动改 profiles.json 或重命名用户配置来“修复”包一致性。所有实际测试输入在人工临时目录，不能拿旧源/UI/Stage3副本尝试别名 apply，也不能用修改保护 ID 或 marker 换取通过。

需要回滚时先停止两端游戏/SMAPI，保持主模块探针与 Harness.Enabled/OpenPreview 禁用，保留 before/prepared、fixture-plan/fixture-apply 和失败日志。若存在真实 apply 的部分更新，逐份核对当前与备份/候选哈希，辨认外部编辑后再恢复一致配置；审计缺失不等于没有替换。不得删除正式存档、设备或玩家库存。

单独回退这两处修复会重开配置别名入口，不建议将旧预检用于身份绑定；确需撤回时回退整个新工具的调用路径，并保持诊断禁用，保留本记录与输入证据。工具没有自动创建/安装农场或启用网络的步骤；本轮未执行真实 apply，不需要对玩家存档做迁移或清理。

## 关联记录

[实际工具](../../tools/configure_network_fixture.py)、[专项测试](../../tests/test_network_fixture.py)、[本轮身份绑定验证](../validation/P1-network-fixture-2026-10-02.md)、[NET02准备](../NET02_TEST_SETUP.md)、[开发命令](../DEVELOPMENT.md)、[P1-NET-02计划](../DEVELOPMENT_PLAN.md)、[主机契约](../P1_HOST_PROTOCOL.md)、[交接](../HANDOFF.md)、[贡献规范](../../CONTRIBUTING.md)、[bugfix模板](TEMPLATE.md)。索引、交接与最终验证已同步，真实绑定和双端的未执行范围仍保留。

实际分支 `feat/p1-game-adapter`，准确基线 `8f2468d83b43fb805073f73bbc8b989cf716852e`；基线不是本修复提交。当前相关变更未提交、未推送，无PR、合并或远程CI结果。以后实际发布时补真实修复SHA、完整commit正文及PR/merge结果，不提前填写。
