import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { AirportSelector } from './AirportSelector.tsx'

describe('AirportSelector', () => {
  it('shows the city caption and the current offset', () => {
    render(
      <AirportSelector
        id="departure"
        label="Departure"
        city="Toronto"
        country="Canada"
        timezone="America/Toronto"
        iataCode="YYZ"
        onChange={() => undefined}
      />,
    )

    expect(screen.getByText('Departure')).toBeInTheDocument()
    expect(screen.getByText('Toronto, Canada')).toBeInTheDocument()
    expect(screen.getByText(/^UTC[+-]/)).toBeInTheDocument()
  })

  it('clears the IATA code when manual timezone is turned on', async () => {
    const onChange = vi.fn()
    render(
      <AirportSelector
        id="departure"
        label="Departure"
        city="Toronto"
        country="Canada"
        timezone="America/Toronto"
        iataCode="YYZ"
        onChange={onChange}
      />,
    )

    await userEvent.click(screen.getByRole('checkbox', { name: /Manual timezone/ }))
    expect(onChange).toHaveBeenCalledWith({
      city: 'Toronto',
      country: 'Canada',
      timezone: 'America/Toronto',
      iataCode: null,
    })
  })
})
