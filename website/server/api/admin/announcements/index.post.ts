import { defineEventHandler, readBody } from 'h3'
import { createAnnouncement, readAnnouncements, writeAnnouncements } from '../../../utils/announcements'
import { requireAdminToken } from '../../../utils/admin-auth'

export default defineEventHandler(async (event) => {
  requireAdminToken(event)

  const body = await readBody(event)
  const current = await readAnnouncements()
  const next = createAnnouncement(body)

  await writeAnnouncements([next, ...current])

  return {
    item: next,
  }
})
