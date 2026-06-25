import { defineEventHandler } from 'h3'
import { pickVisibleAnnouncements, readAnnouncements } from '../utils/announcements'

export default defineEventHandler(async () => {
  const items = await readAnnouncements()

  return {
    items: pickVisibleAnnouncements(items),
  }
})
