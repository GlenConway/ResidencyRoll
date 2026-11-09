import type { CurrentUser, DailyPresence, ForecastResponse, ImportResult, ItineraryParsingResponse, McpInfo, ResidencySummary, SystemInfo, Trip, TripLeg } from './types.ts'

export class ApiError extends Error {
  readonly status: number

  constructor(message: string, status: number) {
    super(message)
    this.status = status
  }
}

export const navigation = {
  redirect(url: string) {
    window.location.assign(url)
  },
}

function csrfRequired(method: string): boolean {
  const normalized = method.toUpperCase()
  return normalized !== 'GET' && normalized !== 'HEAD' && normalized !== 'OPTIONS' && normalized !== 'TRACE'
}

export async function apiFetch(path: string, init: RequestInit = {}): Promise<Response> {
  const headers = new Headers(init.headers)
  const method = init.method ?? 'GET'
  if (csrfRequired(method)) {
    headers.set('X-CSRF', '1')
  }

  if (init.body != null && !(init.body instanceof FormData) && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json')
  }

  const response = await fetch(path, { ...init, headers, credentials: 'same-origin' })
  if (response.status === 401) {
    const returnUrl = `${window.location.pathname}${window.location.search}`
    navigation.redirect(`/auth/login?returnUrl=${encodeURIComponent(returnUrl)}`)
    throw new ApiError('Unauthorized', 401)
  }

  return response
}

async function readJson<T>(response: Response): Promise<T> {
  if (!response.ok) {
    throw new ApiError(await response.text(), response.status)
  }

  return (await response.json()) as T
}

export function filenameFromContentDisposition(header: string | null): string {
  if (header == null) {
    return 'download'
  }

  const encoded = /filename\*=UTF-8''([^;]+)/i.exec(header)
  if (encoded?.[1]) {
    return decodeURIComponent(encoded[1])
  }

  const plain = /filename="?([^";]+)"?/i.exec(header)
  return plain?.[1] ?? 'download'
}

export async function downloadFile(path: string): Promise<void> {
  const response = await apiFetch(path)
  if (!response.ok) {
    throw new ApiError(await response.text(), response.status)
  }

  const filename = filenameFromContentDisposition(response.headers.get('Content-Disposition'))
  const blob = await response.blob()
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = filename
  document.body.append(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}

export async function getCurrentUser(): Promise<CurrentUser> {
  return readJson<CurrentUser>(await apiFetch('/bff/user'))
}

export async function getTrips(): Promise<Trip[]> {
  return readJson<Trip[]>(await apiFetch('/api/v1/trips'))
}

export async function getTrip(id: number): Promise<Trip> {
  return readJson<Trip>(await apiFetch(`/api/v1/trips/${id}`))
}

export async function createTrip(trip: Omit<Trip, 'id'>): Promise<Trip> {
  return readJson<Trip>(await apiFetch('/api/v1/trips', { method: 'POST', body: JSON.stringify(trip) }))
}

export async function updateTrip(id: number, trip: Trip): Promise<void> {
  const response = await apiFetch(`/api/v1/trips/${id}`, { method: 'PUT', body: JSON.stringify(trip) })
  if (!response.ok) {
    throw new ApiError(await response.text(), response.status)
  }
}

export async function deleteTrip(id: number): Promise<void> {
  const response = await apiFetch(`/api/v1/trips/${id}`, { method: 'DELETE' })
  if (!response.ok) {
    throw new ApiError(await response.text(), response.status)
  }
}

export async function getResidencySummary(startDate: string, endDate: string): Promise<ResidencySummary[]> {
  const params = new URLSearchParams({ startDate, endDate })
  return readJson<ResidencySummary[]>(await apiFetch(`/api/v1/trips/residency-summary?${params}`))
}

export async function getDailyPresence(startDate: string, endDate: string): Promise<DailyPresence[]> {
  const params = new URLSearchParams({ startDate, endDate })
  return readJson<DailyPresence[]>(await apiFetch(`/api/v1/trips/daily-presence?${params}`))
}

export function downloadDailyPresence(startDate: string, endDate: string): Promise<void> {
  const params = new URLSearchParams({ startDate, endDate })
  return downloadFile(`/api/v1/trips/daily-presence-export?${params}`)
}

export function downloadTrips(): Promise<void> {
  return downloadFile('/api/v1/trips/export')
}

export async function importTrips(file: File): Promise<ImportResult> {
  const body = new FormData()
  body.append('file', file)
  return readJson<ImportResult>(await apiFetch('/api/v1/trips/import', { method: 'POST', body }))
}

export async function getItineraryParserAvailability(): Promise<boolean> {
  return readJson<boolean>(await apiFetch('/api/v1/trips/parse-itinerary/availability'))
}

export async function parseItinerary(itineraryText: string): Promise<ItineraryParsingResponse> {
  return readJson<ItineraryParsingResponse>(
    await apiFetch('/api/v1/trips/parse-itinerary', {
      method: 'POST',
      body: JSON.stringify({ itineraryText }),
    }),
  )
}

export async function forecast(legs: TripLeg[]): Promise<ForecastResponse> {
  return readJson<ForecastResponse>(
    await apiFetch('/api/v1/trips/forecast', { method: 'POST', body: JSON.stringify({ legs }) }),
  )
}

export async function getMcpInfo(): Promise<McpInfo> {
  return readJson<McpInfo>(await apiFetch('/bff/mcp-info'))
}

export async function getSystemInfo(): Promise<SystemInfo> {
  return readJson<SystemInfo>(await apiFetch('/api/v1/system/info'))
}

export async function backupNow(): Promise<{ fileName: string }> {
  return readJson<{ fileName: string }>(await apiFetch('/api/v1/system/backup', { method: 'POST' }))
}

export function downloadBackup(fileName: string): Promise<void> {
  return downloadFile(`/api/v1/system/backups/${encodeURIComponent(fileName)}`)
}
