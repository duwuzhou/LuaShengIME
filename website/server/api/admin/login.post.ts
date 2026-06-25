import { defineEventHandler } from 'h3'
import { loginAdmin } from '../../utils/admin-auth'

export default defineEventHandler(async (event) => {
  return await loginAdmin(event)
})
