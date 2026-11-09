const units = ['B', 'KB', 'MB', 'GB']

export function formatSize(bytes: number): string {
  let size = bytes
  let unit = 0
  while (size >= 1024 && unit < units.length - 1) {
    size /= 1024
    unit += 1
  }
  if (unit === 0) {
    return `${bytes} B`
  }
  const rounded = trim(size)
  return `${rounded} ${units[unit]} (${bytes.toLocaleString('en-US')} bytes)`
}

function trim(value: number): string {
  return value.toFixed(2).replace(/\.?0+$/, '')
}
