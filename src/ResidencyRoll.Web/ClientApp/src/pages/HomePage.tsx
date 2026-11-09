import { useQuery } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import { downloadDailyPresence, getDailyPresence, getResidencySummary } from '../api/client.ts'
import type { DailyPresence, ResidencySummary } from '../api/types.ts'
import { Field } from '../components/Field.tsx'
import { CountryColorService, transitColor } from '../lib/countryColor.ts'
import { defaultHomeCountry, useHomeCountry } from '../lib/homeCountry.ts'
import {
  cardStatus,
  countriesInPresence,
  defaultDateRange,
  formatDetailDate,
  groupByMonth,
  isTransit,
  presentDay,
} from '../lib/residencyView.ts'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Label } from '@/components/ui/label'
import { Progress } from '@/components/ui/progress'

export function HomePage() {
  const initial = defaultDateRange()
  const [draftStart, setDraftStart] = useState(initial.start)
  const [draftEnd, setDraftEnd] = useState(initial.end)
  const [applied, setApplied] = useState(initial)
  const [timelineStart, setTimelineStart] = useState(initial.start)
  const [timelineEnd, setTimelineEnd] = useState(initial.end)
  const [selected, setSelected] = useState<DailyPresence | null>(null)
  const [homeCountry, setHomeCountry] = useHomeCountry()

  const summaryQuery = useQuery({
    queryKey: ['residency-summary', applied.start, applied.end],
    queryFn: () => getResidencySummary(applied.start, applied.end),
  })
  const presenceQuery = useQuery({
    queryKey: ['daily-presence', timelineStart, timelineEnd],
    queryFn: () => getDailyPresence(timelineStart, timelineEnd),
  })

  const days = useMemo(() => (Array.isArray(presenceQuery.data) ? presenceQuery.data : []), [presenceQuery.data])
  const summaries = Array.isArray(summaryQuery.data) ? summaryQuery.data : []
  const countries = useMemo(() => countriesInPresence(days), [days])
  const resolvedHome = defaultHomeCountry(countries, homeCountry)
  const colors = useMemo(() => {
    const service = new CountryColorService()
    service.homeCountry = resolvedHome
    service.registerCountries(countries)
    return service
  }, [countries, resolvedHome])

  return (
    <div className="flex flex-col gap-8">
      <section className="flex flex-col gap-4">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Residency Status Dashboard</h1>
          <p className="text-sm text-muted-foreground">Residency calculation based on midnight and partial-day rules</p>
        </div>
        <div className="flex flex-wrap items-end gap-3">
          <Field id="summary-start" label="Start Date" type="date" value={draftStart} onChange={(event) => setDraftStart(event.target.value)} />
          <Field id="summary-end" label="End Date" type="date" value={draftEnd} onChange={(event) => setDraftEnd(event.target.value)} />
          <Button onClick={() => setApplied({ start: draftStart, end: draftEnd })}>Calculate</Button>
          <Button variant="outline" onClick={() => void downloadDailyPresence(applied.start, applied.end)}>
            Export CSV
          </Button>
        </div>
        {summaryQuery.isLoading ? <p className="text-sm text-muted-foreground">Loading residency status</p> : null}
        {summaryQuery.isError ? (
          <Alert variant="destructive">
            <AlertDescription>Unable to load residency status</AlertDescription>
          </Alert>
        ) : null}
        {!summaryQuery.isLoading && summaries.length === 0 ? (
          <Alert>
            <AlertDescription>No residency data available. Add trips to see your residency status.</AlertDescription>
          </Alert>
        ) : (
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
            {summaries.map((summary) => (
              <CountryCard key={summary.countryName} summary={summary} homeCountry={resolvedHome} />
            ))}
          </div>
        )}
      </section>

      <section className="flex flex-col gap-4">
        <h2 className="text-xl font-semibold">Daily Presence Timeline</h2>
        <div className="flex flex-wrap items-end gap-3">
          <Field
            id="timeline-start"
            label="Start Date"
            type="date"
            value={timelineStart}
            onChange={(event) => {
              setTimelineStart(event.target.value)
              setSelected(null)
            }}
          />
          <Field
            id="timeline-end"
            label="End Date"
            type="date"
            value={timelineEnd}
            onChange={(event) => {
              setTimelineEnd(event.target.value)
              setSelected(null)
            }}
          />
          {countries.length > 0 ? (
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="home-country">Home Country</Label>
              <select
                id="home-country"
                className="h-8 rounded-lg border border-input bg-transparent px-2.5 text-sm"
                value={resolvedHome ?? ''}
                onChange={(event) => setHomeCountry(event.target.value)}
              >
                {countries.map((country) => (
                  <option key={country} value={country}>
                    {country}
                  </option>
                ))}
              </select>
            </div>
          ) : null}
        </div>
        {presenceQuery.isLoading ? <p className="text-sm text-muted-foreground">Loading daily presence</p> : null}
        {!presenceQuery.isLoading && days.length === 0 ? (
          <Alert>
            <AlertDescription>No daily presence data available for the selected date range.</AlertDescription>
          </Alert>
        ) : null}
        {!presenceQuery.isLoading && days.length > 0 ? (
          <Timeline days={days} colors={colors} homeCountry={resolvedHome} countries={countries} selected={selected} onSelect={setSelected} />
        ) : null}
      </section>
    </div>
  )
}

export function CountryCard({ summary, homeCountry }: { summary: ResidencySummary; homeCountry: string | null }) {
  const status = cardStatus(summary, homeCountry)
  return (
    <Card style={{ borderLeft: `4px solid ${status.borderColor}` }}>
      <CardContent className="flex flex-col gap-2">
        <div className="flex items-center justify-between gap-2">
          <h3 className="text-base font-medium">{summary.countryName}</h3>
          <Badge variant={summary.ruleType === 'Midnight' ? 'default' : 'secondary'}>{summary.ruleType}</Badge>
        </div>
        <p className="flex items-baseline gap-2">
          <span className="text-3xl font-semibold">{summary.totalDays}</span>
          <span className="text-sm text-muted-foreground">{status.dayLabel}</span>
        </p>
        <Progress value={status.progressPercent} aria-label="Progress toward threshold" />
        <p className="text-sm">Threshold: {summary.thresholdDays} days</p>
        <p className="text-sm">{status.badge}</p>
        <p className="text-sm text-muted-foreground">{status.caption}</p>
      </CardContent>
    </Card>
  )
}

function Timeline({
  days,
  colors,
  homeCountry,
  countries,
  selected,
  onSelect,
}: {
  days: DailyPresence[]
  colors: CountryColorService
  homeCountry: string | null
  countries: string[]
  selected: DailyPresence | null
  onSelect: (day: DailyPresence) => void
}) {
  return (
    <div>
      <div className="mb-4 flex flex-wrap gap-3 text-sm">
        {countries.map((country) => (
          <span key={country} className="flex items-center gap-2">
            <span
              className="size-3 rounded-sm"
              style={
                country === homeCountry
                  ? { backgroundColor: '#f5f5f5', border: '1px solid #ddd' }
                  : { backgroundColor: colors.getCountryColor(country) }
              }
            />
            {country}
            {country === homeCountry ? ' (home)' : ''}
          </span>
        ))}
        <span className="flex items-center gap-2">
          <span className="size-3 rounded-sm" style={{ backgroundColor: transitColor }} />
          Transit
        </span>
      </div>
      {groupByMonth(days).map((group) => (
        <div key={group.label} className="mb-4">
          <h3 className="mb-2 text-sm font-medium">{group.label}</h3>
          <div className="flex flex-wrap gap-1">
            {group.days.map((day) => {
              const presented = presentDay(day, colors, homeCountry)
              return (
                <button
                  key={day.date}
                  type="button"
                  className={`day-cell ${presented.className}`}
                  style={presented.style}
                  title={presented.tooltip}
                  onClick={() => onSelect(day)}
                >
                  <span className="text-sm font-medium">{Number(day.date.slice(8, 10))}</span>
                  {presented.codes.length > 0 ? (
                    <span className="text-[10px]">{presented.codes.join(' ')}</span>
                  ) : (
                    <span className="text-[10px]">{presented.label}</span>
                  )}
                </button>
              )
            })}
          </div>
        </div>
      ))}
      {selected ? <DayDetails day={selected} /> : null}
    </div>
  )
}

function DayDetails({ day }: { day: DailyPresence }) {
  return (
    <Card className="mt-4">
      <CardContent className="flex flex-col gap-1">
        <h3 className="text-base font-medium">Details for {formatDetailDate(day.date)}</h3>
        {isTransit(day) ? (
          <>
            <p>In Transit at Midnight</p>
            <p>No country residency claimed for this day.</p>
          </>
        ) : null}
        {!isTransit(day) && day.locationsDuringDay.length > 1 ? (
          <>
            <p>Multiple Countries (Partial Day Rule): {day.locationsDuringDay.join(', ')}</p>
            <p>
              Under the Partial Day Rule, both countries count this day toward residency because you were physically present in each
              country for part of the day.
            </p>
          </>
        ) : null}
        {!isTransit(day) && day.locationsDuringDay.length <= 1 ? (
          <>
            <p>Location at Midnight: {day.locationAtMidnight}</p>
            <p>This day counts toward {day.locationAtMidnight} residency.</p>
          </>
        ) : null}
        {day.locationsDuringDay.length > 0 ? <p>Countries visited during day: {day.locationsDuringDay.join(', ')}</p> : null}
      </CardContent>
    </Card>
  )
}
