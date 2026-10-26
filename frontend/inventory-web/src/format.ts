export const formatQty = (n: number) => n.toLocaleString(undefined, { maximumFractionDigits: 3 })
export const formatDate = (iso: string) => new Date(iso).toLocaleDateString()
export const formatDateTime = (iso: string) => new Date(iso).toLocaleString()

// "" -> undefined, so optional text fields are omitted from requests.
export const optional = (s: string) => (s.trim() === '' ? undefined : s.trim())
