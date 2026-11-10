import { describe, expect, it } from 'vitest'
import { airportsForQuery, findByIataCode, getAllAirports, searchAirports } from './airports.ts'

describe('airports', () => {
  it('includes the bundled catalogue', () => {
    expect(getAllAirports().length).toBeGreaterThan(5000)
    expect(findByIataCode('OOL')?.city).toBe('Gold Coast')
    expect(findByIataCode('CBR')?.city).toBe('Canberra')
    expect(findByIataCode('CMN')?.city).toBe('Casablanca')
  })

  it('finds an airport by IATA code ignoring case', () => {
    expect(findByIataCode('yyz')?.city).toBe('Toronto')
    expect(findByIataCode('NOPE')).toBeUndefined()
  })

  it('searches across code, city, name, and country', () => {
    const matches = searchAirports('pearson')
    expect(matches[0]?.iataCode).toBe('YYZ')
    expect(searchAirports('united kingdom').every((airport) => airport.country === 'United Kingdom')).toBe(true)
  })

  it('ranks an exact IATA code ahead of other matches', () => {
    expect(searchAirports('YUL')[0]?.iataCode).toBe('YUL')
  })

  it('shows the first 20 airports before a query and at most 50 after', () => {
    expect(airportsForQuery('')).toHaveLength(20)
    expect(airportsForQuery('a').length).toBeLessThanOrEqual(50)
  })
})
