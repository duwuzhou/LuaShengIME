export default defineNuxtRouteMiddleware(async (to) => {
  if (import.meta.server) {
    return
  }

  try {
    const session = await $fetch<{ authenticated: boolean }>('/api/admin/session', {
      credentials: 'include',
    })

    if (session.authenticated) {
      return
    }
  } catch {
    // Fall through to login redirect.
  }

  return navigateTo(`/admin/login?redirect=${encodeURIComponent(to.fullPath)}`)
})
