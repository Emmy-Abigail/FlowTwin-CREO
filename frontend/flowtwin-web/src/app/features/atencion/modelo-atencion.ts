import { MIN, hhmm } from '../../core/utils/tiempo';

/**
 * Modelo de P6 · Detalle de atención, igual para las dos fuentes (API real o simulación, ADR-22).
 * Las funciones son puras y se prueban en modelo-atencion.spec.ts.
 */

export type EstadoDetalle = 'EN_ESPERA' | 'EN_ATENCION' | 'ATENDIDO' | 'FINALIZADA' | 'ANULADA';

export interface EstadiaVista {
  zona: string;
  /** INGRESO, ESPERA, SERVICIO, TRANSITO, SALIDA (o DESCONOCIDO si la zona ya no existe). */
  tipo: string;
  entrada: number;
  /** null: está ahí ahora. */
  salida: number | null;
}

export interface DetalleVista {
  codigo: string;
  sede: string;
  tarjeta: string;
  inicio: number;
  /** null mientras sigue abierta. */
  fin: number | null;
  estado: EstadoDetalle;
  /** DEVUELTA, SIN_LECTURAS, CIERRE_JORNADA o ANULADA; null si sigue abierta. */
  motivo: string | null;
  /** null: todavía no hay con qué calcularlo («—», nunca un 0 que engañe). */
  esperaMin: number | null;
  consultaMin: number | null;
  totalMin: number | null;
  estadias: EstadiaVista[];
}

// ------------------------------------------------------------------ carriles

/** Los carriles son por TIPO de zona, no por sala: con más consultorios no crecen. */
export type Carril = 'admision' | 'espera' | 'consulta' | 'paso' | 'sin-detectar';

export const CARRILES: { id: Carril; texto: string }[] = [
  { id: 'admision', texto: 'Admisión' },
  { id: 'espera', texto: 'Espera' },
  { id: 'consulta', texto: 'Consulta' },
  { id: 'paso', texto: 'Paso' },
  { id: 'sin-detectar', texto: 'Sin detectar' },
];

export function carrilDe(tipo: string): Carril {
  switch (tipo) {
    case 'INGRESO': return 'admision';
    case 'ESPERA': return 'espera';
    case 'SERVICIO': return 'consulta';
    default: return 'paso';   // TRANSITO, SALIDA o una zona que ya no existe
  }
}

/**
 * Un hueco menor a un minuto no se muestra: el motor de presencia tarda unos segundos en confirmar
 * cada cambio de zona (histéresis) y esos segundos no son una tarjeta perdida.
 */
export const HUECO_MINIMO_MS = MIN;

export interface Tramo {
  carril: Carril;
  nombre: string;
  desde: number;
  hasta: number;
  /** true: sigue ahí ahora (el tramo termina en «ahora»). */
  enCurso: boolean;
}

/** Fin del dibujo: la devolución, o «ahora» si sigue abierta. */
export const finDe = (d: DetalleVista, ahora: number) => d.fin ?? ahora;

/** Estadías y huecos «Sin detectar» de un minuto o más, en orden. Una entrega anulada no tiene recorrido. */
export function tramos(d: DetalleVista, ahora: number): Tramo[] {
  if (d.estado === 'ANULADA') return [];
  const fin = finDe(d, ahora);
  const lista: Tramo[] = [];
  let cursor = d.inicio;
  const hueco = (hasta: number) => {
    if (hasta - cursor >= HUECO_MINIMO_MS) lista.push({ carril: 'sin-detectar', nombre: 'Sin detectar', desde: cursor, hasta, enCurso: false });
  };
  for (const e of d.estadias) {
    hueco(e.entrada);
    const hasta = e.salida ?? fin;
    lista.push({ carril: carrilDe(e.tipo), nombre: e.zona, desde: e.entrada, hasta, enCurso: e.salida === null && d.fin === null });
    cursor = Math.max(cursor, hasta);
  }
  hueco(fin);
  return lista;
}

/** Posición y ancho de un tramo en % del eje [inicio, fin]. */
export function geometriaTramo(t: Tramo, inicio: number, fin: number): { izq: number; ancho: number } {
  const total = Math.max(fin - inicio, MIN);
  return { izq: ((t.desde - inicio) / total) * 100, ancho: ((t.hasta - t.desde) / total) * 100 };
}

/** Minutos completos de un tramo, como en el backend (RN-06): «12 min» al cumplirse el minuto 12. */
export const minutosDe = (desde: number, hasta: number) => Math.max(0, Math.floor((hasta - desde) / MIN));

// ------------------------------------------------------------------ eje de horas

/**
 * Marcas del eje: el inicio, el fin y, entre ellas, horas «redondas» (cada 5, 10, 15, 30 o 60 min)
 * sin pasar de 6 en total. Las que quedan pegadas a un extremo se omiten para que no se encimen.
 */
export function marcas(inicio: number, fin: number, ahoraEsFin = false): { t: number; texto: string }[] {
  const duracion = Math.max(fin - inicio, MIN);
  const paso = [5, 10, 15, 30, 60, 120, 240].map(m => m * MIN).find(p => duracion / p <= 4) ?? 480 * MIN;
  const lista = [{ t: inicio, texto: hhmm(inicio) }];
  for (let t = Math.ceil(inicio / paso) * paso; t < fin; t += paso) {
    if (t - inicio > duracion * 0.12 && fin - t > duracion * 0.12) lista.push({ t, texto: hhmm(t) });
  }
  lista.push({ t: fin, texto: ahoraEsFin ? `${hhmm(fin)} ahora` : hhmm(fin) });
  return lista;
}

// ------------------------------------------------------------------ pasos

export interface Paso {
  hora: number;
  titulo: string;
  detalle: string;
  /** Color del punto: el de su carril, o 'hito' para la entrega y el cierre. */
  tono: Carril | 'hito';
}

const TEXTO_CIERRE: Record<string, (tarjeta: string) => string> = {
  DEVUELTA: t => `Devolvió la tarjeta ${t}`,
  SIN_LECTURAS: () => 'Se cerró sola: la tarjeta dejó de detectarse',
  CIERRE_JORNADA: () => 'Se cerró al terminar la jornada',
  ANULADA: () => 'Se deshizo la entrega',
};

/** La lista de pasos con hora exacta: entrega, cada tramo (incluidos los huecos) y el cierre. */
export function pasos(d: DetalleVista, ahora: number): Paso[] {
  const lista: Paso[] = [{
    hora: d.inicio, titulo: `Se entregó la tarjeta ${d.tarjeta}`, tono: 'hito',
    detalle: d.estado === 'ANULADA' ? 'Admisión' : 'Admisión · empieza a contar la espera',
  }];
  for (const t of tramos(d, ahora)) {
    const min = minutosDe(t.desde, t.hasta);
    const titulo = t.carril === 'sin-detectar' ? 'No se detectó la tarjeta' : t.nombre;
    const detalle = t.carril === 'sin-detectar'
      ? `${hhmm(t.desde)}–${hhmm(t.hasta)} · ${min} min sin lecturas de ningún nodo`
      : t.enCurso ? `desde las ${hhmm(t.desde)} · ${min} min hasta ahora` : `${hhmm(t.desde)}–${hhmm(t.hasta)} · ${min} min`;
    lista.push({ hora: t.desde, titulo, detalle, tono: t.carril });
  }
  if (d.fin !== null && d.motivo) {
    lista.push({
      hora: d.fin, titulo: (TEXTO_CIERRE[d.motivo] ?? (() => 'Se cerró la atención'))(d.tarjeta), tono: 'hito',
      detalle: d.totalMin === null ? 'No cuenta en las métricas' : `Total ${d.totalMin} min`,
    });
  }
  return lista;
}

// ------------------------------------------------------------------ encabezado y cifras

export type TonoEstado = 'ambar' | 'verde' | 'gris';

export function chipEstado(d: DetalleVista): { texto: string; tono: TonoEstado } {
  switch (d.estado) {
    case 'EN_ESPERA': return { texto: 'En espera', tono: 'ambar' };
    case 'EN_ATENCION': return { texto: 'En consulta', tono: 'verde' };
    case 'ATENDIDO': return { texto: 'Hacia la salida', tono: 'gris' };
    case 'ANULADA': return { texto: 'Anulada · entrega deshecha', tono: 'gris' };
    case 'FINALIZADA': {
      const por = d.motivo === 'DEVUELTA' ? 'tarjeta devuelta' : d.motivo === 'SIN_LECTURAS' ? 'sin lecturas' : 'cierre de jornada';
      return { texto: `Finalizada · ${por}`, tono: 'gris' };
    }
  }
}

/** La aclaración bajo cada cifra: qué mide y, si aún corre, por qué. */
export function aclaraciones(d: DetalleVista): { espera: string; consulta: string; total: string } {
  const abierta = d.fin === null;
  if (d.estado === 'ANULADA') {
    const no = 'la entrega se deshizo: no cuenta';
    return { espera: no, consulta: no, total: no };
  }
  return {
    espera: abierta && d.consultaMin === null ? 'sigue corriendo: aún no entra a consulta' : 'desde la entrega hasta la primera consulta',
    consulta: d.consultaMin === null
      ? (abierta ? 'todavía no entra a una zona de atención' : 'no entró a una zona de atención')
      : 'tiempo en zonas de atención',
    total: abierta ? `en curso desde las ${hhmm(d.inicio)}` : 'de la entrega al cierre',
  };
}

/** Si conviene volver a pedirla: solo mientras sigue abierta. */
export const sigueAbierta = (d: DetalleVista) => d.fin === null && d.estado !== 'ANULADA';
