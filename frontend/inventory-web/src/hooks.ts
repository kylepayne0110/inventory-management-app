import { useCallback, useEffect, useRef, useState } from 'react'

// Runs a loader on mount and whenever its dependencies change; reload() repeats it on demand.
// Only the most recent request may update state, so a slow earlier response cannot overwrite a newer one.
export function useLoad<T>(loader: () => Promise<T>, deps: unknown[]) {
  const [data, setData] = useState<T | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const latest = useRef(0)
  // The caller passes the dependency list explicitly, so it cannot be an array literal here.
  // oxlint-disable-next-line react-hooks/exhaustive-deps, react/use-memo
  const run = useCallback(loader, deps)

  const reload = useCallback(() => {
    const id = ++latest.current
    setLoading(true)
    run().then(
      (d) => { if (id === latest.current) { setData(d); setError(null); setLoading(false) } },
      (e: Error) => { if (id === latest.current) { setError(e.message); setLoading(false) } },
    )
  }, [run])

  // Loading data on mount is synchronising with an external system (the API).
  // oxlint-disable-next-line react/set-state-in-effect
  useEffect(() => { reload() }, [reload])
  return { data, error, loading, reload }
}
