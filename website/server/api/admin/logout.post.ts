import { defineEventHandler } from 'h3'
import { clearAdminSession } from '../../utils/admin-auth'

export default defineEventHandler((event) => {
  clearAdminSession(event)

  return {
    success: true,
  }
})
