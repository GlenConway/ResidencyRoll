import type { DailyPresence, ResidencySummary } from '../api/types.ts'
import { darkenColor, type CountryColorService } from './countryColor.ts'
import { getCountryCode } from './countryCode.ts'

const monthNames = [
  'January',
  'February',
  'March',
  'April',
  'May',
  'June',
  'July',
  'August',
  'September',
  'October',
  'November',
  'December',
]

const shortMonths = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']

export type CardTone = 'danger' | 'warning' | 'success'

export interface CardStatus {
  borderColor: string
  progressTone: CardTone
  progressPercent: number
  badge: string
  dayLabel: string
  caption: string
}

export function isoDate(date: Date): string {
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${date.getFullYear()}-${month}-${day}`
}

export const rollingWindowDays = 365

export function defaultDateRange(today = new Date()): { start: string; end: string } {
  const start = new Date(today)
  start.setDate(start.getDate() - (rollingWindowDays - 1))
  return { start: isoDate(start), end: isoDate(today) }
}

export function isHomeCountry(countryName: string, homeCountry: string | null): boolean {
  return homeCountry != null && homeCountry.length > 0 && countryName.toLowerCase() === homeCountry.toLowerCase()
}

export function cardStatus(summary: ResidencySummary, homeCountry: string | null): CardStatus {
  const home = isHomeCountry(summary.countryName, homeCountry)
  const over = summary.totalDays >= summary.thresholdDays
  const progressPercent = summary.thresholdDays <= 0 ? 0 : Math.min(100, (summary.totalDays / summary.thresholdDays) * 100)

  let borderColor = '#4caf50'
  let progressTone: CardTone = 'success'
  let badge = `✓ ${summary.daysUntilThreshold} days remaining`

  if (over && !home) {
    borderColor = '#f44336'
    progressTone = 'danger'
    badge = '⛔ Over Threshold'
  } else if (home) {
    borderColor = '#4caf50'
    badge = 'Home Country'
  } else if (summary.isApproachingThreshold) {
    borderColor = '#ff9800'
    progressTone = 'warning'
    badge = `⚠️ ${summary.daysUntilThreshold} days left`
  }

  if (!home && summary.isApproachingThreshold && !over) {
    progressTone = 'warning'
  }

  return {
    borderColor,
    progressTone,
    progressPercent,
    badge,
    dayLabel: summary.ruleType === 'Midnight' ? 'Midnights' : 'Partial Days',
    caption:
      summary.ruleType === 'Midnight'
        ? 'Counts days where present at midnight local time'
        : 'Counts any day with presence (partial day rule)',
  }
}

export function countriesInPresence(days: readonly DailyPresence[]): string[] {
  const countries = new Set<string>()
  for (const day of days) {
    if (day.locationAtMidnight && day.locationAtMidnight !== 'IN_TRANSIT') {
      countries.add(day.locationAtMidnight)
    }
    for (const country of day.locationsDuringDay) {
      if (country) {
        countries.add(country)
      }
    }
  }
  return [...countries].sort((left, right) => left.localeCompare(right))
}

export interface MonthGroup {
  label: string
  days: DailyPresence[]
}

export function groupByMonth(days: readonly DailyPresence[]): MonthGroup[] {
  const sorted = [...days].sort((left, right) => right.date.localeCompare(left.date))
  const groups: MonthGroup[] = []
  for (const day of sorted) {
    const label = monthTitle(day.date)
    const current = groups.at(-1)
    if (current?.label === label) {
      current.days.push(day)
    } else {
      groups.push({ label, days: [day] })
    }
  }
  return groups
}

export function formatTooltipDate(iso: string): string {
  const [year, month, day] = iso.split('-')
  return `${shortMonths[Number(month) - 1]} ${day}, ${year}`
}

export function formatDetailDate(iso: string): string {
  const [year, month, day] = iso.split('-')
  return `${monthNames[Number(month) - 1]} ${day}, ${year}`
}

export function isTransit(day: DailyPresence): boolean {
  return day.isInTransitAtMidnight || day.locationAtMidnight === 'IN_TRANSIT'
}

export interface DayPresentation {
  className: string
  style: Record<string, string>
  label: string
  codes: string[]
  tooltip: string
}

export function presentDay(day: DailyPresence, colors: CountryColorService, homeCountry: string | null): DayPresentation {
  if (isTransit(day)) {
    return {
      className: 'transit',
      style: {},
      label: 'Transit',
      codes: [],
      tooltip: `${formatTooltipDate(day.date)} - In Transit at Midnight`,
    }
  }

  if ((day.locationsDuringDay?.length ?? 0) > 1) {
    const ordered = [...day.locationsDuringDay].sort((left, right) => left.localeCompare(right))
    const palette = day.locationsDuringDay.map((country) => colors.getCountryColor(country))
    return {
      className: 'location',
      style: { background: multiCountryGradient(palette), color: 'white' },
      label: '',
      codes: ordered.map((country) => getCountryCode(country)),
      tooltip: `${formatTooltipDate(day.date)} - ${ordered.join(' & ')} (both count under Partial Day Rule)`,
    }
  }

  const location = day.locationsDuringDay?.[0] || day.locationAtMidnight
  if (!location) {
    return { className: 'location', style: {}, label: '', codes: [], tooltip: formatTooltipDate(day.date) }
  }

  if (location === homeCountry) {
    return {
      className: 'location',
      style: { backgroundColor: '#f5f5f5', border: '1px solid #ddd', color: '#999' },
      label: getCountryCode(location),
      codes: [],
      tooltip: `${formatTooltipDate(day.date)} - ${location}`,
    }
  }

  const color = colors.getCountryColor(location)
  return {
    className: 'location',
    style: {
      backgroundColor: color,
      border: `1px solid ${darkenColor(color, 0.8)}`,
      color: 'white',
    },
    label: getCountryCode(location),
    codes: [],
    tooltip: `${formatTooltipDate(day.date)} - ${location}`,
  }
}

function multiCountryGradient(colors: readonly string[]): string {
  if (colors.length === 2) {
    return `linear-gradient(135deg, ${colors[0]} 0%, ${colors[0]} 50%, ${colors[1]} 50%, ${colors[1]} 100%)`
  }

  const step = 100 / colors.length
  const stops = colors.map((color, index) => `${color} ${index * step}%, ${color} ${(index + 1) * step}%`)
  return `linear-gradient(90deg, ${stops.join(', ')})`
}

function monthTitle(iso: string): string {
  const [year, month] = iso.split('-')
  return `${monthNames[Number(month) - 1]} ${year}`
}
