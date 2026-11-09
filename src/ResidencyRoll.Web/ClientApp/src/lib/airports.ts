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

const source = [
  {
    "iataCode": "YYZ",
    "airportName": "Toronto Pearson International",
    "city": "Toronto",
    "country": "Canada",
    "ianaTimezone": "America/Toronto"
  },
  {
    "iataCode": "YUL",
    "airportName": "Montreal-Pierre Elliott Trudeau International",
    "city": "Montreal",
    "country": "Canada",
    "ianaTimezone": "America/Toronto"
  },
  {
    "iataCode": "YVR",
    "airportName": "Vancouver International",
    "city": "Vancouver",
    "country": "Canada",
    "ianaTimezone": "America/Vancouver"
  },
  {
    "iataCode": "YYC",
    "airportName": "Calgary International",
    "city": "Calgary",
    "country": "Canada",
    "ianaTimezone": "America/Edmonton"
  },
  {
    "iataCode": "YOW",
    "airportName": "Ottawa Macdonald-Cartier International",
    "city": "Ottawa",
    "country": "Canada",
    "ianaTimezone": "America/Toronto"
  },
  {
    "iataCode": "YEG",
    "airportName": "Edmonton International",
    "city": "Edmonton",
    "country": "Canada",
    "ianaTimezone": "America/Edmonton"
  },
  {
    "iataCode": "YWG",
    "airportName": "Winnipeg James Armstrong Richardson International",
    "city": "Winnipeg",
    "country": "Canada",
    "ianaTimezone": "America/Winnipeg"
  },
  {
    "iataCode": "YHZ",
    "airportName": "Halifax Stanfield International",
    "city": "Halifax",
    "country": "Canada",
    "ianaTimezone": "America/Halifax"
  },
  {
    "iataCode": "YYJ",
    "airportName": "Victoria International",
    "city": "Victoria",
    "country": "Canada",
    "ianaTimezone": "America/Vancouver"
  },
  {
    "iataCode": "YQB",
    "airportName": "Quebec City Jean Lesage International",
    "city": "Quebec City",
    "country": "Canada",
    "ianaTimezone": "America/Toronto"
  },
  {
    "iataCode": "YXE",
    "airportName": "Saskatoon John G. Diefenbaker International",
    "city": "Saskatoon",
    "country": "Canada",
    "ianaTimezone": "America/Regina"
  },
  {
    "iataCode": "YYT",
    "airportName": "St. John's International",
    "city": "St. John's",
    "country": "Canada",
    "ianaTimezone": "America/St_Johns"
  },
  {
    "iataCode": "YQR",
    "airportName": "Regina International",
    "city": "Regina",
    "country": "Canada",
    "ianaTimezone": "America/Regina"
  },
  {
    "iataCode": "YHM",
    "airportName": "John C. Munro Hamilton International",
    "city": "Hamilton",
    "country": "Canada",
    "ianaTimezone": "America/Toronto"
  },
  {
    "iataCode": "YKF",
    "airportName": "Waterloo Regional",
    "city": "Kitchener",
    "country": "Canada",
    "ianaTimezone": "America/Toronto"
  },
  {
    "iataCode": "JFK",
    "airportName": "John F. Kennedy International",
    "city": "New York",
    "country": "United States",
    "ianaTimezone": "America/New_York"
  },
  {
    "iataCode": "LGA",
    "airportName": "LaGuardia",
    "city": "New York",
    "country": "United States",
    "ianaTimezone": "America/New_York"
  },
  {
    "iataCode": "EWR",
    "airportName": "Newark Liberty International",
    "city": "Newark",
    "country": "United States",
    "ianaTimezone": "America/New_York"
  },
  {
    "iataCode": "LAX",
    "airportName": "Los Angeles International",
    "city": "Los Angeles",
    "country": "United States",
    "ianaTimezone": "America/Los_Angeles"
  },
  {
    "iataCode": "ORD",
    "airportName": "O'Hare International",
    "city": "Chicago",
    "country": "United States",
    "ianaTimezone": "America/Chicago"
  },
  {
    "iataCode": "DFW",
    "airportName": "Dallas/Fort Worth International",
    "city": "Dallas",
    "country": "United States",
    "ianaTimezone": "America/Chicago"
  },
  {
    "iataCode": "DEN",
    "airportName": "Denver International",
    "city": "Denver",
    "country": "United States",
    "ianaTimezone": "America/Denver"
  },
  {
    "iataCode": "SFO",
    "airportName": "San Francisco International",
    "city": "San Francisco",
    "country": "United States",
    "ianaTimezone": "America/Los_Angeles"
  },
  {
    "iataCode": "SEA",
    "airportName": "Seattle-Tacoma International",
    "city": "Seattle",
    "country": "United States",
    "ianaTimezone": "America/Los_Angeles"
  },
  {
    "iataCode": "LAS",
    "airportName": "Harry Reid International",
    "city": "Las Vegas",
    "country": "United States",
    "ianaTimezone": "America/Los_Angeles"
  },
  {
    "iataCode": "MCO",
    "airportName": "Orlando International",
    "city": "Orlando",
    "country": "United States",
    "ianaTimezone": "America/New_York"
  },
  {
    "iataCode": "MIA",
    "airportName": "Miami International",
    "city": "Miami",
    "country": "United States",
    "ianaTimezone": "America/New_York"
  },
  {
    "iataCode": "ATL",
    "airportName": "Hartsfield-Jackson Atlanta International",
    "city": "Atlanta",
    "country": "United States",
    "ianaTimezone": "America/New_York"
  },
  {
    "iataCode": "BOS",
    "airportName": "Logan International",
    "city": "Boston",
    "country": "United States",
    "ianaTimezone": "America/New_York"
  },
  {
    "iataCode": "PHX",
    "airportName": "Phoenix Sky Harbor International",
    "city": "Phoenix",
    "country": "United States",
    "ianaTimezone": "America/Phoenix"
  },
  {
    "iataCode": "IAH",
    "airportName": "George Bush Intercontinental",
    "city": "Houston",
    "country": "United States",
    "ianaTimezone": "America/Chicago"
  },
  {
    "iataCode": "MSP",
    "airportName": "Minneapolis-St. Paul International",
    "city": "Minneapolis",
    "country": "United States",
    "ianaTimezone": "America/Chicago"
  },
  {
    "iataCode": "DTW",
    "airportName": "Detroit Metropolitan Wayne County",
    "city": "Detroit",
    "country": "United States",
    "ianaTimezone": "America/Detroit"
  },
  {
    "iataCode": "PHL",
    "airportName": "Philadelphia International",
    "city": "Philadelphia",
    "country": "United States",
    "ianaTimezone": "America/New_York"
  },
  {
    "iataCode": "PDX",
    "airportName": "Portland International",
    "city": "Portland",
    "country": "United States",
    "ianaTimezone": "America/Los_Angeles"
  },
  {
    "iataCode": "SAN",
    "airportName": "San Diego International",
    "city": "San Diego",
    "country": "United States",
    "ianaTimezone": "America/Los_Angeles"
  },
  {
    "iataCode": "HNL",
    "airportName": "Daniel K. Inouye International",
    "city": "Honolulu",
    "country": "United States",
    "ianaTimezone": "Pacific/Honolulu"
  },
  {
    "iataCode": "ANC",
    "airportName": "Ted Stevens Anchorage International",
    "city": "Anchorage",
    "country": "United States",
    "ianaTimezone": "America/Anchorage"
  },
  {
    "iataCode": "MEX",
    "airportName": "Mexico City International",
    "city": "Mexico City",
    "country": "Mexico",
    "ianaTimezone": "America/Mexico_City"
  },
  {
    "iataCode": "CUN",
    "airportName": "Cancun International",
    "city": "Cancun",
    "country": "Mexico",
    "ianaTimezone": "America/Cancun"
  },
  {
    "iataCode": "GDL",
    "airportName": "Guadalajara International",
    "city": "Guadalajara",
    "country": "Mexico",
    "ianaTimezone": "America/Mexico_City"
  },
  {
    "iataCode": "MTY",
    "airportName": "Monterrey International",
    "city": "Monterrey",
    "country": "Mexico",
    "ianaTimezone": "America/Monterrey"
  },
  {
    "iataCode": "TIJ",
    "airportName": "Tijuana International",
    "city": "Tijuana",
    "country": "Mexico",
    "ianaTimezone": "America/Tijuana"
  },
  {
    "iataCode": "LHR",
    "airportName": "London Heathrow",
    "city": "London",
    "country": "United Kingdom",
    "ianaTimezone": "Europe/London"
  },
  {
    "iataCode": "LGW",
    "airportName": "London Gatwick",
    "city": "London",
    "country": "United Kingdom",
    "ianaTimezone": "Europe/London"
  },
  {
    "iataCode": "MAN",
    "airportName": "Manchester",
    "city": "Manchester",
    "country": "United Kingdom",
    "ianaTimezone": "Europe/London"
  },
  {
    "iataCode": "EDI",
    "airportName": "Edinburgh",
    "city": "Edinburgh",
    "country": "United Kingdom",
    "ianaTimezone": "Europe/London"
  },
  {
    "iataCode": "BHX",
    "airportName": "Birmingham",
    "city": "Birmingham",
    "country": "United Kingdom",
    "ianaTimezone": "Europe/London"
  },
  {
    "iataCode": "GLA",
    "airportName": "Glasgow",
    "city": "Glasgow",
    "country": "United Kingdom",
    "ianaTimezone": "Europe/London"
  },
  {
    "iataCode": "CDG",
    "airportName": "Charles de Gaulle",
    "city": "Paris",
    "country": "France",
    "ianaTimezone": "Europe/Paris"
  },
  {
    "iataCode": "FRA",
    "airportName": "Frankfurt",
    "city": "Frankfurt",
    "country": "Germany",
    "ianaTimezone": "Europe/Berlin"
  },
  {
    "iataCode": "AMS",
    "airportName": "Amsterdam Schiphol",
    "city": "Amsterdam",
    "country": "Netherlands",
    "ianaTimezone": "Europe/Amsterdam"
  },
  {
    "iataCode": "MAD",
    "airportName": "Adolfo Suárez Madrid-Barajas",
    "city": "Madrid",
    "country": "Spain",
    "ianaTimezone": "Europe/Madrid"
  },
  {
    "iataCode": "BCN",
    "airportName": "Barcelona-El Prat",
    "city": "Barcelona",
    "country": "Spain",
    "ianaTimezone": "Europe/Madrid"
  },
  {
    "iataCode": "FCO",
    "airportName": "Leonardo da Vinci-Fiumicino",
    "city": "Rome",
    "country": "Italy",
    "ianaTimezone": "Europe/Rome"
  },
  {
    "iataCode": "MXP",
    "airportName": "Milan Malpensa",
    "city": "Milan",
    "country": "Italy",
    "ianaTimezone": "Europe/Rome"
  },
  {
    "iataCode": "MUC",
    "airportName": "Munich",
    "city": "Munich",
    "country": "Germany",
    "ianaTimezone": "Europe/Berlin"
  },
  {
    "iataCode": "ZRH",
    "airportName": "Zurich",
    "city": "Zurich",
    "country": "Switzerland",
    "ianaTimezone": "Europe/Zurich"
  },
  {
    "iataCode": "VIE",
    "airportName": "Vienna International",
    "city": "Vienna",
    "country": "Austria",
    "ianaTimezone": "Europe/Vienna"
  },
  {
    "iataCode": "CPH",
    "airportName": "Copenhagen",
    "city": "Copenhagen",
    "country": "Denmark",
    "ianaTimezone": "Europe/Copenhagen"
  },
  {
    "iataCode": "ARN",
    "airportName": "Stockholm Arlanda",
    "city": "Stockholm",
    "country": "Sweden",
    "ianaTimezone": "Europe/Stockholm"
  },
  {
    "iataCode": "OSL",
    "airportName": "Oslo Gardermoen",
    "city": "Oslo",
    "country": "Norway",
    "ianaTimezone": "Europe/Oslo"
  },
  {
    "iataCode": "HEL",
    "airportName": "Helsinki-Vantaa",
    "city": "Helsinki",
    "country": "Finland",
    "ianaTimezone": "Europe/Helsinki"
  },
  {
    "iataCode": "BRU",
    "airportName": "Brussels",
    "city": "Brussels",
    "country": "Belgium",
    "ianaTimezone": "Europe/Brussels"
  },
  {
    "iataCode": "DUB",
    "airportName": "Dublin",
    "city": "Dublin",
    "country": "Ireland",
    "ianaTimezone": "Europe/Dublin"
  },
  {
    "iataCode": "LIS",
    "airportName": "Lisbon Portela",
    "city": "Lisbon",
    "country": "Portugal",
    "ianaTimezone": "Europe/Lisbon"
  },
  {
    "iataCode": "PRG",
    "airportName": "Václav Havel Prague",
    "city": "Prague",
    "country": "Czech Republic",
    "ianaTimezone": "Europe/Prague"
  },
  {
    "iataCode": "WAW",
    "airportName": "Warsaw Chopin",
    "city": "Warsaw",
    "country": "Poland",
    "ianaTimezone": "Europe/Warsaw"
  },
  {
    "iataCode": "BUD",
    "airportName": "Budapest Ferenc Liszt International",
    "city": "Budapest",
    "country": "Hungary",
    "ianaTimezone": "Europe/Budapest"
  },
  {
    "iataCode": "ATH",
    "airportName": "Athens International",
    "city": "Athens",
    "country": "Greece",
    "ianaTimezone": "Europe/Athens"
  },
  {
    "iataCode": "IST",
    "airportName": "Istanbul",
    "city": "Istanbul",
    "country": "Turkey",
    "ianaTimezone": "Europe/Istanbul"
  },
  {
    "iataCode": "NRT",
    "airportName": "Narita International",
    "city": "Tokyo",
    "country": "Japan",
    "ianaTimezone": "Asia/Tokyo"
  },
  {
    "iataCode": "HND",
    "airportName": "Tokyo Haneda",
    "city": "Tokyo",
    "country": "Japan",
    "ianaTimezone": "Asia/Tokyo"
  },
  {
    "iataCode": "KIX",
    "airportName": "Kansai International",
    "city": "Osaka",
    "country": "Japan",
    "ianaTimezone": "Asia/Tokyo"
  },
  {
    "iataCode": "ICN",
    "airportName": "Incheon International",
    "city": "Seoul",
    "country": "South Korea",
    "ianaTimezone": "Asia/Seoul"
  },
  {
    "iataCode": "PEK",
    "airportName": "Beijing Capital International",
    "city": "Beijing",
    "country": "China",
    "ianaTimezone": "Asia/Shanghai"
  },
  {
    "iataCode": "PVG",
    "airportName": "Shanghai Pudong International",
    "city": "Shanghai",
    "country": "China",
    "ianaTimezone": "Asia/Shanghai"
  },
  {
    "iataCode": "HKG",
    "airportName": "Hong Kong International",
    "city": "Hong Kong",
    "country": "Hong Kong",
    "ianaTimezone": "Asia/Hong_Kong"
  },
  {
    "iataCode": "SIN",
    "airportName": "Singapore Changi",
    "city": "Singapore",
    "country": "Singapore",
    "ianaTimezone": "Asia/Singapore"
  },
  {
    "iataCode": "BKK",
    "airportName": "Suvarnabhumi",
    "city": "Bangkok",
    "country": "Thailand",
    "ianaTimezone": "Asia/Bangkok"
  },
  {
    "iataCode": "KUL",
    "airportName": "Kuala Lumpur International",
    "city": "Kuala Lumpur",
    "country": "Malaysia",
    "ianaTimezone": "Asia/Kuala_Lumpur"
  },
  {
    "iataCode": "MNL",
    "airportName": "Ninoy Aquino International",
    "city": "Manila",
    "country": "Philippines",
    "ianaTimezone": "Asia/Manila"
  },
  {
    "iataCode": "CGK",
    "airportName": "Soekarno-Hatta International",
    "city": "Jakarta",
    "country": "Indonesia",
    "ianaTimezone": "Asia/Jakarta"
  },
  {
    "iataCode": "DEL",
    "airportName": "Indira Gandhi International",
    "city": "Delhi",
    "country": "India",
    "ianaTimezone": "Asia/Kolkata"
  },
  {
    "iataCode": "BOM",
    "airportName": "Chhatrapati Shivaji Maharaj International",
    "city": "Mumbai",
    "country": "India",
    "ianaTimezone": "Asia/Kolkata"
  },
  {
    "iataCode": "BLR",
    "airportName": "Kempegowda International",
    "city": "Bangalore",
    "country": "India",
    "ianaTimezone": "Asia/Kolkata"
  },
  {
    "iataCode": "DXB",
    "airportName": "Dubai International",
    "city": "Dubai",
    "country": "United Arab Emirates",
    "ianaTimezone": "Asia/Dubai"
  },
  {
    "iataCode": "TLV",
    "airportName": "Ben Gurion",
    "city": "Tel Aviv",
    "country": "Israel",
    "ianaTimezone": "Asia/Tel_Aviv"
  },
  {
    "iataCode": "SYD",
    "airportName": "Sydney Kingsford Smith",
    "city": "Sydney",
    "country": "Australia",
    "ianaTimezone": "Australia/Sydney"
  },
  {
    "iataCode": "MEL",
    "airportName": "Melbourne",
    "city": "Melbourne",
    "country": "Australia",
    "ianaTimezone": "Australia/Melbourne"
  },
  {
    "iataCode": "BNE",
    "airportName": "Brisbane",
    "city": "Brisbane",
    "country": "Australia",
    "ianaTimezone": "Australia/Brisbane"
  },
  {
    "iataCode": "PER",
    "airportName": "Perth",
    "city": "Perth",
    "country": "Australia",
    "ianaTimezone": "Australia/Perth"
  },
  {
    "iataCode": "ADL",
    "airportName": "Adelaide",
    "city": "Adelaide",
    "country": "Australia",
    "ianaTimezone": "Australia/Adelaide"
  },
  {
    "iataCode": "AKL",
    "airportName": "Auckland",
    "city": "Auckland",
    "country": "New Zealand",
    "ianaTimezone": "Pacific/Auckland"
  },
  {
    "iataCode": "WLG",
    "airportName": "Wellington",
    "city": "Wellington",
    "country": "New Zealand",
    "ianaTimezone": "Pacific/Auckland"
  },
  {
    "iataCode": "CHC",
    "airportName": "Christchurch",
    "city": "Christchurch",
    "country": "New Zealand",
    "ianaTimezone": "Pacific/Auckland"
  },
  {
    "iataCode": "GRU",
    "airportName": "São Paulo-Guarulhos International",
    "city": "São Paulo",
    "country": "Brazil",
    "ianaTimezone": "America/Sao_Paulo"
  },
  {
    "iataCode": "GIG",
    "airportName": "Rio de Janeiro-Galeão International",
    "city": "Rio de Janeiro",
    "country": "Brazil",
    "ianaTimezone": "America/Sao_Paulo"
  },
  {
    "iataCode": "EZE",
    "airportName": "Ministro Pistarini International",
    "city": "Buenos Aires",
    "country": "Argentina",
    "ianaTimezone": "America/Argentina/Buenos_Aires"
  },
  {
    "iataCode": "SCL",
    "airportName": "Arturo Merino Benítez International",
    "city": "Santiago",
    "country": "Chile",
    "ianaTimezone": "America/Santiago"
  },
  {
    "iataCode": "LIM",
    "airportName": "Jorge Chávez International",
    "city": "Lima",
    "country": "Peru",
    "ianaTimezone": "America/Lima"
  },
  {
    "iataCode": "BOG",
    "airportName": "El Dorado International",
    "city": "Bogotá",
    "country": "Colombia",
    "ianaTimezone": "America/Bogota"
  },
  {
    "iataCode": "CAI",
    "airportName": "Cairo International",
    "city": "Cairo",
    "country": "Egypt",
    "ianaTimezone": "Africa/Cairo"
  },
  {
    "iataCode": "JNB",
    "airportName": "O.R. Tambo International",
    "city": "Johannesburg",
    "country": "South Africa",
    "ianaTimezone": "Africa/Johannesburg"
  },
  {
    "iataCode": "CPT",
    "airportName": "Cape Town International",
    "city": "Cape Town",
    "country": "South Africa",
    "ianaTimezone": "Africa/Johannesburg"
  },
  {
    "iataCode": "LOS",
    "airportName": "Murtala Muhammed International",
    "city": "Lagos",
    "country": "Nigeria",
    "ianaTimezone": "Africa/Lagos"
  },
  {
    "iataCode": "NBO",
    "airportName": "Jomo Kenyatta International",
    "city": "Nairobi",
    "country": "Kenya",
    "ianaTimezone": "Africa/Nairobi"
  },
  {
    "iataCode": "ADD",
    "airportName": "Addis Ababa Bole International",
    "city": "Addis Ababa",
    "country": "Ethiopia",
    "ianaTimezone": "Africa/Addis_Ababa"
  },
  {
    "iataCode": "CAS",
    "airportName": "Mohammed V International",
    "city": "Casablanca",
    "country": "Morocco",
    "ianaTimezone": "Africa/Casablanca"
  }
] as const

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
