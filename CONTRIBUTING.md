# 开发与维护规范

适用于所有代码、内容、工具和文档变更。目标是让一次提交可审查、一次修复可复现、一次中断可接续。

## 1. 开始前与分支工作流

先读 [HANDOFF](docs/HANDOFF.md) → [开发计划](docs/DEVELOPMENT_PLAN.md) → [架构说明](docs/ARCHITECTURE.md) → [开发环境与命令](docs/DEVELOPMENT.md)。设计争议以 [DESIGN](docs/DESIGN.md) 为范围依据，不以聊天记忆代替仓库记录。

检查分支、未提交文件和远程跟踪状态；`git diff` 不显示未跟踪文件，必须同时查看 `git status --short`。有前人修改时先在交接中登记，不执行 `reset --hard`、`clean` 或盲目覆盖。

干净工作区的新任务流程如下。命令中的分支名只是例子，每个任务使用明确主题：

```powershell
git fetch origin
git switch main
git pull --ff-only origin main
git switch -c feat/p1-machine-menu
```

已有未提交工作时，在其所在基线上创建任务分支并保留修改；不要先拉取或切换到可能冲突的历史。当前首次交付属于这种情况，见 HANDOFF。建议分支前缀为 `feat/`、`fix/`、`docs/`、`refactor/`、`test/`、`chore/`。

开发 → 本地检查 → 明确选文件提交 → 推送任务分支 → PR 到 `main` → 通过检查与审查 → **使用 merge commit 合并** → 核对远程 `main` → 更新交接。默认不 squash，以保留每个完整开发提交；合并提交也填写完整正文。修复冲突在任务分支进行，合并最新 `origin/main` 后重新执行受影响检查，保留解决冲突的依据。不默认改写已推送历史，不强推 `main`。

```powershell
git add -- path/to/changed-file
python tools/check_workflow.py --message .local/commit-message.txt
git commit -F .local/commit-message.txt
python tools/check_workflow.py --commit-range origin/main..HEAD
git push -u origin docs/development-workflow
```

上述推送分支仅为本轮示例；实际推送前用 `git branch --show-current` 核对。已获得当前任务的提交/推送/合并授权时直接按流程完成，不另设重复确认；仅完善规范不代表已请求发布当前所有未提交功能。

当前累计工程的审查分组、跨目录编译依赖和首次完整提交树检查见[P1交付组织](docs/P1_DELIVERY_UNITS.md)。目录分组不等于可独立构建的提交；不能用含未跟踪依赖的工作区检查结果，代替实际提交树完整性。

远程仓库建议对 `main` 配置保护：要求 PR、禁止强推和删除、要求 `catalog`、`machine-core`、`maintenance`、`commit-messages` 检查通过。**本次只提供规则与工作流，未读取或配置远程保护设置**；合并者必须在实际合并前核实检查与目标分支。若没有其他维护者，记录自审范围，不能伪称已由他人审查。

## 2. 完整 commit 信息

标题格式：`type(scope): 具体变化`。类型为 `feat`、`fix`、`docs`、`refactor`、`test`、`build`、`ci`、`chore`、`revert`、`merge`；scope 使用模块名，破坏性变更在冒号前加 `!` 并在风险中说明。

正文必须包含以下六段，每段写实质内容，不留模板占位符：

| 段落 | 必填内容 |
|---|---|
| 背景与目的 | 具体问题、触发条件或对应计划任务 |
| 变更内容 | 行为变化、主要模块、采用方案与必要取舍 |
| 验证结果 | 执行命令、环境、通过/失败结果；未执行及原因单列 |
| 影响与风险 | 存档、稳定 ID、联机、依赖、兼容与尚未验证的部分；无影响也说明依据 |
| 回滚方案 | 如何撤回代码/数据、是否需要备份或迁移；不能默认删除玩家物品 |
| 关联记录 | 计划/验收/bugfix 路径与 `docs/HANDOFF.md`；有关联 Issue/PR 时附真实编号 |

使用 [.github/COMMIT_TEMPLATE.txt](.github/COMMIT_TEMPLATE.txt)。可在本仓库启用编辑器模板：

```powershell
git config --local commit.template .github/COMMIT_TEMPLATE.txt
```

`git commit -m` 或 `-F` 会绕过编辑器模板，因此仍须运行消息检查。`fix` 提交必须引用 `docs/bugfix/BUG-年份-编号-主题.md`，且该提交实际新增或更新此记录；PR 或合并提交不能成为补写 bug 文档的唯一位置。

每次向远程推送后，给出本次实际推送的提交，而不是所有历史提交；逐项附完整 SHA、标题和正文。核对命令例如 `git show --no-patch --format=fuller <SHA>`。PR 合并后补充 PR 地址、实际 merge SHA、远程目标分支与检查结果。SHA 不能写进自身提交正文，应在提交后的交付说明/下一次交接中记录，避免无限 amend。

完整实例见 [提交说明实例](docs/templates/COMMIT_EXAMPLE.md)。实际提交必须根据实际改动和检查重写，不能复制示例中的通过结果。

## 3. Bug 修复闭环

每个缺陷使用 [bugfix 模板](docs/bugfix/TEMPLATE.md)，按 `BUG-YYYY-NNN` 编号并更新 [索引](docs/bugfix/README.md)。状态区分：调查中 → 已复现 → 已修复待验证 → 本地验证通过 → 已合并；关闭状态必须有验证与真实提交证据。

1. 写环境、输入/存档条件、最小复现步骤、预期与实际行为，并保存脱敏证据。
2. 定位根因与为什么已有检查没发现；推测与事实分开。
3. 加入会在旧实现失败的单元/回归测试，记录测试文件、名称、关键断言和修复前结果。
4. 实施修复，说明选择该方案的原因、涉及接口/状态迁移和未采用方案的必要取舍。
5. 记录修复后测试命令、结果、相关实机回归、剩余风险与回滚步骤；更新关键注释与交接。

历史修复没有保留旧失败日志时须写“历史补录，未保存修复前日志”，用可执行负例补足，不捏造红转绿记录。纯外部环境问题也要附自动化可检查的部分及人工复现；不能写一句“无需测试”就关闭。确实无法自动化的环节写明原因、替代验证和未覆盖范围，保持待验收状态。

## 4. 文档、注释与完成定义

| 变更类型 | 必须同步 |
|---|---|
| 任何任务进展/中断 | HANDOFF：工作区、检查证据、下一步 |
| 设计或范围调整 | DESIGN、DEVELOPMENT_PLAN；先调整基线再扩内容 |
| 公共接口、事务、持久化、联机 | ARCHITECTURE、模块契约、关键代码注释；说明不变量与失败路径 |
| 物品/配方/本地化 | catalog 源和生成物；ID 迁移影响 |
| 构建、依赖、安装、运行命令 | DEVELOPMENT、ENVIRONMENT、CI/校验；不要只改 manifest |
| 缺陷修复 | 对应 bugfix、测试、索引、HANDOFF |
| 阶段验收/发布 | 计划状态、validation 记录、README 真实能力边界 |

公开方法/重要模块的注释须让接手者知道：谁调用、参数单位与可信来源、返回值含义、是否有副作用、哪些失败需保留原数据。跨模块协议、并发、存档、路径安全和不直观的兼容处理需要解释“为什么”。简单赋值无需逐行复述；生成文件注明来源而非手工重写。文档和解释性注释优先中文，代码标识符使用稳定英文名。

禁止裸 `TODO`：写对应计划/bug ID、阻塞条件与验收标准。改变规则时一起改注释，不能保留过时的成功承诺。新增持久化字段须说明默认值、版本兼容和失败处理；新增依赖须有版本依据、安装方式和打包边界。

一项任务完成需要：实现/文档一致，适当检查通过，缺陷记录完整，无未解释的失败，HANDOFF 足以复跑，提交范围明确。未执行的实机/联机测试不能被编译或模拟测试替代。

## 5. 检查与提交前清单

```powershell
python tools/check_workflow.py
python tools/catalog.py
python -m unittest discover -s tests -v
dotnet run --project tests/MachineCore/MachineCore.csproj -c Release
git diff --check
git status --short
```

纯文案变动至少执行文档检查与差异检查；改规则/工具时执行相关回归；修改 C# 或依赖时按 DEVELOPMENT 完成本地构建；触及游戏行为时补实机记录。避免为了文案重复启动游戏，也不要省略行为变更所需验证。所有结果注明本地/CI、日期、命令、版本与未覆盖边界。

CI 的 `maintenance` 检查文档结构、bug 记录和本地链接；`commit-messages` 检查 PR 中提交正文，以及 fix 提交是否同时更新 bug 文档。它们不能判断根因分析是否正确、测试是否充分或远程保护是否开启，审查仍须阅读正文。

## 6. 中断与接手

维护 [实时交接](docs/HANDOFF.md)，按 [交接模板](docs/templates/HANDOFF_TEMPLATE.md) 写到可直接执行：已完成事实、准确文件/入口、未提交范围、命令及结果、运行进程、临时资源位置、失败尝试和下一条工作步骤。不要只写“继续 P1”。本机绝对路径只放在交接环境段，通用说明采用相对路径或参数。

长任务在可恢复检查点写一次交接。交接记录描述的是那个时间点；接手者第一步仍须验证 `git status`、分支、日志和进程，不能盲用旧 PID、窗口句柄或历史授权外推的新发布行为。日志/存档留在本地，仅提交脱敏摘要；跨机器可由脚本重建的产物不要当成源码依赖。
