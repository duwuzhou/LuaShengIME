import { createError, defineEventHandler, getQuery } from 'h3'
import { findLatestPublishedRelease, readReleases } from '../../utils/releases'

export default defineEventHandler(async (event) => {
  const query = getQuery(event)
  const platform = typeof query.platform === 'string' ? query.platform : 'android'
  const channel = typeof query.channel === 'string' ? query.channel : 'stable'
  const currentVersionCode =
    typeof query.currentVersionCode === 'string' && query.currentVersionCode.trim()
      ? Number.parseInt(query.currentVersionCode, 10)
      : null

  if (currentVersionCode !== null && Number.isNaN(currentVersionCode)) {
    throw createError({
      statusCode: 400,
      statusMessage: 'currentVersionCode must be an integer',
    })
  }

  const releases = await readReleases()
  const latest = findLatestPublishedRelease(releases, platform as 'android', channel as 'stable' | 'beta' | 'alpha')

  if (!latest) {
    throw createError({
      statusCode: 404,
      statusMessage: 'No published release found',
    })
  }

  const {
    public: { downloadUrl },
  } = useRuntimeConfig()

  const hasUpdate = currentVersionCode === null ? null : latest.versionCode > currentVersionCode
  const forceUpdate =
    currentVersionCode === null
      ? latest.forceUpdate
      : latest.forceUpdate || (
        latest.minSupportedVersionCode !== null &&
        currentVersionCode < latest.minSupportedVersionCode
      )

  return {
    platform: latest.platform,
    channel: latest.channel,
    latestVersion: {
      id: latest.id,
      versionName: latest.versionName,
      versionCode: latest.versionCode,
      changelog: latest.changelog,
      publishedAt: latest.publishedAt,
    },
    downloadUrl: latest.apkUrl ?? downloadUrl,
    hasUpdate,
    forceUpdate,
    currentVersionCode,
  }
})
