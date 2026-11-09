import { renderHook, act } from '@testing-library/react'
import { beforeEach, describe, expect, it } from 'vitest'
import { defaultHomeCountry, homeCountryStorageKey, useHomeCountry } from './homeCountry.ts'

describe('useHomeCountry', () => {
  beforeEach(() => {
    window.localStorage.clear()
  })

  it('reads and writes the residencyroll_home_country key', () => {
    const { result } = renderHook(() => useHomeCountry())
    expect(result.current[0]).toBeNull()

    act(() => result.current[1]('Canada'))

    expect(result.current[0]).toBe('Canada')
    expect(window.localStorage.getItem(homeCountryStorageKey)).toBe('Canada')
  })

  it('defaults to the first country alphabetically when the saved value is missing', () => {
    expect(defaultHomeCountry(['Japan', 'Canada'], null)).toBe('Canada')
    expect(defaultHomeCountry(['Japan', 'Canada'], 'Japan')).toBe('Japan')
    expect(defaultHomeCountry(['Japan', 'Canada'], 'France')).toBe('Canada')
  })
})
