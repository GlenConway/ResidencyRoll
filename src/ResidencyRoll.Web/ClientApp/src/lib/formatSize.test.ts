import { describe, expect, it } from 'vitest'
import { formatSize } from './formatSize.ts'

describe('formatSize', () => {
  it('keeps bytes exact and scales larger sizes', () => {
    expect(formatSize(512)).toBe('512 B')
    expect(formatSize(1536)).toBe('1.5 KB (1,536 bytes)')
    expect(formatSize(1024)).toBe('1 KB (1,024 bytes)')
  })
})
