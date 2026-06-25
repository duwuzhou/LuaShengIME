import { createError, defineEventHandler, getRouterParam } from 'h3'
import { requireAdminToken } from '../../../utils/admin-auth'
import { readReleases, writeReleases } from '../../../utils/releases'

export default defineEventHandler(async (event) => {
  requireAdminToken(event)

  const id = getRouterParam(event, 'id')
  const items = await readReleases()
  const nextItems = items.filter((item) => item.id !== id)

  if (nextItems.length === items.length) {
    throw createError({
      statusCode: 404,
      statusMessage: 'Release not found',
    })
  }

  await writeReleases(nextItems)

  return {
    success: true,
  }
})
