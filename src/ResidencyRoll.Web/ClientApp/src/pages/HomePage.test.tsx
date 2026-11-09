import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import type { DailyPresence } from '../api/types.ts'
import { CountryColorService } from '../lib/countryColor.ts'
import { presentDay } from '../lib/residencyView.ts'
import { CountryCard } from './HomePage.tsx'

describe('CountryCard', () => {
  it('shows the over-threshold badge for a country that is not home', () => {
    render(
      <CountryCard
        homeCountry="Canada"
        summary={{
          countryName: 'France',
          totalDays: 190,
          ruleType: 'Midnight',
          thresholdDays: 183,
          isApproachingThreshold: false,
          daysUntilThreshold: 0,
        }}
      />,
    )

    expect(screen.getByRole('heading', { name: 'France' })).toBeInTheDocument()
    expect(screen.getByText('⛔ Over Threshold')).toBeInTheDocument()
    expect(screen.getByText('Midnights')).toBeInTheDocument()
  })

  it('shows the home country badge', () => {
    render(
      <CountryCard
        homeCountry="Canada"
        summary={{
          countryName: 'Canada',
          totalDays: 300,
          ruleType: 'PartialDay',
          thresholdDays: 183,
          isApproachingThreshold: false,
          daysUntilThreshold: 0,
        }}
      />,
    )

    expect(screen.getByText('Home Country')).toBeInTheDocument()
    expect(screen.getByText('Partial Days')).toBeInTheDocument()
  })
})

describe('multi-country day cell', () => {
  it('stacks country codes for a partial day', async () => {
    const day: DailyPresence = {
      date: '2026-03-20',
      locationAtMidnight: 'France',
      locationsDuringDay: ['France', 'Canada'],
      isInTransitAtMidnight: false,
    }
    const colors = new CountryColorService()
    colors.registerCountries(['Canada', 'France'])
    const presented = presentDay(day, colors, 'Canada')

    render(
      <button type="button" title={presented.tooltip}>
        {presented.codes.join(' ')}
      </button>,
    )

    const cell = screen.getByRole('button', { name: 'CA FR' })
    expect(cell).toHaveAttribute('title', expect.stringContaining('Canada & France'))
    await userEvent.click(cell)
  })
})
