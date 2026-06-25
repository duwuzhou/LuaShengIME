import { createError, defineEventHandler, getRouterParam, readBody } from 'h3'
import { patchAnnouncement, readAnnouncements, writeAnnouncements } from '../../../utils/announcements'
import { requireAdminToken } from '../../../utils/admin-auth'

export default defineEventHandler(async (event) => {
  requireAdminToken(event)

  const id = getRouterParam(event, 'id')
  const body = await readBody(event)
  const items = await readAnnouncements()
  const index = items.findIndex((item) => item.id === id)

  if (index === -1) {
    throw createError({
      statusCode: 404,
      statusMessage: 'Announcement not found',
    })
  }

  const nextItem = patchAnnouncement(items[index], body)
  const nextItems = [...items]
  nextItems[index] = nextItem

  await writeAnnouncements(nextItems)

  return {
    item: nextItem,
  }
})
