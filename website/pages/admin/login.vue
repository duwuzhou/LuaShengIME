<template>
  <main class="min-h-screen bg-[#f3f4f6] text-slate-900">
    <div class="mx-auto grid min-h-screen max-w-7xl items-stretch gap-8 px-4 py-8 sm:px-6 lg:grid-cols-[1.05fr_0.95fr] lg:px-8 lg:py-10">
      <section class="flex flex-col justify-between rounded-[32px] border border-slate-200 bg-white p-8 shadow-sm lg:p-12">
        <div>
          <p class="text-xs font-semibold uppercase tracking-[0.32em] text-slate-400">LuaShengIME Admin</p>
          <h1 class="mt-6 max-w-xl text-4xl font-semibold tracking-tight text-slate-900 sm:text-5xl">
            独立登录入口
          </h1>
          <p class="mt-6 max-w-2xl text-base leading-8 text-slate-500">
            登录后进入后台工作台，统一管理公告、安卓版本发布、安装包上传和客户端更新下发。
          </p>

          <div class="mt-10 grid gap-4 sm:grid-cols-3">
            <article class="rounded-3xl border border-slate-200 bg-slate-50 p-5">
              <p class="text-sm font-semibold text-slate-900">安全会话</p>
              <p class="mt-2 text-sm leading-6 text-slate-500">使用 HttpOnly Cookie，不把敏感凭证暴露给前端脚本。</p>
            </article>
            <article class="rounded-3xl border border-slate-200 bg-slate-50 p-5">
              <p class="text-sm font-semibold text-slate-900">版本上传</p>
              <p class="mt-2 text-sm leading-6 text-slate-500">支持安装包上传，自动生成发布下载地址并回填版本表单。</p>
            </article>
            <article class="rounded-3xl border border-slate-200 bg-slate-50 p-5">
              <p class="text-sm font-semibold text-slate-900">统一下发</p>
              <p class="mt-2 text-sm leading-6 text-slate-500">公告与版本下发集中到一个后台，减少手工维护成本。</p>
            </article>
          </div>
        </div>

        <div class="mt-10 flex flex-wrap gap-3">
          <NuxtLink
            to="/"
            class="inline-flex min-h-11 items-center justify-center rounded-2xl border border-slate-200 bg-white px-5 py-3 text-sm font-medium text-slate-700 transition hover:border-slate-300 hover:bg-slate-50 focus:outline-none focus:ring-2 focus:ring-slate-300"
          >
            返回官网
          </NuxtLink>
          <NuxtLink
            to="/admin"
            class="inline-flex min-h-11 items-center justify-center rounded-2xl bg-slate-900 px-5 py-3 text-sm font-semibold text-white transition hover:bg-slate-800 focus:outline-none focus:ring-2 focus:ring-slate-300"
          >
            进入工作台
          </NuxtLink>
        </div>
      </section>

      <section class="flex items-center">
        <div class="w-full rounded-[32px] border border-slate-200 bg-white p-8 shadow-sm lg:p-10">
          <div class="flex items-start justify-between gap-4">
            <div>
              <p class="text-sm font-medium text-slate-500">管理员验证</p>
              <h2 class="mt-2 text-2xl font-semibold tracking-tight text-slate-900">登录后台</h2>
            </div>
            <span class="inline-flex min-h-11 items-center rounded-full bg-slate-100 px-3 text-xs font-semibold text-slate-600">
              独立入口
            </span>
          </div>

          <p class="mt-3 text-sm leading-7 text-slate-500">
            当前管理员用户名默认填充为“花落”。登录成功后将跳转到后台管理页。
          </p>

          <form class="mt-8 space-y-4" @submit.prevent="login">
            <div>
              <label for="admin-username" class="mb-2 block text-sm font-medium text-slate-700">用户名</label>
              <input
                id="admin-username"
                v-model.trim="loginForm.username"
                autocomplete="username"
                class="min-h-11 w-full rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 outline-none transition focus:border-slate-900 focus:ring-2 focus:ring-slate-200"
                placeholder="管理员用户名"
              >
            </div>

            <div>
              <label for="admin-password" class="mb-2 block text-sm font-medium text-slate-700">密码</label>
              <input
                id="admin-password"
                v-model="loginForm.password"
                :type="showPassword ? 'text' : 'password'"
                autocomplete="current-password"
                class="min-h-11 w-full rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 outline-none transition focus:border-slate-900 focus:ring-2 focus:ring-slate-200"
                placeholder="输入管理员密码"
              >
            </div>

            <label class="flex min-h-11 items-center gap-3 rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-700">
              <input
                v-model="showPassword"
                type="checkbox"
                class="h-4 w-4 rounded border-slate-300 text-slate-900 focus:ring-slate-300"
              >
              <span>显示密码</span>
            </label>

            <button
              type="submit"
              class="inline-flex min-h-11 w-full items-center justify-center rounded-2xl bg-slate-900 px-5 py-3 text-sm font-semibold text-white transition hover:bg-slate-800 focus:outline-none focus:ring-2 focus:ring-slate-300 disabled:cursor-not-allowed disabled:opacity-60"
              :disabled="isAuthenticating || !loginForm.username || !loginForm.password"
            >
              {{ isAuthenticating ? '正在登录' : '登录后台' }}
            </button>
          </form>

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
      </section>
    </div>
  </main>
</template>

<script setup lang="ts">
definePageMeta({
  middleware: ['admin-guest'],
})

useHead({
  title: '后台登录 | LuaShengIME',
})

type RequestFeedback = {
  kind: 'success' | 'error'
  message: string
}

const route = useRoute()
const showPassword = ref(false)
const isAuthenticating = ref(false)
const requestState = reactive<RequestFeedback>({
  kind: 'success',
  message: '',
})

const loginForm = reactive({
  username: '花落',
  password: '',
})

function setFeedback(kind: RequestFeedback['kind'], message: string) {
  requestState.kind = kind
  requestState.message = message
}

function getErrorMessage(error: unknown) {
  const maybeError = error as {
    data?: { statusMessage?: string; message?: string }
    statusMessage?: string
    message?: string
  }

  return (
    maybeError?.data?.statusMessage ||
    maybeError?.data?.message ||
    maybeError?.statusMessage ||
    maybeError?.message ||
    '登录失败'
  )
}

async function login() {
  if (!loginForm.username || !loginForm.password) {
    setFeedback('error', '请输入用户名和密码')
    return
  }

  isAuthenticating.value = true

  try {
    await $fetch('/api/admin/login', {
      method: 'POST',
      body: {
        username: loginForm.username,
        password: loginForm.password,
      },
      credentials: 'include',
    })

    setFeedback('success', '登录成功，正在进入后台')

    const redirect = typeof route.query.redirect === 'string' && route.query.redirect.startsWith('/admin')
      ? route.query.redirect
      : '/admin'

    await navigateTo(redirect)
  } catch (error) {
    setFeedback('error', getErrorMessage(error))
  } finally {
    isAuthenticating.value = false
  }
}
</script>
