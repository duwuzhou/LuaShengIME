import { createError, defineEventHandler, getRouterParam, readBody } from 'h3'
import { requireAdminToken } from '../../../utils/admin-auth'
import { patchRelease, readReleases, writeReleases } from '../../../utils/releases'

export default defineEventHandler(async (event) => {
  requireAdminToken(event)

  const id = getRouterParam(event, 'id')
  const body = await readBody(event)
  const items = await readReleases()
  const index = items.findIndex((item) => item.id === id)

  if (index === -1) {
    throw createError({
      statusCode: 404,
      statusMessage: 'Release not found',
    })
  }

  const nextItem = patchRelease(items[index], body)
  const nextItems = [...items]
  nextItems[index] = nextItem

  await writeReleases(nextItems)

  return {
    item: nextItem,
  }
})
