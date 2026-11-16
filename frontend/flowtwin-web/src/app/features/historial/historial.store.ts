import { HttpClient, HttpResourceRef, httpResource } from '@angular/common/http';
import { DestroyRef, Injectable, Signal, computed, effect, inject, linkedSignal, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API, mensajeDe, problemaDe } from '../../core/api/problema';
import { AuthService } from '../../core/auth/auth.service';
import { ClinicaStore } from '../../core/datos/clinica.store';
import { GeometriaService } from '../../core/datos/geometria.service';
import { ModoDatosService } from '../../core/datos/modo-datos.service';
import { PisoActualService } from '../../core/datos/piso-actual.service';
import { isoFecha } from '../../core/utils/tiempo';
import {
  FilaDatos, OcupacionApi, PaginaApi, PaginaDatos, PeriodoId, Rango, ResumenApi, TAMANO_PAGINA,
  filaDesdeApi, paginar, problemaRango, rangoDe, resumenDe,
} from './modelo-historial';
import { filasSimuladas, ocupacionSimulada } from './simulacion-historial';
import { descargar, nombreDe } from '../../core/utils/descarga';

export type Pestana = 'ocupacion' | 'atenciones';

/** Si el periodo incluye hoy, cada cuánto se vuelve a pedir: el historial de hoy sigue creciendo. */
const REFRESCO_MS = 60_000;

/** Los filtros se recuerdan en la pestaña del navegador: al volver del detalle (P6) se ve lo mismo. */
const CLAVE_FILTROS = 'flowtwin.historial';

interface Filtros { periodo: PeriodoId; elegido: Rango | null; pestana: Pestana; zonaId: number | null; codigo: string; pagina: number }

/**
 * Estado del Historial (P5): periodo, pestaña, zona, búsqueda y página. Real: tres GET a
 * /pisos/{id}/reportes/... que se vuelven a pedir solos al cambiar un filtro (httpResource cancela la
 * petición anterior). Simulación (ADR-22): las mismas respuestas calculadas en el navegador.
 */
@Injectable()
export class HistorialStore {
  private http = inject(HttpClient);
  private modo = inject(ModoDatosService);
  private clinica = inject(ClinicaStore);
  private geo = inject(GeometriaService);
  private pisos = inject(PisoActualService);
  private auth = inject(AuthService);

  readonly periodo = signal<PeriodoId>('7');
  readonly elegido = signal<Rango | null>(null);
  readonly pestana = signal<Pestana>('ocupacion');
  readonly zonaId = signal<number | null>(null);
  readonly codigo = signal('');
  readonly pagina = signal(1);
  readonly exportando = signal(false);

  /** La hora del navegador, al minuto: así «Hoy» cambia solo a medianoche sin recargar. */
  private reloj = signal(Date.now());
  private ahora = computed(() => this.modo.simulacion() ? this.clinica.ahora() : this.reloj());
  readonly hoy = computed(() => isoFecha(this.ahora()));
  readonly rango = computed(() => rangoDe(this.periodo(), this.hoy(), this.elegido()));
  /** Un rango elegido con algo mal no se consulta: se explica junto a las fechas. */
  readonly problema = computed(() => this.periodo() === 'elegir' ? problemaRango(this.rango(), this.hoy()) : null);
  readonly pisoId = computed(() => this.pisos.piso()?.id ?? null);

  // ---------- Real
  private base = computed(() => {
    const piso = this.pisoId();
    if (this.modo.simulacion() || piso === null || this.problema()) return undefined;
    return { url: `${API}/pisos/${piso}/reportes`, desde: this.rango().desde, hasta: this.rango().hasta };
  });

  private resumenReal = httpResource<ResumenApi>(() => {
    const b = this.base();
    return b && { url: `${b.url}/resumen`, params: { desde: b.desde, hasta: b.hasta } };
  });

  private ocupacionReal = httpResource<OcupacionApi>(() => {
    const b = this.base();
    if (!b || this.pestana() !== 'ocupacion') return undefined;
    const z = this.zonaId();
    return { url: `${b.url}/ocupacion`, params: { desde: b.desde, hasta: b.hasta, ...(z === null ? {} : { zonaId: z }) } };
  });

  private paginaReal = httpResource<PaginaApi>(() => {
    const b = this.base();
    if (!b || this.pestana() !== 'atenciones') return undefined;
    return { url: `${b.url}/atenciones`, params: { desde: b.desde, hasta: b.hasta, codigo: this.codigo(), pagina: this.pagina(), tamano: TAMANO_PAGINA } };
  });

  // ---------- Simulación
  private zonaSim = (id: number) => ({ nombre: this.geo.nombreZona(id), tipo: this.geo.zona(id)?.tipo ?? 'DESCONOCIDO' });
  private filasSim = computed<FilaDatos[]>(() => this.modo.simulacion()
    ? filasSimuladas([...this.clinica.historial(), ...this.clinica.atenciones()], this.rango(), this.clinica.ahora(), this.zonaSim)
    : []);

  // ---------- Lo que ve la pantalla (mientras llega la respuesta nueva, se sigue viendo la anterior)
  readonly resumen: Signal<ResumenApi | null> = this.ultimo(this.resumenReal, () => resumenDe(this.filasSim()));

  readonly ocupacion: Signal<OcupacionApi | null> = this.ultimo(this.ocupacionReal, () => ocupacionSimulada(
    [...this.clinica.historial(), ...this.clinica.atenciones()], this.rango(), this.clinica.ahora(),
    this.geo.zonas().map(z => ({ id: z.id, nombre: z.nombre, tipo: z.tipo, aforo: z.aforo ?? null })),
    new Set(this.clinica.nodos().filter(n => n.estado !== 'Baja').map(n => n.zonaId)), this.zonaId()));

  readonly atenciones: Signal<PaginaDatos | null> = this.ultimo(this.paginaReal,
    () => paginar(this.filasSim(), this.codigo(), this.pagina()),
    (p: PaginaApi) => ({ ...p, filas: p.filas.map(filaDesdeApi) }));

  readonly paginas = computed(() => { const p = this.atenciones(); return p ? Math.max(1, Math.ceil(p.total / p.tamano)) : 1; });

  readonly cargando = computed(() => !this.modo.simulacion()
    && (this.resumenReal.isLoading() || this.ocupacionReal.isLoading() || this.paginaReal.isLoading()));

  /** El primer error de las tres consultas, ya en palabras para la persona. */
  readonly error = computed(() => {
    if (this.modo.simulacion()) return null;
    const e = this.resumenReal.error() ?? this.ocupacionReal.error() ?? this.paginaReal.error();
    return e ? mensajeDe(problemaDe(e)) : null;
  });

  constructor() {
    const guardados = leerFiltros();
    if (guardados) {
      this.periodo.set(guardados.periodo);
      this.elegido.set(guardados.elegido);
      this.pestana.set(guardados.pestana);
      this.zonaId.set(guardados.zonaId);
      this.codigo.set(guardados.codigo);
      this.pagina.set(guardados.pagina);
    }
    effect(() => guardarFiltros({
      periodo: this.periodo(), elegido: this.elegido(), pestana: this.pestana(),
      zonaId: this.zonaId(), codigo: this.codigo(), pagina: this.pagina(),
    }));

    const intervalo = setInterval(() => {
      this.reloj.set(Date.now());
      if (this.base() && this.rango().hasta === this.hoy()) {
        this.resumenReal.reload();
        this.ocupacionReal.reload();
        this.paginaReal.reload();
      }
    }, REFRESCO_MS);
    inject(DestroyRef).onDestroy(() => clearInterval(intervalo));
  }

  // ---------- Acciones (cada filtro nuevo vuelve a la página 1)
  cambiarPeriodo(p: PeriodoId): void {
    if (p === 'elegir' && !this.elegido()) this.elegido.set(rangoDe('7', this.hoy(), null));
    this.periodo.set(p);
    this.pagina.set(1);
  }

  elegirFecha(campo: keyof Rango, valor: string): void {
    this.elegido.set({ ...(this.elegido() ?? rangoDe('7', this.hoy(), null)), [campo]: valor });
    this.pagina.set(1);
  }

  buscar(codigo: string): void {
    this.codigo.set(codigo.trim());
    this.pagina.set(1);
  }

  /** Descarga el CSV de las atenciones del periodo (con la búsqueda). Real: lo arma el backend y lo registra en la auditoría. */
  async exportarCsv(): Promise<{ archivo: string } | { error: string }> {
    const r = this.rango();
    if (this.modo.simulacion()) {
      const filas = paginar(this.filasSim(), this.codigo(), 1, Number.MAX_SAFE_INTEGER).filas;
      const archivo = `atenciones_simulacion_${r.desde}_${r.hasta}.csv`;
      descargar(new Blob([csvSimulado(filas)], { type: 'text/csv;charset=utf-8' }), archivo);
      this.clinica.auditar('Reportes', 'Exportó reporte', archivo, 'CSV', this.auth.actor());
      return { archivo };
    }
    const b = this.base();
    if (!b) return { error: this.problema() ?? 'Elige un piso para exportar.' };
    this.exportando.set(true);
    try {
      const resp = await firstValueFrom(this.http.get(`${b.url}/atenciones.csv`,
        { params: { desde: b.desde, hasta: b.hasta, codigo: this.codigo() }, responseType: 'blob', observe: 'response' }));
      const archivo = nombreDe(resp.headers.get('Content-Disposition')) ?? `atenciones_${r.desde}_${r.hasta}.csv`;
      descargar(resp.body!, archivo);
      return { archivo };
    } catch (e) {
      return { error: mensajeDe(problemaDe(e)) };
    } finally {
      this.exportando.set(false);
    }
  }

  /**
   * Lo que se muestra de una consulta: en Simulación, lo calculado; en real, la última respuesta que llegó.
   * Así, al cambiar un filtro, la pantalla no parpadea en blanco mientras llega la nueva.
   */
  private ultimo<A, V = A>(res: HttpResourceRef<A | undefined>, simulado: () => V, convertir: (a: A) => V = a => a as unknown as V): Signal<V | null> {
    const real = linkedSignal<A | undefined, V | null>({
      source: () => res.hasValue() ? res.value() : undefined,
      computation: (v, previo) => v !== undefined ? convertir(v) : (previo?.value ?? null),
    });
    return computed(() => this.modo.simulacion() ? simulado() : (this.base() ? real() : null));
  }
}

/** Comodidad, no estado importante: sin almacenamiento (ventana privada, bloqueado) simplemente no se recuerda. */
function leerFiltros(): Filtros | null {
  try {
    const f = JSON.parse(sessionStorage.getItem(CLAVE_FILTROS) ?? 'null') as Filtros | null;
    return f && ['hoy', '7', '30', 'elegir'].includes(f.periodo) ? f : null;
  } catch { return null; }
}

function guardarFiltros(f: Filtros): void {
  try { sessionStorage.setItem(CLAVE_FILTROS, JSON.stringify(f)); } catch { /* no se recuerda */ }
}

/** Las mismas columnas que el CSV del backend. */
function csvSimulado(filas: FilaDatos[]): string {
  const hora = (t: number | null) => t === null ? '' : new Date(t).toTimeString().slice(0, 5);
  const lineas = [
    ['Código', 'Fecha', 'Entrega', 'Fin', 'Estado', 'Cierre', 'Espera (min)', 'Consulta (min)', 'Total (min)'],
    ...filas.map(f => [f.codigo, f.fecha, hora(f.inicio), hora(f.fin), f.estado, f.motivo ?? '', String(f.esperaMin), f.consultaMin?.toString() ?? '', String(f.totalMin)]),
  ];
  return '﻿' + lineas.map(l => l.join(',')).join('\r\n') + '\r\n';
}

