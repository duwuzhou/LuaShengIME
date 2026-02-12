# IME 项目开发文档

## 1. 项目概览
本项目为 Android 平台输入法（IME），基于 .NET 8 / net8.0-android。核心包含输入引擎接入、键盘视图、候选词面板、用户词库、设置与同步等功能模块。

## 2. 目录结构
以下为关键目录说明（路径相对 `E:\Development Project\IME\IME`）：
- `IME/`：解决方案与主工程
  - `IME.sln`：解决方案入口
  - `IME/`：主工程源码
    - `Features/`：功能模块
      - `Input/`：输入服务、事件分发与引擎管理
      - `Keyboard/`：键盘视图与布局切换
      - `Candidates/`：候选词面板与分页
      - `UserLexicon/`：用户词库解析/合并/同步
      - `Settings/`：设置页与持久化
    - `Shared/`：共享引擎、数据、工具
    - `Resources/`：Android 资源

## 3. 开发环境准备
建议环境：
- .NET 8 SDK（含 Android workload）
- Android SDK / Build Tools
- 合适的 JDK（Android 构建需要）

注意：本项目目标框架为 `net8.0-android`。

## 4. 构建与运行
在 `E:\Development Project\IME\IME` 下执行：

```powershell
# 构建解决方案
DOTNET_CLI_TELEMETRY_OPTOUT=1 dotnet build .\IME.sln -c Debug
```

如果需要发布/部署到设备，请使用 IDE 或根据实际设备配置选择合适的部署方式。

## 5. 核心输入流程概览
- `Features/Input/Ime.cs`：输入服务入口，负责生命周期与组件装配
- `InputEngineManager`：输入引擎（DB/Rime）初始化与切换
- `InputCoordinator`：键盘事件统一分发入口
- `CharacterInputHandler` / `SpecialKeyHandler`：字符与特殊键处理
- `KeyboardHandler`：键盘视图创建、布局切换、候选更新

流程简述：
1. `Ime.OnCreate` 初始化引擎与键盘组件
2. `KeyboardHandler` 创建视图并绑定 `InputCoordinator`
3. `InputCoordinator` 统一分发 key 事件到处理器
4. 处理器调用输入引擎并驱动候选更新

## 6. 候选词面板结构
`Features/Candidates/Candidate` 已拆分为多个 partial 文件：
- `Candidate.cs`：构造与初始化
- `Candidate.Fields.cs`：字段与事件
- `Candidate.UI.cs`：UI 与交互
- `Candidate.Candidates.cs`：候选数据逻辑
- `Candidate.Paging.cs`：分页逻辑
- `Candidate.Panel.cs`：弹出面板交互

## 7. 用户词库与同步
关键文件：
- `UserLexiconService`：导入/导出服务
- `UserLexiconTxt.*`：词库 TXT 解析与序列化
- `UserLexiconMerger`：词条合并逻辑
- `RimeUserLexiconStore`：Rime 词库写入
- `SyncService`：Rime 用户数据 ZIP 导入/导出
- `RimeConfigPatcher`：Rime 配置写入（如候选分页）

## 8. 设置页结构
`SettingsActivity` 已拆分为多个 partial 文件：
- `SettingsActivity.Fields.cs`：字段与常量
- `SettingsActivity.Init.cs`：初始化与加载
- `SettingsActivity.Events.cs`：事件处理
- `SettingsActivity.SettingsStore.cs`：设置读写
- `SettingsActivity.RimeConfig.cs`：Rime 配置写入

## 9. 日志与排查
关键日志标签：
- `IME`：输入服务与引擎初始化
- `KeyboardHandler` / `InputCoordinator`：输入事件分发
- `RimeMaintenance` / `SyncService`：Rime 同步与部署

## 10. 常见问题
1) 候选分页不生效
- 需“重新部署 Rime”，并确保 `default.custom.yaml` 写入成功。

2) Rime 初始化失败
- 确认 Rime 相关 so 库与资源目录存在，并检查日志输出。

3) 构建失败
- 确认 .NET 8 + Android workload、Android SDK、JDK 配置完整。

## 11. 贡献与约定
- 优先保持模块边界清晰，避免 `Ime` 直接依赖具体实现细节。
- 组件化拆分：一个文件只聚焦一种职责。
- 建议新增单元测试或 DEBUG-only smoke tests 验证关键逻辑。

---
如需补充“运行/调试/发布”更详细流程，请告知目标设备与环境。
