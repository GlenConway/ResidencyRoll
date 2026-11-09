import { describe, expect, it } from 'vitest'
import { getCountryCode } from './countryCode.ts'

describe('getCountryCode', () => {
  it('uses the known codes', () => {
    expect(getCountryCode('Canada')).toBe('CA')
    expect(getCountryCode('United States')).toBe('US')
    expect(getCountryCode('USA')).toBe('US')
    expect(getCountryCode('United Kingdom')).toBe('UK')
  })

  it('falls back to the first three letters', () => {
    expect(getCountryCode('Portugal')).toBe('POR')
    expect(getCountryCode('NZ')).toBe('NZ')
  })
})
