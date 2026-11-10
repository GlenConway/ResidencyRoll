import { useQuery, useQueryClient } from '@tanstack/react-query'
import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react'
import { getTravellers, updateTraveller } from '../api/client.ts'
import type { Traveller } from '../api/types.ts'
import { useHomeCountry, writeHomeCountry } from '../lib/homeCountry.ts'
import { chooseTraveller, rememberTraveller, storedTravellerId } from '../lib/travellers.ts'

interface TravellerState {
  travellers: Traveller[]
  selected: Traveller | null
  select: (id: number) => void
  save: (id: number, name: string, homeCountry: string | null) => Promise<void>
}

const TravellerContext = createContext<TravellerState | null>(null)
const emptyTravellers: Traveller[] = []

export function TravellerProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const query = useQuery({
    queryKey: ['travellers'],
    queryFn: getTravellers,
  })
  const travellers = query.data ?? emptyTravellers
  const [pickedId, setPickedId] = useState<number | null>(storedTravellerId)
  const selected = chooseTraveller(travellers, pickedId)

  const select = useCallback((id: number) => {
    rememberTraveller(id)
    setPickedId(id)
  }, [])

  const save = useCallback(
    async (id: number, name: string, homeCountry: string | null) => {
      const updated = await updateTraveller(id, name, homeCountry)
      queryClient.setQueryData<Traveller[]>(['travellers'], (current) =>
        (current ?? []).map((traveller) => (traveller.id === id ? updated : traveller)),
      )
    },
    [queryClient],
  )

  const value = useMemo(() => ({ travellers, selected, select, save }), [travellers, selected, select, save])
  return <TravellerContext.Provider value={value}>{children}</TravellerContext.Provider>
}

export function useTravellers(): TravellerState {
  return (
    useContext(TravellerContext) ?? {
      travellers: [],
      selected: null,
      select: () => undefined,
      save: async () => undefined,
    }
  )
}

export function useSelectedHomeCountry(): [string | null, (country: string) => void] {
  const { selected, save } = useTravellers()
  const [stored, setStored] = useHomeCountry()

  const updateSelected = useCallback(
    (country: string) => {
      writeHomeCountry(country)
      if (!selected) {
        setStored(country)
        return
      }

      void save(selected.id, selected.name, country)
    },
    [save, selected, setStored],
  )

  if (!selected) {
    return [stored, setStored]
  }

  return [selected.homeCountry, updateSelected]
}

export function PageHeading({ title }: { title: string }) {
  const { selected } = useTravellers()
  return (
    <h1 className="text-2xl font-semibold tracking-tight">
      {title}
      {selected ? <span className="ml-3 text-base font-medium text-muted-foreground">{selected.name}</span> : null}
    </h1>
  )
}
