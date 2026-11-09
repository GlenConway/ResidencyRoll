import { ComboBox, Checkbox, Tag } from '@carbon/react'
import { useEffect, useMemo, useState } from 'react'
import { airportsForQuery, findByIataCode, searchAirports, type Airport } from '../lib/airports.ts'
import { formatUtcOffset, manualTimezones, type TimezonePlace } from '../lib/timezones.ts'

export interface AirportSelection {
  city: string
  country: string
  timezone: string
  iataCode: string | null
}

interface AirportSelectorProps {
  id: string
  label: string
  city: string
  country: string
  timezone: string
  iataCode: string | null
  onChange: (selection: AirportSelection) => void
}

export function AirportSelector({ id, label, city, country, timezone, iataCode, onChange }: AirportSelectorProps) {
  const initial = useMemo(() => initialAirport(iataCode, city), [iataCode, city])
  const [manual, setManual] = useState(false)
  const [query, setQuery] = useState('')
  const [debouncedQuery, setDebouncedQuery] = useState('')
  const [selected, setSelected] = useState<Airport | null>(initial)

  useEffect(() => {
    const timer = window.setTimeout(() => setDebouncedQuery(query), 300)
    return () => window.clearTimeout(timer)
  }, [query])

  const items = useMemo(() => airportsForQuery(debouncedQuery), [debouncedQuery])
  const offset = formatUtcOffset(timezone)

  return (
    <div className="airport-selector">
      {manual ? (
        <ComboBox
          id={`${id}-timezone`}
          titleText={label}
          placeholder="Search timezones"
          items={manualTimezones as TimezonePlace[]}
          itemToString={(item) => item?.displayName ?? ''}
          onChange={(data: { selectedItem?: TimezonePlace | null }) => {
            const place = data.selectedItem
            if (!place) return
            onChange({ city: place.city, country: place.country, timezone: place.timezoneId, iataCode: null })
          }}
        />
      ) : (
        <ComboBox
          id={id}
          titleText={label}
          placeholder="Search by code, city, name, or country"
          items={items}
          itemToString={(item) => item?.displayName ?? ''}
          selectedItem={selected}
          onInputChange={(value: string) => setQuery(value)}
          onChange={(data: { selectedItem?: Airport | null }) => {
            const airport = data.selectedItem
            if (!airport) return
            setSelected(airport)
            onChange({
              city: airport.city,
              country: airport.country,
              timezone: airport.ianaTimezone,
              iataCode: airport.iataCode,
            })
          }}
        />
      )}
      {city && country ? (
        <p className="airport-caption">
          {city}, {country}
        </p>
      ) : null}
      {offset ? (
        <Tag type="cool-gray" size="sm">
          {offset}
        </Tag>
      ) : null}
      <Checkbox
        id={`${id}-manual`}
        labelText="Manual timezone"
        checked={manual}
        onChange={(_event, data) => {
          setManual(data.checked)
          if (data.checked) {
            onChange({ city, country, timezone, iataCode: null })
          }
        }}
      />
    </div>
  )
}

function initialAirport(iataCode: string | null, city: string): Airport | null {
  if (iataCode) {
    return findByIataCode(iataCode) ?? null
  }

  if (city) {
    return searchAirports(city).find((airport) => airport.city.toLowerCase() === city.toLowerCase()) ?? null
  }

  return null
}
