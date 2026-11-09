export function getSyncedArrivalDate(
  departureDate: Date | null,
  currentArrivalDate: Date | null,
  isExistingTrip: boolean,
): Date | null {
  if (departureDate == null) {
    return currentArrivalDate
  }

  if (isExistingTrip) {
    return currentArrivalDate
  }

  return new Date(departureDate.getFullYear(), departureDate.getMonth(), departureDate.getDate())
}
