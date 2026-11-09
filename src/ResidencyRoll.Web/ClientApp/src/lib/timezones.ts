import { DateTime } from 'luxon'

export interface TimezonePlace {
  city: string
  country: string
  timezoneId: string
  displayName: string
}

const places: readonly Omit<TimezonePlace, 'displayName'>[] = [
  { city: 'Vancouver', country: 'Canada', timezoneId: 'America/Vancouver' },
  { city: 'Toronto', country: 'Canada', timezoneId: 'America/Toronto' },
  { city: 'Montreal', country: 'Canada', timezoneId: 'America/Toronto' },
  { city: 'Calgary', country: 'Canada', timezoneId: 'America/Edmonton' },
  { city: 'Halifax', country: 'Canada', timezoneId: 'America/Halifax' },
  { city: "St. John's", country: 'Canada', timezoneId: 'America/St_Johns' },
  { city: 'New York', country: 'United States', timezoneId: 'America/New_York' },
  { city: 'Los Angeles', country: 'United States', timezoneId: 'America/Los_Angeles' },
  { city: 'Chicago', country: 'United States', timezoneId: 'America/Chicago' },
  { city: 'Denver', country: 'United States', timezoneId: 'America/Denver' },
  { city: 'Phoenix', country: 'United States', timezoneId: 'America/Phoenix' },
  { city: 'Honolulu', country: 'United States', timezoneId: 'Pacific/Honolulu' },
  { city: 'Anchorage', country: 'United States', timezoneId: 'America/Anchorage' },
  { city: 'London', country: 'United Kingdom', timezoneId: 'Europe/London' },
  { city: 'Paris', country: 'France', timezoneId: 'Europe/Paris' },
  { city: 'Berlin', country: 'Germany', timezoneId: 'Europe/Berlin' },
  { city: 'Rome', country: 'Italy', timezoneId: 'Europe/Rome' },
  { city: 'Madrid', country: 'Spain', timezoneId: 'Europe/Madrid' },
  { city: 'Amsterdam', country: 'Netherlands', timezoneId: 'Europe/Amsterdam' },
  { city: 'Zurich', country: 'Switzerland', timezoneId: 'Europe/Zurich' },
  { city: 'Tokyo', country: 'Japan', timezoneId: 'Asia/Tokyo' },
  { city: 'Seoul', country: 'South Korea', timezoneId: 'Asia/Seoul' },
  { city: 'Hong Kong', country: 'Hong Kong', timezoneId: 'Asia/Hong_Kong' },
  { city: 'Singapore', country: 'Singapore', timezoneId: 'Asia/Singapore' },
  { city: 'Bangkok', country: 'Thailand', timezoneId: 'Asia/Bangkok' },
  { city: 'Dubai', country: 'United Arab Emirates', timezoneId: 'Asia/Dubai' },
  { city: 'Delhi', country: 'India', timezoneId: 'Asia/Kolkata' },
  { city: 'Shanghai', country: 'China', timezoneId: 'Asia/Shanghai' },
  { city: 'Sydney', country: 'Australia', timezoneId: 'Australia/Sydney' },
  { city: 'Melbourne', country: 'Australia', timezoneId: 'Australia/Melbourne' },
  { city: 'Brisbane', country: 'Australia', timezoneId: 'Australia/Brisbane' },
  { city: 'Perth', country: 'Australia', timezoneId: 'Australia/Perth' },
  { city: 'Auckland', country: 'New Zealand', timezoneId: 'Pacific/Auckland' },
  { city: 'São Paulo', country: 'Brazil', timezoneId: 'America/Sao_Paulo' },
  { city: 'Buenos Aires', country: 'Argentina', timezoneId: 'America/Argentina/Buenos_Aires' },
  { city: 'Santiago', country: 'Chile', timezoneId: 'America/Santiago' },
]

export const manualTimezones: readonly TimezonePlace[] = places.map((place) => ({
  ...place,
  displayName: `${place.city}, ${place.country} (${place.timezoneId})`,
}))

export function formatUtcOffset(timezoneId: string, at: Date = new Date()): string {
  if (timezoneId.length === 0) {
    return ''
  }

  const zoned = DateTime.fromJSDate(at, { zone: 'utc' }).setZone(timezoneId)
  if (!zoned.isValid) {
    return timezoneId
  }

  return `UTC${zoned.toFormat('ZZ')}`
}
