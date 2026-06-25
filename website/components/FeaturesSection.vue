<template>
  <section id="features" class="bg-surface py-16 md:py-20">
    <div class="mx-auto max-w-5xl px-4 sm:px-6 lg:px-8">
      <h2 v-reveal class="reveal-up text-center text-2xl font-bold text-body sm:text-3xl">
        为什么选择 LuaShengIME
      </h2>
      <p v-reveal="100" class="reveal-up mx-auto mt-3 max-w-xl text-center text-body/60">
        五个核心卖点，覆盖速度、准确度和可管理性。
      </p>

      <div class="mt-12 grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
        <div
          v-for="(feature, index) in features"
          :key="index"
          v-reveal="160 + index * 90"
          class="lift-card reveal-up rounded-2xl border border-primary/10 bg-white p-6 shadow-sm transition hover:border-primary/30 hover:shadow-md"
        >
          <div class="flex h-12 w-12 items-center justify-center rounded-xl bg-primary/10 text-primary">
            <component :is="feature.icon" />
          </div>
          <h3 class="mt-4 text-lg font-semibold text-body">
            {{ feature.title }}
          </h3>
          <p class="mt-2 text-sm leading-relaxed text-body/60">
            {{ feature.description }}
          </p>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { h } from 'vue'

const IconTarget = () => h('svg', {
  xmlns: 'http://www.w3.org/2000/svg', class: 'h-6 w-6', fill: 'none', viewBox: '0 0 24 24', stroke: 'currentColor', 'stroke-width': '2',
}, [
  h('path', { 'stroke-linecap': 'round', 'stroke-linejoin': 'round', d: 'M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z' }),
])

const IconBook = () => h('svg', {
  xmlns: 'http://www.w3.org/2000/svg', class: 'h-6 w-6', fill: 'none', viewBox: '0 0 24 24', stroke: 'currentColor', 'stroke-width': '2',
}, [
  h('path', { 'stroke-linecap': 'round', 'stroke-linejoin': 'round', d: 'M12 6.253v13m0-13C10.832 5.477 9.246 5 7.5 5S4.168 5.477 3 6.253v13C4.168 18.477 5.754 18 7.5 18s3.332.477 4.5 1.253m0-13C13.168 5.477 14.754 5 16.5 5c1.747 0 3.332.477 4.5 1.253v13C19.832 18.477 18.247 18 16.5 18c-1.746 0-3.332.477-4.5 1.253' }),
])

const IconSwitch = () => h('svg', {
  xmlns: 'http://www.w3.org/2000/svg', class: 'h-6 w-6', fill: 'none', viewBox: '0 0 24 24', stroke: 'currentColor', 'stroke-width': '2',
}, [
  h('path', { 'stroke-linecap': 'round', 'stroke-linejoin': 'round', d: 'M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4' }),
])

const IconLightning = () => h('svg', {
  xmlns: 'http://www.w3.org/2000/svg', class: 'h-6 w-6', fill: 'none', viewBox: '0 0 24 24', stroke: 'currentColor', 'stroke-width': '2',
}, [
  h('path', { 'stroke-linecap': 'round', 'stroke-linejoin': 'round', d: 'M13 10V3L4 14h7v7l9-11h-7z' }),
])

const IconRocket = () => h('svg', {
  xmlns: 'http://www.w3.org/2000/svg', class: 'h-6 w-6', fill: 'none', viewBox: '0 0 24 24', stroke: 'currentColor', 'stroke-width': '2',
}, [
  h('path', { 'stroke-linecap': 'round', 'stroke-linejoin': 'round', d: 'M13 7h8m0 0v8m0-8l-8 8-4-4-6 6' }),
])

const features = [
  {
    icon: IconTarget,
    title: '候选更准',
    description: '结合 Rime 与本地统计，输入更贴合你的习惯。',
  },
  {
    icon: IconBook,
    title: '词库可管理',
    description: '支持导入和导出自定义词库，个人词汇随身带。',
  },
  {
    icon: IconSwitch,
    title: '方案可切换',
    description: '拼音和形码方案自由切换，满足不同输入偏好。',
  },
  {
    icon: IconLightning,
    title: '上屏后预测',
    description: '常用词自动联想，句子更快完成，减少重复敲击。',
  },
  {
    icon: IconRocket,
    title: '轻量顺滑',
    description: '启动快、响应快，日常输入不卡顿。',
  },
]
</script>
