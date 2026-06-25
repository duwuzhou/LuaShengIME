<template>
  <section class="bg-white py-16 md:py-20">
    <div class="mx-auto max-w-4xl px-4 sm:px-6 lg:px-8">
      <h2 v-reveal class="reveal-up text-center text-2xl font-bold text-body sm:text-3xl">
        用户评价
      </h2>
      <p v-reveal="100" class="reveal-up mx-auto mt-3 max-w-xl text-center text-body/60">
        来自真实使用场景的口碑反馈。
      </p>

      <div class="mt-10 grid gap-6 sm:grid-cols-3">
        <div
          v-for="(testimonial, index) in testimonials"
          :key="index"
          v-reveal="160 + index * 90"
          class="lift-card reveal-up rounded-2xl border border-gray-100 bg-surface p-6 shadow-sm"
        >
          <svg xmlns="http://www.w3.org/2000/svg" class="h-8 w-8 text-primary/20" fill="currentColor" viewBox="0 0 24 24">
            <path d="M14.017 21v-7.391c0-5.704 3.731-9.57 8.983-10.609l.995 2.151c-2.432.917-3.995 3.638-3.995 5.849h4v10H14.017zM0 21v-7.391c0-5.704 3.731-9.57 8.983-10.609L9.978 5.151c-2.432.917-3.995 3.638-3.995 5.849H10v10H0z" />
          </svg>

          <p class="mt-3 text-sm leading-relaxed text-body/80">
            {{ testimonial.content }}
          </p>

          <div class="mt-4 flex gap-0.5">
            <svg
              v-for="star in 5"
              :key="star"
              xmlns="http://www.w3.org/2000/svg"
              class="h-4 w-4 text-cta"
              fill="currentColor"
              viewBox="0 0 24 24"
            >
              <path d="M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z" />
            </svg>
          </div>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
const testimonials = [
  { content: '“候选词很聪明，越用越顺手。”' },
  { content: '“词库导入很方便，换机不麻烦。”' },
  { content: '“输入体验稳定，长文也不卡。”' },
]
</script>
