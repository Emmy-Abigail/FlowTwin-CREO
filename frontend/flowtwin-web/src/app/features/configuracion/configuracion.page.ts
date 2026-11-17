import { HttpClient, HttpErrorResponse, httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, effect, inject, linkedSignal, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API, mensajeDe, problemaDe } from '../../core/api/problema';
import { ModoDatosService } from '../../core/datos/modo-datos.service';
import { PisoActualService } from '../../core/datos/piso-actual.service';
import { ToastService } from '../../core/ui/toast.service';
import { IconoComponent } from '../../shared/componentes/icono.component';
import { ModalComponent } from '../../shared/componentes/modal.component';
import { ConfiguracionSimulada } from './configuracion-simulada';
import { Parametros, SedeConfig, TIPOS_ZONA, ZonaConfig, aforoDesdeTexto, horas, llevaAforo, textoTipo } from './modelo-configuracion';

type Pestana = 'estructura' | 'parametros';
type Ventana =
  | { tipo: 'sede'; sede: SedeConfig | null }
  | { tipo: 'piso'; sedeId: number; piso: { id: number; nombre: string; nivel: number } | null }
  | { tipo: 'zona'; zona: ZonaConfig | null }
  | { tipo: 'confirmar'; titulo: string; boton: string; accion: () => Promise<void> };

/**
 * Configuración (ADR-27, solo Administrador): sedes, pisos y zonas como datos (la maqueta 3D la instala el
 * equipo técnico) y los parámetros del sistema. Es la última opción del menú: se usa poco. En la Simulación
 * (ADR-31), una copia ficticia en memoria: los cambios se ven en el gemelo simulado y nunca llegan a la base.
 */
@Component({
  selector: 'app-configuracion',
  imports: [IconoComponent, ModalComponent],
  templateUrl: './configuracion.page.html',
  styleUrl: './configuracion.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ConfiguracionPage {
  private http = inject(HttpClient);
  private toast = inject(ToastService);
  private pisosActuales = inject(PisoActualService);
  private modo = inject(ModoDatosService);
  private sim = inject(ConfiguracionSimulada);
  protected simulando = this.modo.simulacion;

  protected pestana = signal<Pestana>('estructura');
  private recarga = signal(0);

  private arbolRes = httpResource<SedeConfig[]>(() => { this.recarga(); return this.simulando() ? undefined : `${API}/configuracion/arbol`; });
  /** Mientras recarga se sigue viendo la respuesta anterior: si no, la selección del piso se perdería en ese instante. */
  protected arbol = linkedSignal<SedeConfig[] | undefined, SedeConfig[]>({
    source: () => this.simulando() ? this.sim.arbol() : this.arbolRes.hasValue() ? this.arbolRes.value() : undefined,
    computation: (v, previo) => v ?? previo?.value ?? [],
  });
  /** Por defecto, el primer piso: así la pantalla abre mostrando zonas, lo que más se usa. */
  protected pisoId = linkedSignal<SedeConfig[], number | null>({
    source: this.arbol,
    computation: (sedes, previo) => previo?.value != null && sedes.some(s => s.pisos.some(p => p.id === previo.value))
      ? previo.value : sedes.flatMap(s => s.pisos)[0]?.id ?? null,
  });
  protected piso = computed(() => this.arbol().flatMap(s => s.pisos).find(p => p.id === this.pisoId()) ?? null);

  private zonasRes = httpResource<ZonaConfig[]>(() => {
    this.recarga();
    const id = this.pisoId();
    return id === null || this.simulando() ? undefined : `${API}/configuracion/pisos/${id}/zonas`;
  });
  protected zonas = linkedSignal<ZonaConfig[] | undefined, ZonaConfig[]>({
    source: () => this.simulando() ? (this.pisoId() === null ? [] : this.sim.zonas(this.pisoId()!)) : this.zonasRes.hasValue() ? this.zonasRes.value() : undefined,
    computation: (v, previo) => v ?? previo?.value ?? [],
  });

  private parametrosRes = httpResource<Parametros>(() => { this.recarga(); return this.simulando() ? undefined : `${API}/configuracion/parametros`; });
  protected parametros = linkedSignal(() => this.simulando() ? { ...this.sim.parametros() } : this.parametrosRes.hasValue() ? { ...this.parametrosRes.value() } : null);

  protected readonly tipos = TIPOS_ZONA;
  protected readonly textoTipo = textoTipo;
  protected readonly llevaAforo = llevaAforo;
  protected readonly horas = horas;

  protected menu = signal<string | null>(null);
  protected ventana = signal<Ventana | null>(null);
  protected trabajando = signal(false);
  protected error = signal<string | null>(null);

  // Formularios de las ventanas
  protected nombre = signal('');
  protected direccion = signal('');
  protected letra = signal('');
  protected nivel = signal(1);
  protected tipo = signal('ESPERA');
  protected aforoVentana = signal('');

  constructor() {
    // Si cambia la estructura, el selector de piso del gemelo y del historial también debe enterarse
    effect(() => { if (this.recarga() > 0 && !this.simulando()) void this.pisosActuales.cargar(); });
  }

  // ---------- Árbol

  protected abrirSede(sede: SedeConfig | null): void {
    this.prepararVentana();
    this.nombre.set(sede?.nombre ?? ''); this.direccion.set(sede?.direccion ?? ''); this.letra.set(sede?.prefijoAtencion ?? '');
    this.ventana.set({ tipo: 'sede', sede });
  }

  protected abrirPiso(sedeId: number, piso: { id: number; nombre: string; nivel: number } | null): void {
    this.prepararVentana();
    const sede = this.arbol().find(s => s.id === sedeId);
    this.nombre.set(piso?.nombre ?? '');
    this.nivel.set(piso?.nivel ?? Math.max(0, ...(sede?.pisos.map(p => p.nivel) ?? [0])) + 1);
    this.ventana.set({ tipo: 'piso', sedeId, piso });
  }

  protected abrirZona(zona: ZonaConfig | null): void {
    this.prepararVentana();
    this.nombre.set(zona?.nombre ?? ''); this.tipo.set(zona?.tipo ?? 'ESPERA'); this.aforoVentana.set(zona?.aforo?.toString() ?? '');
    this.ventana.set({ tipo: 'zona', zona });
  }

  protected confirmarPublicar(): void {
    const p = this.piso();
    if (!p) return;
    this.prepararVentana();
    this.ventana.set({
      tipo: 'confirmar', titulo: `¿Publicar ${p.nombre}?`, boton: 'Publicar',
      accion: async () => {
        if (this.simulando()) this.sim.publicar(p.id); else await firstValueFrom(this.http.post(`${API}/configuracion/pisos/${p.id}/publicar`, null));
        this.toast.mostrar(`${p.nombre} publicado`); },
    });
  }

  protected confirmarBajaZona(z: ZonaConfig): void {
    this.prepararVentana();
    this.ventana.set({
      tipo: 'confirmar', titulo: `¿Dar de baja ${z.nombre}?`, boton: 'Dar de baja',
      accion: async () => {
        if (this.simulando()) this.sim.darDeBajaZona(z.id); else await firstValueFrom(this.http.post(`${API}/configuracion/zonas/${z.id}/baja`, null));
        this.toast.mostrar(`${z.nombre} dada de baja`); },
    });
  }

  protected async guardarVentana(v: Ventana): Promise<void> {
    await this.trabajar(async () => {
      switch (v.tipo) {
        case 'sede': {
          const cuerpo = { nombre: this.nombre(), direccion: this.direccion() || null, prefijoAtencion: this.letra().toUpperCase() };
          if (this.simulando()) this.sim.guardarSede(v.sede?.id ?? null, cuerpo);
          else if (v.sede) await firstValueFrom(this.http.put(`${API}/configuracion/sedes/${v.sede.id}`, cuerpo));
          else await firstValueFrom(this.http.post(`${API}/configuracion/sedes`, cuerpo));
          break;
        }
        case 'piso': {
          const cuerpo = { nombre: this.nombre(), nivel: this.nivel() };
          if (this.simulando()) { const id = this.sim.guardarPiso(v.sedeId, v.piso?.id ?? null, cuerpo); if (!v.piso) this.pisoId.set(id); }
          else if (v.piso) await firstValueFrom(this.http.put(`${API}/configuracion/pisos/${v.piso.id}`, cuerpo));
          else this.pisoId.set((await firstValueFrom(this.http.post<{ id: number }>(`${API}/configuracion/sedes/${v.sedeId}/pisos`, cuerpo))).id);
          break;
        }
        case 'zona': {
          const aforo = llevaAforo(this.tipo()) ? aforoDesdeTexto(this.aforoVentana()) : null;
          if (aforo === undefined) throw new Error('El aforo debe ser un número entero de 1 o más, o quedar vacío (por definir).');
          const cuerpo = { nombre: this.nombre(), tipo: this.tipo(), aforo };
          if (this.simulando()) this.sim.guardarZona(this.pisoId()!, v.zona?.id ?? null, cuerpo);
          else if (v.zona) await firstValueFrom(this.http.put(`${API}/configuracion/zonas/${v.zona.id}`, cuerpo));
          else await firstValueFrom(this.http.post(`${API}/configuracion/pisos/${this.pisoId()}/zonas`, cuerpo));
          break;
        }
        case 'confirmar':
          await v.accion();
          break;
      }
      this.ventana.set(null);
    });
  }

  // ---------- Parámetros

  protected cambiar<K extends keyof Parametros>(campo: K, valor: number): void {
    const p = this.parametros();
    if (p) this.parametros.set({ ...p, [campo]: valor });
  }

  protected async guardarParametros(): Promise<void> {
    await this.trabajar(async () => {
      if (this.simulando()) this.sim.guardarParametros(this.parametros()!);
      else await firstValueFrom(this.http.put(`${API}/configuracion/parametros`, this.parametros()));
      this.toast.mostrar('Parámetros guardados');
    });
  }

  private prepararVentana(): void {
    this.menu.set(null);
    this.error.set(null);
  }

  private async trabajar(accion: () => Promise<void>): Promise<void> {
    this.trabajando.set(true);
    this.error.set(null);
    try {
      await accion();
      this.recarga.update(n => n + 1);
    } catch (e) {
      if (!(e instanceof HttpErrorResponse)) { this.error.set(e instanceof Error ? e.message : 'Ocurrió un error inesperado.'); return; }
      const p = problemaDe(e);
      const problemas = (p.datos?.['problemas'] as string[] | undefined) ?? [];
      this.error.set(problemas.length ? problemas.join(' ') : mensajeDe(p));
    } finally {
      this.trabajando.set(false);
    }
  }
}
