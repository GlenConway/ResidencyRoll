import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ForecastPage } from './ForecastPage.tsx'

describe('ForecastPage', () => {
  beforeEach(() => {
    window.localStorage.setItem('residencyroll_home_country', 'Canada')
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL) => {
        const url = String(input)
        if (url.includes('availability')) return Promise.resolve(Response.json(false))
        return Promise.resolve(
          Response.json({
            current: [{ countryName: 'Canada', days: 100 }],
            forecast: [
              { countryName: 'France', days: 190 },
              { countryName: 'Canada', days: 80 },
            ],
          }),
        )
      }),
    )
  })

  it('reports a leg that has no arrival country', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    render(
      <QueryClientProvider client={client}>
        <ForecastPage />
      </QueryClientProvider>,
    )

    await userEvent.click(screen.getByRole('button', { name: 'Calculate Forecast' }))
    expect(await screen.findByText('Leg 1: missing arrival country')).toBeInTheDocument()
  })
})
