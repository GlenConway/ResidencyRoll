import { Button, InlineLoading, InlineNotification } from '@carbon/react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { DateTime } from 'luxon'
import { backupNow, downloadBackup, getSystemInfo } from '../api/client.ts'
import { formatSize } from '../lib/formatSize.ts'

export function SystemInfoPage() {
  const queryClient = useQueryClient()
  const infoQuery = useQuery({ queryKey: ['system-info'], queryFn: getSystemInfo })
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
        <h1>System Info</h1>
        <InlineLoading description="Loading system information" />
      </section>
    )
  }

  if (infoQuery.isError || !infoQuery.data || Array.isArray(infoQuery.data)) {
    return (
      <section>
        <h1>System Info</h1>
        <InlineNotification kind="error" title="Unable to load system information." hideCloseButton />
      </section>
    )
  }

  const info = infoQuery.data
  const nextBackup = info.nextBackupUtc ? DateTime.fromISO(info.nextBackupUtc, { zone: 'utc' }) : null

  return (
    <section>
      <h1>System Info</h1>
      <h2>Database</h2>
      <table>
        <tbody>
          <tr>
            <th>Location</th>
            <td>
              <code>{info.databasePath}</code>
            </td>
          </tr>
          <tr>
            <th>Status</th>
            <td>{info.databaseExists ? 'File found' : 'File not found'}</td>
          </tr>
          <tr>
            <th>Size</th>
            <td>{formatSize(info.databaseSizeBytes)}</td>
          </tr>
          <tr>
            <th>Users</th>
            <td>{info.userCount}</td>
          </tr>
          <tr>
            <th>Trips</th>
            <td>{info.tripCount}</td>
          </tr>
        </tbody>
      </table>

      <h2>Backups</h2>
      <p>
        {nextBackup?.isValid
          ? `Next scheduled backup: ${nextBackup.toFormat('yyyy-MM-dd HH:mm')} UTC (weekly, Mondays)`
          : 'Scheduled backups are disabled.'}
      </p>
      <Button disabled={backup.isPending} onClick={() => backup.mutate()}>
        {backup.isPending ? 'Backing up…' : 'Backup now'}
      </Button>
      {backup.isSuccess ? <InlineNotification kind="success" title={`Backup created: ${backup.data.fileName}`} hideCloseButton /> : null}
      {backup.isError ? <InlineNotification kind="error" title="Backup failed. Check the API logs." hideCloseButton /> : null}
      {info.backups.length === 0 ? <p>No backups yet.</p> : null}
      {info.backups.length > 0 ? (
        <table>
          <thead>
            <tr>
              <th>File</th>
              <th>Created (UTC)</th>
              <th>Size</th>
              <th>Download</th>
            </tr>
          </thead>
          <tbody>
            {info.backups.map((item) => (
              <tr key={item.fileName}>
                <td>
                  <code>{item.fileName}</code>
                </td>
                <td>{DateTime.fromISO(item.createdUtc, { zone: 'utc' }).toFormat('yyyy-MM-dd HH:mm')}</td>
                <td>{formatSize(item.sizeBytes)}</td>
                <td>
                  <Button kind="ghost" size="sm" onClick={() => void downloadBackup(item.fileName)}>
                    Download
                  </Button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : null}

      <h2>Application</h2>
      <table>
        <tbody>
          <tr>
            <th>API version</th>
            <td>{info.applicationVersion}</td>
          </tr>
          <tr>
            <th>Environment</th>
            <td>{info.environment}</td>
          </tr>
        </tbody>
      </table>
    </section>
  )
}
