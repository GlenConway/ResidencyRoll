export interface Trip {
  id: number
  departureCountry: string
  departureCity: string
  departureDateTime: string
  departureTimezone: string
  departureIataCode: string | null
  arrivalCountry: string
  arrivalCity: string
  arrivalDateTime: string
  arrivalTimezone: string
  arrivalIataCode: string | null
}

export interface TripLeg {
  departureCountry: string
  departureCity: string
  departureDateTime: string
  departureTimezone: string
  departureIataCode: string | null
  arrivalCountry: string
  arrivalCity: string
  arrivalDateTime: string
  arrivalTimezone: string
  arrivalIataCode: string | null
}

export interface ResidencySummary {
  countryName: string
  totalDays: number
  ruleType: string
  thresholdDays: number
  isApproachingThreshold: boolean
  daysUntilThreshold: number
}

export interface DailyPresence {
  date: string
  locationAtMidnight: string
  locationsDuringDay: string[]
  isInTransitAtMidnight: boolean
}

export interface CountryDays {
  countryName: string
  days: number
}

export interface ForecastResponse {
  current: CountryDays[]
  forecast: CountryDays[]
}

export interface ItineraryFlightLeg {
  departure_airport: string
  departure_datetime_local: string
  arrival_airport: string
  arrival_datetime_local: string | null
}

export interface ItineraryParsingResponse {
  legs: ItineraryFlightLeg[]
  error: string | null
}

export interface ImportResult {
  imported: number
  message: string
  errors: number
}

export interface BackupInfo {
  fileName: string
  sizeBytes: number
  createdUtc: string
}

export interface SystemInfo {
  databasePath: string
  databaseExists: boolean
  databaseSizeBytes: number
  userCount: number
  tripCount: number
  applicationVersion: string
  environment: string
  backups: BackupInfo[]
  nextBackupUtc: string | null
}

export interface CurrentUser {
  name: string
  email: string
  isAuthenticated: boolean
}
