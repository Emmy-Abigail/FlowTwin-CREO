import { Injectable, computed, inject, signal } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';
import { consulta, espera, total, zonaDeConsulta } from '../../core/datos/calculos';
import { ClinicaStore } from '../../core/datos/clinica.store';
import { HORA_APERTURA, HORAS_REPORTE, ZONAS } from '../../core/datos/clinica.config';
import { GeometriaService } from '../../core/datos/geometria.service';
import { Atencion } from '../../core/datos/modelos';
import { DIA, MIN, diaCorto, duracion, fechaCorta, hhmm, inicioDelDia, isoFecha, promedio } from '../../core/utils/tiempo';

export type Rango = 'hoy' | '7' | 'custom';

export const POR_PAGINA = 10;

/** Fila de la tabla de atenciones, ya formateada. */
export interface FilaAtencion {
  id: string;
  fecha: string;
  ingreso: string;
  atendidoEn: string;
  espera: string;
  consulta: string;
  total: string;
  activa: boolean;
  cierre: string;
}

/**
 * Estado de Historial y reportes (P5): filtros, KPI del periodo, aforo histórico por
 * hora, mapa día × hora y tabla de atenciones. La ocupación se calcula a partir de los
 * recorridos reales de las atenciones (no es un número aparte), así todo cuadra.
 */
@Injectable()
export class HistorialStore {
  private clinica = inject(ClinicaStore);
  private geo = inject(GeometriaService);
  private auth = inject(AuthService);

  readonly rango = signal<Rango>('7');
  readonly desde = signal('');
  readonly hasta = signal('');
  readonly zonaId = signal<number>(ZONAS.sala);
  readonly busqueda = signal('');
  readonly pagina = signal(0);

  readonly zonas = this.geo.zonas;
  readonly zona = computed(() => this.geo.zona(this.zonaId()));
  private hoy0 = computed(() => inicioDelDia(this.clinica.ahora()));

  /** [inicio, fin) del rango elegido, en ms. */
  readonly intervalo = computed<[number, number]>(() => {
    const hoy0 = this.hoy0();
    if (this.rango() === 'hoy') return [hoy0, hoy0 + DIA];
    if (this.rango() === 'custom' && this.desde() && this.hasta()) {
      const a = new Date(this.desde() + 'T00:00').getTime();
      const b = new Date(this.hasta() + 'T00:00').getTime() + DIA;
      return [Math.min(a, b), Math.max(a, b)];
    }
    return [hoy0 - 6 * DIA, hoy0 + DIA];
  });

  private todas = computed(() => [...this.clinica.historial(), ...this.clinica.atenciones()]);

  /** Atenciones del periodo que pasaron por la zona elegida, más recientes primero. */
  readonly atenciones = computed(() => {
    const [a, b] = this.intervalo();
    const zona = this.zonaId();
    return this.todas()
      .filter(x => x.inicio >= a && x.inicio < b && x.eventos.some(e => e.zonaId === zona))
      .sort((x, y) => y.inicio - x.inicio);
  });

  readonly filtradas = computed(() => {
    const q = this.busqueda().trim().toLowerCase();
    return q ? this.atenciones().filter(a => a.id.toLowerCase().includes(q)) : this.atenciones();
  });

  readonly paginas = computed(() => Math.max(1, Math.ceil(this.filtradas().length / POR_PAGINA)));

  readonly filas = computed<FilaAtencion[]>(() => {
    const ahora = this.clinica.ahora();
    const pagina = Math.min(this.pagina(), this.paginas() - 1);
    return this.filtradas()
      .slice(pagina * POR_PAGINA, pagina * POR_PAGINA + POR_PAGINA)
      .map(a => this.fila(a, ahora));
  });

  readonly kpis = computed(() => {
    const ahora = this.clinica.ahora();
    const lista = this.atenciones();
    return {
      espera: promedio(lista.map(espera)),
      consulta: promedio(lista.map(a => consulta(a, ahora))),
      total: lista.length,
    };
  });

  /**
   * Ocupación promedio por hora de la zona: matriz de 7 días × 12 horas (07:00 a 18:59).
   * Cada celda = minutos-paciente en esa hora / 60. Las horas que aún no llegan son null.
   */
  readonly matriz = computed(() => {
    const zona = this.zonaId();
    const hoy0 = this.hoy0();
    const ahora = this.clinica.ahora();
    const dia0 = hoy0 - 6 * DIA;
    const minutosPaciente = Array.from({ length: 7 }, () => new Array<number>(HORAS_REPORTE).fill(0));
    for (const a of this.todas()) {
      for (const e of a.eventos) {
        if (e.zonaId !== zona) continue;
        acumular(minutosPaciente, dia0, e.entrada, e.salida ?? ahora);
      }
    }
    return minutosPaciente.map((fila, d) => ({
      dia: dia0 + d * DIA,
      etiqueta: diaCorto(dia0 + d * DIA),
      valores: fila.map((m, h): number | null => {
        const inicioHora = dia0 + d * DIA + (HORA_APERTURA + h) * 60 * MIN;
        return inicioHora > ahora ? null : Math.round((m / 60) * 10) / 10;
      }),
    }));
  });

  /** Serie del gráfico: promedio de cada hora entre los días del rango. */
  readonly serie = computed(() => {
    const [a, b] = this.intervalo();
    const dias = this.matriz().filter(f => f.dia >= a && f.dia < b);
    return Array.from({ length: HORAS_REPORTE }, (_, h) => {
      const v = dias.map(f => f.valores[h]).filter((x): x is number => x != null);
      return v.length ? Math.round((v.reduce((s, x) => s + x, 0) / v.length) * 10) / 10 : null;
    });
  });

  readonly maximo7Dias = computed(() =>
    Math.max(0, ...this.matriz().flatMap(f => f.valores.filter((x): x is number => x != null))));

  readonly hoyIso = computed(() => isoFecha(this.hoy0()));
  readonly haceSeisIso = computed(() => isoFecha(this.hoy0() - 6 * DIA));

  cambiarFiltro(aplicar: () => void): void {
    aplicar();
    this.pagina.set(0);
  }

  /** CSV con las atenciones filtradas (todas las páginas). */
  generarCsv(): { nombre: string; contenido: string; filas: number } {
    const ahora = this.clinica.ahora();
    const filas = this.filtradas().map(a => this.fila(a, ahora));
    const encabezado = ['ID', 'Fecha', 'Ingreso', 'Atendido en', 'Espera', 'Consulta', 'Total', 'Cierre'];
    const csv = [encabezado, ...filas.map(f => [f.id, f.fecha, f.ingreso, f.atendidoEn, f.espera, f.consulta, f.activa ? 'En curso' : f.total, f.cierre])]
      .map(r => r.map(c => `"${String(c).replace(/"/g, '""')}"`).join(','))
      .join('\n');
    return { nombre: this.nombreArchivo('csv'), contenido: '﻿' + csv, filas: filas.length };
  }

  nombreArchivo(extension: string): string {
    const zona = (this.zona()?.nombre ?? 'zona').normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/[^\w]+/g, '');
    return `reporte_Piso1_${zona}_${isoFecha(this.hoy0())}.${extension}`;
  }

  registrarExportacion(archivo: string, formato: string): void {
    this.clinica.auditar('Reportes', 'Exportó reporte', archivo, formato, this.auth.actor());
  }

  private fila(a: Atencion, ahora: number): FilaAtencion {
    return {
      id: a.id,
      fecha: fechaCorta(a.inicio),
      ingreso: hhmm(a.inicio),
      atendidoEn: this.geo.nombreZona(zonaDeConsulta(a)),
      espera: duracion(espera(a)),
      consulta: duracion(consulta(a, ahora)),
      total: duracion(total(a, ahora)),
      activa: a.estado === 'activa',
      cierre: a.motivo ?? '—',
    };
  }
}

/** Reparte los minutos de una estancia [desde, hasta) en las celdas día × hora. */
function acumular(matriz: number[][], dia0: number, desde: number, hasta: number): void {
  let t = desde;
  while (t < hasta) {
    const d = Math.floor((t - dia0) / DIA);
    const inicioDia = dia0 + d * DIA;
    const h = Math.floor((t - inicioDia) / (60 * MIN)) - HORA_APERTURA;
    const finHora = inicioDia + (HORA_APERTURA + h + 1) * 60 * MIN;
    const tramo = Math.min(hasta, finHora) - t;
    if (d >= 0 && d < 7 && h >= 0 && h < HORAS_REPORTE) matriz[d][h] += tramo / MIN;
    t += tramo;
  }
}
