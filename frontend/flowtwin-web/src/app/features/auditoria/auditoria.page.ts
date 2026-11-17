import { HttpClient, httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, linkedSignal, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API, mensajeDe, problemaDe } from '../../core/api/problema';
import { ClinicaStore } from '../../core/datos/clinica.store';
import { ModoDatosService } from '../../core/datos/modo-datos.service';
import { ToastService } from '../../core/ui/toast.service';
import { descargar, nombreDe } from '../../core/utils/descarga';
import { isoFecha } from '../../core/utils/tiempo';
import { EstadoVacioComponent } from '../../shared/componentes/estado-vacio.component';
import { IconoComponent } from '../../shared/componentes/icono.component';
import { ModalComponent } from '../../shared/componentes/modal.component';
import { cuando } from '../dispositivos/modelo-dispositivos';
import { PeriodoId, Rango, rangoDe } from '../historial/modelo-historial';
import {
  CATEGORIAS, DetalleAuditoria, FilaAuditoria, PaginaAuditoria, ResultadoIntegridad, cambios, problemaPeriodo, textoEntidad, textoIntegridad,
} from './modelo-auditoria';
import { BitacoraSimulada } from './simulacion-auditoria';

const ESPERA_BUSQUEDA_MS = 300;
interface UsuarioOpcion { id: number; nombres: string; apellidos: string }

/**
 * P11 · Auditoría (solo Administrador). La bitácora es de solo lectura: se filtra por período, usuario, categoría y
 * texto, se pagina en el servidor y cada registro abre su «antes y después». La verificación recalcula la cadena de
 * sellos del período (ADR-30). En la Simulación (ADR-31) muestra la bitácora de la clínica simulada, sin sello.
 */
@Component({
  selector: 'app-auditoria',
  imports: [IconoComponent, ModalComponent, EstadoVacioComponent],
  templateUrl: './auditoria.page.html',
  styleUrl: './auditoria.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuditoriaPage {
  private http = inject(HttpClient);
  private toast = inject(ToastService);
  private modo = inject(ModoDatosService);
  private clinica = inject(ClinicaStore);
  private bitacora = new BitacoraSimulada(this.clinica);
  protected simulando = this.modo.simulacion;

  protected readonly categorias = CATEGORIAS;
  protected readonly textoEntidad = textoEntidad;
  private relojReal = signal(Date.now());
  /** En la Simulación, la hora de la clínica simulada. */
  protected ahora = computed(() => this.simulando() ? this.clinica.ahora() : this.relojReal());
  protected hoy = computed(() => isoFecha(this.ahora()));

  protected periodo = signal<PeriodoId>('7');
  protected elegido = signal<Rango>({ desde: '', hasta: '' });
  protected rango = computed(() => rangoDe(this.periodo(), this.hoy(), this.periodo() === 'elegir' ? this.elegido() : null));
  protected problema = computed(() => problemaPeriodo(this.rango(), this.hoy()));
  protected usuarioId = signal<number | null>(null);
  protected categoria = signal<string | null>(null);
  protected buscar = signal('');
  /** Lo que se ve escrito en el buscador (la búsqueda se aplica 300 ms después de dejar de escribir). */
  protected textoBuscar = signal('');
  protected pagina = linkedSignal({ source: () => [this.rango(), this.usuarioId(), this.categoria(), this.buscar()], computation: () => 1 });

  /** Los filtros tal como los recibe la API (sin la página). */
  private filtros = computed(() => {
    const r = this.rango();
    const p: Record<string, string> = { desde: r.desde, hasta: r.hasta };
    if (this.usuarioId() !== null) p['usuarioId'] = String(this.usuarioId());
    if (this.categoria()) p['categoria'] = this.categoria()!;
    if (this.buscar()) p['buscar'] = this.buscar();
    return p;
  });

  private recurso = httpResource<PaginaAuditoria>(() =>
    this.problema() || this.simulando() ? undefined : { url: `${API}/auditoria`, params: { ...this.filtros(), pagina: this.pagina() } });
  /** Se conserva la página anterior mientras llega la nueva: la tabla no parpadea al filtrar. */
  protected datos = linkedSignal<PaginaAuditoria | undefined, PaginaAuditoria | null>({
    source: () => this.simulando()
      ? (this.problema() ? undefined : this.bitacora.pagina(this.filtrosSim(), this.pagina()))
      : this.recurso.hasValue() ? this.recurso.value() : undefined,
    computation: (nueva, anterior) => nueva ?? anterior?.value ?? null,
  });
  protected paginas = computed(() => { const d = this.datos(); return d ? Math.max(1, Math.ceil(d.total / d.tamano)) : 1; });
  protected totalTodo = computed(() => Object.values(this.datos()?.conteos ?? {}).reduce((s, n) => s + n, 0));
  protected error = computed(() => { if (this.simulando()) return null; const e = this.recurso.error(); return e ? mensajeDe(problemaDe(e)) : null; });

  private usuariosRes = httpResource<UsuarioOpcion[]>(() => this.simulando() ? undefined : ({ url: `${API}/usuarios`, params: { incluirInactivos: true } }));
  protected usuarios = computed(() => this.simulando() ? this.bitacora.usuarios() : (this.usuariosRes.hasValue() ? this.usuariosRes.value() : [])
    .map(u => ({ id: u.id, nombre: `${u.nombres} ${u.apellidos}`.trim() })).sort((a, b) => a.nombre.localeCompare(b.nombre, 'es')));

  private filtrosSim = computed(() => ({ rango: this.rango(), usuarioId: this.usuarioId(), categoria: this.categoria(), buscar: this.buscar() }));

  protected cuandoTexto = (f: FilaAuditoria) => cuando(Date.parse(f.cuando), this.ahora());

  // ---------- Detalle
  protected detalle = signal<DetalleAuditoria | null>(null);
  protected cambiosDetalle = computed(() => { const d = this.detalle(); return d ? cambios(d.antes, d.despues) : []; });
  protected fechaCompleta = (iso: string) => new Intl.DateTimeFormat('es-PE', { dateStyle: 'long', timeStyle: 'medium' }).format(Date.parse(iso));

  // ---------- Integridad y exportación
  protected integridad = signal<ResultadoIntegridad | null>(null);
  protected textoIntegridad = computed(() => { const r = this.integridad(); return r ? textoIntegridad(r) : null; });
  protected trabajando = signal(false);

  private temporizador?: ReturnType<typeof setTimeout>;

  constructor() {
    const reloj = setInterval(() => this.relojReal.set(Date.now()), 60_000);
    inject(DestroyRef).onDestroy(() => { clearInterval(reloj); clearTimeout(this.temporizador); });
  }

  protected escribir(texto: string): void {
    this.textoBuscar.set(texto);
    clearTimeout(this.temporizador);
    this.temporizador = setTimeout(() => this.buscar.set(texto.trim()), ESPERA_BUSQUEDA_MS);
  }

  protected elegirFecha(campo: keyof Rango, valor: string): void {
    this.elegido.update(r => ({ ...r, [campo]: valor }));
  }

  protected async abrir(f: FilaAuditoria): Promise<void> {
    if (this.simulando()) { this.detalle.set(this.bitacora.detalle(f.id)); return; }
    try {
      this.detalle.set(await firstValueFrom(this.http.get<DetalleAuditoria>(`${API}/auditoria/${f.id}`)));
    } catch (e) {
      this.toast.mostrar(mensajeDe(problemaDe(e)), 'bad');
    }
  }

  /** Desde el detalle: todo lo que hizo esa persona ese día, en cualquier categoría. */
  protected verDiaDe(f: FilaAuditoria): void {
    const dia = isoFecha(Date.parse(f.cuando));
    clearTimeout(this.temporizador);
    this.usuarioId.set(f.usuarioId);
    this.elegido.set({ desde: dia, hasta: dia });
    this.periodo.set('elegir');
    this.categoria.set(null);
    this.buscar.set('');
    this.textoBuscar.set('');
    this.detalle.set(null);
  }

  protected async exportar(): Promise<void> {
    await this.trabajar(async () => {
      if (this.simulando()) {
        const rango = this.rango();
        descargar(this.bitacora.csv(this.filtrosSim()), `auditoria_simulacion_${rango.desde}_${rango.hasta}.csv`);
        return;
      }
      const r = await firstValueFrom(this.http.get(`${API}/auditoria/exportar.csv`, { params: this.filtros(), responseType: 'blob', observe: 'response' }));
      const rango = this.rango();
      descargar(r.body!, nombreDe(r.headers.get('Content-Disposition')) ?? `auditoria_${rango.desde}_${rango.hasta}.csv`);
    });
  }

  protected async verificar(): Promise<void> {
    if (this.simulando()) {
      this.toast.mostrar('La bitácora simulada no lleva sello: es ficticia y no se guarda. En los datos reales, esto verifica la cadena de sellos.', 'warn');
      return;
    }
    await this.trabajar(async () => {
      const r = this.rango();
      this.integridad.set(await firstValueFrom(this.http.post<ResultadoIntegridad>(`${API}/auditoria/integridad`, null, { params: { desde: r.desde, hasta: r.hasta } })));
    });
  }

  private async trabajar(accion: () => Promise<void>): Promise<void> {
    if (this.problema()) return;
    this.trabajando.set(true);
    try { await accion(); }
    catch (e) { this.toast.mostrar(await mensajeDeBlob(e), 'bad'); }
    finally { this.trabajando.set(false); }
  }
}

/** Un error de una descarga llega como Blob: se lee para mostrar el mensaje del servidor (p. ej. «máximo 10 000 filas»). */
async function mensajeDeBlob(e: unknown): Promise<string> {
  const cuerpo = (e as { error?: unknown })?.error;
  if (cuerpo instanceof Blob) {
    try { return mensajeDe(problemaDe({ ...(e as object), error: JSON.parse(await cuerpo.text()) })); } catch { /* sigue */ }
  }
  return mensajeDe(problemaDe(e));
}
