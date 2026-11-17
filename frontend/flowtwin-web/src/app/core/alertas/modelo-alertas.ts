/**
 * Alertas del Operador (F5, ADR-25), sin Angular. Real: GET /pisos/{id}/alertas. Simulación: las mismas
 * reglas sobre los datos simulados. El texto es mínimo a propósito: el manual de usuario explica el resto.
 */
import { esperaCorriendo, zonaActual } from '../datos/calculos';
import { ZONAS_CONSULTA } from '../datos/clinica.config';
import { Atencion, Nodo } from '../datos/modelos';
import { MIN, hhmm } from '../utils/tiempo';

export type TipoAlerta = 'ESPERA_LARGA' | 'TARJETA_SIN_DEVOLVER' | 'NODO_SIN_SENAL';
/** CRITICA: pide actuar ya. ALTA: hay que saberlo. */
export type NivelAlerta = 'ALTA' | 'CRITICA';

/** GET /api/v1/pisos/{id}/alertas (backend: Monitoreo · AlertaPiso). */
export interface AlertaApi {
  clave: string; tipo: TipoAlerta; nivel: NivelAlerta; desde: string;
  atencionId: number | null; codigo: string | null; zonaId: number | null; nodo: string | null;
}

/** clave de atención: el Id real o el código simulado, para abrir su recorrido (P6). */
export interface Alerta {
  clave: string; tipo: TipoAlerta; nivel: NivelAlerta; desde: number;
  atencion: string | null; atencionId: number | null; codigo: string | null; zonaId: number | null; nodo: string | null;
}

/** Valores por defecto; en real los decide el backend. Sin aviso a los 20 min: sería por casos normales (fatiga). */
export const UMBRALES = { alta: 40, critica: 60, sinLecturas: 30 };

/** Suena solo lo que pide actuar ya: espera crítica, posible salida sin devolver la tarjeta y nodo caído. */
export const esUrgente = (a: Alerta) => a.tipo !== 'ESPERA_LARGA' || a.nivel === 'CRITICA';

export function desdeApi(a: AlertaApi): Alerta {
  return {
    clave: a.clave, tipo: a.tipo, nivel: a.nivel, desde: Date.parse(a.desde),
    atencion: a.atencionId === null ? null : String(a.atencionId), atencionId: a.atencionId,
    codigo: a.codigo, zonaId: a.zonaId, nodo: a.nodo,
  };
}

export interface AlertaVista {
  clave: string; enterado: boolean; tono: 'roja' | 'ambar' | 'gris'; icono: 'reloj' | 'radio' | 'alert';
  titulo: string; detalle: string; grupo: 'Pacientes' | 'Equipos';
  atencion: string | null; atencionId: number | null; puedeCerrar: boolean;
}

/** «A-0043 · 62 min esperando» / «Sala de espera». Una línea de título y una de contexto, nada más. */
export function vista(a: Alerta, ahora: number, nombreZona: (id: number) => string, enterado = false): AlertaVista {
  const min = Math.max(0, Math.floor((ahora - a.desde) / MIN));
  const zona = a.zonaId === null ? '' : nombreZona(a.zonaId);
  const base = { clave: a.clave, atencion: a.atencion, atencionId: a.atencionId, enterado };
  switch (a.tipo) {
    case 'ESPERA_LARGA':
      return { ...base, tono: a.nivel === 'CRITICA' ? 'roja' : 'ambar', icono: 'reloj', grupo: 'Pacientes',
        titulo: `${a.codigo} · ${min} min esperando`, detalle: zona, puedeCerrar: false };
    case 'TARJETA_SIN_DEVOLVER':
      return { ...base, tono: 'ambar', icono: 'radio', grupo: 'Pacientes',
        titulo: `${a.codigo} · sin lecturas`, detalle: zona ? `Hace ${min} min · visto en ${zona}` : `Hace ${min} min · nunca se le detectó`, puedeCerrar: true };
    case 'NODO_SIN_SENAL':
      return { ...base, tono: 'gris', icono: 'alert', grupo: 'Equipos',
        titulo: `Nodo ${a.nodo} sin señal`, detalle: `${zona} · desde las ${hhmm(a.desde)}`, puedeCerrar: false };
  }
}

/** Las claves que no estaban antes: suenan y se animan. La primera carga no suena (no son «nuevas» para nadie). */
export function nuevas(antes: ReadonlySet<string> | null, ahora: Alerta[]): Alerta[] {
  return antes === null ? [] : ahora.filter(a => !antes.has(a.clave));
}

const ORDEN: Record<NivelAlerta, number> = { CRITICA: 0, ALTA: 1 };

/** Las mismas reglas que el backend (CalculoAlertas) sobre los datos de la simulación (ADR-22). */
export function alertasSimuladas(atenciones: Atencion[], nodos: Nodo[], ahora: number): Alerta[] {
  const lista: Alerta[] = [];
  for (const a of atenciones.filter(x => x.estado === 'activa')) {
    const enConsulta = a.eventos.some(e => ZONAS_CONSULTA.includes(e.zonaId));
    const min = esperaCorriendo(a, ahora);
    if (!enConsulta && min >= UMBRALES.alta) {
      const nivel: NivelAlerta = min >= UMBRALES.critica ? 'CRITICA' : 'ALTA';
      lista.push({ clave: `ESPERA_LARGA:${a.id}:${nivel}`, tipo: 'ESPERA_LARGA', nivel, desde: a.inicio,
        atencion: a.id, atencionId: null, codigo: a.id, zonaId: zonaActual(a), nodo: null });
    }
  }
  for (const n of nodos.filter(x => x.estado === 'Sin señal')) {
    lista.push({ clave: `NODO_SIN_SENAL:${n.codigo}`, tipo: 'NODO_SIN_SENAL', nivel: 'ALTA', desde: n.ultimoLatido,
      atencion: null, atencionId: null, codigo: null, zonaId: n.zonaId, nodo: n.codigo });
  }
  return lista.sort((x, y) => ORDEN[x.nivel] - ORDEN[y.nivel] || x.desde - y.desde);
}
