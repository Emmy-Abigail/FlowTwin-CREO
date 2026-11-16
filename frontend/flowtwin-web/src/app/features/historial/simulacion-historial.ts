/**
 * El historial en la Simulación (ADR-22): calcula, con las atenciones simuladas, las mismas respuestas
 * que da el backend. Usa las reglas del detalle simulado (P6), así una fila y su detalle coinciden.
 * Se elimina junto con la simulación cuando el hardware esté estable.
 */
import { Atencion } from '../../core/datos/modelos';
import { DIA, MIN, isoFecha } from '../../core/utils/tiempo';
import { desdeSimulacion } from '../atencion/fuentes-atencion';
import { CeldaApi, FilaDatos, FilaMapaApi, OcupacionApi, Rango, ZonaHistorialApi, diaDe, diasDe } from './modelo-historial';

export interface ZonaSimulada { id: number; nombre: string; tipo: string; aforo: number | null }

const SIN_MAPA = ['TRANSITO', 'SALIDA'];
const ORDEN = ['ESPERA', 'TRIAJE', 'SERVICIO', 'INGRESO'];
const HORA = 60 * MIN;

const delRango = (atenciones: Atencion[], r: Rango) => {
  const a = diaDe(r.desde), b = diaDe(r.hasta) + DIA;
  return atenciones.filter(x => x.inicio >= a && x.inicio < b);
};

export function filasSimuladas(atenciones: Atencion[], r: Rango, ahora: number, zona: (id: number) => { nombre: string; tipo: string }): FilaDatos[] {
  return delRango(atenciones, r)
    .sort((x, y) => y.inicio - x.inicio)
    .map(a => {
      const d = desdeSimulacion(a, ahora, zona, '');
      return {
        clave: a.id, codigo: a.id, fecha: isoFecha(a.inicio), inicio: a.inicio, fin: a.fin,
        estado: d.estado === 'ANULADA' ? 'FINALIZADA' : d.estado, motivo: d.motivo,
        esperaMin: d.esperaMin ?? 0, consultaMin: d.consultaMin, totalMin: d.totalMin ?? 0,
      };
    });
}

/** Mismo cálculo que el backend: minutos-paciente ÷ minutos de la hora; con más de 7 días, promedio por día de la semana. */
export function ocupacionSimulada(
  atenciones: Atencion[], r: Rango, ahora: number, zonas: ZonaSimulada[], conNodo: Set<number>, zonaId: number | null,
): OcupacionApi {
  const lista: ZonaHistorialApi[] = zonas
    .filter(z => !SIN_MAPA.includes(z.tipo))
    .sort((a, b) => prioridad(a.tipo) - prioridad(b.tipo) || a.id - b.id)
    .map(z => ({ ...z, conNodo: conNodo.has(z.id) }));
  const zona = lista.find(z => z.id === zonaId) ?? lista[0] ?? null;
  const delPeriodo = delRango(atenciones, r);
  const porFecha = diasDe(r) <= 7;
  const vacio: OcupacionApi = { zonas: lista, zonaId: zona?.id ?? null, agrupacion: porFecha ? 'FECHA' : 'DIA_SEMANA', horaDesde: null, horaHasta: null, filas: [] };
  if (!zona || !delPeriodo.length) return vacio;

  const horaDesde = Math.min(...delPeriodo.map(a => new Date(a.inicio).getHours()));
  const horaHasta = Math.max(horaDesde, ...delPeriodo.map(a => new Date(a.fin ?? ahora).getHours()));
  const tramos = delPeriodo.flatMap(a => a.eventos.filter(e => e.zonaId === zona.id).map(e => [e.entrada, e.salida ?? a.fin ?? ahora] as const));
  const horas = Array.from({ length: horaHasta - horaDesde + 1 }, (_, i) => horaDesde + i);

  const fechas = Array.from({ length: diasDe(r) }, (_, i) => diaDe(r.desde) + i * DIA);
  const filas: FilaMapaApi[] = fechas.map(dia => ({
    fecha: isoFecha(dia), diaSemana: null,
    celdas: horas.map((h): CeldaApi => {
      const inicio = dia + h * HORA;
      if (inicio >= ahora) return { hora: h, personas: null, estado: 'FUTURO' };
      if (!zona.conNodo) return { hora: h, personas: null, estado: 'SIN_DATOS' };
      const fin = Math.min(inicio + HORA, ahora);
      const presencia = tramos.reduce((s, [a, b]) => s + Math.max(0, Math.min(b, fin) - Math.max(a, inicio)), 0);
      return { hora: h, personas: Math.round((presencia / (fin - inicio)) * 10) / 10, estado: 'OK' };
    }),
  }));
  if (porFecha) return { ...vacio, horaDesde, horaHasta, filas };

  const porDia = new Map<number, FilaMapaApi[]>();
  for (const f of filas) {
    const d = new Date(diaDe(f.fecha!)).getDay() || 7;
    porDia.set(d, [...(porDia.get(d) ?? []), f]);
  }
  const agrupadas = [...porDia.entries()].sort(([a], [b]) => a - b).map(([d, grupo]): FilaMapaApi => ({
    fecha: null, diaSemana: d,
    celdas: horas.map((h, i): CeldaApi => {
      const ok = grupo.map(f => f.celdas[i]).filter(c => c.estado === 'OK');
      if (ok.length) return { hora: h, personas: Math.round((ok.reduce((s, c) => s + c.personas!, 0) / ok.length) * 10) / 10, estado: 'OK' };
      return { hora: h, personas: null, estado: grupo.some(f => f.celdas[i].estado === 'SIN_DATOS') ? 'SIN_DATOS' : 'FUTURO' };
    }),
  }));
  return { ...vacio, horaDesde, horaHasta, filas: agrupadas };
}

function prioridad(tipo: string): number {
  const i = ORDEN.indexOf(tipo);
  return i < 0 ? ORDEN.length : i;
}
