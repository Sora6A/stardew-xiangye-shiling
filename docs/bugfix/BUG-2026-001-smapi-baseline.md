# BUG-2026-001：CP 2.9 与初始 SMAPI 基线不匹配

- 状态: 本地验证通过，待提交/合并
- 发现/更新日期: 2026-09-24 / 2026-09-25，Asia/Shanghai
- 责任任务/分支: P0 环境准备；本次补录在 `docs/development-workflow`
- 性质: 依赖兼容；历史修复补录，尚未发布的开发工程

## 现象与影响

最初工程选择 SMAPI 4.1.10 引用，同时声明 Content Patcher 2.9.0。主模块能编译并不表示 CP 的 API 依赖成立；固定标签源码使用 SMAPI 4.4 增加的图像遮罩接口，旧引用无法满足该组合。

影响为依赖声明/开发说明给出不能使用的组合，阻碍最小内容加载验证。未发布给玩家，没有本次缺陷造成存档损失的证据；原游戏安装也没有被升级覆盖。

## 环境与前置条件

游戏 1.6.15、Windows x64、CP 2.9.0 标签 `807ca18be9291f301119a3fa01f49b6a59ac625f`。初始 SMAPI 引用为 4.1.10；修复后为 4.4.0。本地 SDK 7.0.302，目标 .NET 6。

CP 固定标签的 [release notes](https://github.com/Pathoschild/StardewMods/blob/content-patcher/2.9.0/ContentPatcher/docs/release-notes.md)写明 SMAPI 4.4.0+。上游 manifest 残留的旧最低值不足以证明 API 兼容。

## 复现步骤

历史场景：在隔离目录取得 CP 2.9.0 源码，以 SMAPI 4.1.10 引用构建/检查其新图像遮罩调用，旧程序集缺少所需的 `PatchMode.Mask` 能力。此前未保存完整失败构建日志，因此不伪造精确错误码或红转绿输出。

当前可执行的防回归负例：运行下面单元测试。测试分别在内存中将主模块或内容包的 `MinimumApiVersion` 改成 `4.1.10`，不修改工作区文件；两个子场景都必须产生基线错误。正常仓库必须仍通过 `check_repository()`。

```powershell
python -m unittest discover -s tests -p test_catalog.py -v
```

## 根因分析

初始只验证了主模块使用的少量 API 和 manifest 声明，没有同时核对 CP 2.9 的实际 API 与版本说明；主模块自身编译成功掩盖了下游依赖不兼容。原清单回归检查没有针对“只改一端最低版本”的负例。

## 解决方案

把主模块、内容包、MSBuild 引用版本门槛、环境检查、打包说明和开发文档统一为 SMAPI 4.4.0 / CP 2.9.0。`tools/catalog.py` 拒绝两端任一基线回退；本次为该门槛补充独立回归。实际运行使用工作区游戏副本，不覆盖原安装。

## 单元测试

- 文件：[tests/test_catalog.py](../../tests/test_catalog.py)；名称：`CatalogTests.test_smapi_baseline_cannot_regress_in_either_manifest`。
- 两个子场景：代码 manifest 回退、内容包 manifest 回退。
- 断言：均包含 `CP 2.9 requires the SMAPI 4.4.0 baseline`；原始文件完全不写入。已有 `test_frozen_catalog_and_generated_files` 验证正常基线。
- 命令：上方 `unittest discover`。此回归在历史修复后补写；没有重新运行旧 CP 的失败构建，不宣称保存了历史红转绿日志。

## 验证结果

2026-09-25：本次回归检查两个回退负例均被拒绝，正常清单通过；完整 Python 回归结果见 [交接记录](../HANDOFF.md)。此前 SMAPI 4.4.0 下本项目 Release 构建 0 警告、0 错误，CP 补丁及新旧档加载通过，证据见 [P0 实测](../validation/P0-2026-09-25.md)。

未验证所有更高版本、官方 CP 发行二进制与其他模组组合。源码构建 CP 的分析器警告单独记录在 [环境说明](../ENVIRONMENT.md)，不能误报成本项目警告。

## 风险与回滚

最低 SMAPI 要求提高；未改变物品 ID 或玩家存档。若回退依赖，应同时选择经验证的旧 CP 组合，在隔离环境修改所有门槛和说明后重新验证；只回退某个 manifest 会重新引入此缺陷。恢复上一个已验证开发包不需要修改正式存档。

## 关联记录

- [开发计划 P0](../DEVELOPMENT_PLAN.md)、[开发环境](../ENVIRONMENT.md)、[交接](../HANDOFF.md)。
- 修复位置：`src/XiangyeShiling/manifest.json`、`content/XiangyeShiling.Content/manifest.json`、`src/XiangyeShiling/XiangyeShiling.csproj`、`tools/catalog.py`、`tools/check-environment.ps1`、`tools/build.py`。
- Commit/PR：尚未提交或推送；首次提交时在完整正文引用本记录，合并后补实际关联。
