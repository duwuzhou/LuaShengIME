# 仓库贡献指南（Repository Guidelines）

## 项目结构与模块组织
本仓库是 .NET 8 + Android 的输入法项目，核心目录如下：

```text
IME/
├─ Features/            # 业务功能模块
│  ├─ Input/            # 输入流程与协调器
│  ├─ Keyboard/         # 键盘布局、渲染与按键事件
│  ├─ Candidates/       # 候选词与候选面板
│  ├─ Prediction/       # 词预测与管理
│  ├─ Settings/         # 设置页与配置项
│  └─ UserLexicon/      # 用户词库
├─ Shared/              # 公共抽象、模型、数据访问
├─ Resources/           # Android 资源（layout/drawable/values/xml）
├─ Assets/              # 运行时资源
├─ libs/                # 本地 so 依赖
├─ NativeLibraries/     # Native 相关封装/文件
├─ IME.csproj           # 主项目文件
└─ IME.sln              # 解决方案
```

## 构建、测试与开发命令
在 `IME/IME/IME` 目录执行：

- `dotnet restore IME.csproj`：还原 NuGet 依赖。
- `dotnet build IME.csproj -v minimal`：编译并检查资源/代码生成错误。
- `dotnet test IME.sln -v minimal`：运行解决方案中的测试项目（若存在）。
- `dotnet clean IME.csproj`：清理 `bin/`、`obj/`，用于修复脏构建问题。

## 代码风格与命名规范
- 使用 C#（启用 Nullable），UTF-8 编码，4 空格缩进。
- 类型/方法/属性使用 `PascalCase`，局部变量使用 `camelCase`，私有字段使用 `_camelCase`。
- Android 资源文件统一小写下划线命名，例如：`activity_settings.xml`、`key_background.xml`。
- 优先小类、单一职责；复杂逻辑拆分到 `Features/*` 对应模块。

## 测试指南
- 目前自动化测试较少；新增纯逻辑时请补充单元测试（建议 `Tests/IME.Tests`）。
- 测试命名建议：`MethodName_ShouldExpectedBehavior_WhenCondition`。
- 提交前至少执行：`dotnet build`，并进行手工冒烟验证（键盘切换、候选词、预测词、设置开关）。

## 提交与 Pull Request 规范
- Commit 信息使用简短祈使句：如 `Refactor keyboard layout manager`、`Fix prediction export`。
- 一个提交聚焦一个变更点，避免混入无关修改。
- PR 必须包含：变更说明、变更原因、验证步骤/结果（命令输出）、UI 变更截图（如有）。
- 关联 Issue/任务号，并说明是否涉及配置、权限或数据迁移。

## 安全与配置提示
- 禁止提交 `bin/`、`obj/`、密钥、机器本地路径等敏感内容。
- 更新 `libs/arm64-v8a` 或 `NativeLibraries` 时需注明来源与兼容性。
- 涉及 Android 版本差异（尤其存储权限）必须加运行时判断与降级处理。

我是花落 中文回复我 使用utf-8编码格式读写内容 