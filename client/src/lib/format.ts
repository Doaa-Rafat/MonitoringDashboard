export const fmtTime = (iso: string) => new Date(iso).toLocaleTimeString()
export const fmtDateTime = (iso: string | null | undefined) => (iso ? new Date(iso).toLocaleString() : '-')
export const fmtNum = (n: number, unit = '') => `${n.toFixed(1)}${unit}`