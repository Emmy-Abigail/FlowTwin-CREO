/**
 * P5 · Historial: lo que la pantalla necesita, sin Angular. Los tipos *Api son las respuestas de
 * GET /pisos/{id}/reportes/...; la simulación (ADR-22) produce los mismos tipos, así la pantalla no
 * sabe de dónde vienen los datos.
 */
import { TonoChip } from '../../core/datos/modelos';
import { DIA, fechaCorta, hhmm, isoFecha } from '../../core/utils/tiempo';

// ---------- Respuestas de la API (backend: Reportes · Historial)

/** Atenciones sin anuladas; tiempos solo de las terminadas (la espera, solo de las que llegaron a consulta). */
export interface ResumenApi {
  atenciones: number; terminadas: number;
  conEspera: number; esperaMediana: number | null; esperaP90: number | null;
  totalMediana: number | null; totalP90: number | null;
}

export interface ZonaHistorialApi { id: number; nombre: string; tipo: string; aforo: number | null; conNodo: boolean }
export type EstadoCelda = 'OK' | 'SIN_DATOS' | 'FUTURO';
export interface CeldaApi { hora: number; personas: number | null; estado: EstadoCelda }
/** Una fecha ('2026-11-09') o un día de la semana (1 = lunes … 7 = domingo). */
export interface FilaMapaApi { fecha: string | null; diaSemana: number | null; celdas: CeldaApi[] }
export interface OcupacionApi {
  zonas: ZonaHistorialApi[]; zonaId: number | null; agrupacion: 'FECHA' | 'DIA_SEMANA';
  horaDesde: number | null; horaHasta: number | null; filas: FilaMapaApi[];
}

export type EstadoFila = 'EN_ESPERA' | 'EN_ATENCION' | 'ATENDIDO' | 'FINALIZADA';
export interface FilaApi {
  id: number; codigo: string; fecha: string; inicioEn: string; finEn: string | null;
  estado: EstadoFila; motivo: string | null; esperaMin: number; consultaMin: number | null; totalMin: number;
}
export interface PaginaApi { filas: FilaApi[]; total: number; pagina: number; tamano: number }

/** Una fila ya lista para la tabla. clave: lo que va en /atencion/:id (el Id real o el código simulado). */
export interface FilaDatos {
  clave: string; codigo: string; fecha: string; inicio: number; fin: number | null;
  estado: EstadoFila; motivo: string | null; esperaMin: number; consultaMin: number | null; totalMin: number;
}

export interface PaginaDatos { filas: FilaDatos[]; total: number; pagina: number; tamano: number }

export function filaDesdeApi(f: FilaApi): FilaDatos {
  return {
    clave: String(f.id), codigo: f.codigo, fecha: f.fecha, inicio: Date.parse(f.inicioEn), fin: f.finEn ? Date.parse(f.finEn) : null,
    estado: f.estado, motivo: f.motivo, esperaMin: f.esperaMin, consultaMin: f.consultaMin, totalMin: f.totalMin,
  };
}

// ---------- Periodo

export type PeriodoId = 'hoy' | '7' | '30' | 'elegir';
export const MAXIMO_DIAS = 31;
/** Las filas que entran en la tabla sin scroll a 1280×720. */
export const TAMANO_PAGINA = 8;

export interface Rango { desde: string; hasta: string }

/** '2026-11-09' → medianoche local de ese día. */
export function diaDe(iso: string): number {
  const [a, m, d] = iso.split('-').map(Number);
  return new Date(a, m - 1, d).getTime();
}

export function diasDe(r: Rango): number {
  return Math.round((diaDe(r.hasta) - diaDe(r.desde)) / DIA) + 1;
}

/** Hoy, los últimos 7 o 30 días contando hoy, o el rango elegido. */
export function rangoDe(periodo: PeriodoId, hoy: string, elegido: Rango | null): Rango {
  if (periodo === 'hoy') return { desde: hoy, hasta: hoy };
  if (periodo === 'elegir' && elegido) return elegido;
  const dias = periodo === '30' ? 30 : 7;
  return { desde: isoFecha(diaDe(hoy) - (dias - 1) * DIA), hasta: hoy };
}

/** Lo que está mal en un rango elegido, o null si se puede consultar (las mismas reglas que el backend). */
export function problemaRango(r: Rango, hoy: string): string | null {
  if (!r.desde || !r.hasta) return 'Elige las dos fechas.';
  if (r.hasta < r.desde) return 'La fecha final no puede ser anterior a la inicial.';
  if (r.hasta > hoy) return 'El historial llega hasta hoy.';
  if (diasDe(r) > MAXIMO_DIAS) return `Como máximo ${MAXIMO_DIAS} días por consulta.`;
  return null;
}

const largo = (t: number, conMes: boolean) => new Intl.DateTimeFormat('es-PE',
  conMes ? { weekday: 'long', day: 'numeric', month: 'long' } : { weekday: 'long', day: 'numeric' }).format(t).replace(',', '');

/** «Hoy, domingo 15 de noviembre» · «Del lunes 9 al domingo 15 de noviembre». */
export function textoPeriodo(r: Rango, hoy: string): string {
  const a = diaDe(r.desde), b = diaDe(r.hasta);
  if (r.desde === r.hasta) {
    const t = largo(b, true);
    return r.hasta === hoy ? `Hoy, ${t}` : t.charAt(0).toUpperCase() + t.slice(1);
  }
  const mismoMes = new Date(a).getMonth() === new Date(b).getMonth();
  return `Del ${largo(a, !mismoMes)} al ${largo(b, true)}`;
}

// ---------- Cifras

export interface CifraVista { valor: number | null; detalle: string }

/**
 * La frase de la «cola» en lenguaje simple. «9 de cada 10» solo tiene sentido con al menos 10 casos;
 * con menos, se dice de cuántas sale la mediana, para que nadie saque conclusiones de 3 pacientes.
 */
export function cola(n: number, p90: number | null, verbo: string, vacio: string): string {
  if (n === 0 || p90 === null) return vacio;
  if (n < 10) return `mediana de ${n} ${n === 1 ? 'atención terminada' : 'atenciones terminadas'}`;
  return `9 de cada 10 ${verbo} ${p90} min o menos`;
}

export function cifras(r: ResumenApi | null): { atenciones: CifraVista; espera: CifraVista; total: CifraVista } {
  return {
    atenciones: { valor: r?.atenciones ?? null, detalle: 'atenciones del periodo (sin anuladas)' },
    espera: { valor: r?.esperaMediana ?? null, detalle: r ? cola(r.conEspera, r.esperaP90, 'esperaron', 'aún no hay atenciones terminadas que pasaran a consulta') : '' },
    total: { valor: r?.totalMediana ?? null, detalle: r ? cola(r.terminadas, r.totalP90, 'se fueron en', 'aún no hay atenciones terminadas') : '' },
  };
}

// ---------- Mapa día × hora

const DIAS_SEMANA = ['Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado', 'Domingo'];
const DIAS_CORTOS = ['dom', 'lun', 'mar', 'mié', 'jue', 'vie', 'sáb'];

export function etiquetaFila(f: FilaMapaApi): string {
  if (f.diaSemana) return DIAS_SEMANA[f.diaSemana - 1];
  const t = diaDe(f.fecha ?? '');
  return `${DIAS_CORTOS[new Date(t).getDay()]} ${new Date(t).getDate()}`;
}

const numero = new Intl.NumberFormat('es-PE', { maximumFractionDigits: 1 });

export interface CeldaVista {
  estado: EstadoCelda; texto: string; titulo: string;
  /** 0–1: qué tan oscura, relativa al máximo del mapa (sin aforo no hay un «lleno» absoluto). */
  intensidad: number;
}

/** Las celdas listas para pintar: la escala va de 0 al máximo que se ve en el mapa. */
export function celdas(o: OcupacionApi): { etiqueta: string; celdas: CeldaVista[] }[] {
  const maximo = Math.max(0, ...o.filas.flatMap(f => f.celdas.map(c => c.personas ?? 0)));
  return o.filas.map(f => {
    const etiqueta = etiquetaFila(f);
    return {
      etiqueta,
      celdas: f.celdas.map(c => {
        const hora = `${String(c.hora).padStart(2, '0')}:00`;
        if (c.estado === 'SIN_DATOS') return { estado: c.estado, texto: '', intensidad: 0, titulo: `${etiqueta} ${hora} · sin datos: el nodo no tenía señal` };
        if (c.estado === 'FUTURO' || c.personas === null) return { estado: 'FUTURO', texto: '', intensidad: 0, titulo: `${etiqueta} ${hora} · todavía no llega` };
        const texto = numero.format(c.personas);
        return {
          estado: 'OK', texto, intensidad: maximo > 0 ? c.personas / maximo : 0,
          titulo: `${etiqueta} ${hora} · ${texto} ${c.personas === 1 ? 'persona' : 'personas'} en promedio`,
        };
      }),
    };
  });
}

export function horasDe(o: OcupacionApi): string[] {
  if (o.horaDesde === null || o.horaHasta === null) return [];
  return Array.from({ length: o.horaHasta - o.horaDesde + 1 }, (_, i) => String(o.horaDesde! + i).padStart(2, '0'));
}

// ---------- Tabla

export interface FilaVista {
  clave: string; codigo: string; fecha: string; entrega: string; fin: string;
  espera: string; consulta: string; total: string; enCurso: boolean;
  cierre: { texto: string; tono: TonoChip };
}

const MOTIVOS: Record<string, string> = { DEVUELTA: 'Devuelta', SIN_LECTURAS: 'Sin lecturas', CIERRE_JORNADA: 'Cierre de jornada' };
const ABIERTAS: Record<string, string> = { EN_ESPERA: 'En espera', EN_ATENCION: 'En atención', ATENDIDO: 'Atendido' };

/**
 * Cierre: el motivo dice si el tiempo es confiable. «Devuelta» es exacto; «Sin lecturas» y «Cierre de
 * jornada» son aproximados (por eso en ámbar). Las abiertas dicen dónde están y sus minutos «en curso».
 */
export function filaVista(f: FilaDatos): FilaVista {
  const enCurso = f.fin === null;
  return {
    clave: f.clave, codigo: f.codigo, fecha: fechaCorta(diaDe(f.fecha)),
    entrega: hhmm(f.inicio), fin: f.fin === null ? '—' : hhmm(f.fin),
    espera: `${f.esperaMin} min`, consulta: f.consultaMin === null ? '—' : `${f.consultaMin} min`, total: `${f.totalMin} min`,
    enCurso,
    cierre: enCurso
      ? { texto: ABIERTAS[f.estado] ?? 'En curso', tono: f.estado === 'EN_ATENCION' ? 'baja' : 'media' }
      : { texto: MOTIVOS[f.motivo ?? ''] ?? 'Finalizada', tono: f.motivo === 'DEVUELTA' ? 'nd' : 'media' },
  };
}

// ---------- Cálculos de la simulación (ADR-22): las mismas reglas que el backend, en el navegador

/** Mediana en minutos completos: si son dos centrales, su promedio hacia abajo. */
export function mediana(v: number[]): number | null {
  if (!v.length) return null;
  const s = [...v].sort((a, b) => a - b), m = Math.floor(s.length / 2);
  return s.length % 2 ? s[m] : Math.floor((s[m - 1] + s[m]) / 2);
}

/** Percentil 90 por rango más cercano: un valor que ocurrió. */
export function percentil90(v: number[]): number | null {
  if (!v.length) return null;
  const s = [...v].sort((a, b) => a - b);
  return s[Math.ceil(0.9 * s.length) - 1];
}

export function resumenDe(filas: FilaDatos[]): ResumenApi {
  const terminadas = filas.filter(f => f.fin !== null);
  const esperas = terminadas.filter(f => f.consultaMin !== null).map(f => f.esperaMin);
  const totales = terminadas.map(f => f.totalMin);
  return {
    atenciones: filas.length, terminadas: terminadas.length,
    conEspera: esperas.length, esperaMediana: mediana(esperas), esperaP90: percentil90(esperas),
    totalMediana: mediana(totales), totalP90: percentil90(totales),
  };
}

export function paginar(filas: FilaDatos[], codigo: string, pagina: number, tamano = TAMANO_PAGINA): PaginaDatos {
  const q = codigo.trim().toLowerCase();
  const lista = q ? filas.filter(f => f.codigo.toLowerCase().includes(q)) : filas;
  const paginas = Math.max(1, Math.ceil(lista.length / tamano));
  const p = Math.min(Math.max(1, pagina), paginas);
  return { filas: lista.slice((p - 1) * tamano, p * tamano), total: lista.length, pagina: p, tamano };
}
