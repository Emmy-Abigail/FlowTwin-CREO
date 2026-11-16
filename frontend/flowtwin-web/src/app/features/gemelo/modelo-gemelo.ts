import { Signal } from '@angular/core';
import { GeometriaPiso, NivelOcupacion, OcupacionZona, TipoZona } from '../../gemelo-render/geometria';

/**
 * Modelo de la pantalla Gemelo en vivo, igual para las dos fuentes de datos (ADR-22):
 * GemeloEnVivoStore (API + SignalR) y GemeloSimuladoStore (solo para la demo). La pantalla
 * no sabe de cuál viene: así la simulación no puede «verse mejor» que la realidad.
 * Las funciones de abajo son puras y se prueban en modelo-gemelo.spec.ts.
 */

export interface ZonaVista {
  zonaId: number;
  personas: number;
  /** null: la zona no tiene aforo (Tránsito/Salida) o aún está por definir (lo configura el Administrador). */
  aforo: number | null;
  nivel: NivelOcupacion;
  /** Fuera de servicio (RF09, ADR-25): el código del motivo (LIMPIEZA…); null o ausente si está en servicio. */
  fueraDeServicio?: string | null;
}

/** Motivos para poner una zona fuera de servicio (catálogo cat.MotivoPausa). */
export const MOTIVOS_FUERA: { codigo: string; texto: string }[] = [
  { codigo: 'LIMPIEZA', texto: 'Limpieza' },
  { codigo: 'MANTENIMIENTO', texto: 'Mantenimiento' },
  { codigo: 'SIN_ATENCION', texto: 'Sin atención' },
  { codigo: 'OTRO', texto: 'Otro' },
];

export const textoMotivo = (codigo: string | null | undefined) => MOTIVOS_FUERA.find(m => m.codigo === codigo)?.texto ?? 'Otro';

export type EstadoPaciente = 'esperando' | 'en-consulta' | 'atendido' | 'finalizado';

export interface PacienteVista {
  /** Para abrir su detalle (P6): el Id de la atención real, o su código en la simulación. */
  clave: string;
  codigo: string;
  estado: EstadoPaciente;
  /** Zona donde está ahora; null si no se le detecta en ninguna zona del piso. */
  zonaId: number | null;
  /** Cuándo entró a esa zona (ms). */
  zonaDesde: number | null;
  esperaMin: number;
  /** Solo en los finalizados: hora de fin y tiempo total en la clínica. */
  finEn?: number;
  totalMin?: number;
}

export interface KpisVista {
  enClinica: number;
  enEspera: number;
  /** null: todavía no hay con qué calcularlo (no un 0 que engañe). */
  esperaPromMin: number | null;
  consultaPromMin: number | null;
}

export interface NodoVista { codigo: string; zonaId: number; enLinea: boolean; ultimoLatido: number | null; }

export type EstadoConexion = 'conectando' | 'en-vivo' | 'reconectando' | 'sin-conexion' | 'simulacion';

/** Lo que entrega cualquier fuente de datos del gemelo. */
export interface FuenteGemelo {
  readonly geometria: Signal<GeometriaPiso | null>;
  readonly zonas: Signal<ZonaVista[]>;
  readonly kpis: Signal<KpisVista | null>;
  readonly pacientes: Signal<PacienteVista[]>;
  readonly nodos: Signal<NodoVista[]>;
  readonly conexion: Signal<EstadoConexion>;
  /** Reloj de la fuente: el real en vivo, el simulado en simulación. */
  readonly ahora: Signal<number>;
  /** Mensaje si algo falló al cargar (piso inexistente, sin permiso). */
  readonly error: Signal<string | null>;
}

// ------------------------------------------------------------------ desde la API

/** Nivel del backend (BAJA, MEDIA, ALTA, NINGUNO, SIN_DATOS) → el del render. */
export function nivelDeApi(nivel: string): NivelOcupacion {
  switch (nivel) {
    case 'BAJA': return 'baja';
    case 'MEDIA': return 'media';
    case 'ALTA': return 'alta';
    case 'NINGUNO': return 'ninguno';
    default: return 'nd';
  }
}

/** Estado de la atención del backend → la pestaña donde se muestra. */
export function estadoDeApi(estado: string): EstadoPaciente {
  switch (estado) {
    case 'EN_ATENCION': return 'en-consulta';
    case 'ATENDIDO': return 'atendido';
    case 'FINALIZADA': return 'finalizado';
    default: return 'esperando';
  }
}

export interface GeometriaApi {
  pisoId: number; nombre: string; nivel: number; anchoM: number; fondoM: number; altoMuroM: number;
  zonas: { id: number; nombre: string; tipo: string; x: number; y: number; ancho: number; fondo: number;
    aforo: number | null; umbralMedia: number | null; umbralAlta: number | null; }[];
  nodos: { codigo: string; x: number; y: number; zonaId: number }[];
}

/**
 * GET /pisos/{id}/geometria → medidas, zonas y nodos para el render. Los muros que también trae no se
 * usan: lo que se dibuja es la maqueta 3D. El aforo y el nivel de cada zona vienen del estado del piso
 * (ZonaVista), que es la fuente de verdad.
 */
export function geometriaDesdeApi(g: GeometriaApi, sede: string): GeometriaPiso {
  return {
    pisoId: g.pisoId, nombre: g.nombre, nivel: g.nivel, anchoM: g.anchoM, fondoM: g.fondoM, altoMuroM: g.altoMuroM, sede,
    zonas: g.zonas.map(z => ({
      id: z.id, nombre: z.nombre, tipo: z.tipo as GeometriaPiso['zonas'][number]['tipo'],
      x: z.x, y: z.y, ancho: z.ancho, fondo: z.fondo,
      aforo: z.aforo ?? 0, umbralMedia: z.umbralMedia ?? 0, umbralAlta: z.umbralAlta ?? 0,
    })),
    nodos: g.nodos.map(n => ({ codigo: n.codigo, x: n.x, y: n.y, zonaId: n.zonaId })),
  };
}

// ------------------------------------------------------------------ presentación

/** RN-13: los tipos donde la gente permanece tienen aforo; Tránsito y Salida, nunca. */
export function tieneAforo(tipo: TipoZona | undefined): boolean {
  return tipo === 'INGRESO' || tipo === 'ESPERA' || tipo === 'SERVICIO';
}

/**
 * Texto bajo el nombre de la zona en el 3D: «13/24»; «2/—» si el aforo está por definir (se verá el
 * número cuando el Administrador lo configure); «2» en Tránsito, que nunca tiene aforo.
 */
export function etiquetaZona(z: ZonaVista, tipo?: TipoZona): string {
  if (z.nivel === 'nd') return 'Sin datos';
  if (z.aforo === null) return tieneAforo(tipo) ? `${z.personas}/—` : `${z.personas}`;
  return `${z.personas}/${z.aforo}`;
}

/**
 * Lo que recibe el render de cada zona. Una zona que ningún nodo cubre todavía no se monitorea:
 * solo se ve su nombre, sin cifra ni color (hoy, todas menos Admisión, Sala de espera y Módulo 2).
 */
export function ocupacionParaRender(zonas: ZonaVista[], nodos: NodoVista[], tipoDe: (id: number) => TipoZona | undefined): OcupacionZona[] {
  const conNodo = new Set(nodos.map(n => n.zonaId));
  return zonas.map(z => z.fueraDeServicio
    // Gris y con su rótulo: lo que se ve no se interpreta como «sala libre»
    ? { zonaId: z.zonaId, personas: z.personas, aforo: z.aforo, nivel: 'nd', etiqueta: 'Fuera de servicio' }
    : conNodo.has(z.zonaId)
    ? { zonaId: z.zonaId, personas: z.personas, aforo: z.aforo, nivel: z.nivel, etiqueta: etiquetaZona(z, tipoDe(z.zonaId)) }
    : { zonaId: z.zonaId, personas: 0, aforo: null, nivel: 'ninguno', soloNombre: true });
}

export const NOMBRE_NIVEL_VISTA: Record<NivelOcupacion, string> = {
  alta: 'concentración alta', media: 'concentración media', baja: 'concentración baja',
  nd: 'sin datos: ningún nodo la cubre ahora', ninguno: 'sin aforo definido',
};

/** «13 de 24 pacientes · concentración baja», o lo honesto si no se sabe. */
export function resumenZona(z: ZonaVista, tipo?: TipoZona): string {
  if (z.fueraDeServicio) return `Fuera de servicio · ${textoMotivo(z.fueraDeServicio)}`;
  if (z.nivel === 'nd') return 'Sin datos: ningún nodo la cubre ahora';
  const personas = `${z.personas} ${z.personas === 1 ? 'paciente' : 'pacientes'}`;
  if (z.aforo === null) return `${personas} · ${tieneAforo(tipo) ? 'aforo por definir' : 'zona de paso, sin aforo'}`;
  return `${z.personas} de ${z.aforo} pacientes · ${NOMBRE_NIVEL_VISTA[z.nivel]}`;
}

/** El aviso de conexión de arriba a la derecha: qué tan «en vivo» está lo que se ve. */
export function textoConexion(c: EstadoConexion, nodos: NodoVista[]): { texto: string; tono: 'ok' | 'aviso' | 'mal' | 'sim' } {
  const enLinea = nodos.filter(n => n.enLinea).length;
  switch (c) {
    case 'simulacion': return { texto: 'Simulación · datos ficticios', tono: 'sim' };
    case 'conectando': return { texto: 'Conectando…', tono: 'aviso' };
    case 'reconectando': return { texto: 'Reconectando… lo que ves puede estar desactualizado', tono: 'mal' };
    case 'sin-conexion': return { texto: 'Sin conexión con el servidor', tono: 'mal' };
    case 'en-vivo':
      if (nodos.length === 0) return { texto: 'En vivo · sin nodos instalados', tono: 'aviso' };
      if (enLinea === 0) return { texto: `Conectado · 0 de ${nodos.length} nodos con señal`, tono: 'aviso' };
      return { texto: `En vivo · ${enLinea} de ${nodos.length} nodos`, tono: enLinea === nodos.length ? 'ok' : 'aviso' };
  }
}

/**
 * Pacientes por pestaña. Esperando: el que más espera, primero. En consulta: el que entró antes, primero.
 * Atendidos: los que van hacia la salida y los que ya devolvieron la tarjeta, el más reciente primero.
 */
export function pacientesPorPestana(lista: PacienteVista[]) {
  const de = (e: EstadoPaciente) => lista.filter(p => p.estado === e);
  return {
    esperando: de('esperando').sort((a, b) => b.esperaMin - a.esperaMin),
    enConsulta: de('en-consulta').sort((a, b) => (a.zonaDesde ?? 0) - (b.zonaDesde ?? 0)),
    atendidos: [...de('atendido'), ...de('finalizado').sort((a, b) => (b.finEn ?? 0) - (a.finEn ?? 0))],
  };
}

/** Lo que dice cada tarjeta de paciente debajo del código. */
export function detallePaciente(p: PacienteVista, nombreZona: (id: number) => string, hace: (t: number) => string): string {
  if (p.estado === 'finalizado') return `Devolvió la tarjeta ${p.finEn ? hace(p.finEn) : ''}`.trim();
  if (p.zonaId === null) return 'Sin ubicación: no se le detecta ahora';
  return `en ${nombreZona(p.zonaId)}${p.zonaDesde ? ` · ${hace(p.zonaDesde)}` : ''}`;
}
