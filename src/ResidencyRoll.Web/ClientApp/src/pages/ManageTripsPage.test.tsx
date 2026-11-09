import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ManageTripsPage } from './ManageTripsPage.tsx'

const trip = {
  id: 7,
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

describe('ManageTripsPage', () => {
  beforeEach(() => {
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
        const url = String(input)
        const method = init?.method ?? 'GET'
        if (url.endsWith('/parse-itinerary/availability')) return Promise.resolve(Response.json(false))
        if (url.endsWith('/api/v1/trips') && method === 'GET') return Promise.resolve(Response.json([trip]))
        if (url.endsWith('/api/v1/trips/7') && method === 'DELETE') return Promise.resolve(new Response(null, { status: 204 }))
        if (url.endsWith('/api/v1/trips/7') && method === 'GET') return Promise.resolve(Response.json(trip))
        return Promise.resolve(Response.json([]))
      }),
    )
  })

  it('lists a trip and asks before deleting it', async () => {
    renderPage()
    expect(await screen.findByText('YHZ - Halifax')).toBeInTheDocument()
    expect(screen.getByText(/UTC-0[34]:00/)).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Delete' }))
    expect(screen.getByText('Delete this trip from Halifax to Montreal?')).toBeInTheDocument()
    const confirm = screen.getAllByRole('button', { name: 'Delete' }).at(-1)
    await userEvent.click(confirm!)
    expect(fetch).toHaveBeenCalledWith(expect.stringContaining('/api/v1/trips/7'), expect.objectContaining({ method: 'DELETE' }))
  })

  it('keeps an incomplete new trip on the editor', async () => {
    renderPage()
    await screen.findByText('YHZ - Halifax')
    await userEvent.click(screen.getAllByRole('button', { name: 'Add Trip' })[0]!)
    await userEvent.click(screen.getByRole('button', { name: 'Save Trip' }))
    expect(screen.getByText(/Please complete the following required fields: Departure City/)).toBeInTheDocument()
  })
})

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <ManageTripsPage />
    </QueryClientProvider>,
  )
}
