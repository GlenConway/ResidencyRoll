import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { DateTime } from 'luxon'
import { backupNow, downloadBackup, getMcpInfo, getSystemInfo } from '../api/client.ts'
import type { McpInfo } from '../api/types.ts'
import { formatSize } from '../lib/formatSize.ts'
import { claudeCodeCommand, mcpJsonSnippet, mcpUrl } from '../lib/mcpClient.ts'

export function SystemInfoPage() {
  const queryClient = useQueryClient()
  const infoQuery = useQuery({ queryKey: ['system-info'], queryFn: getSystemInfo })
  const mcpQuery = useQuery({ queryKey: ['mcp-info'], queryFn: getMcpInfo })
  const backup = useMutation({
    mutationFn: backupNow,
    onSuccess: async (created) => {
      await queryClient.invalidateQueries({ queryKey: ['system-info'] })
      return created
    },
  })

  if (infoQuery.isLoading) {
    return (
      <section>
        <h1 className="text-2xl font-semibold tracking-tight">System Info</h1>
        <p className="text-sm text-muted-foreground">Loading system information</p>
      </section>
    )
  }

  if (infoQuery.isError || !infoQuery.data || Array.isArray(infoQuery.data)) {
    return (
      <section>
        <h1 className="text-2xl font-semibold tracking-tight">System Info</h1>
        <Alert variant="destructive">
          <AlertDescription>Unable to load system information.</AlertDescription>
        </Alert>
      </section>
    )
  }

  const info = infoQuery.data
  const nextBackup = info.nextBackupUtc ? DateTime.fromISO(info.nextBackupUtc, { zone: 'utc' }) : null

  return (
    <section className="flex max-w-3xl flex-col gap-6">
      <h1 className="text-2xl font-semibold tracking-tight">System Info</h1>
      <div className="rounded-lg border bg-card p-4">
        <h2 className="mb-3 text-lg font-medium">Database</h2>
        <dl className="grid grid-cols-[8rem_1fr] gap-y-2 text-sm">
          <dt className="text-muted-foreground">Location</dt>
          <dd>
            <code>{info.databasePath}</code>
          </dd>
          <dt className="text-muted-foreground">Status</dt>
          <dd>{info.databaseExists ? 'File found' : 'File not found'}</dd>
          <dt className="text-muted-foreground">Size</dt>
          <dd>{formatSize(info.databaseSizeBytes)}</dd>
          <dt className="text-muted-foreground">Users</dt>
          <dd>{info.userCount}</dd>
          <dt className="text-muted-foreground">Trips</dt>
          <dd>{info.tripCount}</dd>
        </dl>
      </div>

      <div className="flex flex-col gap-3 rounded-lg border bg-card p-4">
      <h2 className="text-lg font-medium">Backups</h2>
      <p>
        {nextBackup?.isValid
          ? `Next scheduled backup: ${nextBackup.toFormat('yyyy-MM-dd HH:mm')} UTC (weekly, Mondays)`
          : 'Scheduled backups are disabled.'}
      </p>
      <Button className="w-fit" disabled={backup.isPending} onClick={() => backup.mutate()}>
        {backup.isPending ? 'Backing up…' : 'Backup now'}
      </Button>
      {backup.isSuccess ? (
        <Alert>
          <AlertDescription>Backup created: {backup.data.fileName}</AlertDescription>
        </Alert>
      ) : null}
      {backup.isError ? (
        <Alert variant="destructive">
          <AlertDescription>Backup failed. Check the API logs.</AlertDescription>
        </Alert>
      ) : null}
      {info.backups.length === 0 ? <p>No backups yet.</p> : null}
      {info.backups.length > 0 ? (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>File</TableHead>
              <TableHead>Created (UTC)</TableHead>
              <TableHead>Size</TableHead>
              <TableHead>Download</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {info.backups.map((item) => (
              <TableRow key={item.fileName}>
                <TableCell>
                  <code>{item.fileName}</code>
                </TableCell>
                <TableCell>{DateTime.fromISO(item.createdUtc, { zone: 'utc' }).toFormat('yyyy-MM-dd HH:mm')}</TableCell>
                <TableCell>{formatSize(item.sizeBytes)}</TableCell>
                <TableCell>
                  <Button variant="ghost" size="sm" onClick={() => void downloadBackup(item.fileName)}>
                    Download
                  </Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      ) : null}
      </div>

      <McpConnectPanel query={mcpQuery} />

      <div className="rounded-lg border bg-card p-4">
      <h2 className="mb-3 text-lg font-medium">Application</h2>
      <dl className="grid grid-cols-[8rem_1fr] gap-y-2 text-sm">
        <dt className="text-muted-foreground">API version</dt>
        <dd>{info.applicationVersion}</dd>
        <dt className="text-muted-foreground">Environment</dt>
        <dd>{info.environment}</dd>
      </dl>
      </div>
    </section>
  )
}

function McpConnectPanel({ query }: { query: { isLoading: boolean; isError: boolean; data?: McpInfo } }) {
  const [copied, setCopied] = useState<'claude' | 'json' | 'failed' | null>(null)
  const url = mcpUrl(window.location.origin)
  const command = claudeCodeCommand(url)
  const snippet = mcpJsonSnippet(url)

  async function copy(value: string, which: 'claude' | 'json') {
    try {
      await navigator.clipboard.writeText(value)
      setCopied(which)
    } catch {
      setCopied('failed')
    }
  }

  return (
    <div className="flex flex-col gap-3 rounded-lg border bg-card p-4">
      <h2 className="text-lg font-medium">Connect an AI client</h2>
      {query.isLoading ? <p>Loading connection details.</p> : null}
      {query.isError ? (
        <Alert variant="destructive">
          <AlertDescription>Unable to load MCP connection details.</AlertDescription>
        </Alert>
      ) : null}
      {query.data && !query.data.enabled ? <p>MCP is turned off.</p> : null}
      {query.data?.enabled ? (
        <>
          <p className="text-sm">
            {query.data.oauthRequired
              ? 'OAuth is required. The client signs you in and sends a token issued for this server.'
              : 'OAuth is off. This endpoint is open and runs as the local user. Use it only on a trusted network.'}
          </p>
          <dl className="grid grid-cols-[8rem_1fr] gap-y-2 text-sm">
            <dt className="text-muted-foreground">MCP URL</dt>
            <dd>
              <code>{url}</code>
            </dd>
          </dl>
          <pre className="overflow-x-auto rounded-md bg-muted p-3 text-xs">{command}</pre>
          <Button className="w-fit" variant="outline" onClick={() => void copy(command, 'claude')}>
            Copy Claude Code command
          </Button>
          <pre className="overflow-x-auto rounded-md bg-muted p-3 text-xs">{snippet}</pre>
          <Button className="w-fit" variant="outline" onClick={() => void copy(snippet, 'json')}>
            Copy JSON snippet
          </Button>
          {copied === 'claude' ? <p>Copied the Claude Code command.</p> : null}
          {copied === 'json' ? <p>Copied the JSON snippet.</p> : null}
          {copied === 'failed' ? <p>Copy failed. Select the text and copy it manually.</p> : null}
        </>
      ) : null}
    </div>
  )
}
