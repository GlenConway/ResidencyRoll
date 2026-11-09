import { expect, test } from '@playwright/test'

test('signed-out local mode renders every route', async ({ page }) => {
  await page.goto('/')
  await expect(page.getByRole('heading', { name: 'Residency Status Dashboard' })).toBeVisible()

  await page.getByRole('link', { name: 'Manage Trips' }).click()
  await expect(page.getByRole('heading', { name: 'Manage Trips' })).toBeVisible()

  await page.getByRole('link', { name: 'Forecast' }).click()
  await expect(page.getByRole('heading', { name: 'Trip Forecast' })).toBeVisible()

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

async function selectAirport(page: import('@playwright/test').Page, label: string, code: string) {
  const box = page.getByRole('combobox', { name: label })
  await box.click()
  await box.fill(code)
  await page.getByRole('option', { name: new RegExp(code) }).first().click()
}
