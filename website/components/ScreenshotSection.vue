<template>
  <section class="bg-white py-16 md:py-20">
    <div class="mx-auto max-w-6xl px-4 sm:px-6 lg:px-8">
      <h2 v-reveal class="reveal-up text-center text-2xl font-bold text-body sm:text-3xl">
        实机界面预览
      </h2>
      <p v-reveal="100" class="reveal-up mx-auto mt-3 max-w-2xl text-center text-body/60">
        用 6 张真实截图覆盖启用、输入、功能面板和管理配置，不再使用占位示意图。
      </p>

      <div class="mt-12 grid gap-8 lg:grid-cols-[minmax(0,1.08fr)_minmax(0,0.92fr)] lg:items-center">
        <div
          v-reveal="160"
          v-parallax="{ tilt: 8, shift: 10, scale: 1.01 }"
          class="reveal-up relative overflow-hidden rounded-[2rem] border border-primary/10 bg-gradient-to-br from-primary/5 via-white to-cta/5 px-4 py-8 shadow-[0_28px_80px_rgba(15,23,42,0.08)] sm:px-8"
        >
          <div class="pointer-events-none absolute inset-x-10 top-8 h-28 rounded-full bg-primary/10 blur-3xl" />

          <div class="relative mx-auto max-w-[21rem]">
            <div class="rounded-[2.5rem] border border-slate-900/90 bg-slate-950 p-3 shadow-[0_24px_60px_rgba(15,23,42,0.28)]">
              <div class="mb-3 flex justify-center">
                <div class="h-1.5 w-20 rounded-full bg-white/15" />
              </div>

              <div class="overflow-hidden rounded-[2rem] bg-white ring-1 ring-black/5">
                <img
                  :key="activeScreenshot.src"
                  :src="activeScreenshot.src"
                  :alt="activeScreenshot.alt"
                  class="h-auto w-full object-cover"
                  decoding="async"
                  fetchpriority="high"
                  loading="eager"
                >
              </div>
            </div>
          </div>
        </div>

        <div class="space-y-5">
          <div v-reveal="220" class="reveal-up rounded-[1.75rem] border border-primary/10 bg-surface p-6 shadow-sm">
            <p class="text-sm font-medium uppercase tracking-[0.24em] text-primary/70">
              当前展示
            </p>
            <h3 class="mt-3 text-2xl font-semibold text-body">
              {{ activeScreenshot.title }}
            </h3>
            <p class="mt-3 text-sm leading-7 text-body/65">
              {{ activeScreenshot.description }}
            </p>

            <div class="mt-5 flex flex-wrap gap-3">
              <span class="rounded-full bg-primary/10 px-3 py-1 text-xs font-medium text-primary">
                {{ activeIndex + 1 }} / {{ screenshots.length }}
              </span>
              <span class="rounded-full bg-body/5 px-3 py-1 text-xs font-medium text-body/60">
                真实安卓截图
              </span>
            </div>

            <div class="mt-6 flex gap-3">
              <button
                type="button"
                class="inline-flex items-center justify-center rounded-xl border border-primary/20 bg-white px-4 py-2.5 text-sm font-medium text-body transition hover:border-primary/40 hover:text-primary"
                @click="showPrev"
              >
                上一张
              </button>
              <button
                type="button"
                class="inline-flex items-center justify-center rounded-xl bg-primary px-4 py-2.5 text-sm font-medium text-white transition hover:bg-primary/90"
                @click="showNext"
              >
                下一张
              </button>
            </div>
          </div>

          <div class="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-2">
            <button
              v-for="(item, index) in screenshots"
              :key="item.src"
              v-reveal="260 + index * 60"
              type="button"
              class="reveal-up group overflow-hidden rounded-[1.35rem] border bg-white text-left shadow-sm transition hover:-translate-y-1 hover:shadow-md"
              :class="index === activeIndex ? 'border-primary/50 ring-2 ring-primary/15' : 'border-primary/10 hover:border-primary/35'"
              @click="selectScreenshot(index)"
            >
              <div class="overflow-hidden bg-slate-100">
                <img
                  :src="item.src"
                  :alt="item.alt"
                  class="aspect-[9/16] w-full object-cover transition duration-300 group-hover:scale-[1.02]"
                  decoding="async"
                  loading="lazy"
                >
              </div>
              <div class="px-4 py-3">
                <p class="text-sm font-semibold text-body">
                  {{ item.title }}
                </p>
                <p class="mt-1 text-xs leading-5 text-body/55">
                  {{ item.description }}
                </p>
              </div>
            </button>
          </div>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'

const screenshots = [
  {
    title: '首次启用引导',
    description: '集中完成输入法启用、配置入口与权限准备，新用户第一次打开就知道下一步该做什么。',
    alt: 'LuaShengIME 首次启用与设置入口界面',
    src: '/images/微信图片_20260323210223_31_392.jpg',
  },
  {
    title: '聊天输入界面',
    description: '直接展示在聊天场景中的主键盘布局，用户能立刻看到实际输入状态和面板样式。',
    alt: 'LuaShengIME 在聊天应用中的主键盘输入界面',
    src: '/images/微信图片_20260323210225_32_392.jpg',
  },
  {
    title: '短语分组管理',
    description: '支持分组维护、短语新增和导入导出，常用表达可以长期沉淀并快速复用。',
    alt: 'LuaShengIME 短语分组与导入导出界面',
    src: '/images/微信图片_20260323210227_33_392.jpg',
  },
  {
    title: '预测词管理',
    description: '可维护预测词内容并支持与 RIME 词库导入导出，输入习惯能逐步累积。',
    alt: 'LuaShengIME 预测词管理界面',
    src: '/images/微信图片_20260323210228_34_392.jpg',
  },
  {
    title: '功能面板',
    description: '在聊天页中直接调出快捷短语和剪贴板，说明它不仅是键盘，也承担高频操作入口。',
    alt: 'LuaShengIME 聊天场景中的功能面板界面',
    src: '/images/微信图片_20260323210230_35_392.jpg',
  },
  {
    title: '词库与维护设置',
    description: '覆盖主题、同步、重新部署和用户词库导入导出等能力，突出可管理性。',
    alt: 'LuaShengIME 词库维护与设置界面',
    src: '/images/微信图片_20260323210233_37_392.jpg',
  },
] as const

const activeIndex = ref(0)

const activeScreenshot = computed(() => screenshots[activeIndex.value])

const selectScreenshot = (index: number) => {
  activeIndex.value = index
}

const showPrev = () => {
  activeIndex.value = (activeIndex.value - 1 + screenshots.length) % screenshots.length
}

const showNext = () => {
  activeIndex.value = (activeIndex.value + 1) % screenshots.length
}
</script>
