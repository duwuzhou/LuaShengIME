<template>
  <main class="min-h-screen bg-[#f3f4f6] text-slate-900">
    <div class="mx-auto flex min-h-screen max-w-[1600px] flex-col lg:flex-row">
      <aside class="border-b border-slate-200 bg-white lg:sticky lg:top-0 lg:h-screen lg:w-72 lg:border-b-0 lg:border-r">
        <div class="flex h-full flex-col px-5 py-6 sm:px-6">
          <div class="flex items-start justify-between gap-4">
            <div>
              <p class="text-xs font-semibold uppercase tracking-[0.28em] text-slate-400">
                LuaShengIME
              </p>
              <h1 class="mt-3 text-2xl font-semibold tracking-tight text-slate-900">
                后台管理
              </h1>
              <p class="mt-2 text-sm leading-6 text-slate-500">
                白色控制台界面，集中管理公告、版本发布和更新下发。
              </p>
            </div>
            <span class="inline-flex min-h-11 items-center rounded-full bg-emerald-50 px-3 text-xs font-semibold text-emerald-700">
              已登录
            </span>
          </div>

          <nav class="mt-8 space-y-2" aria-label="后台导航">
            <button
              v-for="item in navItems"
              :key="item.id"
              type="button"
              class="flex min-h-11 w-full items-start justify-between rounded-2xl border px-4 py-3 text-left transition focus:outline-none focus:ring-2 focus:ring-slate-300"
              :class="activeSection === item.id
                ? 'border-slate-900 bg-slate-900 text-white shadow-sm'
                : 'border-slate-200 bg-white text-slate-700 hover:border-slate-300 hover:bg-slate-50'"
              @click="activeSection = item.id"
            >
              <span>
                <span class="block text-sm font-semibold">{{ item.label }}</span>
                <span
                  class="mt-1 block text-xs"
                  :class="activeSection === item.id ? 'text-slate-300' : 'text-slate-400'"
                >
                  {{ item.description }}
                </span>
              </span>
              <span
                class="rounded-full px-2.5 py-1 text-xs font-semibold"
                :class="activeSection === item.id ? 'bg-white/10 text-white' : 'bg-slate-100 text-slate-500'"
              >
                {{ item.badge() }}
              </span>
            </button>
          </nav>

          <div class="mt-8 rounded-3xl border border-slate-200 bg-slate-50 p-4">
            <p class="text-sm font-semibold text-slate-900">当前会话</p>
            <p class="mt-2 text-sm leading-6 text-slate-500">
              已使用账号 {{ session.username }} 登录，接口自动附带 Cookie 会话。
            </p>

            <div class="mt-4 flex flex-wrap gap-3">
              <button
                type="button"
                class="inline-flex min-h-11 items-center justify-center rounded-2xl bg-slate-900 px-4 py-3 text-sm font-semibold text-white transition hover:bg-slate-800 focus:outline-none focus:ring-2 focus:ring-slate-300 disabled:cursor-not-allowed disabled:opacity-60"
                :disabled="isRefreshing"
                @click="loadAll"
              >
                {{ isRefreshing ? '刷新中' : '刷新数据' }}
              </button>
              <button
                type="button"
                class="inline-flex min-h-11 items-center justify-center rounded-2xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm font-medium text-rose-700 transition hover:border-rose-300 hover:bg-rose-100 focus:outline-none focus:ring-2 focus:ring-rose-200 disabled:cursor-not-allowed disabled:opacity-60"
                :disabled="isLoggingOut"
                @click="logout"
              >
                {{ isLoggingOut ? '退出中' : '退出登录' }}
              </button>
            </div>
          </div>

          <div class="mt-6 rounded-3xl border border-slate-200 bg-white p-4">
            <p class="text-sm font-semibold text-slate-900">服务端配置</p>
            <p class="mt-2 text-sm leading-6 text-slate-500">
              管理账号、密码与会话密钥已迁移到 `.env`。登录入口已独立到 `/admin/login`。
            </p>
          </div>

          <div class="mt-auto hidden pt-6 lg:block">
            <div class="flex flex-wrap gap-3">
              <NuxtLink
                to="/admin/login"
                class="inline-flex min-h-11 items-center justify-center rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm font-medium text-slate-700 transition hover:border-slate-300 hover:bg-slate-50 focus:outline-none focus:ring-2 focus:ring-slate-300"
              >
                打开登录页
              </NuxtLink>
              <NuxtLink
                to="/"
                class="inline-flex min-h-11 items-center justify-center rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm font-medium text-slate-700 transition hover:border-slate-300 hover:bg-slate-50 focus:outline-none focus:ring-2 focus:ring-slate-300"
              >
                返回官网
              </NuxtLink>
            </div>
          </div>
        </div>
      </aside>

      <div class="min-w-0 flex-1">
        <header class="border-b border-slate-200 bg-white/95 backdrop-blur">
          <div class="px-4 py-5 sm:px-6 lg:px-8">
            <div class="flex flex-col gap-4 xl:flex-row xl:items-center xl:justify-between">
              <div>
                <p class="text-sm font-medium text-slate-500">管理工作台</p>
                <h2 class="mt-2 text-3xl font-semibold tracking-tight text-slate-900">
                  {{ sectionTitle }}
                </h2>
                <p class="mt-2 text-sm leading-6 text-slate-500">
                  {{ sectionDescription }}
                </p>
              </div>

              <div class="grid gap-3 sm:grid-cols-3 xl:w-[34rem]">
                <article class="rounded-3xl border border-slate-200 bg-slate-50 px-4 py-4">
                  <p class="text-sm text-slate-500">公告总数</p>
                  <p class="mt-2 text-2xl font-semibold text-slate-900">{{ announcements.length }}</p>
                  <p class="mt-1 text-xs text-slate-400">有效 {{ visibleAnnouncementsCount }} 条</p>
                </article>
                <article class="rounded-3xl border border-slate-200 bg-slate-50 px-4 py-4">
                  <p class="text-sm text-slate-500">版本记录</p>
                  <p class="mt-2 text-2xl font-semibold text-slate-900">{{ releases.length }}</p>
                  <p class="mt-1 text-xs text-slate-400">已发布 {{ publishedReleasesCount }} 个</p>
                </article>
                <article class="rounded-3xl border border-slate-200 bg-slate-50 px-4 py-4">
                  <p class="text-sm text-slate-500">最新稳定版</p>
                  <p class="mt-2 text-xl font-semibold text-slate-900">{{ latestStableRelease?.versionName ?? '未设置' }}</p>
                  <p class="mt-1 text-xs text-slate-400">
                    {{ latestStableRelease ? `versionCode ${latestStableRelease.versionCode}` : '等待发布 stable 版本' }}
                  </p>
                </article>
              </div>
            </div>

            <div class="mt-5 flex gap-3 overflow-x-auto pb-1 lg:hidden" aria-label="移动端导航">
              <button
                v-for="item in navItems"
                :key="`mobile-${item.id}`"
                type="button"
                class="inline-flex min-h-11 shrink-0 items-center justify-center rounded-full border px-4 py-2 text-sm font-semibold transition focus:outline-none focus:ring-2 focus:ring-slate-300"
                :class="activeSection === item.id
                  ? 'border-slate-900 bg-slate-900 text-white'
                  : 'border-slate-200 bg-white text-slate-700 hover:border-slate-300 hover:bg-slate-50'"
                @click="activeSection = item.id"
              >
                {{ item.label }}
              </button>
            </div>

            <div
              v-if="requestState.message"
              class="mt-5 flex min-h-11 items-center rounded-2xl border px-4 py-3 text-sm"
              :class="requestState.kind === 'error'
                ? 'border-rose-200 bg-rose-50 text-rose-700'
                : 'border-emerald-200 bg-emerald-50 text-emerald-700'"
            >
              {{ requestState.message }}
            </div>
          </div>
        </header>

        <section class="px-4 py-6 sm:px-6 lg:px-8 lg:py-8">
          <section v-if="activeSection === 'overview'" class="grid gap-6 xl:grid-cols-[0.95fr_1.05fr]">
            <article class="rounded-[28px] border border-slate-200 bg-white p-6 shadow-sm">
              <div class="flex items-start justify-between gap-4">
                <div>
                  <h3 class="text-xl font-semibold text-slate-900">会话状态</h3>
                  <p class="mt-2 text-sm leading-6 text-slate-500">
                    当前页面仅保留工作台能力，登录入口已独立到 `/admin/login`。
                  </p>
                </div>
                <span class="inline-flex min-h-11 items-center rounded-full bg-emerald-50 px-3 text-xs font-semibold text-emerald-700">
                  已登录
                </span>
              </div>

              <div class="mt-6 rounded-3xl border border-slate-200 bg-slate-50 p-5">
                <p class="text-sm font-semibold text-slate-900">当前管理员</p>
                <p class="mt-2 text-sm leading-6 text-slate-500">
                  已使用 <span class="font-semibold text-slate-900">{{ session.username }}</span> 登录。版本发布、公告增删改查和上传接口都会自动带上当前会话。
                </p>
              </div>

              <div class="mt-4 rounded-3xl border border-slate-200 bg-slate-50 p-5">
                <p class="text-sm font-semibold text-slate-900">登录入口</p>
                <p class="mt-2 text-sm leading-6 text-slate-500">
                  如果需要重新验证身份、切换账号或会话失效，请前往独立登录页重新进入后台。
                </p>
                <NuxtLink
                  to="/admin/login"
                  class="mt-4 inline-flex min-h-11 items-center justify-center rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm font-medium text-slate-700 transition hover:border-slate-300 hover:bg-slate-100 focus:outline-none focus:ring-2 focus:ring-slate-300"
                >
                  打开登录页
                </NuxtLink>
              </div>
            </article>

            <article class="rounded-[28px] border border-slate-200 bg-white p-6 shadow-sm">
              <h3 class="text-xl font-semibold text-slate-900">运行状态</h3>
              <p class="mt-2 text-sm leading-6 text-slate-500">
                这里展示最后刷新时间、当前版本通道和管理建议，便于发布前复核。
              </p>

              <div class="mt-6 grid gap-4 sm:grid-cols-2">
                <div class="rounded-3xl border border-slate-200 bg-slate-50 p-4">
                  <p class="text-sm text-slate-500">最后刷新时间</p>
                  <p class="mt-2 text-base font-semibold text-slate-900">{{ formatDisplayDate(lastLoadedAt) }}</p>
                </div>
                <div class="rounded-3xl border border-slate-200 bg-slate-50 p-4">
                  <p class="text-sm text-slate-500">当前鉴权</p>
                  <p class="mt-2 text-base font-semibold text-slate-900">HttpOnly Cookie Session</p>
                </div>
                <div class="rounded-3xl border border-slate-200 bg-slate-50 p-4">
                  <p class="text-sm text-slate-500">默认下载地址</p>
                  <p class="mt-2 break-all text-sm font-medium text-slate-900">{{ defaultDownloadUrl }}</p>
                </div>
                <div class="rounded-3xl border border-slate-200 bg-slate-50 p-4">
                  <p class="text-sm text-slate-500">稳定版下发</p>
                  <p class="mt-2 text-base font-semibold text-slate-900">
                    {{ latestStableRelease ? `${latestStableRelease.versionName} / ${latestStableRelease.versionCode}` : '尚未配置' }}
                  </p>
                </div>
              </div>

              <div class="mt-6 rounded-3xl border border-slate-200 bg-slate-50 p-4">
                <p class="text-sm font-semibold text-slate-900">管理建议</p>
                <ul class="mt-3 space-y-2 text-sm leading-6 text-slate-500">
                  <li>公告用于首页即时提示，版本发布用于客户端更新检测与下载下发。</li>
                  <li>上传安装包后会自动回填下载地址，避免手工复制路径出错。</li>
                  <li>稳定版请维护 `versionCode` 递增，否则客户端不会识别为更新。</li>
                </ul>
              </div>
            </article>
          </section>

          <section
            v-else-if="activeSection === 'announcements'"
            class="grid gap-6 xl:grid-cols-[minmax(0,440px)_minmax(0,1fr)]"
          >
            <form class="rounded-[28px] border border-slate-200 bg-white p-6 shadow-sm" @submit.prevent="submitAnnouncement">
              <div class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                <div>
                  <h3 class="text-xl font-semibold text-slate-900">
                    {{ announcementForm.id ? '编辑公告' : '新增公告' }}
                  </h3>
                  <p class="mt-2 text-sm leading-6 text-slate-500">
                    支持标题、正文、级别、排序和时间窗口。
                  </p>
                </div>
                <button
                  type="button"
                  class="inline-flex min-h-11 items-center justify-center rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm font-medium text-slate-700 transition hover:border-slate-300 hover:bg-slate-50 focus:outline-none focus:ring-2 focus:ring-slate-300"
                  @click="resetAnnouncementForm"
                >
                  清空表单
                </button>
              </div>

              <div class="mt-6 space-y-4">
                <div>
                  <label for="announcement-title" class="mb-2 block text-sm font-medium text-slate-700">公告标题</label>
                  <input
                    id="announcement-title"
                    v-model.trim="announcementForm.title"
                    maxlength="80"
                    class="min-h-11 w-full rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 outline-none transition focus:border-slate-900 focus:ring-2 focus:ring-slate-200"
                    placeholder="例如：1.0.2 版本开放下载"
                  >
                </div>

                <div>
                  <label for="announcement-content" class="mb-2 block text-sm font-medium text-slate-700">公告内容</label>
                  <textarea
                    id="announcement-content"
                    v-model.trim="announcementForm.content"
                    rows="6"
                    class="w-full rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm leading-6 text-slate-900 outline-none transition focus:border-slate-900 focus:ring-2 focus:ring-slate-200"
                    placeholder="填写用户可见说明"
                  />
                </div>

                <div class="grid gap-4 sm:grid-cols-2">
                  <div>
                    <label for="announcement-level" class="mb-2 block text-sm font-medium text-slate-700">级别</label>
                    <select
                      id="announcement-level"
                      v-model="announcementForm.level"
                      class="min-h-11 w-full rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 outline-none transition focus:border-slate-900 focus:ring-2 focus:ring-slate-200"
                    >
                      <option value="info">信息</option>
                      <option value="success">成功</option>
                      <option value="warning">警告</option>
                      <option value="error">错误</option>
                    </select>
                  </div>

                  <div>
                    <label for="announcement-sort" class="mb-2 block text-sm font-medium text-slate-700">排序权重</label>
                    <input
                      id="announcement-sort"
                      v-model.number="announcementForm.sortOrder"
                      type="number"
                      class="min-h-11 w-full rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 outline-none transition focus:border-slate-900 focus:ring-2 focus:ring-slate-200"
                      placeholder="越大越靠前"
                    >
                  </div>
                </div>

                <div class="grid gap-4 sm:grid-cols-2">
                  <div>
                    <label for="announcement-start" class="mb-2 block text-sm font-medium text-slate-700">开始时间</label>
                    <input
                      id="announcement-start"
                      v-model="announcementForm.startsAt"
                      type="datetime-local"
                      class="min-h-11 w-full rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 outline-none transition focus:border-slate-900 focus:ring-2 focus:ring-slate-200"
                    >
                  </div>
                  <div>
                    <label for="announcement-end" class="mb-2 block text-sm font-medium text-slate-700">结束时间</label>
                    <input
                      id="announcement-end"
                      v-model="announcementForm.endsAt"
                      type="datetime-local"
                      class="min-h-11 w-full rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 outline-none transition focus:border-slate-900 focus:ring-2 focus:ring-slate-200"
                    >
                  </div>
                </div>

                <label class="flex min-h-11 items-center gap-3 rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-700">
                  <input
                    v-model="announcementForm.active"
                    type="checkbox"
                    class="h-4 w-4 rounded border-slate-300 text-slate-900 focus:ring-slate-300"
                  >
                  <span>立即生效</span>
                </label>
              </div>

              <div class="mt-6 flex flex-wrap gap-3">
                <button
                  type="submit"
                  class="inline-flex min-h-11 items-center justify-center rounded-2xl bg-slate-900 px-5 py-3 text-sm font-semibold text-white transition hover:bg-slate-800 focus:outline-none focus:ring-2 focus:ring-slate-300 disabled:cursor-not-allowed disabled:opacity-60"
                  :disabled="announcementSubmitting"
                >
                  {{ announcementSubmitting ? '提交中' : announcementForm.id ? '更新公告' : '发布公告' }}
                </button>
                <button
                  v-if="announcementForm.id"
                  type="button"
                  class="inline-flex min-h-11 items-center justify-center rounded-2xl border border-slate-200 bg-white px-5 py-3 text-sm font-medium text-slate-700 transition hover:border-slate-300 hover:bg-slate-50 focus:outline-none focus:ring-2 focus:ring-slate-300"
                  @click="resetAnnouncementForm"
                >
                  取消编辑
                </button>
              </div>
            </form>

            <section class="rounded-[28px] border border-slate-200 bg-white p-6 shadow-sm">
              <div class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                <div>
                  <h3 class="text-xl font-semibold text-slate-900">公告列表</h3>
                  <p class="mt-2 text-sm leading-6 text-slate-500">
                    默认按排序权重和更新时间倒序展示。
                  </p>
                </div>
                <button
                  type="button"
                  class="inline-flex min-h-11 items-center justify-center rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm font-medium text-slate-700 transition hover:border-slate-300 hover:bg-slate-50 focus:outline-none focus:ring-2 focus:ring-slate-300 disabled:cursor-not-allowed disabled:opacity-60"
                  :disabled="announcementsLoading"
                  @click="refreshAnnouncements"
                >
                  {{ announcementsLoading ? '加载中' : '刷新公告' }}
                </button>
              </div>

              <div v-if="!announcements.length" class="mt-6 rounded-3xl border border-dashed border-slate-200 bg-slate-50 px-5 py-10 text-center text-sm text-slate-500">
                暂无公告数据。
              </div>

              <div v-else class="mt-6 space-y-4">
                <article
                  v-for="item in announcements"
                  :key="item.id"
                  class="rounded-3xl border border-slate-200 bg-slate-50 p-5"
                >
                  <div class="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
                    <div class="min-w-0">
                      <div class="flex flex-wrap items-center gap-2">
                        <h4 class="text-lg font-semibold text-slate-900">{{ item.title }}</h4>
                        <span class="rounded-full px-2.5 py-1 text-xs font-semibold" :class="announcementLevelClass(item.level)">
                          {{ announcementLevelLabel(item.level) }}
                        </span>
                        <span
                          class="rounded-full px-2.5 py-1 text-xs font-semibold"
                          :class="item.active ? 'bg-emerald-50 text-emerald-700' : 'bg-slate-200 text-slate-700'"
                        >
                          {{ item.active ? '生效中' : '已停用' }}
                        </span>
                      </div>

                      <p class="mt-3 whitespace-pre-line text-sm leading-7 text-slate-600">{{ item.content }}</p>

                      <div class="mt-4 grid gap-2 text-xs text-slate-400 sm:grid-cols-2">
                        <div>排序权重：{{ item.sortOrder }}</div>
                        <div>更新时间：{{ formatDisplayDate(item.updatedAt) }}</div>
                        <div>开始时间：{{ formatDisplayDate(item.startsAt) }}</div>
                        <div>结束时间：{{ formatDisplayDate(item.endsAt) }}</div>
                      </div>
                    </div>

                    <div class="flex flex-wrap gap-2 lg:w-48 lg:justify-end">
                      <button
                        type="button"
                        class="inline-flex min-h-11 items-center justify-center rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm font-medium text-slate-700 transition hover:border-slate-300 hover:bg-slate-100 focus:outline-none focus:ring-2 focus:ring-slate-300"
                        @click="editAnnouncement(item)"
                      >
                        编辑
                      </button>
                      <button
                        type="button"
                        class="inline-flex min-h-11 items-center justify-center rounded-2xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm font-medium text-rose-700 transition hover:border-rose-300 hover:bg-rose-100 focus:outline-none focus:ring-2 focus:ring-rose-200 disabled:cursor-not-allowed disabled:opacity-60"
                        :disabled="deletingAnnouncementId === item.id"
                        @click="deleteAnnouncement(item)"
                      >
                        {{ deletingAnnouncementId === item.id ? '删除中' : '删除' }}
                      </button>
                    </div>
                  </div>
                </article>
              </div>
            </section>
          </section>

          <section v-else class="grid gap-6 xl:grid-cols-[minmax(0,460px)_minmax(0,1fr)]">
            <form class="rounded-[28px] border border-slate-200 bg-white p-6 shadow-sm" @submit.prevent="submitRelease">
              <div class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                <div>
                  <h3 class="text-xl font-semibold text-slate-900">
                    {{ releaseForm.id ? '编辑版本' : '发布新版本' }}
                  </h3>
                  <p class="mt-2 text-sm leading-6 text-slate-500">
                    配置更新通道、安装包地址、版本号与下发策略。
                  </p>
                </div>
                <button
                  type="button"
                  class="inline-flex min-h-11 items-center justify-center rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm font-medium text-slate-700 transition hover:border-slate-300 hover:bg-slate-50 focus:outline-none focus:ring-2 focus:ring-slate-300"
                  @click="resetReleaseForm"
                >
                  清空表单
                </button>
              </div>

              <div class="mt-6 space-y-4">
                <div class="grid gap-4 sm:grid-cols-2">
                  <div>
                    <label for="release-platform" class="mb-2 block text-sm font-medium text-slate-700">平台</label>
                    <select
                      id="release-platform"
                      v-model="releaseForm.platform"
                      class="min-h-11 w-full rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 outline-none transition focus:border-slate-900 focus:ring-2 focus:ring-slate-200"
                    >
                      <option value="android">Android</option>
                    </select>
                  </div>
                  <div>
                    <label for="release-channel" class="mb-2 block text-sm font-medium text-slate-700">发布通道</label>
                    <select
                      id="release-channel"
                      v-model="releaseForm.channel"
                      class="min-h-11 w-full rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 outline-none transition focus:border-slate-900 focus:ring-2 focus:ring-slate-200"
                    >
                      <option value="stable">Stable</option>
                      <option value="beta">Beta</option>
                      <option value="alpha">Alpha</option>
                    </select>
                  </div>
                </div>

                <div class="grid gap-4 sm:grid-cols-2">
                  <div>
                    <label for="release-version-name" class="mb-2 block text-sm font-medium text-slate-700">版本名称</label>
                    <input
                      id="release-version-name"
                      v-model.trim="releaseForm.versionName"
                      class="min-h-11 w-full rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 outline-none transition focus:border-slate-900 focus:ring-2 focus:ring-slate-200"
                      placeholder="例如：1.0.2"
                    >
                  </div>
                  <div>
                    <label for="release-version-code" class="mb-2 block text-sm font-medium text-slate-700">版本号</label>
                    <input
                      id="release-version-code"
                      v-model="releaseForm.versionCode"
                      type="number"
                      min="1"
                      class="min-h-11 w-full rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 outline-none transition focus:border-slate-900 focus:ring-2 focus:ring-slate-200"
                      placeholder="例如：102"
                    >
                  </div>
                </div>

                <div>
                  <label for="release-changelog" class="mb-2 block text-sm font-medium text-slate-700">更新日志</label>
                  <textarea
                    id="release-changelog"
                    v-model.trim="releaseForm.changelogText"
                    rows="6"
                    class="w-full rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm leading-6 text-slate-900 outline-none transition focus:border-slate-900 focus:ring-2 focus:ring-slate-200"
                    placeholder="每行一条更新内容"
                  />
                </div>

                <div class="rounded-3xl border border-slate-200 bg-slate-50 p-4">
                  <div class="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
                    <div>
                      <p class="text-sm font-semibold text-slate-900">安装包上传</p>
                      <p class="mt-1 text-sm leading-6 text-slate-500">
                        支持 `.apk`、`.xapk`、`.apks`、`.zip`，上传成功后自动回填下载地址。
                      </p>
                    </div>
                    <span class="rounded-full bg-white px-3 py-1 text-xs font-semibold text-slate-500">
                      公共目录：`/uploads/releases`
                    </span>
                  </div>

                  <div class="mt-4 grid gap-3 md:grid-cols-[minmax(0,1fr)_auto]">
                    <input
                      :key="releaseFileInputKey"
                      type="file"
                      accept=".apk,.xapk,.apks,.zip"
                      class="min-h-11 w-full rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-700 file:mr-4 file:rounded-xl file:border-0 file:bg-slate-900 file:px-4 file:py-2 file:text-sm file:font-semibold file:text-white hover:file:bg-slate-800"
                      @change="handleReleaseFileChange"
                    >
                    <button
                      type="button"
                      class="inline-flex min-h-11 items-center justify-center rounded-2xl bg-slate-900 px-5 py-3 text-sm font-semibold text-white transition hover:bg-slate-800 focus:outline-none focus:ring-2 focus:ring-slate-300 disabled:cursor-not-allowed disabled:opacity-60"
                      :disabled="releaseUploading || !selectedReleaseFile"
                      @click="uploadReleaseFile"
                    >
                      {{ releaseUploading ? '上传中' : '上传安装包' }}
                    </button>
                  </div>

                  <div class="mt-3 flex flex-wrap items-center gap-3 text-sm">
                    <span class="rounded-full bg-white px-3 py-1 text-slate-500">
                      {{ selectedReleaseFile?.name ?? '未选择文件' }}
                    </span>
                    <button
                      v-if="selectedReleaseFile"
                      type="button"
                      class="text-sm font-medium text-slate-600 transition hover:text-slate-900"
                      @click="clearReleaseFileSelection"
                    >
                      清除选择
                    </button>
                  </div>
                </div>

                <div>
                  <label for="release-apk-url" class="mb-2 block text-sm font-medium text-slate-700">下载地址</label>
                  <input
                    id="release-apk-url"
                    v-model.trim="releaseForm.apkUrl"
                    class="min-h-11 w-full rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 outline-none transition focus:border-slate-900 focus:ring-2 focus:ring-slate-200"
                    :placeholder="defaultDownloadUrl"
                  >
                  <p class="mt-2 text-xs leading-6 text-slate-400">
                    留空时客户端会回落到默认下载地址：{{ defaultDownloadUrl }}
                  </p>
                </div>

                <div class="grid gap-4 sm:grid-cols-2">
                  <div>
                    <label for="release-min-supported" class="mb-2 block text-sm font-medium text-slate-700">最低支持版本号</label>
                    <input
                      id="release-min-supported"
                      v-model="releaseForm.minSupportedVersionCode"
                      type="number"
                      min="1"
                      class="min-h-11 w-full rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 outline-none transition focus:border-slate-900 focus:ring-2 focus:ring-slate-200"
                      placeholder="留空表示不限"
                    >
                  </div>
                  <div>
                    <label for="release-published-at" class="mb-2 block text-sm font-medium text-slate-700">发布时间</label>
                    <input
                      id="release-published-at"
                      v-model="releaseForm.publishedAt"
                      type="datetime-local"
                      class="min-h-11 w-full rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 outline-none transition focus:border-slate-900 focus:ring-2 focus:ring-slate-200"
                    >
                  </div>
                </div>

                <div class="grid gap-3 sm:grid-cols-2">
                  <label class="flex min-h-11 items-center gap-3 rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-700">
                    <input
                      v-model="releaseForm.published"
                      type="checkbox"
                      class="h-4 w-4 rounded border-slate-300 text-slate-900 focus:ring-slate-300"
                    >
                    <span>立即发布</span>
                  </label>
                  <label class="flex min-h-11 items-center gap-3 rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-700">
                    <input
                      v-model="releaseForm.forceUpdate"
                      type="checkbox"
                      class="h-4 w-4 rounded border-slate-300 text-slate-900 focus:ring-slate-300"
                    >
                    <span>强制更新</span>
                  </label>
                </div>
              </div>

              <div class="mt-6 flex flex-wrap gap-3">
                <button
                  type="submit"
                  class="inline-flex min-h-11 items-center justify-center rounded-2xl bg-slate-900 px-5 py-3 text-sm font-semibold text-white transition hover:bg-slate-800 focus:outline-none focus:ring-2 focus:ring-slate-300 disabled:cursor-not-allowed disabled:opacity-60"
                  :disabled="releaseSubmitting"
                >
                  {{ releaseSubmitting ? '提交中' : releaseForm.id ? '更新版本' : '发布版本' }}
                </button>
                <button
                  v-if="releaseForm.id"
                  type="button"
                  class="inline-flex min-h-11 items-center justify-center rounded-2xl border border-slate-200 bg-white px-5 py-3 text-sm font-medium text-slate-700 transition hover:border-slate-300 hover:bg-slate-50 focus:outline-none focus:ring-2 focus:ring-slate-300"
                  @click="resetReleaseForm"
                >
                  取消编辑
                </button>
              </div>
            </form>

            <section class="rounded-[28px] border border-slate-200 bg-white p-6 shadow-sm">
              <div class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                <div>
                  <h3 class="text-xl font-semibold text-slate-900">版本列表</h3>
                  <p class="mt-2 text-sm leading-6 text-slate-500">
                    接口通过 `GET /api/version/latest?platform=android&channel=stable&currentVersionCode=100` 执行更新检测。
                  </p>
                </div>
                <button
                  type="button"
                  class="inline-flex min-h-11 items-center justify-center rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm font-medium text-slate-700 transition hover:border-slate-300 hover:bg-slate-50 focus:outline-none focus:ring-2 focus:ring-slate-300 disabled:cursor-not-allowed disabled:opacity-60"
                  :disabled="releasesLoading"
                  @click="refreshReleases"
                >
                  {{ releasesLoading ? '加载中' : '刷新版本' }}
                </button>
              </div>

              <div v-if="!releases.length" class="mt-6 rounded-3xl border border-dashed border-slate-200 bg-slate-50 px-5 py-10 text-center text-sm text-slate-500">
                暂无版本数据。
              </div>

              <div v-else class="mt-6 space-y-4">
                <article
                  v-for="item in releases"
                  :key="item.id"
                  class="rounded-3xl border border-slate-200 bg-slate-50 p-5"
                >
                  <div class="flex flex-col gap-4 xl:flex-row xl:items-start xl:justify-between">
                    <div class="min-w-0">
                      <div class="flex flex-wrap items-center gap-2">
                        <h4 class="text-lg font-semibold text-slate-900">{{ item.versionName }}</h4>
                        <span class="rounded-full px-2.5 py-1 text-xs font-semibold" :class="releaseChannelClass(item.channel)">
                          {{ item.channel.toUpperCase() }}
                        </span>
                        <span
                          class="rounded-full px-2.5 py-1 text-xs font-semibold"
                          :class="item.published ? 'bg-emerald-50 text-emerald-700' : 'bg-slate-200 text-slate-700'"
                        >
                          {{ item.published ? '已发布' : '草稿' }}
                        </span>
                        <span
                          v-if="item.forceUpdate"
                          class="rounded-full bg-rose-50 px-2.5 py-1 text-xs font-semibold text-rose-700"
                        >
                          强制更新
                        </span>
                      </div>

                      <div class="mt-4 grid gap-2 text-sm leading-6 text-slate-500 sm:grid-cols-2">
                        <div>平台：{{ item.platform }}</div>
                        <div>versionCode：{{ item.versionCode }}</div>
                        <div>发布时间：{{ formatDisplayDate(item.publishedAt) }}</div>
                        <div>最低支持：{{ item.minSupportedVersionCode ?? '不限' }}</div>
                      </div>

                      <div class="mt-4">
                        <p class="text-sm font-medium text-slate-900">更新日志</p>
                        <ul class="mt-2 space-y-1 text-sm leading-6 text-slate-500">
                          <li v-for="(log, index) in item.changelog" :key="`${item.id}-${index}`">
                            {{ log }}
                          </li>
                          <li v-if="!item.changelog.length" class="text-slate-400">暂无更新日志</li>
                        </ul>
                      </div>

                      <div class="mt-4 text-sm leading-6 text-slate-500">
                        下载地址：
                        <a
                          :href="resolveReleaseDownloadUrl(item.apkUrl)"
                          target="_blank"
                          rel="noreferrer"
                          class="break-all font-medium text-slate-900 underline decoration-slate-300 underline-offset-4 transition hover:text-slate-700"
                        >
                          {{ resolveReleaseDownloadUrl(item.apkUrl) }}
                        </a>
                      </div>
                    </div>

                    <div class="flex flex-wrap gap-2 xl:w-48 xl:justify-end">
                      <button
                        type="button"
                        class="inline-flex min-h-11 items-center justify-center rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm font-medium text-slate-700 transition hover:border-slate-300 hover:bg-slate-100 focus:outline-none focus:ring-2 focus:ring-slate-300"
                        @click="editRelease(item)"
                      >
                        编辑
                      </button>
                      <button
                        type="button"
                        class="inline-flex min-h-11 items-center justify-center rounded-2xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm font-medium text-rose-700 transition hover:border-rose-300 hover:bg-rose-100 focus:outline-none focus:ring-2 focus:ring-rose-200 disabled:cursor-not-allowed disabled:opacity-60"
                        :disabled="deletingReleaseId === item.id"
                        @click="deleteRelease(item)"
                      >
                        {{ deletingReleaseId === item.id ? '删除中' : '删除' }}
                      </button>
                    </div>
                  </div>
                </article>
              </div>
            </section>
          </section>
        </section>
      </div>
    </div>
  </main>
</template>

<script setup lang="ts">
definePageMeta({
  middleware: ['admin-auth'],
})

import { computed, onMounted, reactive, ref } from 'vue'

type RequestFeedback = {
  kind: 'success' | 'error'
  message: string
}

type SessionState = {
  authenticated: boolean
  username: string
}

type AnnouncementLevel = 'info' | 'success' | 'warning' | 'error'
type ReleaseChannel = 'stable' | 'beta' | 'alpha'
type AdminSection = 'overview' | 'announcements' | 'releases'

type AnnouncementItem = {
  id: string
  title: string
  content: string
  level: AnnouncementLevel
  active: boolean
  sortOrder: number
  startsAt: string | null
  endsAt: string | null
  createdAt: string
  updatedAt: string
}

type ReleaseItem = {
  id: string
  platform: 'android'
  channel: ReleaseChannel
  versionName: string
  versionCode: number
  changelog: string[]
  apkUrl: string | null
  forceUpdate: boolean
  minSupportedVersionCode: number | null
  published: boolean
  publishedAt: string
  createdAt: string
  updatedAt: string
}

useHead({
  title: '后台管理 | LuaShengIME',
})

const runtimeConfig = useRuntimeConfig()
const defaultDownloadUrl = runtimeConfig.public.downloadUrl || '/downloads/LuaShengIME.apk'

const activeSection = ref<AdminSection>('overview')
const session = reactive<SessionState>({
  authenticated: false,
  username: '',
})

const announcements = ref<AnnouncementItem[]>([])
const releases = ref<ReleaseItem[]>([])
const selectedReleaseFile = ref<File | null>(null)
const releaseFileInputKey = ref(0)

const announcementsLoading = ref(false)
const releasesLoading = ref(false)
const announcementSubmitting = ref(false)
const releaseSubmitting = ref(false)
const releaseUploading = ref(false)
const deletingAnnouncementId = ref('')
const deletingReleaseId = ref('')
const isRefreshing = ref(false)
const isLoggingOut = ref(false)

const requestState = reactive<RequestFeedback>({
  kind: 'success',
  message: '',
})

const lastLoadedAt = ref('')

const announcementForm = reactive({
  id: '',
  title: '',
  content: '',
  level: 'info' as AnnouncementLevel,
  active: true,
  sortOrder: 0,
  startsAt: '',
  endsAt: '',
})

const releaseForm = reactive({
  id: '',
  platform: 'android' as 'android',
  channel: 'stable' as ReleaseChannel,
  versionName: '',
  versionCode: '',
  changelogText: '',
  apkUrl: '',
  forceUpdate: false,
  minSupportedVersionCode: '',
  published: true,
  publishedAt: toDateTimeLocalInput(new Date().toISOString()),
})

const visibleAnnouncementsCount = computed(() => announcements.value.filter((item) => isAnnouncementVisible(item)).length)
const publishedReleasesCount = computed(() => releases.value.filter((item) => item.published).length)
const latestStableRelease = computed(() => releases.value.find((item) => item.channel === 'stable' && item.published) ?? null)

const navItems = computed(() => [
  {
    id: 'overview' as AdminSection,
    label: '概览',
    description: '会话状态与运行信息',
    badge: () => '在线',
  },
  {
    id: 'announcements' as AdminSection,
    label: '公告管理',
    description: '首页通知与生效时间',
    badge: () => String(announcements.value.length),
  },
  {
    id: 'releases' as AdminSection,
    label: '版本发布',
    description: '更新检测与安装包下发',
    badge: () => String(releases.value.length),
  },
])
const sectionTitle = computed(() => {
  return {
    overview: '总览',
    announcements: '公告管理',
    releases: '版本发布',
  }[activeSection.value]
})

const sectionDescription = computed(() => {
  return {
    overview: '查看会话状态、刷新信息和当前默认下载配置。',
    announcements: '维护首页公告文案、有效期和展示优先级。',
    releases: '管理安卓版本、上传安装包并配置更新下发策略。',
  }[activeSection.value]
})

function setFeedback(kind: RequestFeedback['kind'], message: string) {
  requestState.kind = kind
  requestState.message = message
}

function clearFeedback() {
  requestState.message = ''
}

function clearDashboardState() {
  announcements.value = []
  releases.value = []
  lastLoadedAt.value = ''
  resetAnnouncementForm()
  resetReleaseForm()
}

function setAuthenticatedSession(username: string | null) {
  session.authenticated = Boolean(username)
  session.username = username ?? ''
}

function getErrorMessage(error: unknown) {
  const maybeError = error as {
    data?: { statusMessage?: string; message?: string }
    statusCode?: number
    statusMessage?: string
    message?: string
  }

  if (maybeError?.statusCode === 401) {
    setAuthenticatedSession(null)
    clearDashboardState()
    void navigateTo('/admin/login')
  }

  return (
    maybeError?.data?.statusMessage ||
    maybeError?.data?.message ||
    maybeError?.statusMessage ||
    maybeError?.message ||
    '请求失败'
  )
}

async function adminRequest<T>(path: string, options: Record<string, unknown> = {}) {
  return await $fetch<T>(path, {
    ...options,
    credentials: 'include',
  })
}

function toDateTimeLocalInput(value: string | null) {
  if (!value) {
    return ''
  }

  const date = new Date(value)
  const localDate = new Date(date.getTime() - date.getTimezoneOffset() * 60000)
  return localDate.toISOString().slice(0, 16)
}

function toIsoDateTime(value: string) {
  return value ? new Date(value).toISOString() : null
}

function formatDisplayDate(value?: string | null) {
  if (!value) {
    return '未设置'
  }

  const date = new Date(value)

  if (Number.isNaN(date.getTime())) {
    return '未设置'
  }

  return new Intl.DateTimeFormat('zh-CN', {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(date)
}

function isAnnouncementVisible(item: AnnouncementItem) {
  if (!item.active) {
    return false
  }

  const now = Date.now()

  if (item.startsAt && new Date(item.startsAt).getTime() > now) {
    return false
  }

  if (item.endsAt && new Date(item.endsAt).getTime() < now) {
    return false
  }

  return true
}

function announcementLevelLabel(level: AnnouncementLevel) {
  return {
    info: '信息',
    success: '成功',
    warning: '警告',
    error: '错误',
  }[level]
}

function announcementLevelClass(level: AnnouncementLevel) {
  return {
    info: 'bg-sky-50 text-sky-700',
    success: 'bg-emerald-50 text-emerald-700',
    warning: 'bg-amber-50 text-amber-700',
    error: 'bg-rose-50 text-rose-700',
  }[level]
}

function releaseChannelClass(channel: ReleaseChannel) {
  return {
    stable: 'bg-emerald-50 text-emerald-700',
    beta: 'bg-amber-50 text-amber-700',
    alpha: 'bg-sky-50 text-sky-700',
  }[channel]
}

function resolveReleaseDownloadUrl(apkUrl: string | null) {
  return apkUrl || defaultDownloadUrl
}

function clearReleaseFileSelection() {
  selectedReleaseFile.value = null
  releaseFileInputKey.value += 1
}

function resetAnnouncementForm() {
  announcementForm.id = ''
  announcementForm.title = ''
  announcementForm.content = ''
  announcementForm.level = 'info'
  announcementForm.active = true
  announcementForm.sortOrder = 0
  announcementForm.startsAt = ''
  announcementForm.endsAt = ''
}

function editAnnouncement(item: AnnouncementItem) {
  announcementForm.id = item.id
  announcementForm.title = item.title
  announcementForm.content = item.content
  announcementForm.level = item.level
  announcementForm.active = item.active
  announcementForm.sortOrder = item.sortOrder
  announcementForm.startsAt = toDateTimeLocalInput(item.startsAt)
  announcementForm.endsAt = toDateTimeLocalInput(item.endsAt)
  activeSection.value = 'announcements'
  clearFeedback()
}

function resetReleaseForm() {
  releaseForm.id = ''
  releaseForm.platform = 'android'
  releaseForm.channel = 'stable'
  releaseForm.versionName = ''
  releaseForm.versionCode = ''
  releaseForm.changelogText = ''
  releaseForm.apkUrl = ''
  releaseForm.forceUpdate = false
  releaseForm.minSupportedVersionCode = ''
  releaseForm.published = true
  releaseForm.publishedAt = toDateTimeLocalInput(new Date().toISOString())
  clearReleaseFileSelection()
}

function editRelease(item: ReleaseItem) {
  releaseForm.id = item.id
  releaseForm.platform = item.platform
  releaseForm.channel = item.channel
  releaseForm.versionName = item.versionName
  releaseForm.versionCode = String(item.versionCode)
  releaseForm.changelogText = item.changelog.join('\n')
  releaseForm.apkUrl = item.apkUrl ?? ''
  releaseForm.forceUpdate = item.forceUpdate
  releaseForm.minSupportedVersionCode = item.minSupportedVersionCode === null ? '' : String(item.minSupportedVersionCode)
  releaseForm.published = item.published
  releaseForm.publishedAt = toDateTimeLocalInput(item.publishedAt)
  clearReleaseFileSelection()
  activeSection.value = 'releases'
  clearFeedback()
}

function handleReleaseFileChange(event: Event) {
  const input = event.target as HTMLInputElement | null
  selectedReleaseFile.value = input?.files?.[0] ?? null
}

async function uploadReleaseFile() {
  if (!session.authenticated) {
    setFeedback('error', '请先登录后台')
    return
  }

  if (!selectedReleaseFile.value) {
    setFeedback('error', '请先选择要上传的安装包')
    return
  }

  releaseUploading.value = true

  try {
    const formData = new FormData()
    formData.append('file', selectedReleaseFile.value)

    const response = await adminRequest<{ url: string; fileName: string; originalFileName: string }>(
      '/api/admin/releases/upload',
      {
        method: 'POST',
        body: formData,
      },
    )

    releaseForm.apkUrl = response.url
    clearReleaseFileSelection()
    setFeedback('success', `文件已上传：${response.fileName}`)
  } catch (error) {
    setFeedback('error', getErrorMessage(error))
  } finally {
    releaseUploading.value = false
  }
}

async function loadSession() {
  try {
    const response = await adminRequest<{ authenticated: boolean; username: string | null }>('/api/admin/session')
    setAuthenticatedSession(response.authenticated ? response.username : null)
  } catch {
    setAuthenticatedSession(null)
  }
}

async function logout() {
  isLoggingOut.value = true

  try {
    await adminRequest('/api/admin/logout', {
      method: 'POST',
    })
    setAuthenticatedSession(null)
    clearDashboardState()
    setFeedback('success', '已退出登录')
    await navigateTo('/admin/login')
  } catch (error) {
    setFeedback('error', getErrorMessage(error))
  } finally {
    isLoggingOut.value = false
  }
}

async function loadAnnouncements() {
  announcementsLoading.value = true

  try {
    const response = await adminRequest<{ items: AnnouncementItem[] }>('/api/admin/announcements')
    announcements.value = response.items
    lastLoadedAt.value = new Date().toISOString()
  } finally {
    announcementsLoading.value = false
  }
}

async function refreshAnnouncements() {
  try {
    await loadAnnouncements()
    setFeedback('success', '公告已刷新')
  } catch (error) {
    setFeedback('error', getErrorMessage(error))
  }
}

async function loadReleases() {
  releasesLoading.value = true

  try {
    const response = await adminRequest<{ items: ReleaseItem[] }>('/api/admin/releases')
    releases.value = response.items
    lastLoadedAt.value = new Date().toISOString()
  } finally {
    releasesLoading.value = false
  }
}

async function refreshReleases() {
  try {
    await loadReleases()
    setFeedback('success', '版本列表已刷新')
  } catch (error) {
    setFeedback('error', getErrorMessage(error))
  }
}

async function loadAll() {
  if (!session.authenticated) {
    setFeedback('error', '请先登录后台')
    return
  }

  isRefreshing.value = true

  try {
    await Promise.all([loadAnnouncements(), loadReleases()])
    setFeedback('success', '数据刷新完成')
  } catch (error) {
    setFeedback('error', getErrorMessage(error))
  } finally {
    isRefreshing.value = false
  }
}

async function submitAnnouncement() {
  if (!announcementForm.title || !announcementForm.content) {
    setFeedback('error', '请先填写公告标题和内容')
    return
  }

  announcementSubmitting.value = true

  try {
    const payload = {
      title: announcementForm.title,
      content: announcementForm.content,
      level: announcementForm.level,
      active: announcementForm.active,
      sortOrder: announcementForm.sortOrder,
      startsAt: toIsoDateTime(announcementForm.startsAt),
      endsAt: toIsoDateTime(announcementForm.endsAt),
    }

    if (announcementForm.id) {
      await adminRequest(`/api/admin/announcements/${announcementForm.id}`, {
        method: 'PATCH',
        body: payload,
      })
      setFeedback('success', '公告已更新')
    } else {
      await adminRequest('/api/admin/announcements', {
        method: 'POST',
        body: payload,
      })
      setFeedback('success', '公告已发布')
    }

    resetAnnouncementForm()
    await loadAnnouncements()
  } catch (error) {
    setFeedback('error', getErrorMessage(error))
  } finally {
    announcementSubmitting.value = false
  }
}

async function deleteAnnouncement(item: AnnouncementItem) {
  if (!window.confirm(`确认删除公告「${item.title}」？`)) {
    return
  }

  deletingAnnouncementId.value = item.id

  try {
    await adminRequest(`/api/admin/announcements/${item.id}`, {
      method: 'DELETE',
    })
    setFeedback('success', '公告已删除')
    if (announcementForm.id === item.id) {
      resetAnnouncementForm()
    }
    await loadAnnouncements()
  } catch (error) {
    setFeedback('error', getErrorMessage(error))
  } finally {
    deletingAnnouncementId.value = ''
  }
}

async function submitRelease() {
  if (!releaseForm.versionName || !releaseForm.versionCode) {
    setFeedback('error', '请先填写版本名称和版本号')
    return
  }

  const versionCode = Number.parseInt(releaseForm.versionCode, 10)

  if (Number.isNaN(versionCode) || versionCode <= 0) {
    setFeedback('error', '版本号必须是大于 0 的整数')
    return
  }

  const minSupportedVersionCode = releaseForm.minSupportedVersionCode
    ? Number.parseInt(releaseForm.minSupportedVersionCode, 10)
    : null

  if (releaseForm.minSupportedVersionCode && (Number.isNaN(minSupportedVersionCode) || (minSupportedVersionCode ?? 0) <= 0)) {
    setFeedback('error', '最低支持版本号必须是大于 0 的整数')
    return
  }

  releaseSubmitting.value = true

  try {
    const payload = {
      platform: releaseForm.platform,
      channel: releaseForm.channel,
      versionName: releaseForm.versionName,
      versionCode,
      changelog: releaseForm.changelogText.split('\n').map((item) => item.trim()).filter(Boolean),
      apkUrl: releaseForm.apkUrl || null,
      forceUpdate: releaseForm.forceUpdate,
      minSupportedVersionCode,
      published: releaseForm.published,
      publishedAt: toIsoDateTime(releaseForm.publishedAt) ?? new Date().toISOString(),
    }

    if (releaseForm.id) {
      await adminRequest(`/api/admin/releases/${releaseForm.id}`, {
        method: 'PATCH',
        body: payload,
      })
      setFeedback('success', '版本已更新')
    } else {
      await adminRequest('/api/admin/releases', {
        method: 'POST',
        body: payload,
      })
      setFeedback('success', '版本已发布')
    }

    resetReleaseForm()
    await loadReleases()
  } catch (error) {
    setFeedback('error', getErrorMessage(error))
  } finally {
    releaseSubmitting.value = false
  }
}

async function deleteRelease(item: ReleaseItem) {
  if (!window.confirm(`确认删除版本 ${item.versionName} / ${item.versionCode}？`)) {
    return
  }

  deletingReleaseId.value = item.id

  try {
    await adminRequest(`/api/admin/releases/${item.id}`, {
      method: 'DELETE',
    })
    setFeedback('success', '版本已删除')
    if (releaseForm.id === item.id) {
      resetReleaseForm()
    }
    await loadReleases()
  } catch (error) {
    setFeedback('error', getErrorMessage(error))
  } finally {
    deletingReleaseId.value = ''
  }
}

onMounted(async () => {
  await loadSession()

  if (session.authenticated) {
    await loadAll()
  }
})
</script>
