<template>
  <section class="relative overflow-hidden bg-gradient-to-b from-primary/5 to-surface pt-20 pb-16 md:pt-28 md:pb-24">
    <div class="mx-auto max-w-5xl px-4 text-center sm:px-6 lg:px-8">
      <AnnouncementSection />

      <div class="reveal-pop mb-6 flex justify-center" style="--reveal-delay: 80ms">
        <div
          v-parallax="{ tilt: 12, shift: 10, scale: 1.02 }"
          class="logo-halo flex h-16 w-16 items-center justify-center rounded-2xl bg-primary text-white shadow-lg"
        >
          <svg xmlns="http://www.w3.org/2000/svg" class="h-8 w-8" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M3 5h12M9 3v2m1.048 9.5A18.022 18.022 0 016.412 9m6.088 9h7M11 21l5-10 5 10M12.751 5C11.783 10.77 8.07 15.61 3 18.129" />
          </svg>
        </div>
      </div>

      <h1 class="reveal-up text-3xl font-bold tracking-tight text-body sm:text-4xl md:text-5xl" style="--reveal-delay: 160ms">
        让中文输入更快、更准、更懂你
      </h1>

      <p class="reveal-up mx-auto mt-4 max-w-2xl text-lg text-body/70 sm:text-xl" style="--reveal-delay: 260ms">
        LuaShengIME 内置 Rime 引擎，候选精准、词库可控，越用越顺手。
      </p>

      <div class="reveal-up mt-8 flex flex-col items-center justify-center gap-3 sm:flex-row sm:gap-4" style="--reveal-delay: 340ms">
        <a
          :href="downloadUrl"
          rel="noopener noreferrer"
          target="_blank"
          class="shine-button inline-flex items-center gap-2 rounded-xl bg-cta px-8 py-3.5 text-base font-semibold text-white shadow-md transition hover:bg-cta-hover hover:shadow-lg"
        >
          <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
          </svg>
          立即下载
        </a>
        <a
          href="#features"
          class="shine-button inline-flex items-center gap-2 rounded-xl border-2 border-primary/30 bg-white/60 px-8 py-3.5 text-base font-semibold text-primary backdrop-blur transition hover:border-primary hover:bg-white"
        >
          查看功能
        </a>
      </div>

      <p class="reveal-up mt-6 text-sm text-body/50" style="--reveal-delay: 420ms">
        支持 Android 5.0+（API 21+）
      </p>
    </div>

    <div class="pointer-events-none absolute -top-40 left-1/2 h-[500px] w-[800px] -translate-x-1/2 rounded-full bg-primary/5 blur-3xl" />
  </section>
</template>

<script setup lang="ts">
const {
  public: { downloadUrl },
} = useRuntimeConfig()
</script>
