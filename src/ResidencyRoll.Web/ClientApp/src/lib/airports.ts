import source from '../../../../ResidencyRoll.Shared/Airports/airports.json' with { type: 'json' }

export interface Airport {
  iataCode: string
  airportName: string
  city: string
  country: string
  ianaTimezone: string
  displayName: string
  searchText: string
}

function toAirport(airport: Omit<Airport, 'displayName' | 'searchText'>): Airport {
  return {
    ...airport,
    displayName: `${airport.iataCode} - ${airport.airportName}, ${airport.city}`,
    searchText: `${airport.iataCode} ${airport.city} ${airport.airportName} ${airport.country}`.toLowerCase(),
  }
}

const airports: readonly Airport[] = source.map((airport) => toAirport(airport))

export function getAllAirports(): readonly Airport[] {
  return airports
}

export function findByIataCode(iataCode: string): Airport | undefined {
  return airports.find((airport) => airport.iataCode.toLowerCase() === iataCode.toLowerCase())
}

export function searchAirports(searchTerm: string): Airport[] {
  if (searchTerm.trim().length === 0) {
    return [...airports]
  }

  const term = searchTerm.toLowerCase()
  return airports
    .filter((airport) => airport.searchText.includes(term))
    .sort(
      (left, right) =>
        rank(left, searchTerm) - rank(right, searchTerm) || left.city.localeCompare(right.city),
    )
}

function rank(airport: Airport, searchTerm: string): number {
  if (airport.iataCode.toLowerCase() === searchTerm.toLowerCase()) return 0
  if (airport.city.toLowerCase().startsWith(searchTerm.toLowerCase())) return 1
  return 2
}

export function airportsForQuery(searchTerm: string): Airport[] {
  if (searchTerm.trim().length === 0) {
    return airports.slice(0, 20)
  }

  return searchAirports(searchTerm).slice(0, 50)
}
