import { mkdir, readFile, writeFile } from 'node:fs/promises'
import { dirname, isAbsolute, join } from 'node:path'
import { createError } from 'h3'

function resolveFilePath(filePath: string) {
  return isAbsolute(filePath) ? filePath : join(process.cwd(), filePath)
}

function stripUtf8Bom(value: string) {
  return value.charCodeAt(0) === 0xfeff ? value.slice(1) : value
}

export async function readJsonFile<T>(filePath: string, fallback: T): Promise<T> {
  const resolvedPath = resolveFilePath(filePath)

  try {
    const content = await readFile(resolvedPath, 'utf8')
    return JSON.parse(stripUtf8Bom(content)) as T
  } catch (error) {
    const nodeError = error as NodeJS.ErrnoException

    if (nodeError.code === 'ENOENT') {
      await writeJsonFile(filePath, fallback)
      return fallback
    }

    throw createError({
      statusCode: 500,
      statusMessage: `Failed to read data file: ${filePath}`,
    })
  }
}

export async function writeJsonFile<T>(filePath: string, data: T) {
  const resolvedPath = resolveFilePath(filePath)

  await mkdir(dirname(resolvedPath), { recursive: true })
  await writeFile(resolvedPath, `${JSON.stringify(data, null, 2)}\n`, 'utf8')
}
