import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { HomePage } from './HomePage.tsx'

describe('HomePage', () => {
  beforeEach(() => {
    window.localStorage.removeItem('residencyroll_home_country')
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL) => {
        const url = String(input)
        if (url.includes('residency-summary')) {
          return Promise.resolve(
            Response.json([
              {
                countryName: 'France',
                totalDays: 190,
                ruleType: 'Midnight',
                thresholdDays: 183,
                isApproachingThreshold: false,
                daysUntilThreshold: 0,
              },
            ]),
          )
        }
        if (url.includes('daily-presence')) {
          return Promise.resolve(
            Response.json([
              {
                date: '2026-03-20',
                locationAtMidnight: 'France',
                locationsDuringDay: ['France', 'Canada'],
                isInTransitAtMidnight: false,
              },
              {
                date: '2026-03-19',
                locationAtMidnight: 'Canada',
                locationsDuringDay: ['Canada'],
                isInTransitAtMidnight: false,
              },
            ]),
          )
        }
        return Promise.resolve(Response.json([]))
      }),
    )
  })

  it('shows residency cards and groups the timeline by month', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    render(
      <QueryClientProvider client={client}>
        <HomePage />
      </QueryClientProvider>,
    )

    expect(await screen.findByRole('heading', { name: 'France' })).toBeInTheDocument()
    expect(screen.getByText('⛔ Over Threshold')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'March 2026' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /CA FR/ })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /CA$/ })).toHaveStyle({ backgroundColor: '#f5f5f5' })
  })
})
