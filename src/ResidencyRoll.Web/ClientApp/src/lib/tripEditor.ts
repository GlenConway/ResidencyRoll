import { DateTime } from 'luxon'
import type { ItineraryFlightLeg, Trip } from '../api/types.ts'
import { findByIataCode } from './airports.ts'
import { createNextLeg } from './tripLeg.ts'
import { getSyncedArrivalDate } from './tripDate.ts'

export interface TripDraft {
  id: number
  departureCountry: string
  departureCity: string
  departureTimezone: string
  departureIataCode: string | null
  departureDate: string
  departureTime: string
  arrivalCountry: string
  arrivalCity: string
  arrivalTimezone: string
  arrivalIataCode: string | null
  arrivalDate: string
  arrivalTime: string
}

const requiredLabels = [
  ['departureCity', 'Departure City'],
  ['departureCountry', 'Departure Country'],
  ['departureTimezone', 'Departure Timezone'],
  ['arrivalCity', 'Arrival City'],
  ['arrivalCountry', 'Arrival Country'],
  ['arrivalTimezone', 'Arrival Timezone'],
] as const

export function newTripDraft(today = new Date()): TripDraft {
  const date = datePart(today)
  return {
    id: 0,
    departureCountry: '',
    departureCity: '',
    departureTimezone: 'UTC',
    departureIataCode: null,
    departureDate: date,
    departureTime: '12:00',
    arrivalCountry: '',
    arrivalCity: '',
    arrivalTimezone: 'UTC',
    arrivalIataCode: null,
    arrivalDate: date,
    arrivalTime: '12:00',
  }
}

export function draftFromTrip(trip: Trip): TripDraft {
  const departure = splitDateTime(trip.departureDateTime)
  const arrival = splitDateTime(trip.arrivalDateTime)
  return {
    id: trip.id,
    departureCountry: trip.departureCountry,
    departureCity: trip.departureCity,
    departureTimezone: trip.departureTimezone,
    departureIataCode: trip.departureIataCode,
    departureDate: departure.date,
    departureTime: departure.time,
    arrivalCountry: trip.arrivalCountry,
    arrivalCity: trip.arrivalCity,
    arrivalTimezone: trip.arrivalTimezone,
    arrivalIataCode: trip.arrivalIataCode,
    arrivalDate: arrival.date,
    arrivalTime: arrival.time,
  }
}

export function draftFromNextLeg(trip: Trip): TripDraft {
  const next = createNextLeg({
    ...trip,
    departureDateTime: parseLocal(trip.departureDateTime),
    arrivalDateTime: parseLocal(trip.arrivalDateTime),
  })
  const departure = splitDate(next.departureDateTime)
  const arrival = splitDate(next.arrivalDateTime)
  return {
    id: 0,
    departureCountry: next.departureCountry,
    departureCity: next.departureCity,
    departureTimezone: next.departureTimezone,
    departureIataCode: next.departureIataCode,
    departureDate: departure.date,
    departureTime: departure.time,
    arrivalCountry: next.arrivalCountry,
    arrivalCity: next.arrivalCity,
    arrivalTimezone: next.arrivalTimezone,
    arrivalIataCode: null,
    arrivalDate: arrival.date,
    arrivalTime: arrival.time,
  }
}

export function validationMessage(draft: TripDraft): string | null {
  if (!draft.departureDate || !draft.departureTime || !draft.arrivalDate || !draft.arrivalTime) {
    return 'Please fill in all date and time fields.'
  }

  const missing = requiredLabels.filter(([key]) => draft[key].trim().length === 0).map(([, label]) => label)
  if (missing.length > 0) {
    return `Please complete the following required fields: ${missing.join(', ')}`
  }

  return null
}

export function parsedDraftValidation(drafts: readonly TripDraft[]): string | null {
  for (const draft of drafts) {
    const missing = requiredLabels.filter(([key]) => draft[key].trim().length === 0).map(([, label]) => label)
    if (missing.length > 0) {
      return `Please complete all required fields before saving parsed legs. Missing: ${missing.join(', ')}`
    }
  }
  return null
}

export function toTripBody(draft: TripDraft): Omit<Trip, 'id'> {
  return {
    departureCountry: draft.departureCountry,
    departureCity: draft.departureCity,
    departureDateTime: combine(draft.departureDate, draft.departureTime),
    departureTimezone: draft.departureTimezone,
    departureIataCode: draft.departureIataCode,
    arrivalCountry: draft.arrivalCountry,
    arrivalCity: draft.arrivalCity,
    arrivalDateTime: combine(draft.arrivalDate, draft.arrivalTime),
    arrivalTimezone: draft.arrivalTimezone,
    arrivalIataCode: draft.arrivalIataCode,
  }
}

export function syncArrivalDate(draft: TripDraft, departureDate: string): TripDraft {
  const current = draft.arrivalDate ? parseDateOnly(draft.arrivalDate) : null
  const synced = getSyncedArrivalDate(departureDate ? parseDateOnly(departureDate) : null, current, draft.id > 0)
  return { ...draft, departureDate, arrivalDate: synced ? datePart(synced) : '' }
}

export interface ParsedDraftResult {
  drafts: TripDraft[]
  error: string | null
}

export function draftsFromParsedLegs(legs: readonly ItineraryFlightLeg[]): ParsedDraftResult {
  const drafts: TripDraft[] = []
  for (const leg of legs) {
    const departure = DateTime.fromFormat(leg.departure_datetime_local, "yyyy-MM-dd'T'HH:mm", { zone: 'utc' })
    if (!departure.isValid) {
      return { drafts: [], error: `Could not parse departure datetime: ${leg.departure_datetime_local}` }
    }

    let arrival = departure
    if (leg.arrival_datetime_local?.trim()) {
      const parsedArrival = DateTime.fromFormat(leg.arrival_datetime_local, "yyyy-MM-dd'T'HH:mm", { zone: 'utc' })
      if (parsedArrival.isValid) {
        arrival = parsedArrival
      }
    }

    const departureAirport = findByIataCode(leg.departure_airport)
    const arrivalAirport = findByIataCode(leg.arrival_airport)
    drafts.push({
      id: 0,
      departureCountry: departureAirport?.country ?? leg.departure_airport,
      departureCity: departureAirport?.city ?? leg.departure_airport,
      departureTimezone: departureAirport?.ianaTimezone ?? 'UTC',
      departureIataCode: leg.departure_airport,
      departureDate: departure.toFormat('yyyy-MM-dd'),
      departureTime: departure.toFormat('HH:mm'),
      arrivalCountry: arrivalAirport?.country ?? leg.arrival_airport,
      arrivalCity: arrivalAirport?.city ?? leg.arrival_airport,
      arrivalTimezone: arrivalAirport?.ianaTimezone ?? 'UTC',
      arrivalIataCode: leg.arrival_airport,
      arrivalDate: arrival.toFormat('yyyy-MM-dd'),
      arrivalTime: arrival.toFormat('HH:mm'),
    })
  }

  return { drafts, error: null }
}

export function durationHours(departureLocal: string, departureZone: string, arrivalLocal: string, arrivalZone: string): number {
  const departure = toUtc(departureLocal, departureZone)
  const arrival = toUtc(arrivalLocal, arrivalZone)
  if (!departure.isValid || !arrival.isValid) {
    return 0
  }
  return Math.max(0, Math.floor(arrival.diff(departure, 'hours').hours))
}

export function formatTripWhen(value: string): string {
  const parsed = DateTime.fromISO(value, { setZone: true })
  return parsed.isValid ? parsed.toFormat('yyyy-MM-dd HH:mm') : value
}

export function placeLabel(iataCode: string | null, city: string): string {
  return iataCode ? `${iataCode} - ${city}` : city
}

function toUtc(local: string, zone: string): DateTime {
  const trimmed = local.length === 16 ? `${local}:00` : local
  if (!zone || zone === 'UTC') {
    return DateTime.fromISO(trimmed, { zone: 'utc' })
  }
  const zoned = DateTime.fromISO(trimmed, { zone })
  return zoned.isValid ? zoned.toUTC() : DateTime.fromISO(trimmed, { zone: 'utc' })
}

function combine(date: string, time: string): string {
  return `${date}T${time.length === 5 ? `${time}:00` : time}`
}

function splitDateTime(value: string): { date: string; time: string } {
  const parsed = DateTime.fromISO(value, { setZone: true })
  if (!parsed.isValid) {
    return { date: value.slice(0, 10), time: value.slice(11, 16) }
  }
  return { date: parsed.toFormat('yyyy-MM-dd'), time: parsed.toFormat('HH:mm') }
}

function splitDate(value: Date): { date: string; time: string } {
  return { date: datePart(value), time: `${pad(value.getHours())}:${pad(value.getMinutes())}` }
}

function parseLocal(value: string): Date {
  const parsed = DateTime.fromISO(value, { setZone: true })
  return parsed.isValid ? parsed.toJSDate() : new Date(value)
}

function parseDateOnly(value: string): Date {
  const [year, month, day] = value.split('-').map(Number)
  return new Date(year ?? 1970, (month ?? 1) - 1, day ?? 1)
}

function datePart(value: Date): string {
  return `${value.getFullYear()}-${pad(value.getMonth() + 1)}-${pad(value.getDate())}`
}

function pad(value: number): string {
  return String(value).padStart(2, '0')
}
