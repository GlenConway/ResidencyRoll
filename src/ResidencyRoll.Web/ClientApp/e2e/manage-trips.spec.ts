import { expect, test } from '@playwright/test'

test('adds a trip, adds a leg, edits it, then deletes it', async ({ page }) => {
  test.setTimeout(60_000)
  const trips: Array<Record<string, unknown>> = []
  let nextId = 1

  await page.route('**/bff/user', (route) =>
    route.fulfill({ json: { name: 'Ada Lovelace', email: 'ada@example.com', isAuthenticated: true } }),
  )
  await page.route('**/parse-itinerary/availability', (route) => route.fulfill({ json: false }))
  await page.route('**/api/v1/trips**', async (route) => {
    const request = route.request()
    const url = new URL(request.url())
    const id = Number(url.pathname.split('/').pop())
    if (request.method() === 'GET' && url.pathname.endsWith('/trips')) {
      await route.fulfill({ json: trips })
      return
    }
    if (request.method() === 'GET') {
      await route.fulfill({ json: trips.find((trip) => trip.id === id) ?? null })
      return
    }
    if (request.method() === 'POST') {
      const body = request.postDataJSON() as Record<string, unknown>
      const created = { ...body, id: nextId }
      nextId += 1
      trips.unshift(created)
      await route.fulfill({ json: created })
      return
    }
    if (request.method() === 'PUT') {
      const body = request.postDataJSON() as Record<string, unknown>
      const index = trips.findIndex((trip) => trip.id === id)
      if (index >= 0) trips[index] = { ...trips[index], ...body, id }
      await route.fulfill({ status: 204 })
      return
    }
    if (request.method() === 'DELETE') {
      const index = trips.findIndex((trip) => trip.id === id)
      if (index >= 0) trips.splice(index, 1)
      await route.fulfill({ status: 204 })
      return
    }
    await route.fallback()
  })

  await page.goto('http://127.0.0.1:5173/manage-trips')
  await page.getByRole('button', { name: 'Add Trip' }).first().click()
  await selectAirport(page, 'Departure Airport', 'YHZ')
  await selectAirport(page, 'Arrival Airport', 'YUL')
  await page.getByRole('button', { name: 'Save Trip' }).click()
  await expect(page.getByText('YHZ - Halifax')).toBeVisible()

  await page.getByRole('button', { name: 'Add Leg' }).click()
  await selectAirport(page, 'Arrival Airport', 'LHR')
  await page.getByRole('button', { name: 'Save Trip' }).click()
  await expect(page.getByText('LHR - London')).toBeVisible()

  await page.getByRole('row', { name: /London/ }).getByRole('button', { name: 'Edit' }).click()
  await page.getByLabel('Arrival Time').fill('18:30')
  await page.getByRole('button', { name: 'Save Trip' }).click()
  await expect(page.getByText(/18:30/)).toBeVisible()

  await page.getByRole('row', { name: /London/ }).getByRole('button', { name: 'Delete' }).click()
  await page.getByRole('dialog').getByRole('button', { name: 'Delete' }).click()
  await expect(page.getByText('LHR - London')).toHaveCount(0)
})

async function selectAirport(page: import('@playwright/test').Page, label: string, code: string) {
  const box = page.getByRole('combobox', { name: label })
  await box.click()
  await box.fill(code)
  await page.getByRole('option', { name: new RegExp(code) }).first().click()
}
