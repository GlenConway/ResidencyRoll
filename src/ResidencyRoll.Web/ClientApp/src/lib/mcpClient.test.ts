import { describe, expect, it } from 'vitest'
import { claudeCodeCommand, mcpJsonSnippet, mcpUrl } from './mcpClient.ts'

describe('mcp client snippets', () => {
  it('builds the public URL, Claude Code command, and file snippet', () => {
    expect(mcpUrl('https://residencyroll.kinsac.com')).toBe('https://residencyroll.kinsac.com/mcp')
    expect(mcpUrl('https://residencyroll.kinsac.com/')).toBe('https://residencyroll.kinsac.com/mcp')

    const url = 'http://localhost:8081/mcp'
    expect(claudeCodeCommand(url)).toBe('claude mcp add --transport http residencyroll http://localhost:8081/mcp')
    expect(JSON.parse(mcpJsonSnippet(url))).toEqual({
      mcpServers: { residencyroll: { type: 'http', url } },
    })
  })
})