# LuaShengIME

LuaShengIME 是一个基于 .NET for Android（`net8.0-android`）的中文输入法项目，内置 Rime 引擎与用户词库管理能力，并提供上屏后预测候选。

## 功能概览
- 基础输入：拼音/符号/数字等键盘布局
- Rime 引擎：方案切换、用户数据同步、重新部署
- 候选面板：分页、预览与候选管理
- 词预测：上屏触发预测候选，支持自定义预测词
- 词库管理：导入/导出自定义预测词，导入/导出 Rime 用户词库（zip）

## 运行环境
- .NET 8 SDK（含 Android workload）
- Android SDK / Build Tools
- JDK（Android 构建依赖）
- 目标 Android API：`21+`（`SupportedOSPlatformVersion=21`）
- 仅内置 `arm64-v8a` 原生库（Rime），默认目标 ABI 为 `android-arm64`

## 构建
在项目根目录执行：

```bash
dotnet build IME.sln -c Debug
```

> Release 构建如需 AOT，需设置 `PublishTrimmed=true`；否则会触发 XA1030。

## 目录结构
- `Assets/`：Rime 方案与资源（`Assets/rime`）
- `Features/`：功能模块
  - `Input/`：输入服务、事件分发与引擎管理
  - `Keyboard/`：键盘视图与布局切换
  - `Candidates/`：候选词面板与分页
  - `Prediction/`：词预测与管理 UI
  - `Settings/`：设置页与持久化
  - `UserLexicon/`：用户词库解析/合并/同步
- `Shared/`：共享代码（输入引擎封装、工具）
- `NativeLibraries/`：Rime 原生头文件（API 参考）
- `Resources/`：Android 布局、资源与 XML
- `libs/`：原生库（`librime.so` / `libc++_shared.so`）

## 设置项说明
设置入口可配置：
- 振动/按键音
- 简繁输出
- 候选预览
- 候选分页（3/6/9/12）
- 方案选择（如月光拼音、仓颉5）
- 主题选择（浅色/深色）
- 词预测：启用、是否始终预测、预测轮数

## 词预测与管理
- 上屏后触发预测候选（Rime 优先，本地统计为补充）
- “预测词管理”支持：
  - 添加/删除自定义预测词
  - 导入/导出自定义预测词
  - 导入/导出 Rime 用户词库（zip）

### 自定义预测词导入/导出格式
- 每行格式：`前词\t预测词`
- 编码：UTF‑8（无 BOM）

### Rime 用户词库导入/导出格式
- 导出为 zip，包含多个 `*.txt`
- 导入时使用文件名（去扩展名）作为词库名
- 建议导入/导出后执行“重新部署 Rime”生效

## 权限说明
- 需要外部存储相关权限用于导入/导出（包含 SAF 文件选择）
- 具体权限定义见 `AndroidManifest.xml`

## 常见问题
- **XA1030（AOT）**：Release 构建时需 `PublishTrimmed=true` 或关闭 AOT 相关开关。
- **候选分页不生效**：导入/修改配置后需“重新部署 Rime”。
- **Rime 初始化失败**：确认 `libs/arm64-v8a` 与 `Assets/rime` 资源完整。

## 开发约定
- 保持模块边界清晰（Input / Keyboard / Candidates / Settings）
- 新增逻辑优先拆分到对应模块，避免在 `Ime` 中堆叠细节
- 建议为关键逻辑添加最小化 smoke tests