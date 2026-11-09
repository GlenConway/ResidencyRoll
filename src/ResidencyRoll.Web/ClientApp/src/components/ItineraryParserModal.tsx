import { useState } from 'react'
import { parseItinerary } from '../api/client.ts'
import type { ItineraryParsingResponse } from '../api/types.ts'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'

const placeholder = `Paste your flight itinerary here...

Example formats:
• AC099 23JAN YHZ YUL 1640 1805
• Flight AC 123 from Halifax to Montreal on January 23, 2026 departing 4:40 PM arriving 6:05 PM
• Multiple flights, one per line`

export function ItineraryParserModal({
  open,
  onClose,
  onParsed,
}: {
  open: boolean
  onClose: () => void
  onParsed: (response: ItineraryParsingResponse) => void
}) {
  const [text, setText] = useState('')
  const [error, setError] = useState('')
  const [pending, setPending] = useState(false)

  async function parse() {
    if (!text.trim()) {
      setError('Please paste some itinerary text first.')
      return
    }
    setPending(true)
    setError('')
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
      setText('')
      onClose()
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Unable to parse the itinerary.')
    } finally {
      setPending(false)
    }
  }

  return (
    <Dialog open={open} onOpenChange={(next) => { if (!next) onClose() }}>
      <DialogContent className="sm:max-w-xl">
        <DialogHeader>
          <DialogTitle>Quick Itinerary Parser</DialogTitle>
          <DialogDescription>Paste a booking confirmation or a list of flights. AI extracts each leg.</DialogDescription>
        </DialogHeader>
        <div className="flex flex-col gap-2">
          <Label htmlFor="itinerary-text">Itinerary</Label>
          <Textarea id="itinerary-text" rows={10} value={text} placeholder={placeholder} onChange={(event) => setText(event.target.value)} />
        </div>
        {error ? <p className="text-sm text-destructive">{error}</p> : null}
        {pending ? <p className="text-sm text-muted-foreground">Analyzing your itinerary with AI...</p> : null}
        <DialogFooter>
          <Button type="button" variant="outline" onClick={onClose}>
            Close
          </Button>
          <Button type="button" variant="outline" onClick={() => { setText(''); setError('') }}>
            Clear
          </Button>
          <Button type="button" disabled={pending} onClick={() => void parse()}>
            Parse Itinerary
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
