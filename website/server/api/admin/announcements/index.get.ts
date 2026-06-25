import { defineEventHandler } from 'h3'
import { readAnnouncements } from '../../../utils/announcements'
import { requireAdminToken } from '../../../utils/admin-auth'

export default defineEventHandler(async (event) => {
  requireAdminToken(event)

  return {
    items: await readAnnouncements(),
  }
})
