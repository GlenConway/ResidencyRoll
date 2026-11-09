import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ItineraryParserModal } from './ItineraryParserModal.tsx'

describe('ItineraryParserModal', () => {
  it('asks for text before parsing', async () => {
    render(<ItineraryParserModal open onClose={() => undefined} onParsed={() => undefined} />)
    await userEvent.click(screen.getByRole('button', { name: 'Parse Itinerary' }))
    expect(screen.getByText('Please paste some itinerary text first.')).toBeInTheDocument()
  })

  it('returns the parsed legs', async () => {
    const onParsed = vi.fn()
    const onClose = vi.fn()
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        Response.json({
          legs: [
            {
              departure_airport: 'YHZ',
              departure_datetime_local: '2026-01-23T16:40',
              arrival_airport: 'YUL',
              arrival_datetime_local: '2026-01-23T18:05',
            },
          ],
          error: null,
        }),
      ),
    )

    render(<ItineraryParserModal open onClose={onClose} onParsed={onParsed} />)
    await userEvent.type(screen.getByLabelText('Itinerary'), 'AC099 YHZ to YUL')
    await userEvent.click(screen.getByRole('button', { name: 'Parse Itinerary' }))

    expect(onParsed).toHaveBeenCalled()
    expect(onClose).toHaveBeenCalled()
    vi.unstubAllGlobals()
  })
})
