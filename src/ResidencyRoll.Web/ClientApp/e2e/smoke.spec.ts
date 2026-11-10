import { expect, test } from '@playwright/test'

test('mcp initialize answers with the server name', async ({ request }) => {
  const response = await request.post('/mcp', {
    headers: {
      Accept: 'application/json, text/event-stream',
      'Content-Type': 'application/json',
    },
    data: {
      jsonrpc: '2.0',
      id: 1,
      method: 'initialize',
      params: {
        protocolVersion: '2025-11-25',
        capabilities: {},
        clientInfo: { name: 'playwright', version: '1' },
      },
    },
  })

  expect(response.ok()).toBeTruthy()
  expect(await response.text()).toContain('ResidencyRoll')
})

test('signed-out local mode renders every route', async ({ page }) => {
  await page.goto('/')
  await expect(page.getByRole('heading', { name: 'Residency Status Dashboard' })).toBeVisible()

  await page.getByRole('link', { name: 'Manage Trips' }).click()
  await expect(page.getByRole('heading', { name: 'Manage Trips' })).toBeVisible()

  await page.getByRole('link', { name: 'Forecast' }).click()
  await expect(page.getByRole('heading', { name: 'Trip Forecast' })).toBeVisible()

  await page.getByRole('link', { name: 'Travellers' }).click()
  await expect(page.getByRole('heading', { name: 'Travellers' })).toBeVisible()

  await page.getByRole('link', { name: 'System Info' }).click()
  await expect(page.getByRole('heading', { name: 'System Info' })).toBeVisible()

  await page.goto('/not-a-page')
  await expect(page.getByRole('heading', { name: 'Not Found' })).toBeVisible()
})

test('a trip can be created, exported, forecast, and deleted', async ({ page }) => {
  await page.goto('/manage-trips')
  await page.getByRole('button', { name: 'Add Trip' }).first().click()
  await selectAirport(page, 'Departure Airport', 'YHZ')
  await selectAirport(page, 'Arrival Airport', 'YUL')
  await page.getByRole('button', { name: 'Save Trip' }).click()
  await expect(page.getByText('YHZ - Halifax')).toBeVisible()

  const downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: 'Import / Export' }).click()
  await page.getByRole('button', { name: 'Export Trips (CSV)' }).click()
  expect((await downloadPromise).suggestedFilename()).toMatch(/\.csv$/)

  await page.getByRole('button', { name: 'Delete' }).click()
  await page.getByRole('dialog').getByRole('button', { name: 'Delete' }).click()
  await expect(page.getByText('YHZ - Halifax')).toHaveCount(0)

  await page.goto('/forecast')
  await selectAirport(page, 'Arrival Airport', 'LHR')
  await page.getByRole('button', { name: 'Calculate Forecast' }).click()
  await expect(page.getByRole('heading', { name: 'Current (Last 365 Days)' })).toBeVisible()
})

test('a second traveller keeps a separate day count', async ({ page }) => {
  await page.goto('/travellers')
  await page.getByLabel('Name').fill('Sam')
  await page.getByRole('button', { name: 'Add traveller' }).click()
  await expect(page.getByRole('list').getByText('Sam', { exact: true })).toBeVisible()

  await page.getByRole('link', { name: 'Manage Trips' }).click()
  await saveTrip(page, 'YHZ', 'LHR', '2026-06-01', '2026-06-01', 'Sam')
  await saveTrip(page, 'LHR', 'YHZ', '2026-06-10', '2026-06-10', 'Sam')

  await page.getByLabel('Traveller').selectOption('Sam')
  await expect(page.getByText('LHR - London').first()).toBeVisible()
  await saveTrip(page, 'YUL', 'CDG', '2026-07-01', '2026-07-01')
  await saveTrip(page, 'CDG', 'YUL', '2026-07-10', '2026-07-10')

  await page.goto('/')
  await expect(page.getByRole('heading', { name: 'France' })).toBeVisible()
  await page.getByLabel('Traveller').selectOption({ index: 0 })
  await expect(page.getByRole('heading', { name: 'United Kingdom' })).toBeVisible()
  await expect(page.getByRole('heading', { name: 'France' })).toHaveCount(0)

  await page.goto('/manage-trips')
  await deleteTrips(page, 2)
  await page.getByLabel('Traveller').selectOption('Sam')
  await deleteTrips(page, 4)
  await page.goto('/travellers')
  await page.getByRole('button', { name: 'Delete' }).click()
  await page.getByRole('dialog').getByRole('button', { name: 'Delete' }).click()
})

async function saveTrip(
  page: import('@playwright/test').Page,
  from: string,
  to: string,
  departure: string,
  arrival: string,
  alsoFor?: string,
) {
  await page.getByRole('button', { name: 'Add Trip' }).first().click()
  await selectAirport(page, 'Departure Airport', from)
  await selectAirport(page, 'Arrival Airport', to)
  await page.getByLabel('Departure Date').fill(departure)
  await page.getByLabel('Arrival Date').fill(arrival)
  if (alsoFor) {
    await page.getByRole('checkbox', { name: alsoFor }).check()
  }
  await page.getByRole('button', { name: 'Save Trip' }).click()
  await expect(page.getByText(`${to} -`).first()).toBeVisible()
}

async function deleteTrips(page: import('@playwright/test').Page, count: number) {
  for (let remaining = count; remaining > 0; remaining -= 1) {
    await page.getByRole('button', { name: 'Delete' }).first().click()
    const dialog = page.getByRole('dialog')
    await dialog.getByRole('button', { name: 'Delete' }).click({ force: true })
    await expect(dialog).toBeHidden()
  }
}

async function selectAirport(page: import('@playwright/test').Page, label: string, code: string) {
  const box = page.getByRole('combobox', { name: label })
  await box.click()
  await box.fill(code)
  await page.getByRole('option', { name: new RegExp(code) }).first().click()
}
