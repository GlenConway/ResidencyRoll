import { describe, expect, it } from 'vitest'
import {
  addForecastLeg,
  legLabel,
  legsFromParsed,
  primaryDestination,
  starterLeg,
  totalAway,
  tripDurationDays,
  validationIssues,
  type ForecastLeg,
} from './forecastPlan.ts'

function leg(overrides: Partial<ForecastLeg> = {}): ForecastLeg {
  return {
    ...starterLeg(new Date(2026, 2, 1), 0),
    departureCountry: 'Canada',
    departureCity: 'Halifax',
    departureTimezone: 'America/Halifax',
    arrivalCountry: 'United Kingdom',
    arrivalCity: 'London',
    arrivalTimezone: 'Europe/London',
    departureDate: '2026-04-01',
    departureTime: '12:00',
    arrivalDate: '2026-04-01',
    arrivalTime: '18:00',
    ...overrides,
  }
}

describe('forecast planning', () => {
  it('labels the first, middle, and last legs', () => {
    expect(legLabel(0, 3)).toBe('Outbound')
    expect(legLabel(1, 3)).toBe('Connecting')
    expect(legLabel(2, 3)).toBe('Return')
    expect(legLabel(0, 1)).toBe('Outbound')
  })

  it('names the missing fields on each leg', () => {
    const blank = starterLeg(new Date(2026, 0, 15))
    expect(validationIssues([blank])).toEqual(['Leg 1: missing arrival country'])
  })

  it('counts UTC days from the first arrival to the last departure', () => {
    const outbound = leg({ arrivalDate: '2026-04-01', arrivalTime: '22:00', arrivalTimezone: 'America/Halifax' })
    const returning = leg({
      id: 1,
      departureDate: '2026-04-11',
      departureTime: '02:00',
      departureTimezone: 'Europe/London',
      arrivalCountry: 'Canada',
    })
    expect(tripDurationDays([outbound, returning])).toBe(9)
  })

  it('picks the country with the longest stay as the primary destination', () => {
    const outbound = leg({ arrivalCountry: 'France', arrivalDate: '2026-04-01', arrivalTime: '10:00' })
    const connecting = leg({
      id: 1,
      departureDate: '2026-04-03',
      departureTime: '10:00',
      arrivalCountry: 'Japan',
      arrivalDate: '2026-04-04',
      arrivalTime: '10:00',
    })
    const returning = leg({
      id: 2,
      departureDate: '2026-04-20',
      departureTime: '10:00',
      arrivalDate: '2026-04-20',
      arrivalTime: '18:00',
      arrivalCountry: 'Canada',
    })
    expect(primaryDestination([outbound, connecting, returning])).toBe('Japan')
  })

  it('excludes the home country from Total Away', () => {
    expect(totalAway([{ countryName: 'Canada', days: 200 }, { countryName: 'France', days: 40 }], 'Canada')).toBe(40)
  })

  it('copies the previous arrival into the next departure and replaces legs from a parsed itinerary', () => {
    const next = addForecastLeg([leg()])
    expect(next[1]).toMatchObject({ departureCity: 'London', departureTime: '18:00', arrivalTime: '12:00' })

    const parsed = legsFromParsed(
      [
        {
          departure_airport: 'ZZZ',
          departure_datetime_local: 'not-a-date',
          arrival_airport: 'YUL',
          arrival_datetime_local: '',
        },
      ],
      new Date(2026, 0, 15),
    )
    expect(parsed[0]).toMatchObject({
      departureCity: '',
      departureTimezone: 'UTC',
      departureDate: '2026-02-15',
      departureTime: '12:00',
      arrivalCity: 'Montreal',
      arrivalTime: '14:00',
    })
  })
})
