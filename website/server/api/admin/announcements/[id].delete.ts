import { createError, defineEventHandler, getRouterParam } from 'h3'
import { readAnnouncements, writeAnnouncements } from '../../../utils/announcements'
import { requireAdminToken } from '../../../utils/admin-auth'

export default defineEventHandler(async (event) => {
  requireAdminToken(event)

  const id = getRouterParam(event, 'id')
  const items = await readAnnouncements()
  const nextItems = items.filter((item) => item.id !== id)

  if (nextItems.length === items.length) {
    throw createError({
      statusCode: 404,
      statusMessage: 'Announcement not found',
    })
  }

  await writeAnnouncements(nextItems)

  return {
    success: true,
  }
})
