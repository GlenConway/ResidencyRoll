import { renderHook, act, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { defaultHomeCountry, homeCountryStorageKey, useHomeCountry } from './homeCountry.ts'

describe('useHomeCountry', () => {
  beforeEach(() => {
    window.localStorage.clear()
    vi.stubGlobal(
      'fetch',
      vi.fn(async (_input: RequestInfo | URL, init?: RequestInit) => {
        if (init?.method === 'PUT') {
          return Response.json({ homeCountry: 'Canada' })
        }

        return Response.json({ homeCountry: null })
      }),
    )
  })

  it('reads and writes the residencyroll_home_country key', () => {
    const { result } = renderHook(() => useHomeCountry())
    expect(result.current[0]).toBeNull()

    act(() => result.current[1]('Canada'))

    expect(result.current[0]).toBe('Canada')
    expect(window.localStorage.getItem(homeCountryStorageKey)).toBe('Canada')
  })

  it('uses the saved profile country and copies a browser value up when the profile is empty', async () => {
    window.localStorage.setItem(homeCountryStorageKey, 'Japan')
    const fetchMock = vi.fn(async (_input: RequestInfo | URL, init?: RequestInit) => {
      if (init?.method === 'PUT') {
        return Response.json({ homeCountry: 'Japan' })
      }

      return Response.json({ homeCountry: null })
    })
    vi.stubGlobal('fetch', fetchMock)

    renderHook(() => useHomeCountry())

    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2))
    const put = fetchMock.mock.calls.find((call) => call[1]?.method === 'PUT')
    expect(put?.[1]?.body).toBe(JSON.stringify({ homeCountry: 'Japan' }))
  })

  it('defaults to the first country alphabetically when the saved value is missing', () => {
    expect(defaultHomeCountry(['Japan', 'Canada'], null)).toBe('Canada')
    expect(defaultHomeCountry(['Japan', 'Canada'], 'Japan')).toBe('Japan')
    expect(defaultHomeCountry(['Japan', 'Canada'], 'France')).toBe('Canada')
  })
})
