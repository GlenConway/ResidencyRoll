import { Button, DatePicker, DatePickerInput, Dropdown, InlineLoading, InlineNotification, ProgressBar, Tag, Tile } from '@carbon/react'
import { useQuery } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import { downloadDailyPresence, getDailyPresence, getResidencySummary } from '../api/client.ts'
import type { DailyPresence, ResidencySummary } from '../api/types.ts'
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
    <div className="home-page">
      <section>
        <h1>Residency Status Dashboard</h1>
        <p>Residency calculation based on midnight and partial-day rules</p>
        <div className="home-toolbar">
          <DatePicker datePickerType="single" dateFormat="Y-m-d" value={draftStart} onChange={(event) => setDraftStart(dateValue(event, draftStart))}>
            <DatePickerInput id="summary-start" labelText="Start Date" />
          </DatePicker>
          <DatePicker datePickerType="single" dateFormat="Y-m-d" value={draftEnd} onChange={(event) => setDraftEnd(dateValue(event, draftEnd))}>
            <DatePickerInput id="summary-end" labelText="End Date" />
          </DatePicker>
          <Button onClick={() => setApplied({ start: draftStart, end: draftEnd })}>Calculate</Button>
          <Button kind="secondary" onClick={() => void downloadDailyPresence(applied.start, applied.end)}>
            Export CSV
          </Button>
        </div>
        {summaryQuery.isLoading ? <InlineLoading description="Loading residency status" /> : null}
        {summaryQuery.isError ? <InlineNotification kind="error" title="Unable to load residency status" hideCloseButton /> : null}
        {!summaryQuery.isLoading && summaries.length === 0 ? (
          <InlineNotification kind="info" title="No residency data available. Add trips to see your residency status." hideCloseButton />
        ) : (
          <div className="country-grid">
            {summaries.map((summary) => (
              <CountryCard key={summary.countryName} summary={summary} homeCountry={resolvedHome} />
            ))}
          </div>
        )}
      </section>

      <section>
        <h2>Daily Presence Timeline</h2>
        <div className="home-toolbar">
          <DatePicker
            datePickerType="single"
            dateFormat="Y-m-d"
            value={timelineStart}
            onChange={(event) => {
              setTimelineStart(dateValue(event, timelineStart))
              setSelected(null)
            }}
          >
            <DatePickerInput id="timeline-start" labelText="Start Date" placeholder="Start Date" />
          </DatePicker>
          <DatePicker
            datePickerType="single"
            dateFormat="Y-m-d"
            value={timelineEnd}
            onChange={(event) => {
              setTimelineEnd(dateValue(event, timelineEnd))
              setSelected(null)
            }}
          >
            <DatePickerInput id="timeline-end" labelText="End Date" placeholder="End Date" />
          </DatePicker>
          {countries.length > 0 ? (
            <Dropdown
              id="home-country"
              titleText="Home Country"
              label="Select home country"
              items={countries}
              selectedItem={resolvedHome}
              onChange={(data: { selectedItem?: string | null }) => {
                if (data.selectedItem) {
                  setHomeCountry(data.selectedItem)
                }
              }}
            />
          ) : null}
        </div>
        {presenceQuery.isLoading ? <InlineLoading description="Loading daily presence" /> : null}
        {!presenceQuery.isLoading && days.length === 0 ? (
          <InlineNotification kind="info" title="No daily presence data available for the selected date range." hideCloseButton />
        ) : (
          <Timeline days={days} colors={colors} homeCountry={resolvedHome} countries={countries} selected={selected} onSelect={setSelected} />
        )}
      </section>
    </div>
  )
}

export function CountryCard({ summary, homeCountry }: { summary: ResidencySummary; homeCountry: string | null }) {
  const status = cardStatus(summary, homeCountry)
  return (
    <Tile className="country-card" style={{ borderLeft: `4px solid ${status.borderColor}` }}>
      <div className="country-card-heading">
        <h3>{summary.countryName}</h3>
        <Tag type={summary.ruleType === 'Midnight' ? 'blue' : 'gray'}>{summary.ruleType}</Tag>
      </div>
      <p className="days-display">
        <span className="days-number">{summary.totalDays}</span>
        <span>{status.dayLabel}</span>
      </p>
      <ProgressBar
        label="Progress toward threshold"
        value={status.progressPercent}
        status={status.progressTone === 'danger' ? 'error' : status.progressTone === 'success' ? 'finished' : 'active'}
        hideLabel
      />
      <p>Threshold: {summary.thresholdDays} days</p>
      <p>{status.badge}</p>
      <p>
        <em>{status.caption}</em>
      </p>
    </Tile>
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
      <div className="timeline-legend">
        {countries.map((country) => (
          <span key={country} className="legend-item">
            <span
              className="legend-swatch"
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
        <span className="legend-item">
          <span className="legend-swatch" style={{ backgroundColor: transitColor }} />
          Transit
        </span>
      </div>
      {groupByMonth(days).map((group) => (
        <div key={group.label} className="month-group">
          <h3>{group.label}</h3>
          <div className="days-container">
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
                  <span className="day-number">{Number(day.date.slice(8, 10))}</span>
                  {presented.codes.length > 0 ? (
                    <span className="day-label">{presented.codes.join(' ')}</span>
                  ) : (
                    <span className="day-label">{presented.label}</span>
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
    <Tile>
      <h3>Details for {formatDetailDate(day.date)}</h3>
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
    </Tile>
  )
}

function dateValue(event: unknown, fallback: string): string {
  const dates = event as Date[] | undefined
  const selected = dates?.[0]
  return selected ? isoFromDate(selected) : fallback
}

function isoFromDate(date: Date): string {
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${date.getFullYear()}-${month}-${day}`
}
