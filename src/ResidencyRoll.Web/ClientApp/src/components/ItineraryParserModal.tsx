import { ComposedModal, ModalHeader, ModalBody, ModalFooter, TextArea, Button, InlineLoading, InlineNotification } from '@carbon/react'
import { useState } from 'react'
import { parseItinerary } from '../api/client.ts'
import type { ItineraryParsingResponse } from '../api/types.ts'

const placeholder =
  'Paste your flight itinerary here... (e.g., AC099 YHZ to YUL Jan 23 2026 4:40 PM, AC100 YUL to YHZ Jan 30 2026 2:15 PM)'

interface ItineraryParserModalProps {
  open: boolean
  onClose: () => void
  onParsed: (response: ItineraryParsingResponse) => void
}

export function ItineraryParserModal({ open, onClose, onParsed }: ItineraryParserModalProps) {
  const [text, setText] = useState('')
  const [error, setError] = useState('')
  const [parsing, setParsing] = useState(false)

  async function onParse() {
    setError('')
    if (text.trim().length === 0) {
      setError('Please paste some itinerary text first.')
      return
    }

    setParsing(true)
    try {
      const response = await parseItinerary(text)
      if (response.error) {
        setError(response.error)
        return
      }

      if (response.legs.length === 0) {
        setError('No flight legs could be extracted from the provided itinerary text. Please check the format and try again.')
        return
      }

      onParsed(response)
      onClose()
    } catch (caught) {
      setError(caught instanceof Error ? `Error parsing itinerary: ${caught.message}` : 'Error parsing itinerary.')
    } finally {
      setParsing(false)
    }
  }

  return (
    <ComposedModal open={open} onClose={onClose}>
      <ModalHeader title="Quick Itinerary Parser" />
      <ModalBody>
        <p>Paste flight information from emails, booking confirmations, or any text format. We'll auto-fill your trip legs.</p>
        <TextArea
          id="itinerary-text"
          labelText="Itinerary"
          placeholder={placeholder}
          value={text}
          disabled={parsing}
          onChange={(event) => setText(event.target.value)}
        />
        {parsing ? <InlineLoading description="Analyzing your itinerary with AI..." /> : null}
        {error ? <InlineNotification kind="error" title="Parsing Error:" subtitle={error} hideCloseButton /> : null}
      </ModalBody>
      <ModalFooter>
        <Button kind="secondary" disabled={parsing} onClick={onClose}>
          Close
        </Button>
        <Button
          kind="secondary"
          disabled={parsing}
          onClick={() => {
            setText('')
            setError('')
          }}
        >
          Clear
        </Button>
        <Button disabled={parsing} onClick={() => void onParse()}>
          Parse Itinerary
        </Button>
      </ModalFooter>
    </ComposedModal>
  )
}
