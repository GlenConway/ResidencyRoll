import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { SystemInfoPage } from './SystemInfoPage.tsx'

const info = {
  databasePath: '/app/data/residencyroll.db',
  databaseExists: true,
  databaseSizeBytes: 1536,
  userCount: 1,
  tripCount: 4,
  applicationVersion: '1.0.0',
  environment: 'Development',
  nextBackupUtc: null,
  backups: [{ fileName: 'backup.db', sizeBytes: 512, createdUtc: '2026-03-02T15:04:00Z' }],
}

describe('SystemInfoPage', () => {
  let mcpInfo = { enabled: true, oauthRequired: false, url: 'http://localhost/mcp' }

  beforeEach(() => {
    mcpInfo = { enabled: true, oauthRequired: false, url: 'http://localhost/mcp' }
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
        const url = String(input)
        if (url.includes('/bff/mcp-info')) {
          return Promise.resolve(Response.json(mcpInfo))
        }
        if (url.includes('/backup') && init?.method === 'POST') {
          return Promise.resolve(Response.json({ fileName: 'backup-new.db' }))
        }
        if (url.includes('/backups/')) {
          return Promise.resolve(new Response('db', { headers: { 'Content-Disposition': 'attachment; filename="backup.db"' } }))
        }
        return Promise.resolve(Response.json(info))
      }),
    )
    vi.stubGlobal('URL', { ...URL, createObjectURL: () => 'blob:backup', revokeObjectURL: () => undefined })
    Object.assign(navigator, { clipboard: { writeText: vi.fn().mockResolvedValue(undefined) } })
  })

  it('shows the database, a disabled schedule, and downloads a backup through the API', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    render(
      <QueryClientProvider client={client}>
        <SystemInfoPage />
      </QueryClientProvider>,
    )

    expect(await screen.findByText('/app/data/residencyroll.db')).toBeInTheDocument()
    expect(screen.getByText('1.5 KB (1,536 bytes)')).toBeInTheDocument()
    expect(screen.getByText('Scheduled backups are disabled.')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Download' }))
    expect(fetch).toHaveBeenCalledWith(expect.stringContaining('/api/v1/system/backups/backup.db'), expect.anything())

    const url = `${window.location.origin}/mcp`
    expect(screen.getByText('OAuth is off. This endpoint is open and runs as the local user. Use it only on a trusted network.')).toBeInTheDocument()
    expect(screen.getByText(url)).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Copy Claude Code command' }))
    expect(navigator.clipboard.writeText).toHaveBeenCalledWith(`claude mcp add --transport http residencyroll ${url}`)
    await userEvent.click(screen.getByRole('button', { name: 'Copy JSON snippet' }))
    expect(navigator.clipboard.writeText).toHaveBeenCalledWith(expect.stringContaining('"type": "http"'))
  })

  it('says when OAuth is required', async () => {
    mcpInfo = { enabled: true, oauthRequired: true, url: 'https://residencyroll.kinsac.com/mcp' }
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    render(
      <QueryClientProvider client={client}>
        <SystemInfoPage />
      </QueryClientProvider>,
    )
    expect(await screen.findByText(/OAuth is required/)).toBeInTheDocument()
  })

  it('says when MCP is turned off', async () => {
    mcpInfo = { enabled: false, oauthRequired: false, url: '' }
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    render(
      <QueryClientProvider client={client}>
        <SystemInfoPage />
      </QueryClientProvider>,
    )
    expect(await screen.findByText('MCP is turned off.')).toBeInTheDocument()
  })
})
