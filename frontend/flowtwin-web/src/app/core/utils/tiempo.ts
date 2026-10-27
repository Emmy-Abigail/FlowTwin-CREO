/** Utilidades de fechas y duraciones. Todas las horas son milisegundos (como Date.now()). */
export const MIN = 60_000;
export const DIA = 86_400_000;

const DIAS = ['dom', 'lun', 'mar', 'mié', 'jue', 'vie', 'sáb'];
const MESES = ['ene', 'feb', 'mar', 'abr', 'may', 'jun', 'jul', 'ago', 'sep', 'oct', 'nov', 'dic'];

export const pad2 = (n: number) => String(n).padStart(2, '0');

export function hhmm(t: number): string {
  const d = new Date(t);
  return `${pad2(d.getHours())}:${pad2(d.getMinutes())}`;
}

/** "lun 26 oct" */
export function fechaCorta(t: number): string {
  const d = new Date(t);
  return `${DIAS[d.getDay()]} ${d.getDate()} ${MESES[d.getMonth()]}`;
}

/** "lun 26" para ejes y etiquetas compactas */
export function diaCorto(t: number): string {
  const d = new Date(t);
  return `${DIAS[d.getDay()]} ${d.getDate()}`;
}

/** "2026-10-26", el formato de <input type="date"> */
export function isoFecha(t: number): string {
  const d = new Date(t);
  return `${d.getFullYear()}-${pad2(d.getMonth() + 1)}-${pad2(d.getDate())}`;
}

export function inicioDelDia(t: number): number {
  const d = new Date(t);
  d.setHours(0, 0, 0, 0);
  return d.getTime();
}

export const minutos = (ms: number) => Math.max(0, Math.round(ms / MIN));

/** 41 → "41 min", 75 → "1 h 15 min", null → "—" */
export function duracion(m: number | null | undefined): string {
  if (m == null) return '—';
  return m >= 60 ? `${Math.floor(m / 60)} h ${m % 60} min` : `${m} min`;
}

/** Promedio redondeado que ignora los valores nulos. */
export function promedio(valores: (number | null)[]): number | null {
  const v = valores.filter((x): x is number => x != null);
  return v.length ? Math.round(v.reduce((s, x) => s + x, 0) / v.length) : null;
}
