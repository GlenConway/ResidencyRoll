import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import App from '../App.tsx'

function renderApp(path: string) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[path]}>
        <App />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('App shell', () => {
  beforeEach(() => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockImplementation(() => Promise.resolve(Response.json({ name: 'Ada Lovelace', email: 'ada@example.com', isAuthenticated: true }))),
    )
  })

  it('shows the header, the signed-in name, and the navigation', async () => {
    renderApp('/')
    expect(screen.getByText('Residency Roll - Residency Status Dashboard')).toBeInTheDocument()
    expect(await screen.findByText('Ada Lovelace')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Home' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Manage Trips' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Forecast' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Travellers' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'System Info' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Logout' })).toBeInTheDocument()
    expect(document.title).toBe('Residency Roll - Travel Tracker')
  })

  it('posts logout with the CSRF field', () => {
    renderApp('/')
    const form = screen.getByRole('button', { name: 'Logout' }).closest('form')
    expect(form).toHaveAttribute('action', '/auth/logout')
    expect(form).toHaveAttribute('method', 'post')
    expect(form?.querySelector('input[name="X-CSRF"]')).toHaveValue('1')
  })

  it('renders the not-found page for an unknown route', async () => {
    renderApp('/missing')
    expect(await screen.findByRole('heading', { name: 'Not Found' })).toBeInTheDocument()
    expect(document.title).toBe('Not Found - Residency Roll')
  })
})
