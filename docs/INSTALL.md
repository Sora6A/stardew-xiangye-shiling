# 《乡野时令》安装与更新

发行包内有 `XiangyeShiling` 与 `XiangyeShiling.Content` 两个模组目录。实测环境为 Windows 版《星露谷物语》1.6.15、SMAPI 4.4.0 和 Content Patcher 2.9.0。先单独安装 [SMAPI](https://smapi.io/) 与 [Content Patcher](https://www.nexusmods.com/stardewvalley/mods/1915)，再安装本模组。

## 新装与更新

1. 退出游戏，将原有存档目录另存备份。Windows 的存档通常位于 `%APPDATA%\StardewValley\Saves`。
2. 从本项目的 [GitHub Releases](https://github.com/Sora6A/stardew-xiangye-shiling/releases) 下载 `XiangyeShiling-<版本>.zip`。解压 ZIP，把两个模组目录放入游戏的 `Mods`。
3. 从 SMAPI 启动，进入一个新档或存档副本。游戏内语言为中文时显示中文，其他语言采用英文。具体来源、加工与解锁见 [上手指南](https://github.com/Sora6A/stardew-xiangye-shiling/blob/main/docs/PLAYER_GUIDE.md)。

更新时先备份 `XiangyeShiling/config.json`，替换两个旧模组目录后迁回需要的配置。物品 ID、学习配方键和已开工批次保持稳定。等待处理中的设备完成后再跨版本联机。

## 联机设备操作

单人可直接进入。主机和农工须安装相同版本的代码模组与内容包。在双方 `XiangyeShiling/config.json` 中设置 `"EnableLanMachineOperations": true`，重启游戏，从原版多人菜单使用直接 IP。已核对 Windows 原版环回直连。Steam 邀请、Galaxy、分屏、其他操作系统、地图扩展与 Automate 组合的兼容性尚未验收。

主机确认开工、领取与争用，农工的款项和物品留在相应角色。工作中和待领取的设备会保留批次，待领取时需有足够背包空位。其他开发诊断选项保持默认关闭。

## 卸载与反馈

卸载前在装有模组的情况下先完成并领取所有批次，收获地里作物，拆回空闲设备并处理模组物品。正常就寝保存后另存备份，再移走这两个模组目录。没有这些内容包时，自定义物品、作物和设备可能无法载入，先用存档副本确认结果。

遇到内容缺失，核对 SMAPI 日志里的两个模组版本和 Content Patcher 错误。提交 [GitHub Issue](https://github.com/Sora6A/stardew-xiangye-shiling/issues) 时带上游戏、SMAPI、Content Patcher 和模组版本、复现步骤及脱敏的相关日志。保留原存档备份供本机排查。
