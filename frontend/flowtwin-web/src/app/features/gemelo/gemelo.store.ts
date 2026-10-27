import { Injectable, computed, inject, signal } from '@angular/core';
import { NombreIcono } from '../../shared/componentes/icono.component';
import {
  NOMBRE_NIVEL, consulta, espera, esperaCorriendo, estadoAtencion, nivelOcupacion, total, zonaActual,
} from '../../core/datos/calculos';
import { ClinicaStore } from '../../core/datos/clinica.store';
import { ESPERA_AVISO_MIN, ESPERA_CRITICA_MIN, ZONAS, ZONAS_CONSULTA } from '../../core/datos/clinica.config';
import { GeometriaService } from '../../core/datos/geometria.service';
import { Atencion, TonoChip } from '../../core/datos/modelos';
import { DIA, hhmm, inicioDelDia, minutos, promedio } from '../../core/utils/tiempo';
import { OcupacionZona, TipoZona, ZonaHover } from '../../gemelo-render/geometria';

export type FiltroFeed = 'activos' | 'atendidos' | 'todos';

/** Una tarjeta del feed "Actividad en vivo", ya lista para dibujar. */
export interface ItemFeed {
  id: string;
  icono: NombreIcono;
  detalle: string;
  estado: { texto: string; tono: TonoChip };
  derecha: string;
  borde: '' | 'w1' | 'w2' | 'done';
  recien: boolean;
}

export const NOMBRE_TIPO: Record<TipoZona, string> = {
  INGRESO: 'Ingreso', ESPERA: 'Espera', SERVICIO: 'Servicio', TRANSITO: 'Tránsito', SALIDA: 'Salida',
};

/**
 * Estado de la pantalla Gemelo en vivo (P4). Lee el estado compartido de ClinicaStore y
 * lo convierte en lo que la pantalla necesita: ocupación por zona para el 3D, KPI, feed
 * y detalle de zona. Todo son computed(): se recalculan solos en cada tick.
 */
@Injectable()
export class GemeloStore {
  private clinica = inject(ClinicaStore);
  private geo = inject(GeometriaService);

  readonly geometria = this.geo.piso;
  readonly planoUrl = this.geo.planoUrl;

  readonly filtroFeed = signal<FiltroFeed>('activos');
  readonly zonaSeleccionada = signal<number | null>(null);
  readonly hover = signal<ZonaHover | null>(null);

  /** Una zona sin datos se ve gris: está pausada o su nodo dejó de enviar señal. */
  private sinDatos(zonaId: number): boolean {
    const nodo = this.clinica.nodoDeZona(zonaId);
    return zonaId in this.clinica.pausas() || (nodo != null && nodo.estado !== 'En línea');
  }

  /** Lo que recibe el render 3D. */
  readonly ocupacion = computed<OcupacionZona[]>(() =>
    this.geo.zonas().map(z => {
      const personas = this.clinica.enZona(z.id).length;
      const sinDatos = this.sinDatos(z.id);
      const pausada = z.id in this.clinica.pausas();
      return {
        zonaId: z.id, personas, aforo: z.aforo,
        nivel: nivelOcupacion(personas, z, sinDatos),
        etiqueta: pausada ? 'Pausado' : sinDatos ? 'Sin datos' : undefined,
      };
    }));

  /** Leyenda con los umbrales del piso (todas las zonas del Piso 1 usan los mismos). */
  readonly leyenda = computed(() => {
    const z = this.geo.zonas()[0];
    const media = z?.umbralMedia ?? 60, alta = z?.umbralAlta ?? 85;
    return { alta: `${alta} % o más del aforo`, media: `${media} a ${alta - 1} %`, baja: `menos de ${media} %` };
  });

  // ---------------------------------------------------------------- KPI

  private ayer = computed(() => {
    const hoy0 = inicioDelDia(this.clinica.ahora());
    return this.clinica.historial().filter(a => a.inicio >= hoy0 - DIA && a.inicio < hoy0);
  });

  readonly kpis = computed(() => {
    const ahora = this.clinica.ahora();
    const hoy = this.clinica.atenciones();
    const sala = this.geo.zona(ZONAS.sala);
    const enSala = this.clinica.enZona(ZONAS.sala).length;
    const nivelSala = sala ? nivelOcupacion(enSala, sala, this.sinDatos(ZONAS.sala)) : 'nd';
    const esperaHoy = promedio(hoy.map(espera));
    const consultaHoy = promedio(hoy.map(a => consulta(a, ahora)));
    return {
      enClinica: this.clinica.activas().length,
      atendidosHoy: hoy.filter(a => a.estado === 'cerrada').length,
      enSala, aforoSala: sala?.aforo ?? 0, nivelSala, textoNivelSala: NOMBRE_NIVEL[nivelSala].toLowerCase(),
      esperaHoy, consultaHoy,
      esperaVsAyer: comparar(esperaHoy, promedio(this.ayer().map(espera))),
      consultaVsAyer: comparar(consultaHoy, promedio(this.ayer().map(a => consulta(a, a.fin!)))),
    };
  });

  // ---------------------------------------------------------------- Feed

  private ordenadas = computed(() => {
    const ultimaActividad = (a: Atencion) => a.fin ?? a.eventos.at(-1)!.entrada;
    return [...this.clinica.atenciones()].sort((a, b) => ultimaActividad(b) - ultimaActividad(a));
  });

  readonly conteos = computed(() => {
    const todas = this.ordenadas();
    const activos = todas.filter(a => a.estado === 'activa').length;
    return { activos, atendidos: todas.length - activos, todos: todas.length };
  });

  readonly feed = computed<ItemFeed[]>(() => {
    const filtro = this.filtroFeed();
    const ahora = this.clinica.ahora();
    return this.ordenadas()
      .filter(a => filtro === 'todos' || (filtro === 'activos') === (a.estado === 'activa'))
      .slice(0, 60)
      .map(a => this.itemFeed(a, ahora));
  });

  private itemFeed(a: Atencion, ahora: number): ItemFeed {
    const z = zonaActual(a);
    const ultimo = a.eventos.at(-1)!;
    if (a.estado === 'cerrada') {
      return {
        id: a.id, icono: 'check', estado: estadoAtencion(a), borde: 'done', recien: a.fin === ahora,
        detalle: `Devolvió ${a.tarjetaId} a las ${hhmm(a.fin!)}${a.motivo !== 'Devuelta' ? ` · ${a.motivo}` : ''}`,
        derecha: `Total ${total(a, ahora)} min`,
      };
    }
    // La espera corre hasta que el paciente entra a consulta.
    const w = espera(a) == null ? esperaCorriendo(a, ahora) : 0;
    return {
      id: a.id,
      icono: iconoZona(z),
      estado: estadoAtencion(a),
      detalle: `en ${this.geo.nombreZona(z)} hace ${minutos(ahora - ultimo.entrada)} min`,
      derecha: w ? `Espera ${w} min` : '',
      borde: w > ESPERA_CRITICA_MIN ? 'w2' : w > ESPERA_AVISO_MIN ? 'w1' : '',
      recien: ultimo.entrada === ahora,
    };
  }

  // ---------------------------------------------------------------- Zona (tooltip y detalle M2)

  private resumenZona(zonaId: number) {
    const zona = this.geo.zona(zonaId);
    if (!zona) return null;
    const ahora = this.clinica.ahora();
    const ocup = this.ocupacion().find(o => o.zonaId === zonaId)!;
    const atenciones = this.clinica.enZona(zonaId).map(a => ({
      id: a.id, tarjetaId: a.tarjetaId, minutos: minutos(ahora - a.eventos.at(-1)!.entrada),
    })).sort((a, b) => b.minutos - a.minutos);
    const nodo = this.clinica.nodoDeZona(zonaId);
    return {
      zona, tipo: NOMBRE_TIPO[zona.tipo], nivel: ocup.nivel, nombreNivel: NOMBRE_NIVEL[ocup.nivel],
      personas: ocup.personas, atenciones,
      permanenciaAhora: promedio(atenciones.map(a => a.minutos)),
      textoNodo: nodo ? `Nodo ${nodo.codigo} ${nodo.estado.toLowerCase()}` : 'Sin nodo propio',
      nodo,
    };
  }

  readonly tooltip = computed(() => {
    const h = this.hover();
    const r = h && this.resumenZona(h.zonaId);
    return r ? { ...r, x: h.clientX, y: h.clientY } : null;
  });

  readonly detalle = computed(() => {
    const id = this.zonaSeleccionada();
    const r = id == null ? null : this.resumenZona(id);
    if (!r) return null;
    // Permanencia promedio de hoy: todas las estancias en la zona, incluidas las que siguen abiertas.
    const ahora = this.clinica.ahora();
    const estancias = this.clinica.atenciones().flatMap(a =>
      a.eventos.filter(e => e.zonaId === id).map(e => minutos((e.salida ?? ahora) - e.entrada)));
    return { ...r, permanenciaHoy: promedio(estancias) };
  });
}

function comparar(hoy: number | null, ayer: number | null): string {
  if (hoy == null || ayer == null) return '';
  if (hoy === ayer) return 'igual que ayer';
  return `${hoy < ayer ? '▼' : '▲'} ${Math.abs(hoy - ayer)} min vs. ayer (${ayer} min)`;
}

function iconoZona(zonaId: number | null): NombreIcono {
  if (zonaId === ZONAS.admision) return 'scan';
  if (zonaId === ZONAS.sala) return 'sofa';
  if (zonaId != null && (ZONAS_CONSULTA.includes(zonaId) || zonaId === ZONAS.triaje)) return 'steth';
  return 'walk';
}
