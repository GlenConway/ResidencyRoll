import { afterEach, describe, expect, it, vi } from 'vitest'
import { apiFetch, downloadFile, filenameFromContentDisposition, navigation } from './client.ts'

describe('apiFetch', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
  })

  it('adds the CSRF header on requests that change data', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response('{}', { status: 200 }))
    vi.stubGlobal('fetch', fetchMock)

    await apiFetch('/api/v1/trips', { method: 'POST', body: JSON.stringify({ id: 1 }) })

    const [, init] = fetchMock.mock.calls[0] as [string, RequestInit]
    expect(new Headers(init.headers).get('X-CSRF')).toBe('1')
  })

  it('does not add the CSRF header on GET', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response('[]', { status: 200 }))
    vi.stubGlobal('fetch', fetchMock)

    await apiFetch('/api/v1/trips')

    const [, init] = fetchMock.mock.calls[0] as [string, RequestInit]
    expect(new Headers(init.headers).has('X-CSRF')).toBe(false)
  })

  it('sends the browser to login when the API returns 401', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('', { status: 401 })))
    const redirect = vi.spyOn(navigation, 'redirect').mockImplementation(() => undefined)
    window.history.pushState({}, '', '/manage-trips?tab=1')

    await expect(apiFetch('/api/v1/trips')).rejects.toThrow('Unauthorized')
    expect(redirect).toHaveBeenCalledWith('/auth/login?returnUrl=%2Fmanage-trips%3Ftab%3D1')
  })
})

describe('filenameFromContentDisposition', () => {
  it('reads the filename the server sends', () => {
    expect(filenameFromContentDisposition('attachment; filename="trips_20260101.csv"')).toBe('trips_20260101.csv')
    expect(filenameFromContentDisposition("attachment; filename*=UTF-8''daily_presence.csv")).toBe('daily_presence.csv')
    expect(filenameFromContentDisposition(null)).toBe('download')
  })
})

describe('downloadFile', () => {
  it('saves the response using the content-disposition filename', async () => {
    const response = new Response('a,b', {
      status: 200,
      headers: { 'Content-Disposition': 'attachment; filename="trips.csv"' },
    })
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(response))
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:trips')
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined)
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined)

    await downloadFile('/api/v1/trips/export')

    expect(click).toHaveBeenCalled()
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
  })
})
