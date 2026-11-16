/** Configuración (ADR-27), sin Angular: tipos de la API y textos. */

export interface PisoConfig { id: number; nombre: string; nivel: number; publicado: boolean }
export interface SedeConfig { id: number; nombre: string; direccion: string | null; prefijoAtencion: string; pisos: PisoConfig[] }
export interface ZonaConfig { id: number; nombre: string; tipo: string; aforo: number | null; tieneAforo: boolean }
export interface Parametros { esperaAltaMin: number; esperaCriticaMin: number; sinLecturasMin: number; horaCierre: number; semaforoAmbarPct: number }

/** Los tipos en palabras de la clínica. Solo Ingreso y Espera llevan aforo (donde la gente se acumula). */
export const TIPOS_ZONA: { codigo: string; texto: string; aforo: boolean }[] = [
  { codigo: 'ESPERA', texto: 'Espera', aforo: true },
  { codigo: 'INGRESO', texto: 'Ingreso', aforo: true },
  { codigo: 'TRIAJE', texto: 'Triaje', aforo: false },
  { codigo: 'SERVICIO', texto: 'Consulta', aforo: false },
  { codigo: 'TRANSITO', texto: 'Paso', aforo: false },
  { codigo: 'SALIDA', texto: 'Salida', aforo: false },
];

export const textoTipo = (codigo: string) => TIPOS_ZONA.find(t => t.codigo === codigo)?.texto ?? codigo;
export const llevaAforo = (codigo: string) => TIPOS_ZONA.find(t => t.codigo === codigo)?.aforo ?? false;

/** «» → null (por definir); un número válido → ese número; otro texto → undefined (no se guarda). */
export function aforoDesdeTexto(texto: string): number | null | undefined {
  const t = texto.trim();
  if (t === '') return null;
  const n = Number(t);
  return Number.isInteger(n) && n >= 1 && n <= 9999 ? n : undefined;
}

export const horas = Array.from({ length: 24 }, (_, h) => ({ valor: h, texto: `${String(h).padStart(2, '0')}:00` }));
