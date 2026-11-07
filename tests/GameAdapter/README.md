# 本机游戏适配 CLI 回归

此工程直接引用实际生产项目和本机合法的 Stardew Valley 1.6.15 / SMAPI 4.4 SDK；它不是无游戏依赖的 MachineCore 检查，不进入普通 CI 或发布包。所有 SDK / 生产引用均为 `Private=false`，运行时仅从明确指定的本地目录解析程序集，不复制或分发游戏 DLL。

运行先显式加载`--production`文件，再在测试循环外核实际MachineService程序集Location与该路径一致，并输出实际路径/SHA-256；误绑作为环境失败退出2，不跳过后冒称通过。此定位校验是首轮五红/五绿之后补充的测试健壮性检查，原标准路径红栈仍对应实际生产源码。

```powershell
dotnet run --project tests/GameAdapter/GameAdapter.csproj -c Release -p:GamePath="D:\path\to\game" -p:SmapiPath="D:\path\to\smapi" -- --game-path "D:\path\to\game" --smapi-path "D:\path\to\smapi" --production "D:\code\stardew\src\XiangyeShiling\bin\Release\net6.0\XiangyeShiling.dll"
```

空对象回归直接调用生产 `TryGetKind`、`TryRead`、`BlocksRemoval` 和实际 private `ProtectNpc`，并在独立 CLI 内临时注册该实际 Harmony prefix，调用 SDK 的 `checkDestroyItem(null, tile, false)`。原生该分支在空对象处短路，不读取地图或 Game1；用未初始化 GameLocation 仅覆盖这个入口，`finally` 撤销本次补丁。SDK/Harmony运行时使用本工程的net6 CLI，不能把在不同CLR版本的PowerShell宿主中直接加载Harmony当作等价运行。

补充回归用实际SDK无参数 Object 构造函数及Net字段构造普通物品/原版BC和两种自定义设备，不调用ItemRegistry或资产初始化；Busy由实际MachineRules.Start生成。普通/Idle仅核生产分类、状态/保护API及prefix放行/ref结果不变；Processing、Ready、缺失/损坏/未来版本/Kind不符还通过实际注册prefix的原生入口核拒绝和原raw字符串/metadata引用保持。受保护非空对象必须被prefix阻止进入未初始化世界的原生正文；不得用该夹具对普通/Idle非空对象执行原生地图移除。

当前共13项：五项空输入及实际空原生入口，普通O/BC放行，以及两种设备的Idle/Processing/Ready/Corrupt/Missing/Future/Mismatch保护。它不启动游戏窗口、不初始化资产、不读取或保存世界；真实SDK对象/入口回归不代替完整NPC update、实际玩家移动、世界设备移除或联机验收。

每项失败独立记录，失败退出1、环境/解析失败退出2；没有跳过后冒称通过。生产修复前后应分别保存新的 `.local` 日志，禁止覆盖原始红证据。
