export function PlaceholderPage({ heading, message }: { heading: string; message: string }) {
  return (
    <section>
      <h1>{heading}</h1>
      <p>{message}</p>
    </section>
  )
}
