<template>
  <section id="download" class="bg-gradient-to-b from-primary to-primary/90 py-16 md:py-20">
    <div class="mx-auto max-w-3xl px-4 text-center sm:px-6 lg:px-8">
      <h2 v-reveal class="reveal-up text-2xl font-bold text-white sm:text-3xl">
        开始更高效的中文输入
      </h2>
      <p v-reveal="100" class="reveal-up mx-auto mt-3 max-w-lg text-base text-white/80">
        免费下载 LuaShengIME，体验精准候选与可管理词库带来的顺畅输入。
      </p>

      <div v-reveal="180" class="reveal-up mt-8 flex flex-col items-center justify-center gap-4 sm:flex-row">
        <a
          :href="downloadUrl"
          rel="noopener noreferrer"
          target="_blank"
          class="shine-button inline-flex items-center gap-2 rounded-xl bg-cta px-8 py-3.5 text-base font-semibold text-white shadow-lg transition hover:bg-cta-hover hover:shadow-xl"
        >
          <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
          </svg>
          立即下载
        </a>

        <div class="flex flex-col items-center">
          <a
            :href="groupChatUrl"
            target="_blank"
            rel="noopener noreferrer"
            class="group flex flex-col items-center"
          >
            <div
              class="pulse-frame flex h-28 w-28 items-center justify-center rounded-xl border-2 border-white/30 bg-white/10 backdrop-blur transition group-hover:border-white/50 group-hover:bg-white/15"
            >
              <svg xmlns="http://www.w3.org/2000/svg" class="h-10 w-10 text-white/60" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
                <path stroke-linecap="round" stroke-linejoin="round" d="M17 8h2a2 2 0 012 2v8l-4-3H9a2 2 0 01-2-2v-1m10-4V6a2 2 0 00-2-2H5a2 2 0 00-2 2v8a2 2 0 002 2h2l4 3v-3h4a2 2 0 002-2z" />
              </svg>
            </div>
            <p class="mt-2 text-xs text-white/72">点击加入群聊</p>
          </a>
        </div>
      </div>

      <p class="reveal-up mt-6 text-sm text-white/50" style="--reveal-delay: 260ms">
        系统要求：Android 5.0+（API 21+）
      </p>
    </div>
  </section>
</template>
<script setup lang="ts">
const {
  public: { downloadUrl, groupChatUrl },
} = useRuntimeConfig()
</script>
