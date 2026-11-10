import { describe, expect, it } from 'vitest'
import type { Traveller } from '../api/types.ts'
import { chooseTraveller, withTraveller } from './travellers.ts'

const self: Traveller = { id: 1, name: 'Me', homeCountry: 'Canada', isSelf: true }
const sam: Traveller = { id: 2, name: 'Sam', homeCountry: null, isSelf: false }

describe('chooseTraveller', () => {
  it('uses the stored id when that traveller is still in the list', () => {
    expect(chooseTraveller([self, sam], 2)).toBe(sam)
  })

  it('falls back to the signed-in traveller', () => {
    expect(chooseTraveller([sam, self], 99)?.id).toBe(1)
  })

  it('returns null when there are no travellers', () => {
    expect(chooseTraveller([], 1)).toBeNull()
  })
})

describe('withTraveller', () => {
  it('leaves an unscoped path unchanged', () => {
    expect(withTraveller('/api/v1/trips', null)).toBe('/api/v1/trips')
  })

  it('appends the id to a path that already has a query', () => {
    expect(withTraveller('/api/v1/trips?startDate=2026-01-01', 4)).toBe('/api/v1/trips?startDate=2026-01-01&travellerId=4')
  })
})
