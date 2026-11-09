import { useState } from 'react'
import { airportsForQuery } from '../lib/airports.ts'
import { formatUtcOffset, manualTimezones } from '../lib/timezones.ts'
import type { Airport } from '../lib/airports.ts'
import { Badge } from '@/components/ui/badge'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

export interface AirportSelection {
  city: string
  country: string
  timezone: string
  iataCode: string | null
}

export function AirportSelector({
  id,
  label,
  city,
  country,
  timezone,
  iataCode,
  onChange,
}: {
  id: string
  label: string
  city: string
  country: string
  timezone: string
  iataCode: string | null
  onChange: (selection: AirportSelection) => void
}) {
  const [manual, setManual] = useState(iataCode == null && city !== '')
  const [query, setQuery] = useState('')
  const [open, setOpen] = useState(false)

  const items = airportsForQuery(query)
  const caption = [city, country].filter(Boolean).join(', ')

  function selectAirport(airport: Airport) {
    setQuery(`${airport.city} (${airport.iataCode})`)
    setOpen(false)
    onChange({
      city: airport.city,
      country: airport.country,
      timezone: airport.ianaTimezone,
      iataCode: airport.iataCode,
    })
  }

  return (
    <div className="flex flex-col gap-2">
      <Label htmlFor={id}>{label}</Label>
      {manual ? (
        <select
          id={id}
          aria-label="Timezone"
          className="h-9 rounded-lg border border-input bg-background px-2.5 text-sm shadow-xs"
          value={timezone}
          onChange={(event) => {
            const place = manualTimezones.find((item) => item.timezoneId === event.target.value)
            onChange({
              city: place?.city ?? city,
              country: place?.country ?? country,
              timezone: event.target.value,
              iataCode: null,
            })
          }}
        >
          {manualTimezones.map((place) => (
            <option key={place.timezoneId} value={place.timezoneId}>
              {place.displayName}
            </option>
          ))}
        </select>
      ) : (
        <div className="relative">
          <Input
            id={id}
            role="combobox"
            aria-expanded={open && items.length > 0}
            aria-autocomplete="list"
            value={query}
            placeholder="Search by city, country, or IATA code"
            onFocus={() => setOpen(true)}
            onBlur={() => setOpen(false)}
            onChange={(event) => {
              setQuery(event.target.value)
              setOpen(true)
            }}
          />
          {open && items.length > 0 ? (
            <ul className="absolute z-20 mt-1 max-h-56 w-full overflow-auto rounded-lg border bg-popover p-1 text-sm shadow-md">
              {items.map((airport) => (
                <li key={airport.iataCode}>
                  <button
                    type="button"
                    role="option"
                    className="w-full rounded-md px-2 py-1.5 text-left hover:bg-accent hover:text-accent-foreground"
                    onMouseDown={(event) => event.preventDefault()}
                    onClick={() => selectAirport(airport)}
                  >
                    {airport.displayName}
                  </button>
                </li>
              ))}
            </ul>
          ) : null}
        </div>
      )}
      {caption ? (
        <p className="flex items-center gap-2 text-sm text-muted-foreground">
          <span>{caption}</span>
          {timezone ? <Badge variant="secondary">{formatUtcOffset(timezone)}</Badge> : null}
        </p>
      ) : null}
      <label className="flex items-center gap-2 text-sm">
        <Checkbox
          checked={manual}
          onCheckedChange={(checked) => {
            const enabled = checked === true
            setManual(enabled)
            if (enabled) {
              setQuery('')
              onChange({ city, country, timezone, iataCode: null })
            }
          }}
        />
        Manual timezone
      </label>
    </div>
  )
}
