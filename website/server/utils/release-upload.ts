import { randomUUID } from 'node:crypto'
import { mkdir, writeFile } from 'node:fs/promises'
import { basename, extname, join } from 'node:path'
import { createError } from 'h3'

const ALLOWED_EXTENSIONS = new Set(['.apk', '.xapk', '.apks', '.zip'])
const MAX_UPLOAD_SIZE = 200 * 1024 * 1024

function sanitizeBaseName(fileName: string) {
  const normalized = fileName
    .normalize('NFKD')
    .replace(/[^\w.-]+/g, '-')
    .replace(/-+/g, '-')
    .replace(/^-|-$/g, '')

  return normalized || 'release-package'
}

function getUploadDirectory() {
  return join(process.cwd(), 'public', 'uploads', 'releases')
}

function getUploadBaseUrl() {
  return '/uploads/releases'
}

function ensureAllowedExtension(fileName: string) {
  const extension = extname(fileName).toLowerCase()

  if (!ALLOWED_EXTENSIONS.has(extension)) {
    throw createError({
      statusCode: 400,
      statusMessage: `Only ${Array.from(ALLOWED_EXTENSIONS).join(', ')} files are allowed`,
    })
  }

  return extension
}

export function validateReleaseUpload(fileName: string, fileSize: number) {
  if (!fileName) {
    throw createError({
      statusCode: 400,
      statusMessage: 'Upload file name is required',
    })
  }

  if (!fileSize) {
    throw createError({
      statusCode: 400,
      statusMessage: 'Upload file is empty',
    })
  }

  if (fileSize > MAX_UPLOAD_SIZE) {
    throw createError({
      statusCode: 400,
      statusMessage: `Upload file is too large. Max size is ${Math.floor(MAX_UPLOAD_SIZE / 1024 / 1024)} MB`,
    })
  }

  return ensureAllowedExtension(fileName)
}

export async function saveReleaseUpload(fileName: string, fileData: Buffer) {
  const extension = validateReleaseUpload(fileName, fileData.length)
  const baseName = sanitizeBaseName(basename(fileName, extension))
  const savedFileName = `${Date.now()}-${randomUUID().slice(0, 8)}-${baseName}${extension}`
  const uploadDirectory = getUploadDirectory()

  await mkdir(uploadDirectory, { recursive: true })
  await writeFile(join(uploadDirectory, savedFileName), fileData)

  return {
    fileName: savedFileName,
    originalFileName: fileName,
    size: fileData.length,
    url: `${getUploadBaseUrl()}/${savedFileName}`,
  }
}
