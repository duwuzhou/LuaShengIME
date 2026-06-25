import { createError, defineEventHandler, readMultipartFormData } from 'h3'
import { requireAdminAccess } from '../../../utils/admin-auth'
import { saveReleaseUpload } from '../../../utils/release-upload'

export default defineEventHandler(async (event) => {
  requireAdminAccess(event)

  const parts = await readMultipartFormData(event)
  const file = parts?.find((part) => part.name === 'file' && part.filename && part.data)

  if (!file?.filename || !file.data) {
    throw createError({
      statusCode: 400,
      statusMessage: 'Please upload a release file',
    })
  }

  return await saveReleaseUpload(file.filename, file.data)
})
