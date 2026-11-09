import { describe, expect, it } from 'vitest'
import type { DailyPresence, ResidencySummary } from '../api/types.ts'
import { CountryColorService } from './countryColor.ts'
import { cardStatus, countriesInPresence, defaultDateRange, groupByMonth, presentDay } from './residencyView.ts'

function summary(overrides: Partial<ResidencySummary> = {}): ResidencySummary {
  return {
    countryName: 'France',
    totalDays: 10,
    ruleType: 'Midnight',
    thresholdDays: 183,
    isApproachingThreshold: false,
    daysUntilThreshold: 173,
    ...overrides,
  }
}

function day(overrides: Partial<DailyPresence> = {}): DailyPresence {
  return {
    date: '2026-03-15',
    locationAtMidnight: 'France',
    locationsDuringDay: ['France'],
    isInTransitAtMidnight: false,
    ...overrides,
  }
}

describe('defaultDateRange', () => {
  it('counts both ends so the range is 365 days', () => {
    const range = defaultDateRange(new Date(2026, 11, 22))
    expect(range).toEqual({ start: '2025-12-23', end: '2026-12-22' })
  })
})

describe('cardStatus', () => {
  it('marks a country over the threshold in red when it is not home', () => {
    const status = cardStatus(summary({ totalDays: 183, daysUntilThreshold: 0 }), 'Canada')
    expect(status.borderColor).toBe('#f44336')
    expect(status.progressTone).toBe('danger')
    expect(status.badge).toBe('⛔ Over Threshold')
  })

  it('keeps the home country green even when it is over the threshold', () => {
    const status = cardStatus(summary({ countryName: 'Canada', totalDays: 200, daysUntilThreshold: 0 }), 'Canada')
    expect(status.borderColor).toBe('#4caf50')
    expect(status.badge).toBe('Home Country')
    expect(status.progressTone).toBe('success')
  })

  it('uses the approaching warning when days are running out', () => {
    const status = cardStatus(summary({ totalDays: 160, isApproachingThreshold: true, daysUntilThreshold: 23 }), null)
    expect(status.borderColor).toBe('#ff9800')
    expect(status.progressTone).toBe('warning')
    expect(status.badge).toBe('⚠️ 23 days left')
    expect(status.dayLabel).toBe('Midnights')
  })

  it('labels partial-day rules', () => {
    const status = cardStatus(summary({ ruleType: 'PartialDay' }), null)
    expect(status.dayLabel).toBe('Partial Days')
    expect(status.caption).toContain('partial day rule')
    expect(status.badge).toBe('✓ 173 days remaining')
  })
})

describe('timeline grouping', () => {
  const days = [
    day({ date: '2026-01-02', locationAtMidnight: 'Canada', locationsDuringDay: ['Canada'] }),
    day({ date: '2026-03-01', locationAtMidnight: 'France', locationsDuringDay: ['France'] }),
    day({ date: '2026-03-20', locationAtMidnight: 'Japan', locationsDuringDay: ['Japan', 'France'] }),
  ]

  it('groups months newest first and days newest first', () => {
    const groups = groupByMonth(days)
    expect(groups.map((group) => group.label)).toEqual(['March 2026', 'January 2026'])
    expect(groups[0]?.days.map((item) => item.date)).toEqual(['2026-03-20', '2026-03-01'])
  })

  it('lists countries alphabetically, including partial-day countries and skipping transit', () => {
    const withTransit = [
      ...days,
      day({ date: '2026-02-01', locationAtMidnight: 'IN_TRANSIT', locationsDuringDay: [], isInTransitAtMidnight: true }),
    ]
    expect(countriesInPresence(withTransit)).toEqual(['Canada', 'France', 'Japan'])
  })
})

describe('presentDay', () => {
  it('paints a multi-country day as a gradient of the country colours', () => {
    const colors = new CountryColorService()
    colors.registerCountries(['Canada', 'France'])
    const presented = presentDay(
      day({ locationsDuringDay: ['France', 'Canada'], locationAtMidnight: 'France' }),
      colors,
      'Canada',
    )
    expect(presented.style.background).toContain('linear-gradient(135deg')
    expect(presented.codes).toEqual(['CA', 'FR'])
    expect(presented.tooltip).toContain('Canada & France')
  })

  it('uses stripes when three countries share a day', () => {
    const colors = new CountryColorService()
    const presented = presentDay(
      day({ locationsDuringDay: ['Canada', 'France', 'Japan'] }),
      colors,
      null,
    )
    expect(presented.style.background).toContain('linear-gradient(90deg')
  })

  it('renders the home country as a neutral cell', () => {
    const presented = presentDay(day({ locationAtMidnight: 'Canada', locationsDuringDay: ['Canada'] }), new CountryColorService(), 'Canada')
    expect(presented.style.backgroundColor).toBe('#f5f5f5')
    expect(presented.label).toBe('CA')
  })

  it('labels a transit day', () => {
    const presented = presentDay(
      day({ locationAtMidnight: 'IN_TRANSIT', locationsDuringDay: [], isInTransitAtMidnight: true, date: '2026-04-01' }),
      new CountryColorService(),
      null,
    )
    expect(presented.label).toBe('Transit')
    expect(presented.className).toBe('transit')
    expect(presented.tooltip).toBe('Apr 01, 2026 - In Transit at Midnight')
  })
})
