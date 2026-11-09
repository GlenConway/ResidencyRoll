import { describe, expect, it } from 'vitest'
import { draftsFromParsedLegs, durationHours, newTripDraft, syncArrivalDate, validationMessage } from './tripEditor.ts'

describe('trip editor', () => {
  it('names every missing required field', () => {
    const draft = newTripDraft(new Date(2026, 2, 20))
    expect(validationMessage(draft)).toBe(
      'Please complete the following required fields: Departure City, Departure Country, Arrival City, Arrival Country',
    )
  })

  it('asks for dates before the other fields', () => {
    const draft = { ...newTripDraft(), departureDate: '' }
    expect(validationMessage(draft)).toBe('Please fill in all date and time fields.')
  })

  it('copies the departure date onto a new trip and leaves an existing trip alone', () => {
    const created = syncArrivalDate(newTripDraft(new Date(2026, 2, 1)), '2026-04-02')
    expect(created.arrivalDate).toBe('2026-04-02')

    const existing = syncArrivalDate({ ...newTripDraft(), id: 4, arrivalDate: '2026-05-01' }, '2026-04-02')
    expect(existing.arrivalDate).toBe('2026-05-01')
  })

  it('falls back to the airport code and UTC when the IATA code is unknown', () => {
    const result = draftsFromParsedLegs([
      {
        departure_airport: 'ZZZ',
        departure_datetime_local: '2026-03-20T09:00',
        arrival_airport: 'YYY',
        arrival_datetime_local: null,
      },
    ])
    expect(result.error).toBeNull()
    expect(result.drafts[0]).toMatchObject({
      departureCity: 'ZZZ',
      departureCountry: 'ZZZ',
      departureTimezone: 'UTC',
      arrivalCity: 'YYY',
      arrivalTimezone: 'UTC',
      arrivalDate: '2026-03-20',
      arrivalTime: '09:00',
    })
  })

  it('rejects a departure time that is not yyyy-MM-ddTHH:mm', () => {
    const result = draftsFromParsedLegs([
      {
        departure_airport: 'YHZ',
        departure_datetime_local: 'March 20',
        arrival_airport: 'YUL',
        arrival_datetime_local: '2026-03-20T12:00',
      },
    ])
    expect(result.error).toBe('Could not parse departure datetime: March 20')
  })

  it('counts whole UTC hours between the local departure and arrival', () => {
    expect(durationHours('2026-03-20T12:00:00', 'UTC', '2026-03-20T15:40:00', 'UTC')).toBe(3)
    expect(durationHours('2026-03-20T22:00:00', 'America/Halifax', '2026-03-21T01:00:00', 'America/Toronto')).toBe(4)
  })
})
