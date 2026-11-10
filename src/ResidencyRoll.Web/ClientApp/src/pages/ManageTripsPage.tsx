import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import {
  createTrip,
  deleteTrip,
  downloadTrips,
  getItineraryParserAvailability,
  getTrip,
  getTrips,
  importTrips,
  updateTrip,
} from '../api/client.ts'
import type { ItineraryParsingResponse, Trip } from '../api/types.ts'
import { AirportSelector } from '../components/AirportSelector.tsx'
import { Field } from '../components/Field.tsx'
import { ItineraryParserModal } from '../components/ItineraryParserModal.tsx'
import { formatUtcOffset } from '../lib/timezones.ts'
import {
  draftFromNextLeg,
  draftFromTrip,
  draftsFromParsedLegs,
  durationHours,
  formatTripWhen,
  newTripDraft,
  parsedDraftValidation,
  placeLabel,
  syncArrivalDate,
  toTripBody,
  validationMessage,
  type TripDraft,
} from '../lib/tripEditor.ts'
import { PageHeading, useTravellers } from '../shell/travellers.tsx'

export function ManageTripsPage() {
  const queryClient = useQueryClient()
  const { travellers, selected } = useTravellers()
  const travellerId = selected?.id
  const tripsQuery = useQuery({ queryKey: ['trips', travellerId ?? null], queryFn: () => getTrips(travellerId) })
  const parserQuery = useQuery({ queryKey: ['parser-availability'], queryFn: getItineraryParserAvailability })
  const [filter, setFilter] = useState('')
  const [showImport, setShowImport] = useState(false)
  const [importMessage, setImportMessage] = useState('')
  const [importError, setImportError] = useState(false)
  const [file, setFile] = useState<File | null>(null)
  const [tab, setTab] = useState('list')
  const [draft, setDraft] = useState<TripDraft | null>(null)
  const [adding, setAdding] = useState(false)
  const [validation, setValidation] = useState('')
  const [parsed, setParsed] = useState<TripDraft[]>([])
  const [parsedIndex, setParsedIndex] = useState(-1)
  const [parserOpen, setParserOpen] = useState(false)
  const [parserError, setParserError] = useState('')
  const [parserSuccess, setParserSuccess] = useState(false)
  const [pendingDelete, setPendingDelete] = useState<Trip | null>(null)
  const [targets, setTargets] = useState<number[]>([])

  const trips = useMemo(() => {
    const rows = Array.isArray(tripsQuery.data) ? [...tripsQuery.data] : []
    rows.sort((left, right) => right.departureDateTime.localeCompare(left.departureDateTime))
    const term = filter.trim().toLowerCase()
    if (!term) return rows
    return rows.filter((trip) =>
      [trip.departureCity, trip.arrivalCity, trip.departureIataCode, trip.arrivalIataCode]
        .filter(Boolean)
        .some((value) => value!.toLowerCase().includes(term)),
    )
  }, [tripsQuery.data, filter])

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['trips'] })

  function openNew() {
    setParsed([])
    setParsedIndex(-1)
    setParserError('')
    setParserSuccess(false)
    setValidation('')
    setDraft(newTripDraft())
    setTargets(selected ? [selected.id] : [])
    setAdding(true)
    setTab('editor')
  }

  function openEdit(trip: Trip) {
    setParsed([])
    setParsedIndex(-1)
    setValidation('')
    setDraft(draftFromTrip(trip))
    setAdding(false)
    setTab('editor')
  }

  function openLeg(trip: Trip) {
    setParsed([])
    setParsedIndex(-1)
    setValidation('')
    setDraft(draftFromNextLeg(trip))
    setAdding(false)
    setTab('editor')
  }

  function closeEditor() {
    setDraft(null)
    setAdding(false)
    setParsed([])
    setParsedIndex(-1)
    setValidation('')
    setTab('list')
  }

  function showParsed(index: number) {
    if (!draft) return
    const next = parsed.map((item, itemIndex) => (itemIndex === parsedIndex ? draft : item))
    setParsed(next)
    setParsedIndex(index)
    setDraft(next[index] ?? null)
  }

  async function save(addLegAfter: boolean) {
    if (!draft) return
    const message = validationMessage(draft)
    if (message) {
      setValidation(message)
      return
    }
    setValidation('')
    const body = toTripBody(draft)
    let saved: Trip
    if (draft.id > 0) {
      await updateTrip(draft.id, { ...body, id: draft.id }, travellerId)
      saved = await getTrip(draft.id, travellerId)
    } else {
      const ids = targets.length > 0 ? targets : selected ? [selected.id] : undefined
      saved = await createTrip(body, travellers.length > 0 ? ids : undefined)
    }
    await refresh()
    if (addLegAfter) {
      openLeg(saved)
      return
    }
    closeEditor()
  }

  async function saveParsed() {
    if (!draft) return
    const next = parsed.map((item, index) => (index === parsedIndex ? draft : item))
    const message = parsedDraftValidation(next)
    if (message) {
      setValidation(message)
      setParsed(next)
      return
    }
    const ids = targets.length > 0 ? targets : selected ? [selected.id] : undefined
    for (const item of next) {
      await createTrip(toTripBody(item), travellers.length > 0 ? ids : undefined)
    }
    await refresh()
    closeEditor()
  }

  function applyParsed(response: ItineraryParsingResponse) {
    setParserOpen(false)
    if (response.error) {
      setParserError(response.error)
      setParserSuccess(false)
      return
    }
    if (response.legs.length === 0) {
      setParserError('No flight legs could be extracted from the provided itinerary text. Please check the format and try again.')
      return
    }
    const result = draftsFromParsedLegs(response.legs)
    if (result.error || result.drafts.length === 0) {
      setParserError(result.error ?? 'No flight legs could be extracted from the provided itinerary text. Please check the format and try again.')
      return
    }
    setParsed(result.drafts)
    setParsedIndex(0)
    setDraft(result.drafts[0] ?? null)
    setParserError('')
    setParserSuccess(true)
  }

  const remove = useMutation({
    mutationFn: (trip: Trip) => deleteTrip(trip.id, travellerId),
    onSuccess: async () => {
      setPendingDelete(null)
      await refresh()
    },
  })

  const importing = useMutation({
    mutationFn: (file: File) => importTrips(file, travellerId),
    onSuccess: async (result) => {
      setImportMessage(result.message)
      setImportError(result.errors > 0 && result.imported === 0)
      await refresh()
    },
    onError: (error: Error) => {
      setImportMessage(`Error: ${error.message}`)
      setImportError(true)
    },
  })

  return (
    <section className="flex flex-col gap-4">
      <PageHeading title="Manage Trips" />
      <Tabs value={draft ? tab : 'list'} onValueChange={setTab}>
        <TabsList>
          <TabsTrigger value="list">Trip List</TabsTrigger>
          {draft ? <TabsTrigger value="editor">Trip Editor</TabsTrigger> : null}
        </TabsList>
        <TabsContent value="list" className="flex flex-col gap-4">
          <div className="flex flex-wrap items-center gap-2">
            <Button onClick={openNew}>Add Trip</Button>
            <Button variant="outline" onClick={() => setShowImport((value) => !value)}>
              Import / Export
            </Button>
            <Input
              className="max-w-xs"
              placeholder="Filter trips"
              value={filter}
              onChange={(event) => setFilter(event.target.value)}
            />
          </div>
          {showImport ? (
            <div className="flex flex-wrap items-center gap-2">
              <Button variant="outline" onClick={() => void downloadTrips(travellerId)}>
                Export Trips (CSV)
              </Button>
              <input
                aria-label="Trip CSV file"
                type="file"
                onChange={(event) => {
                  setFile(event.target.files?.[0] ?? null)
                  setImportMessage('')
                }}
              />
              <Button disabled={!file || importing.isPending} onClick={() => file && importing.mutate(file)}>
                Import
              </Button>
              {importMessage ? <span className={importError ? 'text-destructive' : 'text-primary'}>{importMessage}</span> : null}
            </div>
          ) : null}
          {tripsQuery.isLoading ? <p className="text-sm text-muted-foreground">Loading trips</p> : null}
          <div className="overflow-hidden rounded-lg border bg-card">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Departure</TableHead>
                <TableHead>Departure Time</TableHead>
                <TableHead>Arrival</TableHead>
                <TableHead>Arrival Time</TableHead>
                <TableHead>Duration</TableHead>
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {trips.map((trip) => (
                <TableRow key={trip.id}>
                  <TableCell>
                    <div>{placeLabel(trip.departureIataCode, trip.departureCity)}</div>
                    <div className="text-xs text-muted-foreground">{formatUtcOffset(trip.departureTimezone)}</div>
                    {trip.bookingRef || trip.source ? (
                      <div className="text-xs text-muted-foreground">{[trip.bookingRef, trip.source].filter(Boolean).join(' · ')}</div>
                    ) : null}
                  </TableCell>
                  <TableCell>{formatTripWhen(trip.departureDateTime)}</TableCell>
                  <TableCell>
                    <div>{placeLabel(trip.arrivalIataCode, trip.arrivalCity)}</div>
                    <div className="text-xs text-muted-foreground">{formatUtcOffset(trip.arrivalTimezone)}</div>
                  </TableCell>
                  <TableCell>{formatTripWhen(trip.arrivalDateTime)}</TableCell>
                  <TableCell>
                    {durationHours(trip.departureDateTime, trip.departureTimezone, trip.arrivalDateTime, trip.arrivalTimezone)} hours
                  </TableCell>
                  <TableCell className="space-x-1 text-right">
                    <Button size="sm" variant="ghost" onClick={() => openEdit(trip)}>
                      Edit
                    </Button>
                    <Button size="sm" variant="ghost" onClick={() => openLeg(trip)}>
                      Add Leg
                    </Button>
                    <Button size="sm" variant="ghost" className="text-destructive" onClick={() => setPendingDelete(trip)}>
                      Delete
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
          </div>
        </TabsContent>
        {draft ? (
          <TabsContent value="editor" className="flex flex-col gap-4">
            <h2 className="text-xl font-semibold">{draft.id > 0 ? 'Edit Trip' : 'Add New Trip'}</h2>
            {adding && parserQuery.isSuccess && parserQuery.data ? (
              <div className="flex flex-col gap-2">
                <h3 className="font-medium">Quick Itinerary Parser</h3>
                <Button variant="outline" className="w-fit" onClick={() => setParserOpen(true)}>
                  Parse Itinerary
                </Button>
                {parsed.length > 0 ? (
                  <p className="text-sm">
                    Parsed legs: {parsed.length} (Leg {parsedIndex + 1} selected)
                  </p>
                ) : null}
                {parserError ? (
                  <Alert variant="destructive">
                    <AlertDescription>{parserError}</AlertDescription>
                  </Alert>
                ) : null}
                {parserSuccess ? (
                  <Alert>
                    <AlertDescription>Itinerary loaded. Review each leg before saving.</AlertDescription>
                  </Alert>
                ) : null}
              </div>
            ) : null}
            {adding && parserQuery.isSuccess && !parserQuery.data ? (
              <Alert>
                <AlertDescription>Itinerary parser is unavailable until OpenAI configuration is set.</AlertDescription>
              </Alert>
            ) : null}
            <TripFields
              draft={draft}
              onChange={(next) => {
                setDraft(next)
              }}
            />
            {adding && travellers.length > 1 ? (
              <fieldset className="flex flex-col gap-2">
                <legend className="text-sm font-medium">Save for</legend>
                <div className="flex flex-wrap gap-4">
                  {travellers.map((person) => (
                    <label key={person.id} className="flex items-center gap-2 text-sm">
                      <input
                        type="checkbox"
                        checked={targets.includes(person.id)}
                        onChange={(event) => {
                          setTargets((current) =>
                            event.target.checked ? [...current, person.id] : current.filter((id) => id !== person.id),
                          )
                        }}
                      />
                      {person.name}
                    </label>
                  ))}
                </div>
              </fieldset>
            ) : null}
            {parsed.length > 0 ? (
              <div className="flex flex-wrap gap-2">
                <Button variant="ghost" disabled={parsedIndex <= 0} onClick={() => showParsed(parsedIndex - 1)}>
                  Previous Leg
                </Button>
                <Button variant="ghost" disabled={parsedIndex >= parsed.length - 1} onClick={() => showParsed(parsedIndex + 1)}>
                  Next Leg
                </Button>
                <Button onClick={() => void saveParsed()}>Save Parsed Legs</Button>
                <Button variant="ghost" onClick={closeEditor}>
                  Cancel
                </Button>
              </div>
            ) : (
              <div className="flex flex-wrap gap-2">
                <Button onClick={() => void save(false)}>Save Trip</Button>
                <Button variant="outline" onClick={() => void save(true)}>
                  Save and Add Leg
                </Button>
                <Button variant="ghost" onClick={closeEditor}>
                  Cancel
                </Button>
              </div>
            )}
            {validation ? <p className="text-sm text-destructive">{validation}</p> : null}
          </TabsContent>
        ) : null}
      </Tabs>
      <ItineraryParserModal open={parserOpen} onClose={() => setParserOpen(false)} onParsed={applyParsed} />
      <Dialog open={pendingDelete != null} onOpenChange={(open) => { if (!open) setPendingDelete(null) }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Delete trip</DialogTitle>
          </DialogHeader>
          <p>
            Delete this trip from {pendingDelete?.departureCity} to {pendingDelete?.arrivalCity}?
          </p>
          <DialogFooter>
            <Button variant="outline" onClick={() => setPendingDelete(null)}>
              Cancel
            </Button>
            <Button variant="destructive" onClick={() => pendingDelete && remove.mutate(pendingDelete)}>
              Delete
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </section>
  )
}

function TripFields({ draft, onChange }: { draft: TripDraft; onChange: (draft: TripDraft) => void }) {
  return (
    <div className="grid gap-4 rounded-lg border bg-card p-5 md:grid-cols-2">
      <div className="flex flex-col gap-3">
        <h3 className="font-medium">Departure Information</h3>
        <AirportSelector
          id="departure-airport"
          label="Departure Airport"
          city={draft.departureCity}
          country={draft.departureCountry}
          timezone={draft.departureTimezone}
          iataCode={draft.departureIataCode}
          onChange={(selection) =>
            onChange({
              ...draft,
              departureCity: selection.city,
              departureCountry: selection.country,
              departureTimezone: selection.timezone,
              departureIataCode: selection.iataCode,
            })
          }
        />
        <div className="grid grid-cols-2 gap-3">
        <Field
          id="departure-date"
          label="Departure Date"
          type="date"
          value={draft.departureDate}
          onChange={(event) => onChange(syncArrivalDate(draft, event.target.value))}
        />
        <Field
          id="departure-time"
          label="Departure Time"
          type="time"
          value={draft.departureTime}
          onChange={(event) => onChange({ ...draft, departureTime: event.target.value })}
        />
        </div>
      </div>
      <div className="flex flex-col gap-3">
        <h3 className="font-medium">Arrival Information</h3>
        <AirportSelector
          id="arrival-airport"
          label="Arrival Airport"
          city={draft.arrivalCity}
          country={draft.arrivalCountry}
          timezone={draft.arrivalTimezone}
          iataCode={draft.arrivalIataCode}
          onChange={(selection) =>
            onChange({
              ...draft,
              arrivalCity: selection.city,
              arrivalCountry: selection.country,
              arrivalTimezone: selection.timezone,
              arrivalIataCode: selection.iataCode,
            })
          }
        />
        <div className="grid grid-cols-2 gap-3">
        <Field
          id="arrival-date"
          label="Arrival Date"
          type="date"
          value={draft.arrivalDate}
          onChange={(event) => onChange({ ...draft, arrivalDate: event.target.value })}
        />
        <Field
          id="arrival-time"
          label="Arrival Time"
          type="time"
          value={draft.arrivalTime}
          onChange={(event) => onChange({ ...draft, arrivalTime: event.target.value })}
        />
        </div>
      </div>
      <div className="grid gap-3 md:col-span-2 md:grid-cols-3">
        <Field
          id="trip-source"
          label="Source"
          value={draft.source}
          onChange={(event) => onChange({ ...draft, source: event.target.value })}
        />
        <Field
          id="trip-booking"
          label="Booking reference"
          value={draft.bookingRef}
          onChange={(event) => onChange({ ...draft, bookingRef: event.target.value })}
        />
        <Field
          id="trip-notes"
          label="Notes"
          value={draft.notes}
          onChange={(event) => onChange({ ...draft, notes: event.target.value })}
        />
      </div>
    </div>
  )
}
