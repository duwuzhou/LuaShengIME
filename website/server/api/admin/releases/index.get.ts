import { defineEventHandler } from 'h3'
import { requireAdminToken } from '../../../utils/admin-auth'
import { readReleases } from '../../../utils/releases'

export default defineEventHandler(async (event) => {
  requireAdminToken(event)

  return {
    items: await readReleases(),
  }
})
