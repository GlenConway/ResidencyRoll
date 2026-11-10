import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { MemoryRouter } from 'react-router'
import App from '../App.tsx'
import type { Traveller } from '../api/types.ts'
import { ManageTripsPage } from '../pages/ManageTripsPage.tsx'
import { travellerStorageKey } from '../lib/travellers.ts'
import { TravellerProvider } from './travellers.tsx'

const self: Traveller = { id: 1, name: 'Me', homeCountry: 'Canada', isSelf: true }
const sam: Traveller = { id: 2, name: 'Sam', homeCountry: null, isSelf: false }

const trip = {
  id: 8,
  departureCountry: 'Canada',
  departureCity: 'Halifax',
  departureDateTime: '2026-03-20T12:00:00',
  departureTimezone: 'America/Halifax',
  departureIataCode: 'YHZ',
  arrivalCountry: 'Canada',
  arrivalCity: 'Montreal',
  arrivalDateTime: '2026-03-20T14:00:00',
  arrivalTimezone: 'America/Toronto',
  arrivalIataCode: 'YUL',
}

describe('traveller picker', () => {
  beforeEach(() => {
    window.localStorage.removeItem(travellerStorageKey)
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL) => {
        const url = String(input)
        if (url.endsWith('/bff/user')) {
          return Promise.resolve(Response.json({ name: 'Ada Lovelace', email: 'ada@example.com', isAuthenticated: true }))
        }
        if (url.endsWith('/api/v1/travellers')) {
          return Promise.resolve(Response.json([self, sam]))
        }
        return Promise.resolve(Response.json([]))
      }),
    )
  })

  it('remembers the selected traveller', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    render(
      <QueryClientProvider client={client}>
        <MemoryRouter>
          <App />
        </MemoryRouter>
      </QueryClientProvider>,
    )

    const picker = await screen.findByRole('combobox', { name: 'Traveller' })
    expect(picker).toHaveValue('1')
    await userEvent.selectOptions(picker, '2')
    expect(window.localStorage.getItem(travellerStorageKey)).toBe('2')
    expect(picker).toHaveValue('2')
  })
})

describe('saving a trip for several travellers', () => {
  beforeEach(() => {
    window.localStorage.removeItem(travellerStorageKey)
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
        const url = String(input)
        const method = init?.method ?? 'GET'
        if (url.endsWith('/api/v1/travellers')) return Promise.resolve(Response.json([self, sam]))
        if (url.endsWith('/parse-itinerary/availability')) return Promise.resolve(Response.json(false))
        if (url.includes('/api/v1/trips') && method === 'POST') return Promise.resolve(Response.json(trip))
        if (url.endsWith('/api/v1/trips') && method === 'GET') return Promise.resolve(Response.json([]))
        return Promise.resolve(Response.json([]))
      }),
    )
  })

  it('posts one save with every checked traveller', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    render(
      <QueryClientProvider client={client}>
        <TravellerProvider>
          <ManageTripsPage />
        </TravellerProvider>
      </QueryClientProvider>,
    )

    await screen.findByRole('heading', { name: /Manage Trips/ })
    await userEvent.click(screen.getByRole('button', { name: 'Add Trip' }))
    await chooseAirport('Departure Airport', 'YHZ')
    await chooseAirport('Arrival Airport', 'YUL')
    await userEvent.click(screen.getByRole('checkbox', { name: 'Sam' }))
    await userEvent.click(screen.getByRole('button', { name: 'Save Trip' }))

    const post = vi.mocked(fetch).mock.calls.find((call) => (call[1]?.method ?? 'GET') === 'POST' && String(call[0]).includes('/api/v1/trips'))
    expect(String(post?.[0])).toContain('travellerIds=1')
    expect(String(post?.[0])).toContain('travellerIds=2')
  })
})

async function chooseAirport(label: string, code: string) {
  const box = screen.getByRole('combobox', { name: label })
  await userEvent.click(box)
  await userEvent.type(box, code)
  await userEvent.click(await screen.findByRole('option', { name: new RegExp(code) }))
}
