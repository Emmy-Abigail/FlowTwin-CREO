import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, afterNextRender, computed, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { fechaLarga } from '../../core/utils/tiempo';
import { BotonDirective } from '../../shared/componentes/boton.directive';
import { IconoComponent } from '../../shared/componentes/icono.component';
import { ModalComponent } from '../../shared/componentes/modal.component';
import { AdmisionStore } from './admision.store';
import { Pregunta, haceCuanto } from './presentacion-admision';
import { SeleccionTarjetasComponent, TarjetaSeleccionable } from './seleccion-tarjetas.component';

/** Un modal de tanda abierto: qué se hace y con qué tarjetas. */
interface Tanda { tipo: 'desinfectar' | 'extraviar'; tarjetas: TarjetaSeleccionable[]; }

/** Pausa al escribir a mano antes de consultar la vista previa (el lector, en cambio, termina con Enter). */
const ESPERA_ESCRITURA_MS = 400;

/**
 * P3 · Admisión. El lector QR (un teclado USB) escribe el código en el campo y termina con Enter:
 * ese Enter NO registra, muestra qué va a pasar con la tarjeta y pasa el foco a «Registrar». El
 * operador confirma con un clic (o con Enter). Así ve lo que hace antes de hacerlo.
 * Las reglas R1–R9 están en presentacion-admision.ts.
 */
@Component({
  selector: 'app-admision',
  imports: [FormsModule, BotonDirective, IconoComponent, ModalComponent, SeleccionTarjetasComponent],
  templateUrl: './admision.page.html',
  styleUrl: './admision.page.scss',
  providers: [AdmisionStore],
  host: { '(document:keydown)': 'teclaFueraDelCampo($event)' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdmisionPage {
  protected store = inject(AdmisionStore);
  protected codigo = signal('');
  /** Modales (R3, R4, R5). Mientras uno está abierto, el lector no escribe en la pantalla de atrás. */
  protected confirmarRegistro = signal<Pregunta | null>(null);
  protected tanda = signal<Tanda | null>(null);
  private hayModal = computed(() => !!(this.confirmarRegistro() || this.tanda()));
  protected readonly accionDesinfectar = (n: number) => n === 1 ? 'Marcar 1 como desinfectada' : `Marcar ${n} como desinfectadas`;
  protected readonly accionExtraviar = (n: number) => n === 1 ? 'Marcar 1 como extraviada' : `Marcar ${n} como extraviadas`;
  private campo = viewChild.required<ElementRef<HTMLInputElement>>('campo');
  private boton = viewChild.required<ElementRef<HTMLButtonElement>>('botonRegistrar');
  private temporizadorEscritura: ReturnType<typeof setTimeout> | null = null;

  /** Reloj de 1 s solo para la cuenta regresiva de «Deshacer». */
  private ahora = signal(Date.now());
  protected segundosDeshacer = computed(() => {
    const d = this.store.deshacible();
    // el reloj avanza cada 1 s: nunca se cuenta desde antes de que empezara la ventana de deshacer
    return d ? Math.max(0, Math.ceil((d.hasta - Math.max(this.ahora(), d.desde)) / 1000)) : 0;
  });

  constructor() {
    afterNextRender(() => this.enfocar());
    const reloj = setInterval(() => this.ahora.set(Date.now()), 1000);
    inject(DestroyRef).onDestroy(() => {
      clearInterval(reloj);
      if (this.temporizadorEscritura) clearTimeout(this.temporizadorEscritura);
    });
  }

  protected hoy = computed(() => fechaLarga(this.ahora()));

  protected hace(fecha: number | null): string {
    return fecha === null ? '—' : haceCuanto(fecha, this.ahora());
  }

  /** Cambió el texto: es otra lectura (R8). Si se escribe a mano, se consulta al hacer una pausa. */
  protected escribir(valor: string): void {
    this.codigo.set(valor);
    this.store.nuevaLectura();
    if (this.temporizadorEscritura) clearTimeout(this.temporizadorEscritura);
    this.temporizadorEscritura = setTimeout(() => this.store.previsualizar(valor), ESPERA_ESCRITURA_MS);
  }

  /**
   * Enter en el campo (el lector lo manda al final de cada lectura). Si aún no se ve qué va a pasar,
   * lo muestra y pasa el foco a Registrar. Si ya se ve, registra (Enter dos veces = confirmar).
   */
  protected async enterEnCampo(e: Event): Promise<void> {
    e.preventDefault();
    if (this.temporizadorEscritura) clearTimeout(this.temporizadorEscritura);
    const texto = this.codigo().trim();
    if (texto && this.store.vistaPrevia()?.texto === texto) return this.pedirRegistro();
    await this.store.previsualizar(texto);
    if (texto) this.boton().nativeElement.focus();
  }

  /** Registrar: antes revisa la vista previa de lo que hay en el campo (R2, R3, R7). */
  protected async pedirRegistro(): Promise<void> {
    if (this.store.registrando()) return;                                   // R9
    const texto = this.codigo().trim();
    if (!texto) { this.store.avisarSinTarjeta(); sonido('error'); return this.enfocar(); }   // R7
    if (this.temporizadorEscritura) clearTimeout(this.temporizadorEscritura);
    if (this.store.vistaPrevia()?.texto !== texto) await this.store.previsualizar(texto);

    const aviso = this.store.vistaPrevia()?.aviso;
    if (aviso?.tono === 'rojo') {                                            // R2
      sonido('error');
      this.enfocar();
      return this.campo().nativeElement.select();
    }
    if (aviso?.confirmar) return this.confirmarRegistro.set(aviso.confirmar);   // R3
    await this.registrar();
  }

  protected async confirmarYRegistrar(): Promise<void> {
    this.confirmarRegistro.set(null);
    await this.registrar();
  }

  protected cancelarRegistro(): void {
    this.confirmarRegistro.set(null);
    this.enfocar();
    this.campo().nativeElement.select();
  }

  private async registrar(): Promise<void> {
    const ok = await this.store.registrar(this.codigo());
    sonido(ok ? 'ok' : 'error');
    if (ok) this.codigo.set('');
    this.enfocar();
  }

  /**
   * Si llega una letra o número con el foco fuera del campo (por ejemplo, en Registrar o en
   * Desinfectada), es el lector con la siguiente tarjeta: se lleva al campo para no perder la lectura.
   */
  protected teclaFueraDelCampo(e: KeyboardEvent): void {
    if (this.hayModal() || e.ctrlKey || e.altKey || e.metaKey) return;
    if (e.key.length !== 1 || e.key === ' ') return;          // Espacio sigue pulsando botones
    const campo = this.campo().nativeElement;
    if (e.target === campo || (e.target as HTMLElement).closest('input, textarea, select')) return;
    e.preventDefault();
    this.escribir(e.key);
    campo.focus();
  }

  /** R4: desinfectar por tandas. Sin argumento, todas las que esperan; con una, solo esa (desde el aviso). */
  protected abrirDesinfeccion(una?: { id: number; codigo: string }): void {
    const lista = una ? this.store.porDesinfectar().filter(t => t.id === una.id) : this.store.porDesinfectar();
    const tarjetas = (lista.length ? lista : una ? [{ ...una, desde: null }] : [])
      .map(t => ({ id: t.id, codigo: t.codigo, detalle: t.desde === null ? '' : `devuelta ${this.hace(t.desde)}` }));
    this.tanda.set({ tipo: 'desinfectar', tarjetas });
  }

  /** R5: revisar las que no volvieron y marcar las extraviadas. */
  protected abrirRevision(): void {
    this.tanda.set({
      tipo: 'extraviar',
      tarjetas: this.store.sinDevolver().map(t => ({ id: t.id, codigo: t.codigo, detalle: `${t.atencion} · sin señal ${this.hace(t.desde)}` })),
    });
  }

  protected async confirmarTanda(ids: number[]): Promise<void> {
    const t = this.tanda();
    this.tanda.set(null);
    if (!t) return;
    const elegidas = t.tarjetas.filter(x => ids.includes(x.id));
    if (t.tipo === 'desinfectar') await this.store.desinfectar(elegidas);
    else await this.store.extraviar(elegidas);
    this.enfocar();
  }

  protected cerrarTanda(): void {
    this.tanda.set(null);
    this.enfocar();
  }

  protected async deshacer(): Promise<void> {
    await this.store.deshacer();
    this.enfocar();
  }

  private enfocar(): void {
    this.campo().nativeElement.focus({ preventScroll: true });
  }
}

/**
 * Sonido corto: agudo si quedó registrado, grave y doble si no. El operador mira al paciente, no la
 * pantalla: el sonido le dice si tiene que mirar.
 */
function sonido(tipo: 'ok' | 'error'): void {
  try {
    const ctx = new AudioContext();
    const tonos = tipo === 'ok' ? [{ f: 880, t: 0 }] : [{ f: 220, t: 0 }, { f: 220, t: 0.22 }];
    for (const { f, t } of tonos) {
      const osc = ctx.createOscillator();
      const vol = ctx.createGain();
      osc.frequency.value = f;
      vol.gain.setValueAtTime(0.08, ctx.currentTime + t);
      vol.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + t + 0.18);
      osc.connect(vol).connect(ctx.destination);
      osc.start(ctx.currentTime + t);
      osc.stop(ctx.currentTime + t + 0.18);
    }
    setTimeout(() => ctx.close(), 600);
  } catch { /* sin audio disponible */ }
}
