# BUG-2026-024：世界输入场景菜单可用但移动无响应

- 状态: 调查中；新进程人工输入正常，旧失败根因未定，未实施移动修复
- 发现/更新日期: 2026-11-06，Asia/Shanghai
- 责任任务/分支: P1单人两设备世界链，feat/p1-game-adapter
- 性质: 操作者报告的实机输入问题；暂不能归因生产模组、夹具或外部输入环境

## 现象与影响

同一WorldInteraction进程中，豆坊已由真实制作菜单生成并入包；操作者按住D约1秒没有移动，Esc菜单可以响应。当前停在Step0/Passed0，完整20步未验收。自动D/Right/Esc短按也未观察到变化，但不能用自动短按失败替代人工输入证据。此次尚无存档写入证据；最终磁盘结论须在退出后审计，不能仅凭Saved=false推断。

## 环境与前置条件

独立Windows测试运行时，游戏1.6.15、SMAPI4.4.0、CP2.9.0；唯一游戏进程36832，WorldInteraction启用、预览关闭，专用单人XYSP1UI/8624092802。源安装、正式存档未用于本次测试。报告中位置(448,464)/Tile(7,7)、UsingTool=false、Menu=null、Failure=null；这些是周期报告时刻读数，未覆盖失败按键瞬间的所有门禁。

## 复现步骤

1. 以原夹具在独立运行时加载固定副本；原生制作一台豆坊，放入背包并关闭菜单，保持Step0。
2. 点击游戏区域、切到英文输入法，按住D约1秒，再按Esc，打开菜单后关闭。不按中键、不睡觉或保存。
3. 本次操作者明确回复“移动无响应，菜单可以”。当前另待W/A/S各约1秒的方向对照，尚不把仅向右受阻与全部移动失效视为同一结论。

原报告、源码、运行DLL和本进程日志已在改诊断代码之前归档到本地`.local/p1-world-chain-2026-11-06/user-input-failure`，evidence.json记录各文件SHA及操作者结果。该原DLL不会因后续构建而变成带新诊断的版本。

## 根因分析

尚未确定。已核专用存档的四移动绑定为W/A/S/D，菜单为E/Escape；这不能证明运行时绑定没有变化。WorldInteraction.Tick只固定时钟、观察和写报告，除阶段准备或真实击落后的单次观察定位外，不每帧重置角色位置；生产源码搜索未发现移动键Suppress调用。这些缩小调查范围，不能证明角色没有冻结、输入未被其他层过滤、当前位置无碰撞或原生控制更新已经执行。

旧报告仅有UsingTool/Menu/位置，StageMovementInputs还只针对阶段7计数。当前Step0的零值不能证明操作者没有按D。后续必须分别看按键发生时的控制门禁、实际运行绑定、输入层和位移，不能强置CanMove、清动画/控制器来制造通过。

## 解决方案

尚无移动修复。当前只给WorldInteraction增加只读ControlState及最多32条W/A/S/D/Escape事件快照，记录焦点、菜单、冻结/暂停、CanMove、坐卧/动画/控制器、速度/方向、实际绑定和有限键的输入层。聊天或任意文本Subscriber存在时键字段为null且不记录事件；超限只计数，诊断异常单独记录。不抑制按键、不调用移动/拾取、不改变20步通过条件，不进入玩家包。

新代码未热替换到36832。旧进程已标题栏X退出，after-input-failure审计确认八存档不变、六目标配置原字节恢复禁用；其余已知88路径仅build夹具DLL/PDB因诊断构建变化。首次临时关闭预检使用了错误的build-harness键名，在任何归档/恢复前拒绝；按实际baseline的build键名修正后通过，未修改存档或放宽路径比较。

随后另存新88项停止基线，停止状态部署本次DLL/PDB到runtime，启动新进程9004；不更新网络profiles/curated，不改生产包。新诊断原字节在本地`.local/p1-world-controls-2026-11-06`，新运行DLL SHA为`c8785359aa902b7cdd6b236e0864d2a4f9403385f008965dfec5b66609f33250`。当前等待新窗口完整人工方向对照，不能将新字段回填旧报告；定位根因后先补能揭示该根因的回归，再实施修复。

## 单元测试

移动缺陷的具名回归尚未编写/执行：根因未知，不能用“无错误返回”或手写预期位置模拟修复。没有修复前单元红与修后绿，不宣称此bug已修复。

2026-11-06实际执行`dotnet run --project tests/MachineCore/MachineCore.csproj -c Release`，exit0，115项通过。其中已有`input observation omits text and arbitrary keys`验证当前复用策略在聊天/Subscriber存在时禁止键记录；`input observation refuses unrelated saves clients split screens and preview`等只验证原有纯策略。新世界报告、事件上限和SDK状态采样仍需实机核，115项不代表移动恢复或本问题已验收。

## 验证结果

2026-11-06新诊断代码三次SDK Release构建均exit0、0警告/0错误；最后一次为完整字段及格式整理后的部署版本，原始构建输出在world-controls/build.txt，终端解码显示乱码不改写该输出。原运行DLL另存，115核心已实际通过，不因后续文案重复运行。

新进程9004已自动加载固定副本，实际鼠标关闭制作菜单，仍Step0/未制作/未中键。周期读数Active/ActiveWithoutOverlay/KeyboardFocus=true、CanMove=true、UsingTool=false、各冻结/暂停/新日/事件/坐卧/动画暂停false、freezePause/noMovementPause=0、controller=null、速度5、addedSpeed=0、四绑定为W/A/S/D；只说明该次空闲快照没有上述门禁。一次自动D短按后没有ControlInputEvents或位移，不能据此推断人工按住D也未到达。原报告保留为world-controls/after-auto-d.json，观测异常null。

操作者对旧W/A/S问题晚到回复“没看到画面”；当时旧窗口已关闭、新窗口未启动，所以不算方向或输入结果。随后明确确认新9004“四个方向和菜单都正常”。8条W/S/A/D/S/A/S/Escape事件均未抑制，焦点/CanMove=true，各键同时出现在MonoGame/InputState/聊天过滤中；事件之间的位置显示上、下、左、右均有位移，最终位置(484.96002,537.9201)。这是新进程的输入可用证据，不能倒推旧失败原因或称只读字段修复了输入。

原报告SHA`d850ce8d8369f6d00fe8070c9a44e961587ca3e0b45913f77cc366e568725229`，在world-controls/manual-input留存；Esc有事件，菜单开关由操作者确认，未给旧报告补写MenuChanged证据。随后同9004完整20步世界链通过，豆坊和发酵缸两段实际步行分别有6/5方向事件与61/60条轨迹，原实体消失/同payload入包/重放新身份均核。最终报告SHA`ce6a7c6a535d2acea6f526396755682df45bc405eeafef6b1917dccd9ac7bc54`，退出后CIM0/八存档不变/六配置禁用审计通过，见[完整实测](../validation/P1-world-ui-2026-11-06.md)。不拼接旧7/20，不因新进程通过而把旧输入失败标为已修复；后续若复发需取失败当时控制/输入证据再补具名根因回归。

## 风险与回滚

此阶段不能确认影响范围，更不能将调查当作生产修复发包。只读报告可能漏掉秒级采样之间的短按，事件回调也可能早于原生控制更新；各输入层依次读取，不是原子快照或操作系统原始输入。32条事件容量到达后不再保留新事件；完整输入观测需另行设计明确范围，不能为取绿无限扩采。

撤回诊断时停止游戏后恢复本轮before-source中的WorldInteractionScenario并重新构建；运行DLL/PDB须成套部署，保留调查原报告。只恢复本轮自改配置，不回滚或清空玩家存档，不撤回已有机器事务/工具保护。

## 关联记录

[世界场景](../../tests/GameHarness/WorldInteractionScenario.cs)、[夹具说明](../../tests/GameHarness/README.md)、[历史世界链](../validation/P1-world-ui-2026-10-01.md)、[计划](../DEVELOPMENT_PLAN.md)、[交接](../HANDOFF.md)。当前未提交、推送、创建PR或合并；无对应新SHA。
