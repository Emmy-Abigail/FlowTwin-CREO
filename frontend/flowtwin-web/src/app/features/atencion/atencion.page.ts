import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { API, mensajeDe, problemaDe } from '../../core/api/problema';
import { ClinicaStore } from '../../core/datos/clinica.store';
import { GeometriaService } from '../../core/datos/geometria.service';
import { ModoDatosService } from '../../core/datos/modo-datos.service';
import { PisoActualService } from '../../core/datos/piso-actual.service';
import { fechaLarga, hhmm } from '../../core/utils/tiempo';
import { EstadoVacioComponent } from '../../shared/componentes/estado-vacio.component';
import { IconoComponent } from '../../shared/componentes/icono.component';
import { AtencionDetalleApi, desdeApi, desdeSimulacion } from './fuentes-atencion';
import {
  CARRILES, DetalleVista, aclaraciones, chipEstado, finDe, geometriaTramo, marcas, minutosDe, pasos, sigueAbierta, tramos,
} from './modelo-atencion';

/** Mientras la atención sigue abierta, cada cuánto se vuelve a pedir. */
const REFRESCO_MS = 15_000;

/**
 * P6 · Detalle de atención: tiempos (RN-06) y recorrido en carriles por tipo de zona. Real: GET
 * /atenciones/{id}, que responde 404 si no existe o no es de las sedes del usuario (RN-24).
 * Simulación (ADR-22): la atención simulada con el mismo modelo. No tiene acciones: Gerencia la ve igual.
 */
@Component({
  selector: 'app-atencion',
  imports: [RouterLink, IconoComponent, EstadoVacioComponent],
  templateUrl: './atencion.page.html',
  styleUrl: './atencion.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AtencionPage {
  private http = inject(HttpClient);
  private modo = inject(ModoDatosService);
  private clinica = inject(ClinicaStore);
  private geo = inject(GeometriaService);
  private pisos = inject(PisoActualService);

  /** :id de la ruta: el Id de la atención real, o su código en la simulación. */
  readonly id = input.required<string>();
  /** ?desde=historial para volver al historial; por defecto, al gemelo. */
  readonly desde = input<string>();

  private real = signal<DetalleVista | null>(null);
  /** Momento de la última respuesta: el «ahora» de lo que se dibuja, coherente con los minutos del servidor. */
  private respondidoEn = signal(Date.now());
  protected cargando = signal(false);
  protected error = signal<{ titulo: string; texto: string } | null>(null);
  private reloj?: ReturnType<typeof setTimeout>;

  protected volver = computed(() => this.desde() === 'historial'
    ? { ruta: '/historial', texto: 'Volver al historial' }
    : { ruta: '/gemelo', texto: 'Volver al gemelo' });

  protected ahora = computed(() => this.modo.simulacion() ? this.clinica.ahora() : this.respondidoEn());

  protected detalle = computed<DetalleVista | null>(() => {
    if (!this.modo.simulacion()) return this.real();
    const a = this.clinica.atenciones().find(x => x.id === this.id());
    if (!a) return null;
    const zona = (id: number) => ({ nombre: this.geo.nombreZona(id), tipo: this.geo.zona(id)?.tipo ?? 'DESCONOCIDO' });
    return desdeSimulacion(a, this.clinica.ahora(), zona, this.pisos.sede()?.nombre ?? 'Sede Principal');
  });

  // ---- lo que dibuja la plantilla
  protected chip = computed(() => { const d = this.detalle(); return d ? chipEstado(d) : null; });
  protected notas = computed(() => { const d = this.detalle(); return d ? aclaraciones(d) : null; });
  protected enVivo = computed(() => { const d = this.detalle(); return !!d && sigueAbierta(d); });
  protected fecha = computed(() => { const d = this.detalle(); return d ? fechaLarga(d.inicio) : ''; });
  protected readonly carriles = CARRILES;
  protected readonly hhmm = hhmm;

  /** Tramos de cada carril con su posición en el eje, y las marcas de hora. */
  protected recorrido = computed(() => {
    const d = this.detalle();
    if (!d) return null;
    const ahora = this.ahora(), fin = finDe(d, ahora);
    const lista = tramos(d, ahora).map(t => ({ ...t, ...geometriaTramo(t, d.inicio, fin), min: minutosDe(t.desde, t.hasta) }));
    return {
      porCarril: new Map(CARRILES.map(c => [c.id, lista.filter(t => t.carril === c.id)])),
      marcas: marcas(d.inicio, fin, d.fin === null).map(m => ({ ...m, izq: ((m.t - d.inicio) / Math.max(fin - d.inicio, 1)) * 100 })),
      ahoraEnEje: d.fin === null && d.estado !== 'ANULADA',
    };
  });

  /** Pasos en dos columnas que se leen de arriba abajo: filas = la mitad, redondeada hacia arriba. */
  protected pasos = computed(() => { const d = this.detalle(); return d ? pasos(d, this.ahora()) : []; });
  protected filasPasos = computed(() => Math.max(1, Math.ceil(this.pasos().length / 2)));

  constructor() {
    effect(() => {
      const id = this.id(), simulando = this.modo.simulacion();
      untracked(() => {
        clearTimeout(this.reloj);
        if (!simulando) this.cargar(id);
      });
    });
    inject(DestroyRef).onDestroy(() => clearTimeout(this.reloj));
  }

  private async cargar(id: string, silencioso = false): Promise<void> {
    if (!/^\d+$/.test(id)) {
      this.real.set(null);
      this.error.set({ titulo: 'No se encontró la atención', texto: 'El enlace no corresponde a ninguna atención.' });
      return;
    }
    if (!silencioso) { this.cargando.set(true); this.error.set(null); }
    try {
      const dto = await firstValueFrom(this.http.get<AtencionDetalleApi>(`${API}/atenciones/${id}`));
      if (id !== this.id()) return;   // ya se abrió otra
      const d = desdeApi(dto);
      this.real.set(d);
      this.respondidoEn.set(Date.now());
      this.error.set(null);
      if (sigueAbierta(d)) this.reloj = setTimeout(() => this.cargar(id, true), REFRESCO_MS);
    } catch (e) {
      const p = problemaDe(e);
      if (silencioso && p.status !== 404) {
        // Un corte momentáneo no borra lo que ya se ve: se reintenta en el siguiente ciclo
        this.reloj = setTimeout(() => this.cargar(id, true), REFRESCO_MS);
        return;
      }
      this.real.set(null);
      this.error.set(p.status === 404
        ? { titulo: 'No se encontró la atención', texto: 'No existe o no es de las sedes que tienes asignadas.' }
        : { titulo: 'No se pudo cargar la atención', texto: mensajeDe(p) });
    } finally {
      if (!silencioso) this.cargando.set(false);
    }
  }
}
