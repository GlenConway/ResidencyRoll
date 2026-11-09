import { DateTime } from 'luxon'
import type { CountryDays, ItineraryFlightLeg, TripLeg } from '../api/types.ts'
import { findByIataCode } from './airports.ts'

export interface ForecastLeg {
  id: number
  departureCity: string
  departureCountry: string
  departureTimezone: string
  departureIataCode: string | null
  departureDate: string
  departureTime: string
  arrivalCity: string
  arrivalCountry: string
  arrivalTimezone: string
  arrivalIataCode: string | null
  arrivalDate: string
  arrivalTime: string
}

export function starterLeg(today = new Date(), id = 0): ForecastLeg {
  const date = datePart(addMonths(today, 1))
  return emptyLeg(id, date, '12:00', date, '12:00')
}

export function addForecastLeg(legs: readonly ForecastLeg[]): ForecastLeg[] {
  const last = legs.at(-1)
  const date = last?.arrivalDate || datePart(addMonths(new Date(), 1))
  const next = emptyLeg(nextId(legs), date, last?.arrivalTime || '12:00', date, '12:00')
  next.departureCity = last?.arrivalCity ?? ''
  next.departureCountry = last?.arrivalCountry ?? ''
  next.departureTimezone = last?.arrivalTimezone || 'UTC'
  next.departureIataCode = last?.arrivalIataCode ?? null
  return [...legs, next]
}

export function legLabel(index: number, count: number): string {
  if (index === 0) return 'Outbound'
  if (index === count - 1) return 'Return'
  return 'Connecting'
}

export function validationIssues(legs: readonly ForecastLeg[]): string[] {
  return legs.flatMap((leg, index) => {
    const missing: string[] = []
    if (!leg.arrivalCountry.trim()) missing.push('arrival country')
    if (!leg.arrivalDate) missing.push('arrival date')
    if (!leg.arrivalTime) missing.push('arrival time')
    if (!leg.departureDate) missing.push('departure date')
    if (!leg.departureTime) missing.push('departure time')
    return missing.length > 0 ? [`Leg ${index + 1}: missing ${missing.join(', ')}`] : []
  })
}

export function toForecastRequest(legs: readonly ForecastLeg[]): TripLeg[] {
  return legs.map((leg) => ({
    departureCountry: leg.departureCountry,
    departureCity: leg.departureCity,
    departureDateTime: combine(leg.departureDate, leg.departureTime),
    departureTimezone: leg.departureTimezone,
    departureIataCode: leg.departureIataCode,
    arrivalCountry: leg.arrivalCountry,
    arrivalCity: leg.arrivalCity,
    arrivalDateTime: combine(leg.arrivalDate, leg.arrivalTime),
    arrivalTimezone: leg.arrivalTimezone,
    arrivalIataCode: leg.arrivalIataCode,
  }))
}

export function tripDurationDays(legs: readonly ForecastLeg[]): number {
  const first = legs[0]
  const last = legs.at(-1)
  if (!first?.arrivalDate || !first.arrivalTime || !last?.departureDate || !last.departureTime) {
    return 0
  }
  const arrival = toUtc(combine(first.arrivalDate, first.arrivalTime), first.arrivalTimezone)
  const departure = toUtc(combine(last.departureDate, last.departureTime), last.departureTimezone)
  if (!arrival.isValid || !departure.isValid) return 0
  return Math.max(0, Math.floor(departure.diff(arrival, 'days').days))
}

export function primaryDestination(legs: readonly ForecastLeg[]): string {
  let longest: ForecastLeg | undefined
  let maxStay = 0
  for (let index = 0; index < legs.length; index += 1) {
    const leg = legs[index]
    if (!leg?.arrivalDate || !leg.arrivalTime) continue
    const next = legs[index + 1]
    const end = next ? dateTime(next.departureDate, next.departureTime) : dateTime(leg.departureDate, leg.departureTime)
    const start = dateTime(leg.arrivalDate, leg.arrivalTime)
    if (!end?.isValid || !start?.isValid) continue
    const stay = end.diff(start, 'milliseconds').milliseconds
    if (stay > maxStay) {
      maxStay = stay
      longest = leg
    }
  }
  return longest?.arrivalCountry ?? legs[0]?.arrivalCountry ?? ''
}

export function totalAway(rows: readonly CountryDays[], homeCountry: string | null): number {
  return rows.filter((row) => !isHome(row.countryName, homeCountry)).reduce((sum, row) => sum + row.days, 0)
}

export function sortByDays(rows: readonly CountryDays[]): CountryDays[] {
  return [...rows].sort((left, right) => right.days - left.days || left.countryName.localeCompare(right.countryName))
}

export function isHome(country: string, homeCountry: string | null): boolean {
  return Boolean(homeCountry?.trim()) && country.toLowerCase() === homeCountry!.toLowerCase()
}

export function legsFromParsed(parsed: readonly ItineraryFlightLeg[], today = new Date()): ForecastLeg[] {
  return parsed.map((leg, index) => {
    const departureAirport = findByIataCode(leg.departure_airport)
    const arrivalAirport = findByIataCode(leg.arrival_airport)
    const fallback = datePart(addMonths(today, 1))
    const departure = DateTime.fromFormat(leg.departure_datetime_local, "yyyy-MM-dd'T'HH:mm")
    const departureDate = departure.isValid ? departure.toFormat('yyyy-MM-dd') : fallback
    const departureTime = departure.isValid ? departure.toFormat('HH:mm') : '12:00'
    const arrival = leg.arrival_datetime_local
      ? DateTime.fromFormat(leg.arrival_datetime_local, "yyyy-MM-dd'T'HH:mm")
      : DateTime.invalid('missing')
    return {
      id: index,
      departureCity: departureAirport?.city ?? '',
      departureCountry: departureAirport?.country ?? '',
      departureTimezone: departureAirport?.ianaTimezone ?? 'UTC',
      departureIataCode: leg.departure_airport,
      departureDate,
      departureTime,
      arrivalCity: arrivalAirport?.city ?? '',
      arrivalCountry: arrivalAirport?.country ?? '',
      arrivalTimezone: arrivalAirport?.ianaTimezone ?? 'UTC',
      arrivalIataCode: leg.arrival_airport,
      arrivalDate: arrival.isValid ? arrival.toFormat('yyyy-MM-dd') : departureDate,
      arrivalTime: arrival.isValid ? arrival.toFormat('HH:mm') : '14:00',
    }
  })
}

export const apiUnreachableMessage =
  'Unable to reach the API. Make sure the API project is running and accessible at https://localhost:5003.'

export const unexpectedForecastMessage =
  'An unexpected error occurred while calculating the forecast. Check the logs for details.'

function emptyLeg(id: number, departureDate: string, departureTime: string, arrivalDate: string, arrivalTime: string): ForecastLeg {
  return {
    id,
    departureCity: '',
    departureCountry: '',
    departureTimezone: 'UTC',
    departureIataCode: null,
    departureDate,
    departureTime,
    arrivalCity: '',
    arrivalCountry: '',
    arrivalTimezone: 'UTC',
    arrivalIataCode: null,
    arrivalDate,
    arrivalTime,
  }
}

function nextId(legs: readonly ForecastLeg[]): number {
  return legs.reduce((max, leg) => Math.max(max, leg.id), -1) + 1
}

function combine(date: string, time: string): string {
  return `${date}T${time.length === 5 ? `${time}:00` : time}`
}

function dateTime(date: string, time: string): DateTime | null {
  if (!date || !time) return null
  return DateTime.fromISO(combine(date, time))
}

function toUtc(local: string, zone: string): DateTime {
  if (!zone || zone === 'UTC') return DateTime.fromISO(local, { zone: 'utc' })
  const zoned = DateTime.fromISO(local, { zone })
  return zoned.isValid ? zoned.toUTC() : DateTime.fromISO(local, { zone: 'utc' })
}

function addMonths(date: Date, months: number): Date {
  const next = new Date(date)
  next.setMonth(next.getMonth() + months)
  return next
}

function datePart(value: Date): string {
  return `${value.getFullYear()}-${String(value.getMonth() + 1).padStart(2, '0')}-${String(value.getDate()).padStart(2, '0')}`
}
