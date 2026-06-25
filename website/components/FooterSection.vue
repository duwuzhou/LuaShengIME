<template>
  <footer class="bg-body py-10">
    <div class="mx-auto max-w-5xl px-4 sm:px-6 lg:px-8">
      <div class="grid gap-8 sm:grid-cols-3">
        <div>
          <h3 class="text-base font-semibold text-white">LuaShengIME</h3>
          <p class="mt-2 text-sm text-white/50">
            面向安卓的中文输入法，内置 Rime 引擎，候选精准、词库可管理。
          </p>
        </div>

        <div>
          <h4 class="text-sm font-semibold text-white/80">相关链接</h4>
          <ul class="mt-3 space-y-2 text-sm">
            <li>
              <a href="#features" class="text-white/50 transition hover:text-white">功能介绍</a>
            </li>
            <li>
              <a href="#download" class="text-white/50 transition hover:text-white">下载安装</a>
            </li>
            <li>
              <a href="#faq" class="text-white/50 transition hover:text-white">常见问题</a>
            </li>
            <li>
              <a :href="groupChatUrl" target="_blank" rel="noopener noreferrer" class="text-white/50 transition hover:text-white">加入群聊</a>
            </li>
            <li>
              <a href="#" class="text-white/50 transition hover:text-white">隐私政策</a>
            </li>
          </ul>
        </div>

        <div>
          <h4 class="text-sm font-semibold text-white/80">权限与隐私</h4>
          <ul class="mt-3 space-y-2 text-sm text-white/50">
            <li>输入法仅在输入时运行</li>
            <li>词库数据默认保存在本地</li>
            <li>不开通额外权限也可正常输入</li>
          </ul>
        </div>
      </div>

      <div class="mt-8 border-t border-white/10 pt-6 text-center text-xs text-white/30">
        &copy; {{ currentYear }} LuaShengIME. All rights reserved.
      </div>
    </div>
  </footer>
</template>

<script setup lang="ts">
const currentYear = new Date().getFullYear()
const {
  public: { groupChatUrl },
} = useRuntimeConfig()
</script>
