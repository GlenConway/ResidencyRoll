const colorPalette = [
  '#1f77b4',
  '#ff7f0e',
  '#2ca02c',
  '#d62728',
  '#9467bd',
  '#8c564b',
  '#e377c2',
  '#7f7f7f',
  '#bcbd22',
  '#17becf',
  '#aec7e8',
  '#ffbb78',
  '#98df8a',
  '#ff9896',
  '#c5b0d5',
  '#c49c94',
  '#f7b6d2',
  '#c7c7c7',
  '#dbbd22',
  '#9edae5',
]

export const transitColor = '#FF9800'

export class CountryColorService {
  homeCountry: string | null = null
  private readonly colors = new Map<string, string>()

  getCountryColor(country: string): string {
    if (country.length === 0) {
      return '#808080'
    }

    const existing = this.colors.get(country)
    if (existing) {
      return existing
    }

    const color = colorPalette[this.colors.size % colorPalette.length] ?? '#808080'
    this.colors.set(country, color)
    return color
  }

  getCountryColorDark(country: string): string {
    return darkenColor(this.getCountryColor(country), 0.8)
  }

  registerCountries(countries: readonly string[]): void {
    for (const country of countries) {
      this.getCountryColor(country)
    }
  }

  reset(): void {
    this.colors.clear()
    this.homeCountry = null
  }
}

export function assignCountryColors(countries: readonly string[]): Map<string, string> {
  const service = new CountryColorService()
  service.registerCountries([...countries].sort((left, right) => left.localeCompare(right)))
  return new Map(
    [...countries].sort((left, right) => left.localeCompare(right)).map((country) => [country, service.getCountryColor(country)]),
  )
}

export function darkenColor(hexColor: string, factor: number): string {
  if (!hexColor.startsWith('#') || hexColor.length !== 7) {
    return hexColor
  }

  const channel = (start: number) =>
    Math.trunc(Number.parseInt(hexColor.slice(start, start + 2), 16) * factor)
      .toString(16)
      .padStart(2, '0')
      .toUpperCase()

  return `#${channel(1)}${channel(3)}${channel(5)}`
}

export function colorWithAlpha(hexColor: string, alpha = 0.1): string {
  if (!hexColor.startsWith('#') || hexColor.length !== 7) {
    return hexColor
  }

  const r = Number.parseInt(hexColor.slice(1, 3), 16)
  const g = Number.parseInt(hexColor.slice(3, 5), 16)
  const b = Number.parseInt(hexColor.slice(5, 7), 16)
  return `rgba(${r}, ${g}, ${b}, ${alpha})`
}
