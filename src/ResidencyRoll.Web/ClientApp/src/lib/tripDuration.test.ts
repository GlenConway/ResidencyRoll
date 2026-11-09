import { describe, expect, it } from 'vitest'
import { durationDays } from './tripDuration.ts'

describe('durationDays', () => {
  it('matches the midnight day count for a cross-month trip', () => {
    expect(durationDays(new Date(2025, 1, 7), new Date(2025, 2, 30))).toBe(51)
  })

  it('returns zero when the end is before the start', () => {
    expect(durationDays(new Date(2025, 2, 30), new Date(2025, 1, 7))).toBe(0)
  })

  it('counts a single night', () => {
    expect(durationDays(new Date(2025, 1, 7), new Date(2025, 1, 8))).toBe(1)
  })
})
