/**
 * Reglas de negocio puras (sin Angular): tiempos de espera y consulta, nivel de
 * ocupación de una zona y estado visible de una atención. Las usan los stores de
 * todas las pantallas, así el gemelo, el feed y los reportes calculan igual.
 */
import { NivelOcupacion, ZonaGeometria } from '../../gemelo-render/geometria';
import { ZONAS, ZONAS_CONSULTA } from './clinica.config';
import { Atencion, TonoChip } from './modelos';
import { minutos } from '../utils/tiempo';

export function zonaActual(a: Atencion): number | null {
  const e = a.eventos.at(-1);
  return e && e.salida == null ? e.zonaId : null;
}

/** Minutos desde el escaneo en Admisión hasta entrar a consulta. null si aún no entra. */
export function espera(a: Atencion): number | null {
  const e = a.eventos.find(ev => ZONAS_CONSULTA.includes(ev.zonaId));
  return e ? minutos(e.entrada - a.inicio) : null;
}

/** Espera que sigue corriendo para quien aún no entra a consulta. */
export function esperaCorriendo(a: Atencion, ahora: number): number {
  return espera(a) ?? minutos((a.fin ?? ahora) - a.inicio);
}

/** Minutos dentro de las zonas de consulta. null si nunca entró. */
export function consulta(a: Atencion, ahora: number): number | null {
  const evs = a.eventos.filter(ev => ZONAS_CONSULTA.includes(ev.zonaId));
  if (!evs.length) return null;
  return minutos(evs.reduce((s, ev) => s + ((ev.salida ?? a.fin ?? ahora) - ev.entrada), 0));
}

export const total = (a: Atencion, ahora: number) => minutos((a.fin ?? ahora) - a.inicio);

/** Zona donde se atendió (o se atiende) al paciente. */
export function zonaDeConsulta(a: Atencion): number | null {
  return a.eventos.find(ev => ZONAS_CONSULTA.includes(ev.zonaId))?.zonaId ?? null;
}

/**
 * Concentración de una zona según sus umbrales (en % del aforo).
 * sinDatos = la zona está pausada o su nodo no envía señal: se ve gris.
 */
export function nivelOcupacion(personas: number, zona: ZonaGeometria, sinDatos = false): NivelOcupacion {
  if (sinDatos) return 'nd';
  const pct = (personas / Math.max(1, zona.aforo)) * 100;
  return pct >= zona.umbralAlta ? 'alta' : pct >= zona.umbralMedia ? 'media' : 'baja';
}

export const NOMBRE_NIVEL: Record<NivelOcupacion, string> = {
  alta: 'Alta', media: 'Media', baja: 'Baja', nd: 'Sin datos',
};

/** Texto y color del chip de estado de una atención. */
export function estadoAtencion(a: Atencion): { texto: string; tono: TonoChip } {
  if (a.estado === 'cerrada') return { texto: 'Atendido', tono: 'dark' };
  const z = zonaActual(a);
  if (z === ZONAS.admision) return { texto: 'En admisión', tono: '' };
  if (z === ZONAS.triaje) return { texto: 'En triaje', tono: 'baja' };
  if (z != null && ZONAS_CONSULTA.includes(z)) return { texto: 'En consulta', tono: 'baja' };
  if (z === ZONAS.hall && espera(a) != null) return { texto: 'Hacia la salida', tono: '' };
  return { texto: 'En espera', tono: 'media' };
}
