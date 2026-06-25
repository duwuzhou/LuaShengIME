import { randomUUID } from 'node:crypto'
import { createError } from 'h3'
import { readJsonFile, writeJsonFile } from './content-store'

export type AnnouncementLevel = 'info' | 'success' | 'warning' | 'error'

export type AnnouncementRecord = {
  id: string
  title: string
  content: string
  level: AnnouncementLevel
  active: boolean
  sortOrder: number
  startsAt: string | null
  endsAt: string | null
  createdAt: string
  updatedAt: string
}

type AnnouncementCollection = {
  items: AnnouncementRecord[]
}

const DEFAULT_DATA: AnnouncementCollection = {
  items: [],
}

const VALID_LEVELS: AnnouncementLevel[] = ['info', 'success', 'warning', 'error']

function getDataFile() {
  return useRuntimeConfig().announcementDataFile
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

function parseLevel(value: unknown, fallback: AnnouncementLevel) {
  if (value === undefined) {
    return fallback
  }

  if (typeof value !== 'string' || !VALID_LEVELS.includes(value as AnnouncementLevel)) {
    throw createError({
      statusCode: 400,
      statusMessage: `level must be one of: ${VALID_LEVELS.join(', ')}`,
    })
  }

  return value as AnnouncementLevel
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

function parseInteger(value: unknown, fallback: number, field: string) {
  if (value === undefined) {
    return fallback
  }

  if (typeof value !== 'number' || !Number.isInteger(value)) {
    throw createError({
      statusCode: 400,
      statusMessage: `${field} must be an integer`,
    })
  }

  return value
}

function parseNullableDateTime(value: unknown, field: string, fallback: string | null) {
  if (value === undefined) {
    return fallback
  }

  if (value === null || value === '') {
    return null
  }

  if (typeof value !== 'string' || Number.isNaN(Date.parse(value))) {
    throw createError({
      statusCode: 400,
      statusMessage: `${field} must be a valid ISO date string or null`,
    })
  }

  return new Date(value).toISOString()
}

function sortAnnouncements(items: AnnouncementRecord[]) {
  return [...items].sort((left, right) => {
    if (right.sortOrder !== left.sortOrder) {
      return right.sortOrder - left.sortOrder
    }

    return new Date(right.updatedAt).getTime() - new Date(left.updatedAt).getTime()
  })
}

export async function readAnnouncements() {
  const data = await readJsonFile<AnnouncementCollection>(getDataFile(), DEFAULT_DATA)
  return sortAnnouncements(data.items)
}

export async function writeAnnouncements(items: AnnouncementRecord[]) {
  await writeJsonFile<AnnouncementCollection>(getDataFile(), {
    items: sortAnnouncements(items),
  })
}

export function createAnnouncement(input: unknown) {
  const body = ensureObject(input)
  const now = new Date().toISOString()

  return {
    id: randomUUID(),
    title: ensureString(body.title, 'title'),
    content: ensureString(body.content, 'content'),
    level: parseLevel(body.level, 'info'),
    active: parseBoolean(body.active, true, 'active'),
    sortOrder: parseInteger(body.sortOrder, 0, 'sortOrder'),
    startsAt: parseNullableDateTime(body.startsAt, 'startsAt', null),
    endsAt: parseNullableDateTime(body.endsAt, 'endsAt', null),
    createdAt: now,
    updatedAt: now,
  } satisfies AnnouncementRecord
}

export function patchAnnouncement(current: AnnouncementRecord, input: unknown) {
  const body = ensureObject(input)

  return {
    ...current,
    title: body.title === undefined ? current.title : ensureString(body.title, 'title'),
    content: body.content === undefined ? current.content : ensureString(body.content, 'content'),
    level: parseLevel(body.level, current.level),
    active: parseBoolean(body.active, current.active, 'active'),
    sortOrder: parseInteger(body.sortOrder, current.sortOrder, 'sortOrder'),
    startsAt: parseNullableDateTime(body.startsAt, 'startsAt', current.startsAt),
    endsAt: parseNullableDateTime(body.endsAt, 'endsAt', current.endsAt),
    updatedAt: new Date().toISOString(),
  } satisfies AnnouncementRecord
}

export function isAnnouncementVisible(item: AnnouncementRecord, now = new Date()) {
  if (!item.active) {
    return false
  }

  const currentTime = now.getTime()

  if (item.startsAt && new Date(item.startsAt).getTime() > currentTime) {
    return false
  }

  if (item.endsAt && new Date(item.endsAt).getTime() < currentTime) {
    return false
  }

  return true
}

export function pickVisibleAnnouncements(items: AnnouncementRecord[], now = new Date()) {
  return sortAnnouncements(items).filter((item) => isAnnouncementVisible(item, now))
}
