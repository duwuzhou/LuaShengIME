export default defineNuxtRouteMiddleware(async () => {
  if (import.meta.server) {
    return
  }

  try {
    const session = await $fetch<{ authenticated: boolean }>('/api/admin/session', {
      credentials: 'include',
    })

    if (session.authenticated) {
      return navigateTo('/admin')
    }
  } catch {
    return
  }
})
