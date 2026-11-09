const codes: Record<string, string> = {
  Canada: 'CA',
  'United States': 'US',
  USA: 'US',
  'United Kingdom': 'UK',
  Australia: 'AU',
  'New Zealand': 'NZ',
  France: 'FR',
  Germany: 'DE',
  Japan: 'JP',
  China: 'CN',
  India: 'IN',
  Brazil: 'BR',
  Mexico: 'MX',
  Spain: 'ES',
  Italy: 'IT',
}

export function getCountryCode(countryName: string): string {
  const known = codes[countryName]
  if (known) {
    return known
  }

  return countryName.slice(0, Math.min(3, countryName.length)).toUpperCase()
}
