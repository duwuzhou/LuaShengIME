# Repository Guidelines

## 项目结构与模块组织
本仓库是 `LuaShengIME` 官网，基于 Nuxt 3。页面入口在 `pages/index.vue`，页面区块拆分在 `components/`，如 `HeroSection.vue`、`StorySection.vue`。全局样式位于 `assets/css/main.css`，客户端动效指令位于 `plugins/`。静态资源放在 `public/`，其中 `public/downloads/LuaShengIME.apk` 为默认下载包。构建产物目录 `dist/`、`.nuxt/`、`.output/` 和依赖目录 `node_modules/` 不应手工修改。

## 构建、测试与开发命令
- `npm install`：安装依赖并触发 `nuxt prepare`。
- `npm run dev`：启动本地开发服务器。
- `npm run build`：生成生产构建，提交前至少执行一次。
- `npm run preview`：本地预览生产包。
- `npm run generate`：生成静态站点版本。

## 代码风格与命名约定
统一使用 UTF-8 编码，文档与界面文案使用中文。Vue 单文件组件使用 2 空格缩进，优先使用 `script setup` 和 Composition API。组件文件使用 PascalCase 命名，例如 `ScreenshotSection.vue`；目录与静态资源路径保持小写或现有命名，不随意重命名已发布资源。Tailwind 类按“布局 -> 间距 -> 视觉”顺序书写，复用现有配色与动效类，避免引入无必要的新依赖。

## 测试指南
仓库当前未接入自动化测试框架，因此以构建验证和人工检查为主。修改后至少运行 `npm run build`，并手动检查首页首屏、截图区、下载链接和移动端布局。若后续新增测试，建议放在与功能相邻的位置，并采用 `*.spec.ts` 命名。

## 提交与合并请求规范
现有提交历史以英文祈使句为主，例如 `Add configurable simulated typing feature`、`Update IME features and release protection`。继续沿用该风格：`Add ...`、`Fix ...`、`Update ...`，单次提交聚焦一个主题。提交 PR 时应包含变更说明、影响范围、验证方式；涉及页面改动时附上桌面端和移动端截图。若修改下载地址、群链接或 SEO 文案，请在描述中明确标注。

## 配置与维护提示
运行时公开配置位于 `nuxt.config.ts` 的 `runtimeConfig.public`。隐私政策、下载地址和群链接属于对外信息，修改前请与维护者花落确认。不要提交密钥、临时日志或与站点无关的父项目改动。
