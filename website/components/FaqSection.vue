<template>
  <section id="faq" class="bg-surface py-16 md:py-20">
    <div class="mx-auto max-w-3xl px-4 sm:px-6 lg:px-8">
      <h2 v-reveal class="reveal-up text-center text-2xl font-bold text-body sm:text-3xl">
        常见问题
      </h2>
      <p v-reveal="100" class="reveal-up mx-auto mt-3 max-w-xl text-center text-body/60">
        快速了解 LuaShengIME 的上手、迁移和续航表现。
      </p>

      <div class="mt-10 space-y-3">
        <div
          v-for="(faq, index) in faqs"
          :key="index"
          v-reveal="160 + index * 80"
          class="reveal-up overflow-hidden rounded-xl border border-primary/10 bg-white shadow-sm"
        >
          <button
            class="flex w-full items-center justify-between px-6 py-4 text-left transition hover:bg-primary/5"
            :aria-expanded="openIndex === index"
            :aria-controls="`faq-answer-${index}`"
            @click="toggle(index)"
          >
            <span class="text-sm font-semibold text-body sm:text-base">{{ faq.question }}</span>
            <svg
              xmlns="http://www.w3.org/2000/svg"
              class="h-5 w-5 shrink-0 text-primary/50 transition-transform duration-200"
              :class="{ 'rotate-180': openIndex === index }"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
              stroke-width="2"
            >
              <path stroke-linecap="round" stroke-linejoin="round" d="M19 9l-7 7-7-7" />
            </svg>
          </button>
          <div
            :id="`faq-answer-${index}`"
            class="grid transition-all duration-200"
            :class="openIndex === index ? 'grid-rows-[1fr]' : 'grid-rows-[0fr]'"
          >
            <div class="overflow-hidden">
              <p class="px-6 pb-4 text-sm leading-relaxed text-body/60">
                {{ faq.answer }}
              </p>
            </div>
          </div>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { ref } from 'vue'

const openIndex = ref<number | null>(null)

function toggle(index: number) {
  openIndex.value = openIndex.value === index ? null : index
}

const faqs = [
  {
    question: '会不会很难上手？',
    answer: '默认设置已适配多数用户，安装后即可直接使用。',
  },
  {
    question: '词库怎么迁移？',
    answer: '支持导入和导出自定义词库，换机也能保留输入习惯。',
  },
  {
    question: '会很耗电吗？',
    answer: '输入法占用很轻，日常使用不影响续航。',
  },
]
</script>
