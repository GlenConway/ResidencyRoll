import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { ApiError, forecast, getItineraryParserAvailability } from '../api/client.ts'
import type { CountryDays, ForecastResponse, ItineraryParsingResponse } from '../api/types.ts'
import { AirportSelector } from '../components/AirportSelector.tsx'
import { Field } from '../components/Field.tsx'
import { ItineraryParserModal } from '../components/ItineraryParserModal.tsx'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import {
  addForecastLeg,
  apiUnreachableMessage,
  isHome,
  legLabel,
  legsFromParsed,
  primaryDestination,
  sortByDays,
  starterLeg,
  toForecastRequest,
  totalAway,
  tripDurationDays,
  unexpectedForecastMessage,
  validationIssues,
  type ForecastLeg,
} from '../lib/forecastPlan.ts'
import { useHomeCountry } from '../lib/homeCountry.ts'

export function ForecastPage() {
  const [homeCountry] = useHomeCountry()
  const [legs, setLegs] = useState<ForecastLeg[]>(() => [starterLeg()])
  const [issues, setIssues] = useState<string[]>([])
  const [result, setResult] = useState<ForecastResponse | null>(null)
  const [parserOpen, setParserOpen] = useState(false)
  const [parserError, setParserError] = useState('')
  const [parsedCount, setParsedCount] = useState(0)
  const parserQuery = useQuery({ queryKey: ['parser-availability'], queryFn: getItineraryParserAvailability })

  function updateLeg(id: number, next: ForecastLeg) {
    setLegs((current) => current.map((leg) => (leg.id === id ? next : leg)))
  }

  async function calculate() {
    const problems = validationIssues(legs)
    if (problems.length > 0) {
      setIssues(problems)
      setResult(null)
      return
    }
    setIssues([])
    try {
      setResult(await forecast(toForecastRequest(legs)))
    } catch (error) {
      setResult(null)
      setIssues([error instanceof TypeError || (error instanceof ApiError && error.status === 0) ? apiUnreachableMessage : unexpectedForecastMessage])
    }
  }

  function applyParsed(response: ItineraryParsingResponse) {
    setParserOpen(false)
    if (response.error) {
      setParserError(response.error)
      setParsedCount(0)
      return
    }
    if (response.legs.length === 0) {
      setParserError('No flight legs could be extracted from the provided itinerary text. Please check the format and try again.')
      return
    }
    setLegs(legsFromParsed(response.legs))
    setParsedCount(response.legs.length)
    setParserError('')
    setResult(null)
  }

  const primary = result ? primaryDestination(legs) : ''
  const primaryDays = result?.forecast.find((row) => row.countryName === primary)?.days ?? 0
  const duration = tripDurationDays(legs)
  const visited = new Set(legs.map((leg) => leg.arrivalCountry).filter(Boolean))

  return (
    <section>
      <h1 className="text-2xl font-semibold tracking-tight">Trip Forecast</h1>
      {parserQuery.data ? (
        <div>
          <h2>Quick Itinerary Parser</h2>
          <p>Paste flight information from emails, booking confirmations, or any text format. We'll auto-fill your trip legs.</p>
          <Button variant="outline" onClick={() => setParserOpen(true)}>
            Open Itinerary Parser
          </Button>
          {parserError ? (
            <Alert variant="destructive">
              <AlertDescription>{parserError}</AlertDescription>
            </Alert>
          ) : null}
          {parsedCount > 0 ? (
            <Alert>
              <AlertDescription>Parsed {parsedCount} flight leg(s). Your trip legs have been updated.</AlertDescription>
            </Alert>
          ) : null}
        </div>
      ) : null}
      {parserQuery.isSuccess && !parserQuery.data ? (
        <Alert>
          <AlertDescription>Quick Itinerary Parser is unavailable until OpenAI configuration is set.</AlertDescription>
        </Alert>
      ) : null}
      <div className="my-4 flex flex-wrap items-end justify-between gap-3">
        <div>
          <h2 className="text-xl font-semibold">Plan a Future Trip (Multi-leg Supported)</h2>
          <p className="text-sm text-muted-foreground">Total legs: {legs.length}</p>
        </div>
        <Button onClick={() => setLegs((current) => addForecastLeg(current))}>Add Leg</Button>
      </div>
      {issues.length > 0 ? (
        <Alert>
          <AlertTitle>Missing information</AlertTitle>
          <AlertDescription>{issues.join(' ')}</AlertDescription>
        </Alert>
      ) : null}
      {legs.map((leg, index) => (
        <article key={leg.id} className="my-4 flex flex-col gap-3 rounded-xl border bg-card p-4">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <h3>
              Leg {index + 1} ({legLabel(index, legs.length)})
            </h3>
            {legs.length > 1 ? (
              <Button variant="ghost" className="text-destructive" onClick={() => setLegs((current) => current.filter((item) => item.id !== leg.id))}>
                Remove
              </Button>
            ) : null}
          </div>
          <h4>Departure</h4>
          <AirportSelector
            id={`forecast-departure-${leg.id}`}
            label="Departure Airport"
            city={leg.departureCity}
            country={leg.departureCountry}
            timezone={leg.departureTimezone}
            iataCode={leg.departureIataCode}
            onChange={(selection) =>
              updateLeg(leg.id, {
                ...leg,
                departureCity: selection.city,
                departureCountry: selection.country,
                departureTimezone: selection.timezone,
                departureIataCode: selection.iataCode,
              })
            }
          />
          <Field
            id={`forecast-departure-date-${leg.id}`}
            label="Departure Date"
            type="date"
            value={leg.departureDate}
            onChange={(event) => updateLeg(leg.id, { ...leg, departureDate: event.target.value, arrivalDate: event.target.value })}
          />
          <Field
            id={`forecast-departure-time-${leg.id}`}
            label="Departure Time"
            type="time"
            value={leg.departureTime}
            onChange={(event) => updateLeg(leg.id, { ...leg, departureTime: event.target.value })}
          />
          <h4>Arrival</h4>
          <AirportSelector
            id={`forecast-arrival-${leg.id}`}
            label="Arrival Airport"
            city={leg.arrivalCity}
            country={leg.arrivalCountry}
            timezone={leg.arrivalTimezone}
            iataCode={leg.arrivalIataCode}
            onChange={(selection) =>
              updateLeg(leg.id, {
                ...leg,
                arrivalCity: selection.city,
                arrivalCountry: selection.country,
                arrivalTimezone: selection.timezone,
                arrivalIataCode: selection.iataCode,
              })
            }
          />
          <Field
            id={`forecast-arrival-date-${leg.id}`}
            label="Arrival Date"
            type="date"
            value={leg.arrivalDate}
            onChange={(event) => updateLeg(leg.id, { ...leg, arrivalDate: event.target.value })}
          />
          <Field
            id={`forecast-arrival-time-${leg.id}`}
            label="Arrival Time"
            type="time"
            value={leg.arrivalTime}
            onChange={(event) => updateLeg(leg.id, { ...leg, arrivalTime: event.target.value })}
          />
        </article>
      ))}
      <Button onClick={() => void calculate()}>Calculate Forecast</Button>
      {result ? (
        <div className="mt-6 grid gap-8 md:grid-cols-2">
          <DayColumn title="Current (Last 365 Days)" rows={sortByDays(result.current)} empty="No trips recorded in the last 365 days." homeCountry={homeCountry} />
          <DayColumn
            title="Forecast (365 Days from Trip End)"
            rows={sortByDays(result.forecast)}
            empty="No trips in forecast period."
            homeCountry={homeCountry}
            visited={visited}
            duration={duration}
          />
        </div>
      ) : null}
      {result && primary && primaryDays > 183 && !isHome(primary, homeCountry) ? (
        <div className="my-4 rounded-xl border border-destructive/40 bg-destructive/10 p-4 text-sm">
          <h3>183-Day Limit Warning</h3>
          <p>
            Your forecasted trip would result in {primaryDays} days in {primary} over a 365-day period, which exceeds the typical
            183-day limit for tax residency.
          </p>
          <p>Consider adjusting your trip dates or duration to stay below the limit.</p>
        </div>
      ) : null}
      <ItineraryParserModal open={parserOpen} onClose={() => setParserOpen(false)} onParsed={applyParsed} />
    </section>
  )
}

function DayColumn({
  title,
  rows,
  empty,
  homeCountry,
  visited,
  duration = 0,
}: {
  title: string
  rows: CountryDays[]
  empty: string
  homeCountry: string | null
  visited?: Set<string>
  duration?: number
}) {
  return (
    <div>
      <h3>{title}</h3>
      {rows.length === 0 ? <p>{empty}</p> : null}
      {rows.map((row) => {
        const seen = visited?.has(row.countryName) ?? false
        const exceeds = seen && row.days > 183 && !isHome(row.countryName, homeCountry)
        return (
          <p
            key={row.countryName}
            style={{
              background: !seen ? '#f8f9fa' : exceeds ? '#f8d7da' : '#d4edda',
              border: !seen ? undefined : `2px solid ${exceeds ? '#dc3545' : '#28a745'}`,
            }}
          >
            <strong>{row.countryName}</strong>
            {isHome(row.countryName, homeCountry) ? ' (home)' : ''}
            {seen && duration > 0 ? ` (+ ${duration} days)` : ''}
            {exceeds ? ' ⚠️ Exceeds 183' : ''}
            <span> {row.days} days</span>
          </p>
        )
      })}
      {rows.length > 0 ? (
        <p>
          Total Away{homeCountry ? ' (excl. home)' : ''}: {totalAway(rows, homeCountry)} days
        </p>
      ) : null}
    </div>
  )
}
