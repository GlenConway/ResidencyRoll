export interface TripLegInput {
  departureCountry?: string
  departureCity?: string
  departureTimezone?: string
  departureIataCode?: string | null
  departureDateTime?: Date
  arrivalCountry?: string
  arrivalCity?: string
  arrivalTimezone?: string
  arrivalIataCode?: string | null
  arrivalDateTime?: Date
}

export interface NextLeg {
  departureCountry: string
  departureCity: string
  departureTimezone: string
  departureIataCode: string | null
  departureDateTime: Date
  arrivalCountry: string
  arrivalCity: string
  arrivalTimezone: string
  arrivalIataCode: null
  arrivalDateTime: Date
}

const hour = 60 * 60 * 1000

export function createNextLeg(
  baseTrip: TripLegInput,
  layoverMs = 2 * hour,
  defaultDurationMs = 2 * hour,
): NextLeg {
  const baseArrival =
    baseTrip.arrivalDateTime == null || baseTrip.arrivalDateTime.getFullYear() <= 1
      ? new Date()
      : baseTrip.arrivalDateTime
  const departureTime = new Date(baseArrival.getTime() + layoverMs)
  const timezone = baseTrip.arrivalTimezone?.trim() ? baseTrip.arrivalTimezone : 'UTC'

  return {
    departureCountry: baseTrip.arrivalCountry ?? '',
    departureCity: baseTrip.arrivalCity ?? '',
    departureTimezone: timezone,
    departureIataCode: baseTrip.arrivalIataCode ?? null,
    departureDateTime: departureTime,
    arrivalTimezone: timezone,
    arrivalDateTime: new Date(departureTime.getTime() + defaultDurationMs),
    arrivalCountry: '',
    arrivalCity: '',
    arrivalIataCode: null,
  }
}
