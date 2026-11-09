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
  beforeEach(() => {
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
        const url = String(input)
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
  })
})
