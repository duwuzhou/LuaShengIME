<template>
  <section
    id="story"
    ref="sectionRef"
    class="story-shell py-20 text-white md:py-28"
    :style="{ '--story-progress': progress.toFixed(3) }"
  >
    <div class="relative mx-auto max-w-6xl px-4 sm:px-6 lg:px-8">
      <div class="mx-auto max-w-3xl text-center lg:mx-0 lg:text-left">
        <p v-reveal class="reveal-up text-xs font-semibold uppercase tracking-[0.35em] text-white/[0.55]">
          Scroll Story
        </p>
        <h2 v-reveal="100" class="reveal-up mt-4 text-3xl font-bold tracking-tight sm:text-4xl">
          输入过程不是一步完成，而是逐步贴近你的习惯
        </h2>
        <p v-reveal="180" class="reveal-up mt-4 max-w-2xl text-base leading-7 text-white/70 sm:text-lg">
          从开始敲字，到候选收敛，再到词库接管常用表达，LuaShengIME 会把输入这件事变得越来越顺手。
        </p>
      </div>

      <div class="mt-14 grid items-start gap-12 lg:grid-cols-[0.92fr_1.08fr] lg:gap-16">
        <div class="lg:min-h-[70vh]">
          <div
            v-parallax="{ tilt: 7, shift: 10, scale: 1.01 }"
            class="story-device rounded-[2rem] p-5 shadow-[0_28px_80px_rgba(0,0,0,0.22)]"
          >
            <div class="flex flex-wrap gap-2">
              <span
                v-for="(step, index) in steps"
                :key="step.kicker"
                class="story-chip rounded-full border px-3 py-1 text-[11px] font-medium tracking-[0.22em] text-white/[0.72]"
                :class="stepChipClass(index)"
              >
                {{ step.kicker }}
              </span>
            </div>

            <div class="mt-6 rounded-[1.6rem] border border-white/10 bg-[#062f2e]/90 p-5 shadow-inner shadow-black/10">
              <div class="flex items-center justify-between text-[11px] uppercase tracking-[0.28em] text-white/[0.45]">
                <span>Live Preview</span>
                <span>{{ currentStep.metric }}</span>
              </div>

              <div class="mt-4 h-2 overflow-hidden rounded-full bg-white/10">
                <div class="story-meter-fill h-full rounded-full bg-gradient-to-r from-teal-300 via-cyan-200 to-orange-300 transition-all duration-300" />
              </div>

              <div class="mt-6 rounded-[1.4rem] border border-white/10 bg-white/[0.06] p-4">
                <div class="flex items-center gap-2 text-xs text-white/[0.55]">
                  <span class="h-2 w-2 rounded-full bg-emerald-300" />
                  <span>{{ currentStep.label }}</span>
                </div>

                <div class="mt-4 rounded-2xl bg-[#082b2a] p-4">
                  <div class="flex justify-between text-[11px] text-white/40">
                    <span>Rime Core</span>
                    <span>{{ currentStep.kicker }}</span>
                  </div>

                  <div class="mt-4 flex flex-wrap gap-2">
                    <span
                      v-for="chip in currentStep.chips"
                      :key="chip"
                      class="story-preview-panel rounded-full border border-white/10 px-3 py-1.5 text-sm text-white/[0.82]"
                      :class="{ 'is-active': chip === currentStep.chips[0] }"
                    >
                      {{ chip }}
                    </span>
                  </div>

                  <div class="mt-5 grid gap-3 rounded-2xl bg-white/[0.06] p-3">
                    <div class="story-preview-panel rounded-2xl border border-white/[0.08] p-3" :class="{ 'is-active': activeStep === 0 }">
                      <div class="text-[11px] uppercase tracking-[0.24em] text-white/[0.42]">Input</div>
                      <div class="mt-2 text-lg font-semibold tracking-[0.2em] text-white/90">luashengime</div>
                    </div>

                    <div class="story-preview-panel rounded-2xl border border-white/[0.08] p-3" :class="{ 'is-active': activeStep === 1 }">
                      <div class="text-[11px] uppercase tracking-[0.24em] text-white/[0.42]">Candidate</div>
                      <div class="mt-2 text-base text-white/[0.85]">{{ currentStep.description }}</div>
                    </div>

                    <div class="story-preview-panel rounded-2xl border border-white/[0.08] p-3" :class="{ 'is-active': activeStep === 2 }">
                      <div class="text-[11px] uppercase tracking-[0.24em] text-white/[0.42]">Lexicon</div>
                      <div class="mt-2 text-base text-white/[0.85]">常用表达被记住，下次更快上屏。</div>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div class="relative">
          <div class="story-progress-rail hidden lg:block" />

          <article
            v-for="(step, index) in steps"
            :key="step.kicker"
            class="flex min-h-[52vh] items-center lg:min-h-[68vh]"
          >
            <div
              v-reveal="{ delay: 120 + index * 80, once: false }"
              class="story-step max-w-xl"
              :class="{ 'is-active': activeStep === index }"
            >
              <p class="text-xs font-semibold uppercase tracking-[0.35em] text-white/[0.45]">
                {{ step.kicker }}
              </p>
              <h3 class="mt-4 text-2xl font-semibold tracking-tight text-white sm:text-3xl">
                {{ step.title }}
              </h3>
              <p class="mt-4 text-base leading-7 text-white/[0.72] sm:text-lg">
                {{ step.description }}
              </p>

              <div class="mt-6 grid gap-3">
                <div
                  v-for="bullet in step.bullets"
                  :key="bullet"
                  class="rounded-2xl border border-white/10 bg-white/5 px-4 py-3 text-sm leading-6 text-white/70 backdrop-blur-sm"
                >
                  {{ bullet }}
                </div>
              </div>
            </div>
          </article>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'

type StoryStep = {
  kicker: string
  title: string
  description: string
  label: string
  metric: string
  chips: string[]
  bullets: string[]
}

const steps: StoryStep[] = [
  {
    kicker: 'Step 01',
    title: '从你开始输入的那一刻，就进入更顺手的节奏',
    description: '默认布局直接可用，不需要额外学习成本，输入动作从第一秒开始就是熟悉的。',
    label: '开始输入，立刻响应',
    metric: 'Ready',
    chips: ['快速响应', '熟悉布局', '立即可用'],
    bullets: [
      '默认配置适配多数用户，装好即可开始输入。',
      '键盘布局清晰，减少首次切换时的陌生感。',
    ],
  },
  {
    kicker: 'Step 02',
    title: '候选逐步收敛，让你更少看屏、更少回改',
    description: '候选排序会越来越贴近常用表达，连续输入时更容易直接命中目标词。',
    label: '候选越来越贴合习惯',
    metric: 'Focus',
    chips: ['候选更准', '连贯输入', '减少回改'],
    bullets: [
      '常用候选更靠前，长句输入节奏更稳定。',
      '连续输入时的联想更自然，不需要频繁中断。',
    ],
  },
  {
    kicker: 'Step 03',
    title: '词库和常用表达沉淀下来，形成你自己的输入流',
    description: '导入、管理和迁移词库后，常用表达会逐步固化成你的长期输入资产。',
    label: '词库开始接管重复工作',
    metric: 'Saved',
    chips: ['词库可管', '换机可迁移', '表达可沉淀'],
    bullets: [
      '自定义词库可导入导出，输入习惯跟着设备一起走。',
      '高频短语反复复用，越用越像为自己定制。',
    ],
  },
]

const sectionRef = ref<HTMLElement | null>(null)
const progress = ref(0)
const activeStep = ref(0)

const currentStep = computed(() => steps[activeStep.value] ?? steps[0])

function stepChipClass(index: number) {
  if (index === activeStep.value) {
    return 'is-active border-white/10 bg-white/[0.08]'
  }

  if (index < activeStep.value) {
    return 'border-white/10 bg-white/5 text-white/[0.54]'
  }

  return 'border-white/[0.08] bg-transparent text-white/[0.42]'
}

let ticking = false
let cleanup: (() => void) | null = null

function updateStoryState() {
  const el = sectionRef.value
  if (!el) {
    return
  }

  const rect = el.getBoundingClientRect()
  const viewportHeight = window.innerHeight
  const start = viewportHeight * 0.72
  const travel = Math.max(rect.height - viewportHeight * 0.38, 1)
  const nextProgress = Math.min(1, Math.max(0, (start - rect.top) / travel))

  progress.value = nextProgress
  activeStep.value = Math.min(steps.length - 1, Math.floor(nextProgress * steps.length))
}

function requestUpdate() {
  if (ticking) {
    return
  }

  ticking = true
  window.requestAnimationFrame(() => {
    updateStoryState()
    ticking = false
  })
}

onMounted(() => {
  updateStoryState()

  const onScroll = () => requestUpdate()
  const onResize = () => requestUpdate()

  window.addEventListener('scroll', onScroll, { passive: true })
  window.addEventListener('resize', onResize)

  cleanup = () => {
    window.removeEventListener('scroll', onScroll)
    window.removeEventListener('resize', onResize)
  }
})

onBeforeUnmount(() => {
  cleanup?.()
})
</script>
