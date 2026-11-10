import type { Traveller } from '../api/types.ts'

export const travellerStorageKey = 'residencyroll_traveller_id'

export function storedTravellerId(): number | null {
  const raw = window.localStorage.getItem(travellerStorageKey)
  if (raw == null || raw.length === 0) {
    return null
  }

  const id = Number(raw)
  return Number.isInteger(id) ? id : null
}

export function rememberTraveller(id: number): void {
  window.localStorage.setItem(travellerStorageKey, String(id))
}

export function chooseTraveller(travellers: readonly Traveller[], storedId: number | null): Traveller | null {
  if (travellers.length === 0) {
    return null
  }

  const stored = travellers.find((traveller) => traveller.id === storedId)
  if (stored) {
    return stored
  }

  return travellers.find((traveller) => traveller.isSelf) ?? travellers[0] ?? null
}

export function withTraveller(path: string, travellerId: number | null | undefined): string {
  if (travellerId == null) {
    return path
  }

  const join = path.includes('?') ? '&' : '?'
  return `${path}${join}travellerId=${travellerId}`
}
