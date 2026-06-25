import { createHmac, timingSafeEqual } from 'node:crypto'
import type { H3Event } from 'h3'
import { createError, deleteCookie, getCookie, getHeader, readBody, setCookie } from 'h3'

const ADMIN_SESSION_COOKIE = 'luashengime_admin_session'

type AdminSessionPayload = {
  username: string
  exp: number
}

function toBase64Url(value: string) {
  return Buffer.from(value, 'utf8').toString('base64url')
}

function fromBase64Url(value: string) {
  return Buffer.from(value, 'base64url').toString('utf8')
}

function createSignature(secret: string, payload: string) {
  return createHmac('sha256', secret).update(payload).digest('base64url')
}

function getSessionSecret() {
  return useRuntimeConfig().adminSessionSecret?.trim()
}

function getSessionMaxAge() {
  return Number(useRuntimeConfig().adminSessionMaxAge) || 60 * 60 * 12
}

function getConfiguredAdmin() {
  const config = useRuntimeConfig()
  const username = config.adminUsername?.trim()
  const password = config.adminPassword?.trim()

  if (!username || !password) {
    throw createError({
      statusCode: 503,
      statusMessage: 'Admin username or password is not configured',
    })
  }

  return { username, password }
}

function getSessionCookieOptions() {
  return {
    httpOnly: true,
    sameSite: 'lax' as const,
    secure: Boolean(useRuntimeConfig().adminSessionSecure),
    path: '/',
    maxAge: getSessionMaxAge(),
  }
}

function verifyTokenSignature(secret: string, payload: string, signature: string) {
  const expected = createSignature(secret, payload)
  const expectedBuffer = Buffer.from(expected)
  const actualBuffer = Buffer.from(signature)

  if (expectedBuffer.length !== actualBuffer.length) {
    return false
  }

  return timingSafeEqual(expectedBuffer, actualBuffer)
}

export function createAdminSessionToken(username: string) {
  const secret = getSessionSecret()

  if (!secret) {
    throw createError({
      statusCode: 503,
      statusMessage: 'ADMIN_SESSION_SECRET is not configured',
    })
  }

  const payload = toBase64Url(JSON.stringify({
    username,
    exp: Date.now() + getSessionMaxAge() * 1000,
  } satisfies AdminSessionPayload))

  return `${payload}.${createSignature(secret, payload)}`
}

export function readAdminSession(event: H3Event) {
  const secret = getSessionSecret()
  const sessionToken = getCookie(event, ADMIN_SESSION_COOKIE)

  if (!secret || !sessionToken) {
    return null
  }

  const [payload, signature] = sessionToken.split('.')
  if (!payload || !signature || !verifyTokenSignature(secret, payload, signature)) {
    return null
  }

  try {
    const session = JSON.parse(fromBase64Url(payload)) as AdminSessionPayload

    if (!session.username || !session.exp || session.exp <= Date.now()) {
      return null
    }

    return session
  } catch {
    return null
  }
}

export function setAdminSession(event: H3Event, username: string) {
  setCookie(event, ADMIN_SESSION_COOKIE, createAdminSessionToken(username), getSessionCookieOptions())
}

export function clearAdminSession(event: H3Event) {
  deleteCookie(event, ADMIN_SESSION_COOKIE, {
    path: '/',
  })
}

export async function loginAdmin(event: H3Event) {
  const body = await readBody(event)
  const { username, password } = getConfiguredAdmin()
  const input = body as { username?: string; password?: string }

  if (input?.username?.trim() !== username || input?.password !== password) {
    throw createError({
      statusCode: 401,
      statusMessage: '用户名或密码错误',
    })
  }

  setAdminSession(event, username)

  return {
    authenticated: true,
    username,
  }
}

export function readHeaderAdminToken(event: H3Event) {
  const token = useRuntimeConfig().adminToken?.trim()
  if (!token) {
    return null
  }

  const headerToken = getHeader(event, 'x-admin-token')?.trim()
  const authorization = getHeader(event, 'authorization')?.trim()
  const bearerToken = authorization?.startsWith('Bearer ')
    ? authorization.slice('Bearer '.length).trim()
    : ''

  if (headerToken === token || bearerToken === token) {
    return {
      username: 'token-admin',
      exp: Number.POSITIVE_INFINITY,
    }
  }

  return null
}

export function requireAdminAccess(event: H3Event) {
  const session = readAdminSession(event) ?? readHeaderAdminToken(event)

  if (!session) {
    throw createError({
      statusCode: 401,
      statusMessage: 'Unauthorized',
    })
  }

  return session
}

export const requireAdminToken = requireAdminAccess
