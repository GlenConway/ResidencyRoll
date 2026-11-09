import { useCallback, useEffect, useState } from 'react'
import { getHomeCountry, setHomeCountry as saveHomeCountry } from '../api/client.ts'

export const homeCountryStorageKey = 'residencyroll_home_country'

export function readHomeCountry(): string | null {
  const value = window.localStorage.getItem(homeCountryStorageKey)
  return value == null || value.length === 0 ? null : value
}

export function writeHomeCountry(country: string): void {
  window.localStorage.setItem(homeCountryStorageKey, country)
}

export function defaultHomeCountry(countries: readonly string[], saved: string | null): string | null {
  if (countries.length === 0) {
    return saved
  }

  const sorted = [...countries].sort((left, right) => left.localeCompare(right))
  if (saved && sorted.includes(saved)) {
    return saved
  }

  return sorted[0] ?? null
}

export function useHomeCountry(): [string | null, (country: string) => void] {
  const [country, setCountry] = useState<string | null>(() => readHomeCountry())

  useEffect(() => {
    let cancelled = false
    void (async () => {
      try {
        const saved = await getHomeCountry()
        if (cancelled) {
          return
        }

        if (saved) {
          writeHomeCountry(saved)
          setCountry(saved)
          return
        }

        const local = readHomeCountry()
        if (local) {
          await saveHomeCountry(local)
        }
      } catch {
        // Keep the browser value when the profile API is unreachable.
      }
    })()

    return () => {
      cancelled = true
    }
  }, [])

  const update = useCallback((next: string) => {
    writeHomeCountry(next)
    setCountry(next)
    void saveHomeCountry(next).catch(() => {
      // The browser value still applies until the next successful save.
    })
  }, [])

  return [country, update]
}
