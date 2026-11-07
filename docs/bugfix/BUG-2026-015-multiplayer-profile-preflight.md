# BUG-2026-015：双人配置预检拒绝运行配置并可能先更新单侧

- 状态: 旧实际工具先红后绿，11 项专项及当前 37 项 Python 通过；真实新两端配置准备/哈希审计通过，双客户端连接/设备操作未执行，待提交/合并
- 发现/更新日期: 发现于 2026-10-01，更新于 2026-10-02，Asia/Shanghai
- 责任任务/分支: `P1-NET-02` 独立测试配置准备，`feat/p1-game-adapter`
- 性质: 配置准备工具的预检顺序与文件所有权缺陷；本轮先留旧实际代码红证据再修复，非历史补录、非线上事故

## 现象与影响

旧 `prepare_multiplayer.py` 不复制来源中的 `config.json`，却将目标 Mods 里正常运行产生的同名配置视为意外文件。已经运行过的主机/客户端配置因此无法再次准备；预期是保留各端自己的配置原文，并继续核对/更新声明的模组文件。

旧工具还逐角色执行“检查→复制→校验”。若主机没有意外文件、客户端有额外文件，工具会先更新主机，再在客户端预检失败；最后的 `profiles.json` 未重写，仍描述上一轮成功配置。预期是所有来源与两端均完成只读预检后才开始写入，客户端的预检拒绝不能先改变主机或旧记录。

真实红回归在临时目录执行：主模块由 `module-v1` 改为 `module-v2`，客户端额外 `unrelated.dll` 导致拒绝，但主机 DLL 已更新；另一个回归添加正常 Content Patcher 配置后，被旧工具报为 Unexpected files。本次没有对既有 `.local/multiplayer`、游戏安装或私人存档执行该负例，没有已知玩家保存损失。双端文件准备不等于网络连接或游戏兼容已验收。

## 环境与前置条件

Windows、本机 Python、标准库 `unittest`。测试创建并清理 `TemporaryDirectory`，使用三个声明身份正确的假包：Content Patcher 2.9.0、主模块/内容包 `0.1.0-alpha.2`，项目声明 SMAPI 4.4.0 最低基线。测试 DLL 与运行入口只是不可执行的字节夹具，不加载游戏或 SDK。

旧红轮没有测试 API：测试用 `unittest.mock.patch` 将旧工具的 ROOT 和命令行参数指向临时工作区，实际执行旧 `main()`，没有复制一份工具逻辑。修复后测试直接调用不依赖全局 ROOT 的 `prepare_profiles`。游戏/SMAPI 的真实二进制身份仍需根任务另外核验，本工具只检查清单声明、文件存在和 SHA-256。

## 复现步骤

以下针对修改前实际工具，红日志已经保留，当前直接运行会得到绿色：

1. 建立三个测试包及假的 `StardewModdingAPI.exe`，让旧工具首次准备成功，保存两端全部文件和 `profiles.json` 的原始字节。
2. 修改来源主模块 DLL，向客户端 Mods 增加 `unrelated.dll`，再次准备。预期抛 ValueError 且两端/记录原文均不变、额外文件保留；实际抛拒绝后，主机 DLL 已由 `module-v1` 变为 `module-v2`。
3. 在另一个独立场景首次准备后，分别给两端 `ContentPatcher/config.json` 写入带换行的用户配置。来源也有不同配置。预期重跑成功、目标配置原字节保留、来源配置不混入；实际旧工具在主机阶段报 `Unexpected files ... ContentPatcher/config.json`。
4. 先新增上述两个回归，未改工具时实际执行：

   ```powershell
   python -m unittest discover -s tests -p test_multiplayer.py -v
   ```

5. 红轮退出码 1，2 项中 1 failure、1 error；本机证据 `.local/bug015-profiles-red-2026-10-01.txt`。明确失败断言为 `BUG-015: preflight rejection already changed host/Mods/XiangyeShiling/XiangyeShiling.dll`；Windows 日志路径使用反斜杠。工具修复后再跑同一命令，11 项全部通过，日志 `.local/bug015-profiles-green-2026-10-01.txt`。

完整本机日志含临时个人目录，仅留 `.local`；公开记录使用脱敏路径和结果，不提交完整日志或假的测试二进制。

## 根因分析

来源文件集合排除所有 config，而目标预检直接把“全部目标文件减来源集合”当成意外文件，没有区分软件包归属与用户运行配置。目标正常配置因而必然落入差集。

角色循环把预检与写入混合，主机循环完成才开始客户端检查。单侧拒绝时既没有回滚，也尚未更新最终记录；此前没有工具级单元测试揭示这种顺序。固定输出 `.local/multiplayer` 和直接依赖全局 ROOT，也使调用者难以在独立目录复现或准备新的网络试验配置。

## 解决方案

入口 [prepare_multiplayer.py](../../tools/prepare_multiplayer.py) 新增：

```python
prepare_profiles(source, runtime, destination, *, harness_dir=None)
```

库函数只处理调用者拥有的输入/目标，不查全局 ROOT；CLI 独立执行 `validate_output(output, workspace_root)`，只接受工作区 `.local` 的严格后代，拒绝 `.local` 本身、目录穿越与外部输出。保留默认旧路径，新增 `--output` 允许显式选择 `.local/net02-profiles`；不会自动更新其他配置目录。

- 写入前检查全部来源清单、必需 DLL/content、版本依赖、来源/runtime/可选 harness 与目标的双向互含、链接/越界路径，以及两端所有意外文件。目标意外文件仍拒绝并保留，不自动删除。现有目标硬链接也拒绝，避免更新共享文件。
- 核验 CP/项目 UniqueID、主模块与内容包精确同版本、P1 最低版本、项目 SMAPI 最低声明、内容包归属和实际提供的依赖版本满足最低要求。清单校验不证明 DLL 版本、SDK 引用或运行时真实版本，结果明确 `runtimeBinaryVersionVerified=false`。
- 排除来源 config，允许目标已知模组根目录的 `config.json`，保留其原始字节；不把任意其他额外文件解释为配置。
- 可选 `--harness-dir` 只接受整理好的 manifest、其声明的 EntryDll 和同名可选 deps/runtimeconfig 白名单；目标为 `Mods/GameHarness`。不复制来源配置；缺配置时生成 `Enabled=false`、`OpenPreview=false`。已存在配置继续原文保留，若用户原来启用，结果的 `roles.harnessEnabled` 如实记录 true，工具不会自动启用或禁用它。两个角色现有 harness 配置的格式也在写入前预检。
- 完成两端复制与哈希校验后，最后写 schemaVersion 2 的 `profiles.json`。`roles.files/configs/versions` 来自实际目标 SHA/manifest，软件包文件两端一致，用户配置单独记录；脚本不启动游戏、不建立网络连接，也不读/写存档。

**全预检不是完整 I/O 事务。** 磁盘/权限错误、来源在预检后变化或进程中断仍可能留下单侧更新；工具报失败，不承诺自动回滚。`profiles.json` 此时保留上一轮成功记录，不能据它宣称当前两端一致。重新准备前检查真实文件，修复意外内容/错误后重跑并核对新记录。

## 单元测试

测试文件：[test_multiplayer.py](../../tests/test_multiplayer.py)；命令见复现步骤，均只在临时目录使用实际工具代码。

| 测试名称 | 核心断言 | 旧轮/修复后 |
|---|---|---|
| `test_client_extra_preflight_does_not_update_host_or_profiles` | 客户端意外文件拒绝前不改变任一端/记录，意外文件不删除 | 实际旧轮 failure；修复后通过 |
| `test_existing_runtime_configs_are_preserved_and_source_config_is_not_copied` | 新配置不继承来源，重跑保留两端原字节 | 实际旧轮 error；修复后通过 |
| `test_all_source_manifest_failures_preflight_before_either_role_changes` | 版本不配对、缺依赖/低依赖与错误身份拒绝，旧两端不变 | 新增，修复后通过；无旧轮结果 |
| `test_missing_source_binary_preflight_does_not_create_destination` | 缺必需 DLL 时无目标写入 | 同上 |
| `test_source_runtime_and_destination_must_not_overlap_in_either_direction` | 同路径、父子两方向、运行目录重叠拒绝且原树不变 | 同上 |
| `test_cli_output_boundary_accepts_new_local_child_and_rejects_escape_or_local_root` | 严格 `.local` 后代可用，根/穿越/外部路径拒绝 | 同上 |
| `test_cli_explicit_new_output_preserves_older_profiles_and_api_ignores_global_root` | 新 CLI 输出实际使用，旧 profile 不变；库 API 不依赖 ROOT | 同上 |
| `test_optional_harness_is_whitelisted_defaults_disabled_and_audit_hashes_actual_roles` | 来源启用配置不继承，新默认禁用，两端真实 SHA/版本/schema 与记录一致 | 同上 |
| `test_existing_harness_config_is_preserved_and_reported_without_automatic_enabling` | 现有用户配置字节保留、实际 Enabled 报告准确，未替换为来源配置 | 同上 |
| `test_harness_extra_and_bad_existing_client_config_preflight_before_host_copy` | 白名单外包文件/客户端错误配置在主机更新前拒绝 | 同上 |
| `test_io_failure_does_not_replace_last_successful_profiles_record_or_claim_rollback` | 注入客户端复制失败时旧记录不覆盖；主机可能更新，客户端仍旧，明确没有事务回滚 | 同上 |

## 验证结果

| 检查 | 10-01 红/专项绿，10-02 最终验证 |
|---|---|
| 两个揭示旧缺陷的回归 | 工具修改前已执行，退出 1：1 failure/1 error，真实红日志已保存 |
| 修复后工具专项单元 | 已执行，11 项通过，退出 0 |
| 全部 Python/维护检查 | 10-02 全37项通过；文档最终维护检查以 HANDOFF 为准，不冒称远程CI |
| 临时目录下 CLI 与可选 harness | 已通过上述单元；使用字节夹具，不加载 SDK/游戏 |
| 真实 `.local/net02-profiles` 准备与二进制审计 | 根任务10-02用当前包/整理后的harness实际准备及重跑成功，角色config原文保留、目标hash一致；旧profiles/六存档不变，开关均false/ID0，见本轮validation |
| 双客户端连接、重连、只读回执与设备操作 | 未执行，P1 网络/总体验收仍未完成 |
| 正式安装/私人保存 | 本任务未操作；不会把假包测试标为实机验收 |
| 提交、推送、PR、合并 | 未执行，无本修复 SHA/PR/merge 结果 |

## 风险与回滚

调用库 API 的代码负责确认目标所有权；CLI 才施加工作区 `.local` 边界。真实来源必须是已核验的模组包，manifest 声明和字节相同不等于可加载或 DLL 引用正确。已有 harness 配置可仍启用，准备结果记录事实，运行者在启动前须依独立测试方案核对配置和专用农场。游戏/Mods 配置隔离不改变正常用户的 Saves/日志目录。

回滚只撤回工具/API/专项测试与对应文档，保留红证据；旧工具仍会误拒正常配置并可能单侧更新，不再用其失败后的旧记录证明两端一致。不要删除意外文件、用户配置或私人保存来通过准备。I/O 失败后若需恢复，应使用此前验证的包重新准备并核验实际哈希，不能声称脚本已经回滚。

## 关联记录

[P1-NET-02 计划](../DEVELOPMENT_PLAN.md)、[交接](../HANDOFF.md)、[开发命令](../DEVELOPMENT.md)、[贡献规范](../../CONTRIBUTING.md)、[工具](../../tools/prepare_multiplayer.py)、[专项回归](../../tests/test_multiplayer.py)、[BUG-005 内容依赖基线](BUG-2026-005-content-module-version.md)、[BUG-014 只读重连](BUG-2026-014-readonly-reconnect.md)。红/绿日志见上文，完整日志留 `.local`。

任务分支 `feat/p1-game-adapter`，基线 `8f2468d83b43fb805073f73bbc8b989cf716852e`；基线不是修复提交。未提交/推送/PR/合并，关联SHA/远程结果待实际完成。索引已更新，实际角色准备结果见 [10-02验证](../validation/P1-network-probe-2026-10-02.md) 和 [NET02说明](../NET02_TEST_SETUP.md)，不把临时假包测试写成游戏验收。
