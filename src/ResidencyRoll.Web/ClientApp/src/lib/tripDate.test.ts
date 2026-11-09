import { describe, expect, it } from 'vitest'
import { getSyncedArrivalDate } from './tripDate.ts'

describe('getSyncedArrivalDate', () => {
  it('syncs the arrival date to the departure date for a new trip', () => {
    const departure = new Date(2024, 6, 10, 15, 30)
    const arrival = getSyncedArrivalDate(departure, null, false)
    expect(arrival).toEqual(new Date(2024, 6, 10))
  })

  it('does not override the arrival date of an existing trip', () => {
    const departure = new Date(2024, 6, 10)
    const existingArrival = new Date(2024, 6, 12)
    expect(getSyncedArrivalDate(departure, existingArrival, true)).toEqual(existingArrival)
  })

  it('leaves the arrival date alone when departure is empty', () => {
    const arrival = new Date(2024, 6, 12)
    expect(getSyncedArrivalDate(null, arrival, false)).toEqual(arrival)
  })
})
