import { defineEventHandler, readBody } from 'h3'
import { requireAdminToken } from '../../../utils/admin-auth'
import { createRelease, readReleases, writeReleases } from '../../../utils/releases'

export default defineEventHandler(async (event) => {
  requireAdminToken(event)

  const body = await readBody(event)
  const current = await readReleases()
  const next = createRelease(body)

  await writeReleases([next, ...current])

  return {
    item: next,
  }
})
