import {
  Button,
  ComposedModal,
  DataTable,
  InlineLoading,
  InlineNotification,
  ModalBody,
  ModalFooter,
  ModalHeader,
  Tab,
  TabList,
  TabPanel,
  TabPanels,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableHeader,
  TableRow,
  TableToolbar,
  TableToolbarContent,
  TableToolbarSearch,
  Tabs,
  TextInput,
} from '@carbon/react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useMemo, useState, type ChangeEvent } from 'react'
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

const headers = [
  { key: 'departure', header: 'Departure' },
  { key: 'departureTime', header: 'Departure Time' },
  { key: 'arrival', header: 'Arrival' },
  { key: 'arrivalTime', header: 'Arrival Time' },
  { key: 'duration', header: 'Duration' },
  { key: 'actions', header: '' },
]

export function ManageTripsPage() {
  const queryClient = useQueryClient()
  const tripsQuery = useQuery({ queryKey: ['trips'], queryFn: getTrips })
  const parserQuery = useQuery({ queryKey: ['parser-availability'], queryFn: getItineraryParserAvailability })
  const [filter, setFilter] = useState('')
  const [showImport, setShowImport] = useState(false)
  const [importMessage, setImportMessage] = useState('')
  const [importError, setImportError] = useState(false)
  const [file, setFile] = useState<File | null>(null)
  const [tab, setTab] = useState(0)
  const [draft, setDraft] = useState<TripDraft | null>(null)
  const [adding, setAdding] = useState(false)
  const [validation, setValidation] = useState('')
  const [parsed, setParsed] = useState<TripDraft[]>([])
  const [parsedIndex, setParsedIndex] = useState(-1)
  const [parserOpen, setParserOpen] = useState(false)
  const [parserError, setParserError] = useState('')
  const [parserSuccess, setParserSuccess] = useState(false)
  const [pendingDelete, setPendingDelete] = useState<Trip | null>(null)

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
    setAdding(true)
    setTab(1)
  }

  function openEdit(trip: Trip) {
    setParsed([])
    setParsedIndex(-1)
    setValidation('')
    setDraft(draftFromTrip(trip))
    setAdding(false)
    setTab(1)
  }

  function openLeg(trip: Trip) {
    setParsed([])
    setParsedIndex(-1)
    setValidation('')
    setDraft(draftFromNextLeg(trip))
    setAdding(false)
    setTab(1)
  }

  function closeEditor() {
    setDraft(null)
    setAdding(false)
    setParsed([])
    setParsedIndex(-1)
    setValidation('')
    setTab(0)
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
      await updateTrip(draft.id, { ...body, id: draft.id })
      saved = await getTrip(draft.id)
    } else {
      saved = await createTrip(body)
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
    for (const item of next) {
      await createTrip(toTripBody(item))
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
    mutationFn: (trip: Trip) => deleteTrip(trip.id),
    onSuccess: async () => {
      setPendingDelete(null)
      await refresh()
    },
  })

  const importing = useMutation({
    mutationFn: (selected: File) => importTrips(selected),
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

  const tableRows = trips.map((trip) => ({
    id: String(trip.id),
    departure: placeLabel(trip.departureIataCode, trip.departureCity),
    departureTime: formatTripWhen(trip.departureDateTime),
    arrival: placeLabel(trip.arrivalIataCode, trip.arrivalCity),
    arrivalTime: formatTripWhen(trip.arrivalDateTime),
    duration: `${durationHours(trip.departureDateTime, trip.departureTimezone, trip.arrivalDateTime, trip.arrivalTimezone)} hours`,
    actions: '',
  }))

  return (
    <section>
      <h1>Manage Trips</h1>
      <Tabs selectedIndex={tab} onChange={({ selectedIndex }) => setTab(selectedIndex)}>
        <TabList aria-label="Manage trips">
          <Tab>Trip List</Tab>
          {draft ? <Tab>Trip Editor</Tab> : null}
        </TabList>
        <TabPanels>
          <TabPanel>
            <div className="home-toolbar">
              <Button kind="primary" onClick={openNew}>
                Add Trip
              </Button>
              <Button kind="secondary" onClick={() => setShowImport((value) => !value)}>
                Import / Export
              </Button>
            </div>
            {showImport ? (
              <div className="home-toolbar">
                <Button kind="secondary" onClick={() => void downloadTrips()}>
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
                {importMessage ? <span style={{ color: importError ? '#dc3545' : '#198754' }}>{importMessage}</span> : null}
              </div>
            ) : null}
            {tripsQuery.isLoading ? <InlineLoading description="Loading trips" /> : null}
            <DataTable rows={tableRows} headers={headers} isSortable>
              {({ rows, headers: tableHeaders, getHeaderProps, getRowProps, getTableProps }) => (
                <TableContainer>
                  <TableToolbar>
                    <TableToolbarContent>
                      <TableToolbarSearch
                        persistent
                        placeholder="Filter trips"
                        onChange={(event: ChangeEvent<HTMLInputElement> | string) =>
                          setFilter(typeof event === 'string' ? event : event.target.value)
                        }
                      />
                    </TableToolbarContent>
                  </TableToolbar>
                  <Table {...getTableProps()}>
                    <TableHead>
                      <TableRow>
                        {tableHeaders.map((header) => (
                          <TableHeader {...getHeaderProps({ header })} key={header.key}>
                            {header.header}
                          </TableHeader>
                        ))}
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {rows.map((row) => {
                        const trip = trips.find((item) => String(item.id) === row.id)
                        return (
                          <TableRow {...getRowProps({ row })} key={row.id}>
                            {row.cells.map((cell) =>
                              cell.info.header === 'actions' && trip ? (
                                <TableCell key={cell.id}>
                                  <Button size="sm" kind="ghost" onClick={() => openEdit(trip)}>
                                    Edit
                                  </Button>
                                  <Button size="sm" kind="ghost" onClick={() => openLeg(trip)}>
                                    Add Leg
                                  </Button>
                                  <Button size="sm" kind="danger--ghost" onClick={() => setPendingDelete(trip)}>
                                    Delete
                                  </Button>
                                </TableCell>
                              ) : cell.info.header === 'departure' && trip ? (
                                <TableCell key={cell.id}>
                                  <div>{cell.value}</div>
                                  <div>{formatUtcOffset(trip.departureTimezone)}</div>
                                </TableCell>
                              ) : cell.info.header === 'arrival' && trip ? (
                                <TableCell key={cell.id}>
                                  <div>{cell.value}</div>
                                  <div>{formatUtcOffset(trip.arrivalTimezone)}</div>
                                </TableCell>
                              ) : (
                                <TableCell key={cell.id}>{cell.value}</TableCell>
                              ),
                            )}
                          </TableRow>
                        )
                      })}
                    </TableBody>
                  </Table>
                </TableContainer>
              )}
            </DataTable>
            <Button kind="primary" onClick={openNew}>
              Add Trip
            </Button>
          </TabPanel>
          {draft ? (
            <TabPanel>
              <h2>{draft.id > 0 ? 'Edit Trip' : 'Add New Trip'}</h2>
              {adding && parserQuery.isSuccess && parserQuery.data ? (
                <div>
                  <h3>Quick Itinerary Parser</h3>
                  <Button kind="tertiary" onClick={() => setParserOpen(true)}>
                    Parse Itinerary
                  </Button>
                  {parsed.length > 0 ? (
                    <p>
                      Parsed legs: {parsed.length} (Leg {parsedIndex + 1} selected)
                    </p>
                  ) : null}
                  {parserError ? <InlineNotification kind="error" title={parserError} hideCloseButton /> : null}
                  {parserSuccess ? (
                    <InlineNotification kind="success" title="Itinerary loaded. Review each leg before saving." hideCloseButton />
                  ) : null}
                </div>
              ) : null}
              {adding && parserQuery.isSuccess && !parserQuery.data ? (
                <InlineNotification kind="info" title="Itinerary parser is unavailable until OpenAI configuration is set." hideCloseButton />
              ) : null}
              <TripFields
                draft={draft}
                onChange={(next) => {
                  setDraft(next)
                }}
              />
              {parsed.length > 0 ? (
                <div className="home-toolbar">
                  <Button kind="ghost" disabled={parsedIndex <= 0} onClick={() => showParsed(parsedIndex - 1)}>
                    Previous Leg
                  </Button>
                  <Button kind="ghost" disabled={parsedIndex >= parsed.length - 1} onClick={() => showParsed(parsedIndex + 1)}>
                    Next Leg
                  </Button>
                  <Button onClick={() => void saveParsed()}>Save Parsed Legs</Button>
                  <Button kind="ghost" onClick={closeEditor}>
                    Cancel
                  </Button>
                </div>
              ) : (
                <div className="home-toolbar">
                  <Button onClick={() => void save(false)}>Save Trip</Button>
                  <Button kind="tertiary" onClick={() => void save(true)}>
                    Save and Add Leg
                  </Button>
                  <Button kind="ghost" onClick={closeEditor}>
                    Cancel
                  </Button>
                </div>
              )}
              {validation ? <p style={{ color: '#dc3545' }}>{validation}</p> : null}
            </TabPanel>
          ) : null}
        </TabPanels>
      </Tabs>
      <ItineraryParserModal open={parserOpen} onClose={() => setParserOpen(false)} onParsed={applyParsed} />
      <ComposedModal open={pendingDelete != null} onClose={() => setPendingDelete(null)}>
        <ModalHeader title="Delete trip" />
        <ModalBody>
          <p>Delete this trip from {pendingDelete?.departureCity} to {pendingDelete?.arrivalCity}?</p>
        </ModalBody>
        <ModalFooter>
          <Button kind="secondary" onClick={() => setPendingDelete(null)}>
            Cancel
          </Button>
          <Button kind="danger" onClick={() => pendingDelete && remove.mutate(pendingDelete)}>
            Delete
          </Button>
        </ModalFooter>
      </ComposedModal>
    </section>
  )
}

function TripFields({ draft, onChange }: { draft: TripDraft; onChange: (draft: TripDraft) => void }) {
  return (
    <div className="trip-editor">
      <h3>Departure Information</h3>
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
      <TextInput
        id="departure-date"
        labelText="Departure Date"
        type="date"
        value={draft.departureDate}
        onChange={(event) => onChange(syncArrivalDate(draft, event.target.value))}
      />
      <TextInput
        id="departure-time"
        labelText="Departure Time"
        type="time"
        value={draft.departureTime}
        onChange={(event) => onChange({ ...draft, departureTime: event.target.value })}
      />
      <h3>Arrival Information</h3>
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
      <TextInput
        id="arrival-date"
        labelText="Arrival Date"
        type="date"
        value={draft.arrivalDate}
        onChange={(event) => onChange({ ...draft, arrivalDate: event.target.value })}
      />
      <TextInput
        id="arrival-time"
        labelText="Arrival Time"
        type="time"
        value={draft.arrivalTime}
        onChange={(event) => onChange({ ...draft, arrivalTime: event.target.value })}
      />
    </div>
  )
}
