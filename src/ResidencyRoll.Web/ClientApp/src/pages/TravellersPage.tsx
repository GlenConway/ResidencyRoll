import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { ApiError, createTraveller, deleteTraveller } from '../api/client.ts'
import type { Traveller } from '../api/types.ts'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Field } from '../components/Field.tsx'
import { PageHeading, useTravellers } from '../shell/travellers.tsx'

export function TravellersPage() {
  const queryClient = useQueryClient()
  const { travellers, save } = useTravellers()
  const [name, setName] = useState('')
  const [homeCountry, setHomeCountry] = useState('')
  const [message, setMessage] = useState('')
  const [editing, setEditing] = useState<Traveller | null>(null)
  const [pendingDelete, setPendingDelete] = useState<Traveller | null>(null)
  const [needsTripDelete, setNeedsTripDelete] = useState(false)

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['travellers'] })

  const adding = useMutation({
    mutationFn: () => createTraveller(name.trim(), homeCountry.trim() || null),
    onSuccess: async () => {
      setName('')
      setHomeCountry('')
      setMessage('')
      await refresh()
    },
    onError: (error: Error) => setMessage(error.message),
  })

  const removing = useMutation({
    mutationFn: ({ person, deleteTrips }: { person: Traveller; deleteTrips: boolean }) => deleteTraveller(person.id, deleteTrips),
    onSuccess: async () => {
      setPendingDelete(null)
      setNeedsTripDelete(false)
      await refresh()
    },
    onError: (error: Error) => {
      if (error instanceof ApiError && error.status === 409 && error.message.includes('deleteTrips')) {
        setNeedsTripDelete(true)
        return
      }

      setMessage(error.message)
      setPendingDelete(null)
    },
  })

  return (
    <section className="flex flex-col gap-6">
      <PageHeading title="Travellers" />
      <p className="text-sm text-muted-foreground">Each person keeps their own trips and home country. The header chooses who you are looking at.</p>
      <form
        className="flex flex-wrap items-end gap-3 rounded-lg border bg-card p-5"
        onSubmit={(event) => {
          event.preventDefault()
          adding.mutate()
        }}
      >
        <Field className="w-56" id="traveller-name" label="Name" value={name} onChange={(event) => setName(event.target.value)} />
        <Field
          className="w-56"
          id="traveller-home"
          label="Home country"
          value={homeCountry}
          onChange={(event) => setHomeCountry(event.target.value)}
        />
        <Button type="submit" disabled={name.trim().length === 0 || adding.isPending}>
          Add traveller
        </Button>
      </form>
      {message ? <p className="text-sm text-destructive">{message}</p> : null}
      <ul className="flex flex-col gap-3">
        {travellers.map((person) => (
          <li key={person.id} className="flex flex-wrap items-center justify-between gap-3 rounded-lg border bg-card px-4 py-3">
            <div>
              <p className="font-medium">
                {person.name}
                {person.isSelf ? <span className="ml-2 text-sm font-normal text-muted-foreground">(you)</span> : null}
              </p>
              <p className="text-sm text-muted-foreground">{person.homeCountry ?? 'No home country'}</p>
            </div>
            <div className="flex gap-2">
              <Button size="sm" variant="outline" onClick={() => setEditing(person)}>
                Edit
              </Button>
              {person.isSelf ? null : (
                <Button
                  size="sm"
                  variant="ghost"
                  className="text-destructive"
                  onClick={() => {
                    setNeedsTripDelete(false)
                    setPendingDelete(person)
                  }}
                >
                  Delete
                </Button>
              )}
            </div>
          </li>
        ))}
      </ul>
      <Dialog open={editing != null} onOpenChange={(open) => { if (!open) setEditing(null) }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Edit traveller</DialogTitle>
          </DialogHeader>
          {editing ? (
            <EditForm
              traveller={editing}
              onCancel={() => setEditing(null)}
              onSave={async (nextName, nextHome) => {
                await save(editing.id, nextName, nextHome)
                setEditing(null)
              }}
            />
          ) : null}
        </DialogContent>
      </Dialog>
      <Dialog
        open={pendingDelete != null}
        onOpenChange={(open) => {
          if (!open) {
            setPendingDelete(null)
            setNeedsTripDelete(false)
          }
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{needsTripDelete ? 'Delete traveller and trips' : 'Delete traveller'}</DialogTitle>
          </DialogHeader>
          <p className="text-sm">
            {needsTripDelete
              ? `${pendingDelete?.name} has trips. Deleting the traveller also deletes those trips.`
              : `Delete ${pendingDelete?.name}? Their trips stay until you confirm a second time.`}
          </p>
          <DialogFooter>
            <Button
              variant="ghost"
              onClick={() => {
                setPendingDelete(null)
                setNeedsTripDelete(false)
              }}
            >
              Cancel
            </Button>
            <Button
              variant="destructive"
              onClick={() => {
                if (pendingDelete) {
                  removing.mutate({ person: pendingDelete, deleteTrips: needsTripDelete })
                }
              }}
            >
              Delete
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </section>
  )
}

function EditForm({
  traveller,
  onCancel,
  onSave,
}: {
  traveller: Traveller
  onCancel: () => void
  onSave: (name: string, homeCountry: string | null) => Promise<void>
}) {
  const [name, setName] = useState(traveller.name)
  const [homeCountry, setHomeCountry] = useState(traveller.homeCountry ?? '')
  return (
    <form
      className="flex flex-col gap-3"
      onSubmit={(event) => {
        event.preventDefault()
        void onSave(name.trim(), homeCountry.trim() || null)
      }}
    >
      <label className="flex flex-col gap-1.5 text-sm" htmlFor="edit-traveller-name">
        Name
        <Input id="edit-traveller-name" value={name} onChange={(event) => setName(event.target.value)} />
      </label>
      <label className="flex flex-col gap-1.5 text-sm" htmlFor="edit-traveller-home">
        Home country
        <Input id="edit-traveller-home" value={homeCountry} onChange={(event) => setHomeCountry(event.target.value)} />
      </label>
      <DialogFooter>
        <Button type="button" variant="ghost" onClick={onCancel}>
          Cancel
        </Button>
        <Button type="submit" disabled={name.trim().length === 0}>
          Save
        </Button>
      </DialogFooter>
    </form>
  )
}
