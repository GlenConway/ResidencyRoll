import { describe, expect, it } from 'vitest'
import { formatUtcOffset, manualTimezones } from './timezones.ts'

describe('formatUtcOffset', () => {
  it('formats UTC with a zero offset', () => {
    expect(formatUtcOffset('UTC', new Date('2026-01-15T12:00:00Z'))).toBe('UTC+00:00')
  })

  it('formats a zone that does not observe daylight saving', () => {
    expect(formatUtcOffset('Asia/Tokyo', new Date('2026-01-15T12:00:00Z'))).toBe('UTC+09:00')
  })

  it('returns the zone id when it is not a known IANA zone', () => {
    expect(formatUtcOffset('Not/AZone')).toBe('Not/AZone')
  })

  it('returns an empty string when the zone is empty', () => {
    expect(formatUtcOffset('')).toBe('')
  })
})

describe('manualTimezones', () => {
  it('includes the curated city list used by the manual override', () => {
    expect(manualTimezones.find((place) => place.city === "St. John's")?.timezoneId).toBe('America/St_Johns')
    expect(manualTimezones.find((place) => place.city === 'São Paulo')?.displayName).toContain('America/Sao_Paulo')
  })
})
