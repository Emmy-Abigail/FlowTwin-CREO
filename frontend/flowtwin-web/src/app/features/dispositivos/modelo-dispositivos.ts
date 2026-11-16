/**
 * P9 · Dispositivos, sin Angular: textos de cada problema y filtros. Por excepción: la pantalla abre en
 * lo que requiere atención y lo demás se encuentra con filtros y búsqueda (escala a miles).
 */
import { DIA, hhmm, inicioDelDia } from '../../core/utils/tiempo';

export interface PaginaInventario<T> { filas: T[]; total: number; pagina: number; tamano: number; conteos: Record<string, number> }

export interface TarjetaInventario {
  id: number; codigo: string; estado: string; estadoNombre: string; codigoQr: string;
  problema: 'EXTRAVIADA' | 'NUNCA_DETECTADA' | 'SIN_DETECCION' | null;
  ultimaDeteccionEn: string | null; zonaUltimaDeteccion: string | null;
}

export interface NodoInventario {
  id: number; codigo: string; mac: string; zonaId: number; zona: string; pisoId: number; piso: string;
  estado: 'EN_LINEA' | 'SIN_SENAL'; ultimoLatidoEn: string | null; requiereAtencion: boolean;
  posX: number | null; posY: number | null; ajusteRssiDb: number;
}

/** Lo que el técnico carga en el ESP32 (se muestra una sola vez, con la clave). */
export interface CuentaMqtt { usuario: string; temaLecturas: string; temaLatidos: string; temaRespuestas: string }
export interface NodoConClave { nodo: { id: number; codigo: string }; clave: string; mqtt: CuentaMqtt }

export interface Filtro { id: string; texto: string; n: number; atencion: boolean }

const ESTADOS_TARJETA: [string, string][] = [
  ['DISPONIBLE', 'Disponible'], ['EN_USO', 'En uso'], ['POR_DESINFECTAR', 'Por desinfectar'],
  ['SIN_DEVOLVER', 'Sin devolver'], ['EXTRAVIADA', 'Extraviada'], ['BAJA', 'De baja'],
];

/** «Requieren atención» primero; los estados solo si tienen alguna tarjeta; «Todas» al final. */
export function filtrosTarjetas(conteos: Record<string, number>): Filtro[] {
  return [
    { id: 'ATENCION', texto: 'Requieren atención', n: conteos['ATENCION'] ?? 0, atencion: true },
    ...ESTADOS_TARJETA.filter(([id]) => (conteos[id] ?? 0) > 0).map(([id, texto]) => ({ id, texto, n: conteos[id], atencion: false })),
    { id: 'TODAS', texto: 'Todas', n: conteos['TODAS'] ?? 0, atencion: false },
  ];
}

export function filtrosNodos(conteos: Record<string, number>): Filtro[] {
  return [
    { id: 'ATENCION', texto: 'Requieren atención', n: conteos['ATENCION'] ?? 0, atencion: true },
    { id: 'EN_LINEA', texto: 'En línea', n: conteos['EN_LINEA'] ?? 0, atencion: false },
    { id: 'TODAS', texto: 'Todos', n: conteos['TODAS'] ?? 0, atencion: false },
  ];
}

/** «hoy 09:41», «ayer 17:20» o «12 nov 08:05». */
export function cuando(t: number, ahora: number): string {
  const hoy = inicioDelDia(ahora);
  if (t >= hoy) return `hoy ${hhmm(t)}`;
  if (t >= hoy - DIA) return `ayer ${hhmm(t)}`;
  const d = new Date(t);
  return `${d.getDate()} ${new Intl.DateTimeFormat('es-PE', { month: 'short' }).format(d).replace('.', '')} ${hhmm(t)}`;
}

/** El motivo en palabras, con lo que hay que hacer cuando no es obvio. Null si no hay problema. */
export function textoProblema(t: TarjetaInventario, ahora: number): { texto: string; aviso: boolean } | null {
  const visto = t.ultimaDeteccionEn ? Date.parse(t.ultimaDeteccionEn) : null;
  const donde = visto === null ? '' : `${cuando(visto, ahora)}${t.zonaUltimaDeteccion ? ' · ' + t.zonaUltimaDeteccion : ''}`;
  switch (t.problema) {
    case 'NUNCA_DETECTADA': return { texto: 'Nunca se ha detectado: ¿batería?', aviso: true };
    case 'SIN_DETECCION': {
      const dias = Math.floor((ahora - visto!) / DIA);
      return { texto: `No se detecta hace ${dias} ${dias === 1 ? 'día' : 'días'}: ¿batería?`, aviso: true };
    }
    case 'EXTRAVIADA': return { texto: visto === null ? 'Extraviada' : `Extraviada · vista por última vez ${donde}`, aviso: true };
    default: return donde ? { texto: `Vista ${donde}`, aviso: false } : null;
  }
}

/** «En línea», «Sin señal desde las 15:52», «Sin señal desde ayer 18:10» o «Nunca se ha conectado». */
export function textoNodo(n: NodoInventario, ahora: number): string {
  if (n.estado === 'EN_LINEA') return 'En línea';
  if (!n.ultimoLatidoEn) return 'Nunca se ha conectado';
  const t = Date.parse(n.ultimoLatidoEn);
  return t >= inicioDelDia(ahora) ? `Sin señal desde las ${hhmm(t)}` : `Sin señal desde ${cuando(t, ahora)}`;
}

/** La máquina de estados de la tarjeta (backend): no se retira una que tiene un paciente o que falta devolver. */
export const puedeDarDeBaja = (estado: string) => ['DISPONIBLE', 'POR_DESINFECTAR', 'EXTRAVIADA'].includes(estado);

/** flowtwin/v1/nodos/a4cf129b3e04/lecturas → flowtwin/v1/nodos/a4cf129b3e04 */
export const raizTemas = (c: CuentaMqtt) => c.temaLecturas.replace(/\/lecturas$/, '');
