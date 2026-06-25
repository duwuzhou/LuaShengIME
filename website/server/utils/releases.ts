import { randomUUID } from 'node:crypto'
import { createError } from 'h3'
import { readJsonFile, writeJsonFile } from './content-store'

export type ReleasePlatform = 'android'
export type ReleaseChannel = 'stable' | 'beta' | 'alpha'

export type ReleaseRecord = {
  id: string
  platform: ReleasePlatform
  channel: ReleaseChannel
  versionName: string
  versionCode: number
  changelog: string[]
  apkUrl: string | null
  forceUpdate: boolean
  minSupportedVersionCode: number | null
  published: boolean
  publishedAt: string
  createdAt: string
  updatedAt: string
}

type ReleaseCollection = {
  items: ReleaseRecord[]
}

const DEFAULT_DATA: ReleaseCollection = {
  items: [],
}

const VALID_PLATFORMS: ReleasePlatform[] = ['android']
const VALID_CHANNELS: ReleaseChannel[] = ['stable', 'beta', 'alpha']

function getDataFile() {
  return useRuntimeConfig().releaseDataFile
}

function ensureObject(value: unknown, name = 'body') {
  if (!value || typeof value !== 'object' || Array.isArray(value)) {
    throw createError({
      statusCode: 400,
      statusMessage: `${name} must be an object`,
    })
  }

  return value as Record<string, unknown>
}

function ensureString(value: unknown, field: string) {
  if (typeof value !== 'string' || !value.trim()) {
    throw createError({
      statusCode: 400,
      statusMessage: `${field} must be a non-empty string`,
    })
  }

  return value.trim()
}

function parsePlatform(value: unknown, fallback: ReleasePlatform) {
  if (value === undefined) {
    return fallback
  }

  if (typeof value !== 'string' || !VALID_PLATFORMS.includes(value as ReleasePlatform)) {
    throw createError({
      statusCode: 400,
      statusMessage: `platform must be one of: ${VALID_PLATFORMS.join(', ')}`,
    })
  }

  return value as ReleasePlatform
}

function parseChannel(value: unknown, fallback: ReleaseChannel) {
  if (value === undefined) {
    return fallback
  }

  if (typeof value !== 'string' || !VALID_CHANNELS.includes(value as ReleaseChannel)) {
    throw createError({
      statusCode: 400,
      statusMessage: `channel must be one of: ${VALID_CHANNELS.join(', ')}`,
    })
  }

  return value as ReleaseChannel
}

function parseInteger(value: unknown, fallback: number | null, field: string) {
  if (value === undefined) {
    return fallback
  }

  if (value === null) {
    return null
  }

  if (typeof value !== 'number' || !Number.isInteger(value)) {
    throw createError({
      statusCode: 400,
      statusMessage: `${field} must be an integer`,
    })
  }

  return value
}

function parseBoolean(value: unknown, fallback: boolean, field: string) {
  if (value === undefined) {
    return fallback
  }

  if (typeof value !== 'boolean') {
    throw createError({
      statusCode: 400,
      statusMessage: `${field} must be a boolean`,
    })
  }

  return value
}

function parseDateTime(value: unknown, field: string, fallback: string) {
  if (value === undefined) {
    return fallback
  }

  if (typeof value !== 'string' || Number.isNaN(Date.parse(value))) {
    throw createError({
      statusCode: 400,
      statusMessage: `${field} must be a valid ISO date string`,
    })
  }

  return new Date(value).toISOString()
}

function parseNullableUrl(value: unknown, field: string, fallback: string | null) {
  if (value === undefined) {
    return fallback
  }

  if (value === null || value === '') {
    return null
  }

  if (typeof value !== 'string') {
    throw createError({
      statusCode: 400,
      statusMessage: `${field} must be a string or null`,
    })
  }

  return value.trim()
}

function parseChangelog(value: unknown, fallback: string[]) {
  if (value === undefined) {
    return fallback
  }

  if (!Array.isArray(value) || value.some((item) => typeof item !== 'string' || !item.trim())) {
    throw createError({
      statusCode: 400,
      statusMessage: 'changelog must be an array of non-empty strings',
    })
  }

  return value.map((item) => item.trim())
}

function sortReleases(items: ReleaseRecord[]) {
  return [...items].sort((left, right) => {
    if (right.versionCode !== left.versionCode) {
      return right.versionCode - left.versionCode
    }

    return new Date(right.publishedAt).getTime() - new Date(left.publishedAt).getTime()
  })
}

export async function readReleases() {
  const data = await readJsonFile<ReleaseCollection>(getDataFile(), DEFAULT_DATA)
  return sortReleases(data.items)
}

export async function writeReleases(items: ReleaseRecord[]) {
  await writeJsonFile<ReleaseCollection>(getDataFile(), {
    items: sortReleases(items),
  })
}

export function createRelease(input: unknown) {
  const body = ensureObject(input)
  const now = new Date().toISOString()

  return {
    id: randomUUID(),
    platform: parsePlatform(body.platform, 'android'),
    channel: parseChannel(body.channel, 'stable'),
    versionName: ensureString(body.versionName, 'versionName'),
    versionCode: parseInteger(body.versionCode, null, 'versionCode') ?? 0,
    changelog: parseChangelog(body.changelog, []),
    apkUrl: parseNullableUrl(body.apkUrl, 'apkUrl', null),
    forceUpdate: parseBoolean(body.forceUpdate, false, 'forceUpdate'),
    minSupportedVersionCode: parseInteger(body.minSupportedVersionCode, null, 'minSupportedVersionCode'),
    published: parseBoolean(body.published, true, 'published'),
    publishedAt: parseDateTime(body.publishedAt, 'publishedAt', now),
    createdAt: now,
    updatedAt: now,
  } satisfies ReleaseRecord
}

export function patchRelease(current: ReleaseRecord, input: unknown) {
  const body = ensureObject(input)

  return {
    ...current,
    platform: parsePlatform(body.platform, current.platform),
    channel: parseChannel(body.channel, current.channel),
    versionName: body.versionName === undefined ? current.versionName : ensureString(body.versionName, 'versionName'),
    versionCode: parseInteger(body.versionCode, current.versionCode, 'versionCode') ?? current.versionCode,
    changelog: parseChangelog(body.changelog, current.changelog),
    apkUrl: parseNullableUrl(body.apkUrl, 'apkUrl', current.apkUrl),
    forceUpdate: parseBoolean(body.forceUpdate, current.forceUpdate, 'forceUpdate'),
    minSupportedVersionCode: parseInteger(body.minSupportedVersionCode, current.minSupportedVersionCode, 'minSupportedVersionCode'),
    published: parseBoolean(body.published, current.published, 'published'),
    publishedAt: parseDateTime(body.publishedAt, 'publishedAt', current.publishedAt),
    updatedAt: new Date().toISOString(),
  } satisfies ReleaseRecord
}

export function findLatestPublishedRelease(
  items: ReleaseRecord[],
  platform: ReleasePlatform,
  channel: ReleaseChannel,
) {
  return sortReleases(items).find((item) => item.published && item.platform === platform && item.channel === channel) ?? null
}
