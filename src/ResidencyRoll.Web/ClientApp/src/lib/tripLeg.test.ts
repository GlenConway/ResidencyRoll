import { describe, expect, it } from 'vitest'
import { createNextLeg } from './tripLeg.ts'

describe('createNextLeg', () => {
  it('uses the arrival as the next departure', () => {
    const next = createNextLeg(
      {
        arrivalCountry: 'Canada',
        arrivalCity: 'Toronto',
        arrivalIataCode: 'YYZ',
        arrivalTimezone: 'America/Toronto',
        arrivalDateTime: new Date(2024, 7, 1, 14, 0, 0),
      },
      2 * 60 * 60 * 1000,
      3 * 60 * 60 * 1000,
    )

    expect(next.departureCountry).toBe('Canada')
    expect(next.departureCity).toBe('Toronto')
    expect(next.departureIataCode).toBe('YYZ')
    expect(next.departureDateTime).toEqual(new Date(2024, 7, 1, 16, 0, 0))
    expect(next.arrivalDateTime).toEqual(new Date(2024, 7, 1, 19, 0, 0))
    expect(next.departureTimezone).toBe('America/Toronto')
    expect(next.arrivalTimezone).toBe('America/Toronto')
  })

  it('blanks the arrival fields', () => {
    const next = createNextLeg({
      arrivalCountry: 'France',
      arrivalCity: 'Paris',
      arrivalIataCode: 'CDG',
      arrivalTimezone: 'Europe/Paris',
      arrivalDateTime: new Date(2024, 8, 10, 9, 30, 0),
    })

    expect(next.arrivalCountry).toBe('')
    expect(next.arrivalCity).toBe('')
    expect(next.arrivalIataCode).toBeNull()
  })

  it('falls back to UTC when the arrival timezone is missing', () => {
    const next = createNextLeg(
      {
        arrivalCountry: 'USA',
        arrivalCity: 'Chicago',
        arrivalIataCode: 'ORD',
        arrivalTimezone: '',
        arrivalDateTime: new Date(2024, 9, 5, 6, 0, 0),
      },
      60 * 60 * 1000,
    )

    expect(next.departureTimezone).toBe('UTC')
    expect(next.departureDateTime).toEqual(new Date(2024, 9, 5, 7, 0, 0))
  })

  it('keeps the saved arrival airport when adding the next leg', () => {
    const saved = {
      departureCountry: 'Canada',
      departureCity: 'Montreal',
      departureIataCode: 'YUL',
      departureTimezone: 'America/Toronto',
      departureDateTime: new Date(2024, 6, 15, 8, 0, 0),
      arrivalCountry: 'United Kingdom',
      arrivalCity: 'London',
      arrivalIataCode: 'LHR',
      arrivalTimezone: 'Europe/London',
      arrivalDateTime: new Date(2024, 6, 15, 20, 30, 0),
    }

    const next = createNextLeg(saved)
    expect(next.departureCountry).toBe('United Kingdom')
    expect(next.departureCity).toBe('London')
    expect(next.departureIataCode).toBe('LHR')
    expect(next.departureTimezone).toBe('Europe/London')
    expect(next.departureDateTime).toEqual(new Date(2024, 6, 15, 22, 30, 0))
    expect(next.arrivalDateTime).toEqual(new Date(2024, 6, 16, 0, 30, 0))
    expect(next.arrivalTimezone).toBe('Europe/London')
    expect(next.departureCountry).not.toBe('')
  })

  it('copies an empty arrival country through to the next departure', () => {
    const next = createNextLeg({
      arrivalCountry: '',
      arrivalCity: 'London',
      arrivalIataCode: 'LHR',
      arrivalTimezone: 'UTC',
      arrivalDateTime: new Date(2024, 6, 15, 20, 30, 0),
    })

    expect(next.departureCountry).toBe('')
    expect(next.departureTimezone).toBe('UTC')
  })
})
