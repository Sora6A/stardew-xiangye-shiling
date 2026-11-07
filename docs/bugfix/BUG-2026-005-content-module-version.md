# BUG-2026-005：内容包仍允许不具备机器适配器的旧主模块

- 状态: 本地自动回归与旧版混装启动拒绝集成验证通过；未提交、未合并
- 发现/更新日期: 发现 2026-09-28；更新 2026-10-01，Asia/Shanghai
- 责任任务/分支: alpha.2 打包核对，`feat/p1-game-adapter`
- 性质: 内容包依赖版本漏改；本轮未发布

## 现象与影响

2026-09-28 发现时，代码与内容包 Version 均为 alpha.2，但内容包 Dependencies 的主模块最低版本仍为 alpha.1。该声明允许新机器数据配旧代码，旧代码没有菜单/事务/移除保护。此依赖现已修复；本次未进行这种混装的玩家存档测试，没有已知数据损失。

## 环境与前置条件

自动回归使用 Python 3.10.9、清单与两份 manifest；纯检查器复现不需要游戏。2026-10-01 启动集成使用工作区游戏副本 1.6.15 / SMAPI 4.4.0 / CP 2.9.0，两个新隔离 Mods 目录均不含 GameHarness，未更改源安装或常用 Mods，也不读取或写入农场。

负例使用原始 `artifacts/XiangyeShiling-0.1.0-alpha.1-dev.zip` 的真实主模块，不是降低新 DLL 的 manifest 版本。其主模块 DLL SHA-256 为 `93dcf195090445a99074e986dd108d3e395f5a6a840db228cafd608db1d4ced1`，包内 manifest 与二进制 informational version 均为 alpha.1。正例采用夹具准备时的 alpha.2 主模块与内容包；该主模块是 BUG-2026-009 修复前版本，不代表后续最终开发 ZIP。来源与逐文件哈希保存在 `.local/bug005-fixture-audit-2026-10-01.json`。

## 复现步骤

1. 检查内容包依赖旧值 alpha.1，同时两个 Version 已为 alpha.2。
2. 在旧检查器上运行 `python -m unittest discover -s tests -p test_catalog.py -v`。
3. 新用例把主模块依赖设置为 alpha.1，期望收到版本不匹配错误；旧检查器返回空错误列表，用例失败。证据 `.local/p1-dependency-before.txt`。

以上为 2026-09-28 的修复前自动回归；2026-10-01 没有重新修改依赖修复，也没有重新运行旧检查器制造新的失败。当天补验的是已经修复的版本门槛是否由真实 SMAPI 执行：

1. 关闭测试游戏，在 `.local/bug005-mixed-2026-10-01/Mods` 放真实 alpha.1 主模块、alpha.2 内容包与 CP 2.9.0；保持内容包 `MinimumVersion=alpha.2`，不放自动夹具。
2. 用工作区 `StardewModdingAPI.exe` 和 `--mods-path` 指向该目录，仅启动到标题。核对日志确实加载 alpha.1 主模块，alpha.2 内容包被 Skipped，理由为主模块需 alpha.2 或更高。
3. 保存日志后关闭该测试进程，再以 `.local/bug005-matched-2026-10-01/Mods` 中 alpha.2 主模块/内容包与同一 CP 运行时启动。核对两个 alpha.2 包加载，CP 实际编辑 Objects、BigCraftables、CraftingRecipes、CookingRecipes。
4. 两轮均不进入加载农场界面，不触发 SaveLoaded；各自日志复制留档后，核对可执行路径并结束对应工作区测试进程。SMAPI 的普通用户日志路径仍共享，必须先保留上一轮日志再启动下一轮。

## 根因分析

升级时只同步自身 Version 和 csproj，遗漏内容包引用主模块的最低版本。旧检查只验证两个自身版本和外部依赖，没有核对包内模块之间的依赖关系。

## 解决方案

主模块最低版本改为 alpha.2。check_repository 要求恰好一个主模块依赖、IsRequired=true、MinimumVersion 与当前代码 manifest 一致。保持物品 ID、状态 schema 与数据不变。

## 单元测试

`tests/test_catalog.py` 的 `test_content_pack_requires_current_machine_module` 使用只读 mock 降低依赖版本，断言检查器拒绝。2026-09-28 修复前失败，修复后通过；`test_frozen_catalog_and_generated_files` 同时证明匹配版本配置仍被接受。命令：`python -m unittest discover -s tests -v`。当时 25 项通过为历史结果；2026-10-01 最新完整 Python 回归 26 项通过，不把新增其他模块用例称为本缺陷新修复。

## 验证结果

历史 catalog 与开发包构建通过，包内 manifest 已核对；此前 9 项实机集成使用的是 alpha.2/alpha.2 匹配配置。2026-10-01 补充的两轮实际启动结果如下：

| 检查 | 实际断言与结果 |
|---|---|
| 真实旧模块负例 | alpha.1 主模块加载；alpha.2 内容包因要求主模块 alpha.2 被 Skipped。SMAPI 的拒绝 Error 和旧主模块的“内容包缺失”诊断符合负例预期，不属于正常匹配配置成功日志 |
| 匹配版本正例 | alpha.2 主模块与内容包加载，CP 实际编辑 Data/Objects、Data/BigCraftables、Data/CraftingRecipes、Data/CookingRecipes；无 ERROR/WARN |
| 启动范围 | 两轮日志均出现 `titleScreenGameMode (0)`，没有 SaveLoaded/农场读取记录；仅验证启动依赖门槛及匹配包加载，不宣称存档或完整玩法验收 |
| 隔离与关闭 | 使用两个新 Mods 目录，不含 GameHarness；窗口以 Hidden 后台启动，没有可操作目标窗口。操作者保存日志、核对工作区可执行路径后结束两个测试进程，未使用正式存档 |

负例日志 `.local/bug005-mixed-smapi-2026-10-01.txt`，正例日志 `.local/bug005-matched-smapi-2026-10-01.txt`，夹具来源、模块/内容包哈希与启动断言见 `.local/bug005-fixture-audit-2026-10-01.json`。这证明已有修复的真实旧版拒绝路径；不是本日新增生产修复的红转绿，也不是所有游戏/模块版本兼容声明。正例主模块为 BUG-2026-009 修复前的 alpha.2，不能把此启动证据写成后续最终 ZIP 的整包验收。

## 风险与回滚

内容包会要求用户同步更新主模块，这是需要的限制。若回退，主模块与内容包一起回退并恢复兼容测试备份，不单独降低依赖放行未知组合；不清空玩家设备状态。

## 关联记录

`tools/catalog.py`、`tests/test_catalog.py`、`content/XiangyeShiling.Content/manifest.json`；[交接](../HANDOFF.md)、[本轮验证](../validation/P1-2026-09-28.md)。未提交/推送，暂无 commit/PR。
