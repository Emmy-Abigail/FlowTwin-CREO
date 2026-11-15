import { consulta, espera, esperaCorriendo, total, zonaActual } from '../../core/datos/calculos';
import { Atencion, MotivoCierre } from '../../core/datos/modelos';
import { DetalleVista, EstadoDetalle } from './modelo-atencion';

/** GET /api/v1/atenciones/{id} (backend: Reportes · AtencionDetalle). */
export interface AtencionDetalleApi {
  id: number; codigo: string; fecha: string; sede: string; tarjeta: string;
  inicioEn: string; finEn: string | null; estado: EstadoDetalle; motivo: string | null;
  esperaMin: number | null; consultaMin: number | null; totalMin: number | null;
  estadias: { zonaId: number; zona: string; tipo: string; entradaEn: string; salidaEn: string | null }[];
}

/** La respuesta de la API, con las fechas en milisegundos para dibujar. */
export function desdeApi(a: AtencionDetalleApi): DetalleVista {
  return {
    codigo: a.codigo, sede: a.sede, tarjeta: a.tarjeta,
    inicio: Date.parse(a.inicioEn), fin: a.finEn ? Date.parse(a.finEn) : null,
    estado: a.estado, motivo: a.motivo,
    esperaMin: a.esperaMin, consultaMin: a.consultaMin, totalMin: a.totalMin,
    estadias: a.estadias.map(e => ({ zona: e.zona, tipo: e.tipo, entrada: Date.parse(e.entradaEn), salida: e.salidaEn ? Date.parse(e.salidaEn) : null })),
  };
}

const MOTIVO_SIMULADO: Record<MotivoCierre, string> = {
  'Devuelta': 'DEVUELTA', 'Sin lecturas': 'SIN_LECTURAS', 'Cierre de jornada': 'CIERRE_JORNADA',
};

/**
 * Una atención de la simulación (ADR-22) con el mismo modelo que la real. Usa las mismas reglas que
 * el gemelo simulado (calculos.ts), para que las cifras coincidan con lo que se vio en el gemelo.
 */
export function desdeSimulacion(
  a: Atencion, ahora: number, zona: (id: number) => { nombre: string; tipo: string }, sede: string,
): DetalleVista {
  const actual = zonaActual(a);
  const estado: EstadoDetalle = a.estado === 'cerrada' ? 'FINALIZADA'
    : actual !== null && zona(actual).tipo === 'SERVICIO' ? 'EN_ATENCION'
    : espera(a) === null ? 'EN_ESPERA' : 'ATENDIDO';
  return {
    codigo: a.id, sede, tarjeta: a.tarjetaId, inicio: a.inicio, fin: a.fin, estado,
    motivo: a.motivo ? MOTIVO_SIMULADO[a.motivo] : null,
    esperaMin: esperaCorriendo(a, ahora), consultaMin: consulta(a, ahora), totalMin: total(a, ahora),
    estadias: a.eventos.map(e => ({ zona: zona(e.zonaId).nombre, tipo: zona(e.zonaId).tipo, entrada: e.entrada, salida: e.salida })),
  };
}
