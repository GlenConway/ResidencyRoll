export function mcpUrl(origin: string): string {
  return `${origin.replace(/\/$/, '')}/mcp`
}

export function claudeCodeCommand(url: string): string {
  return `claude mcp add --transport http residencyroll ${url}`
}

export function mcpJsonSnippet(url: string): string {
  return JSON.stringify({ mcpServers: { residencyroll: { type: 'http', url } } }, null, 2)
}
