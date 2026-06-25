<template>
  <section class="reveal-up mx-auto mb-8 max-w-4xl" style="--reveal-delay: 40ms">
    <div class="overflow-hidden rounded-full border border-primary/15 bg-white/80 shadow-sm backdrop-blur">
      <div class="flex items-center gap-3 border-b border-primary/10 px-4 py-3 sm:hidden">
        <span class="rounded-full bg-primary px-2.5 py-1 text-[11px] font-semibold uppercase tracking-[0.18em] text-white">
          公告
        </span>
        <p class="truncate text-sm text-body/75">
          {{ mobileAnnouncementText }}
        </p>
      </div>

      <div class="hidden items-center gap-4 px-4 py-3 sm:flex">
        <span class="shrink-0 rounded-full bg-primary px-3 py-1 text-xs font-semibold uppercase tracking-[0.18em] text-white">
          最新公告
        </span>

        <div class="announcement-marquee-track min-w-0 flex-1 overflow-hidden">
          <div class="announcement-marquee-content flex min-w-max items-center gap-4 pr-4">
            <template v-for="item in marqueeItems" :key="item.key">
              <span class="rounded-full px-2.5 py-1 text-xs font-semibold" :class="levelClass(item.level)">
                {{ levelLabel(item.level) }}
              </span>
              <span class="text-sm font-medium text-body">
                {{ item.title }}
              </span>
              <span class="max-w-[26rem] truncate text-sm text-body/65">
                {{ item.content }}
              </span>
              <span class="text-xs text-body/40">
                {{ formatDate(item.updatedAt) }}
              </span>
              <span class="h-1.5 w-1.5 rounded-full bg-primary/25" />
            </template>
          </div>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
type AnnouncementLevel = 'info' | 'success' | 'warning' | 'error'

type AnnouncementItem = {
  id: string
  title: string
  content: string
  level: AnnouncementLevel
  updatedAt: string
}

type AnnouncementTickerItem = AnnouncementItem & {
  key: string
}

const fallbackAnnouncements: AnnouncementItem[] = [
  {
    id: 'fallback-notice',
    title: '暂无公告',
    content: '当前还没有发布中的公告，后续版本动态会展示在这里。',
    level: 'info',
    updatedAt: new Date().toISOString(),
  },
]

const { data } = await useAsyncData('homepage-announcements', () => $fetch<{ items: AnnouncementItem[] }>('/api/announcements'))

const displayAnnouncements = computed(() => {
  const items = (data.value?.items ?? []).slice(0, 3)
  return items.length ? items : fallbackAnnouncements
})

const marqueeItems = computed<AnnouncementTickerItem[]>(() => {
  return Array.from({ length: 2 }).flatMap((_, round) => {
    return displayAnnouncements.value.map((item) => ({
      ...item,
      key: `${item.id}-${round}`,
    }))
  })
})

const mobileAnnouncementText = computed(() => {
  return displayAnnouncements.value.map((item) => `${item.title}：${item.content}`).join('  ·  ')
})

function levelLabel(level: AnnouncementLevel) {
  return {
    info: '信息',
    success: '更新',
    warning: '提醒',
    error: '通知',
  }[level]
}

function levelClass(level: AnnouncementLevel) {
  return {
    info: 'bg-sky-50 text-sky-700',
    success: 'bg-emerald-50 text-emerald-700',
    warning: 'bg-amber-50 text-amber-700',
    error: 'bg-rose-50 text-rose-700',
  }[level]
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat('zh-CN', {
    month: 'numeric',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(value))
}
</script>

<style scoped>
.announcement-marquee-track {
  mask-image: linear-gradient(90deg, transparent, black 6%, black 94%, transparent);
}

.announcement-marquee-content {
  animation: announcement-marquee 28s linear infinite;
}

.announcement-marquee-track:hover .announcement-marquee-content {
  animation-play-state: paused;
}

@keyframes announcement-marquee {
  from {
    transform: translateX(0);
  }

  to {
    transform: translateX(-50%);
  }
}

@media (prefers-reduced-motion: reduce) {
  .announcement-marquee-content {
    animation: none;
  }
}
</style>
