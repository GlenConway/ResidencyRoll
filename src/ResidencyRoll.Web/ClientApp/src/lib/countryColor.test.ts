import { describe, expect, it } from 'vitest'
import { assignCountryColors, CountryColorService, darkenColor } from './countryColor.ts'

describe('CountryColorService', () => {
  it('assigns palette colours in sorted country order', () => {
    const colors = assignCountryColors(['Japan', 'Canada', 'France'])
    expect(colors.get('Canada')).toBe('#1f77b4')
    expect(colors.get('France')).toBe('#ff7f0e')
    expect(colors.get('Japan')).toBe('#2ca02c')
  })

  it('uses gray for an unknown country and darkens an assigned colour', () => {
    const service = new CountryColorService()
    expect(service.getCountryColor('')).toBe('#808080')
    expect(darkenColor('#ffffff', 0.8)).toBe('#CCCCCC')
  })

  it('keeps the assignment stable until reset', () => {
    const service = new CountryColorService()
    service.registerCountries(['Canada', 'Japan'])
    const first = service.getCountryColor('Canada')
    service.reset()
    expect(service.homeCountry).toBeNull()
    expect(service.getCountryColor('Japan')).toBe(first)
  })
})
