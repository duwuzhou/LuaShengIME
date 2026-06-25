import { defineEventHandler } from 'h3'
import { readAdminSession } from '../../utils/admin-auth'

export default defineEventHandler((event) => {
  const session = readAdminSession(event)

  return {
    authenticated: Boolean(session),
    username: session?.username ?? null,
  }
})
