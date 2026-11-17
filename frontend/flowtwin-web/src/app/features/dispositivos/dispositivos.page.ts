import { HttpClient, httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, linkedSignal, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API, mensajeDe, problemaDe } from '../../core/api/problema';
import { AuthService } from '../../core/auth/auth.service';
import { ClinicaStore } from '../../core/datos/clinica.store';
import { GeometriaService } from '../../core/datos/geometria.service';
import { ModoDatosService } from '../../core/datos/modo-datos.service';
import { PisoActualService } from '../../core/datos/piso-actual.service';
import { ToastService } from '../../core/ui/toast.service';
import { EstadoVacioComponent } from '../../shared/componentes/estado-vacio.component';
import { IconoComponent } from '../../shared/componentes/icono.component';
import { ModalComponent } from '../../shared/componentes/modal.component';
import {
  CuentaMqtt, NodoConClave, NodoInventario, PaginaInventario, TarjetaInventario,
  filtrosNodos, filtrosTarjetas, puedeDarDeBaja, raizTemas, textoNodo, textoProblema,
} from './modelo-dispositivos';
import { InventarioSimulado } from './simulacion-dispositivos';

type Pestana = 'nodos' | 'tarjetas';
interface ZonaApi { id: number; nombre: string; tipo: string }
interface Confirmacion { titulo: string; boton: string; accion: () => Promise<void> }
interface ClaveVista { codigo: string; filas: { etiqueta: string; valor: string }[] }

const ESPERA_BUSQUEDA_MS = 300;

/**
 * P9 · Dispositivos (solo Administrador), por excepción: abre en lo que requiere atención; lo demás se
 * encuentra con filtros (con su conteo) y búsqueda, paginado en el servidor. En la Simulación (ADR-31), los
 * nodos y tarjetas de la clínica simulada: las acciones se hacen en memoria y nunca llegan a la base.
 */
@Component({
  selector: 'app-dispositivos',
  imports: [IconoComponent, ModalComponent, EstadoVacioComponent],
  templateUrl: './dispositivos.page.html',
  styleUrl: './dispositivos.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DispositivosPage {
  private http = inject(HttpClient);
  protected pisos = inject(PisoActualService);
  private toast = inject(ToastService);
  private modo = inject(ModoDatosService);
  private clinica = inject(ClinicaStore);
  private geometriaSim = inject(GeometriaService);
  private auth = inject(AuthService);
  private inv = new InventarioSimulado(this.clinica, id => this.geometriaSim.nombreZona(id), () => this.auth.actor());
  protected simulando = this.modo.simulacion;

  protected pestana = signal<Pestana>('nodos');
  protected filtro = linkedSignal({ source: this.pestana, computation: () => 'ATENCION' });
  protected buscar = linkedSignal({ source: this.pestana, computation: () => '' });
  protected pagina = linkedSignal({ source: () => [this.pestana(), this.filtro(), this.buscar()], computation: () => 1 });
  private recarga = signal(0);
  private relojReal = signal(Date.now());
  /** En la Simulación, la hora de la clínica simulada (los «hace» se cuentan desde ahí). */
  protected ahora = computed(() => this.simulando() ? this.clinica.ahora() : this.relojReal());

  private sedeId = computed(() => this.pisos.sede()?.id ?? null);

  private nodosRes = httpResource<PaginaInventario<NodoInventario>>(() => {
    this.recarga();
    return this.pestana() !== 'nodos' || this.simulando() ? undefined
      : { url: `${API}/inventario/nodos`, params: { filtro: this.filtro(), buscar: this.buscar(), pagina: this.pagina() } };
  });
  private tarjetasRes = httpResource<PaginaInventario<TarjetaInventario>>(() => {
    this.recarga();
    const sede = this.sedeId();
    return this.pestana() !== 'tarjetas' || sede === null || this.simulando() ? undefined
      : { url: `${API}/inventario/tarjetas`, params: { sedeId: sede, filtro: this.filtro(), buscar: this.buscar(), pagina: this.pagina() } };
  });
  /** Los conteos de las pestañas se piden una vez, sin filtro: el total de cada una. */
  private totalNodos = httpResource<PaginaInventario<NodoInventario>>(() => { this.recarga(); return this.simulando() ? undefined : { url: `${API}/inventario/nodos`, params: { filtro: 'TODAS' } }; });
  private totalTarjetas = httpResource<PaginaInventario<TarjetaInventario>>(() => {
    this.recarga();
    const sede = this.sedeId();
    return sede === null || this.simulando() ? undefined : { url: `${API}/inventario/tarjetas`, params: { sedeId: sede, filtro: 'TODAS' } };
  });

  protected nodos = computed(() => this.simulando() ? this.inv.nodos(this.filtro(), this.buscar(), this.pagina())
    : this.nodosRes.hasValue() ? this.nodosRes.value() : null);
  protected tarjetas = computed(() => this.simulando() ? this.inv.tarjetas(this.filtro(), this.buscar(), this.pagina())
    : this.tarjetasRes.hasValue() ? this.tarjetasRes.value() : null);
  protected cuentaNodos = computed(() => this.simulando() ? this.inv.nodos('TODAS', '', 1).conteos['TODAS']
    : this.totalNodos.hasValue() ? this.totalNodos.value().conteos['TODAS'] : null);
  protected cuentaTarjetas = computed(() => this.simulando() ? this.inv.tarjetas('TODAS', '', 1).conteos['TODAS']
    : this.totalTarjetas.hasValue() ? this.totalTarjetas.value().conteos['TODAS'] : null);
  protected pagina$ = computed(() => this.pestana() === 'nodos' ? this.nodos() : this.tarjetas());
  protected paginas = computed(() => { const p = this.pagina$(); return p ? Math.max(1, Math.ceil(p.total / p.tamano)) : 1; });
  protected filtros = computed(() => {
    const p = this.pagina$();
    if (!p) return [];
    return this.pestana() === 'nodos' ? filtrosNodos(p.conteos) : filtrosTarjetas(p.conteos);
  });
  protected error = computed(() => {
    if (this.simulando()) return null;
    const e = this.nodosRes.error() ?? this.tarjetasRes.error();
    return e ? mensajeDe(problemaDe(e)) : null;
  });

  protected readonly textoNodo = textoNodo;
  protected readonly textoProblema = textoProblema;
  protected readonly puedeDarDeBaja = puedeDarDeBaja;

  // ---------- Menú ⋯ y ventanas
  protected menu = signal<string | null>(null);
  protected confirmacion = signal<Confirmacion | null>(null);
  protected clave = signal<ClaveVista | null>(null);
  protected registrandoNodo = signal(false);
  protected cambiandoZona = signal<NodoInventario | null>(null);
  protected registrandoTarjetas = signal(false);
  protected tarjetasNuevas = signal<{ codigo: string; codigoQr: string }[] | null>(null);
  protected trabajando = signal(false);
  protected errorVentana = signal<string | null>(null);

  // Formularios
  protected codigoNodo = signal('');
  protected macNodo = signal('');
  protected pisoElegido = signal<number | null>(null);
  protected zonaElegida = signal<number | null>(null);
  protected desde = signal<number | null>(null);
  protected hasta = signal<number | null>(null);

  protected pisosDeSede = computed(() => this.pisos.pisosDeSede());
  private geometria = httpResource<{ zonas: ZonaApi[] }>(() => {
    const p = this.pisoElegido();
    return p === null || this.simulando() ? undefined : `${API}/pisos/${p}/geometria`;
  });
  protected zonas = computed(() => (this.simulando() ? this.geometriaSim.zonas() : this.geometria.hasValue() ? this.geometria.value().zonas : [])
    .filter(z => z.tipo !== 'TRANSITO' && z.tipo !== 'SALIDA'));

  private temporizador?: ReturnType<typeof setTimeout>;

  constructor() {
    const reloj = setInterval(() => this.relojReal.set(Date.now()), 60_000);
    inject(DestroyRef).onDestroy(() => { clearInterval(reloj); clearTimeout(this.temporizador); });
  }

  protected escribir(texto: string): void {
    clearTimeout(this.temporizador);
    this.temporizador = setTimeout(() => this.buscar.set(texto.trim()), ESPERA_BUSQUEDA_MS);
  }

  protected abrirRegistro(): void {
    this.errorVentana.set(null);
    if (this.pestana() === 'nodos') {
      this.codigoNodo.set(''); this.macNodo.set('');
      this.pisoElegido.set(this.pisos.piso()?.id ?? this.pisosDeSede()[0]?.id ?? null); this.zonaElegida.set(null);
      this.registrandoNodo.set(true);
    } else {
      this.desde.set(null); this.hasta.set(null); this.tarjetasNuevas.set(null);
      this.registrandoTarjetas.set(true);
    }
  }

  protected async registrarNodo(): Promise<void> {
    await this.trabajar(async () => {
      const r = this.simulando() ? this.inv.registrarNodo(this.codigoNodo(), this.macNodo(), this.zonaElegida()!)
        : await firstValueFrom(this.http.post<NodoConClave>(`${API}/nodos`,
          { codigo: this.codigoNodo(), mac: this.macNodo(), zonaId: this.zonaElegida(), posX: null, posY: null }));
      this.registrandoNodo.set(false);
      this.mostrarClave(r);
    });
  }

  protected abrirCambioZona(n: NodoInventario): void {
    this.menu.set(null); this.errorVentana.set(null);
    this.pisoElegido.set(n.pisoId || this.pisos.piso()?.id || null);
    this.zonaElegida.set(n.zonaId);
    this.cambiandoZona.set(n);
  }

  protected async cambiarZona(n: NodoInventario): Promise<void> {
    await this.trabajar(async () => {
      if (this.simulando()) this.inv.cambiarZona(n.codigo, this.zonaElegida()!);
      else await firstValueFrom(this.http.put(`${API}/nodos/${n.id}`, { zonaId: this.zonaElegida(), posX: n.posX, posY: n.posY, ajusteRssiDb: n.ajusteRssiDb }));
      this.cambiandoZona.set(null);
      this.toast.mostrar(`${n.codigo} ahora está en otra zona`);
    });
  }

  protected confirmarRegenerar(n: NodoInventario): void {
    this.confirmar({
      titulo: `¿Regenerar la clave de ${n.codigo}?`, boton: 'Regenerar',
      accion: async () => this.mostrarClave(this.simulando() ? this.inv.regenerarClave(n.codigo)
        : await firstValueFrom(this.http.post<NodoConClave>(`${API}/nodos/${n.id}/regenerar-clave`, null))),
    });
  }

  protected confirmarBajaNodo(n: NodoInventario): void {
    this.confirmar({
      titulo: `¿Dar de baja ${n.codigo}?`, boton: 'Dar de baja',
      accion: async () => {
        if (this.simulando()) this.inv.darDeBajaNodo(n.codigo); else await firstValueFrom(this.http.post(`${API}/nodos/${n.id}/baja`, null));
        this.toast.mostrar(`${n.codigo} dado de baja`); },
    });
  }

  protected confirmarBajaTarjeta(t: TarjetaInventario): void {
    this.confirmar({
      titulo: `¿Dar de baja ${t.codigo}?`, boton: 'Dar de baja',
      accion: async () => {
        if (this.simulando()) this.inv.darDeBajaTarjeta(t.codigo); else await firstValueFrom(this.http.post(`${API}/inventario/tarjetas/${t.id}/baja`, null));
        this.toast.mostrar(`${t.codigo} dada de baja`); },
    });
  }

  protected async ejecutarConfirmacion(c: Confirmacion): Promise<void> {
    await this.trabajar(async () => { await c.accion(); this.confirmacion.set(null); });
  }

  protected async registrarTarjetas(): Promise<void> {
    await this.trabajar(async () => {
      const r = this.simulando() ? this.inv.registrarTarjetas(this.desde()!, this.hasta()!)
        : await firstValueFrom(this.http.post<{ codigo: string; codigoQr: string }[]>(`${API}/inventario/tarjetas`,
          { sedeId: this.sedeId(), desde: this.desde(), hasta: this.hasta() }));
      this.tarjetasNuevas.set(r);
    });
  }

  protected async copiar(texto: string): Promise<void> {
    try { await navigator.clipboard.writeText(texto); this.toast.mostrar('Copiado'); }
    catch { this.toast.mostrar('No se pudo copiar: selecciona el texto y cópialo a mano', 'warn'); }
  }

  protected listaQr = computed(() => (this.tarjetasNuevas() ?? []).map(t => t.codigoQr).join('\n'));

  private confirmar(c: Confirmacion): void {
    this.menu.set(null); this.errorVentana.set(null);
    this.confirmacion.set(c);
  }

  private mostrarClave(r: NodoConClave): void {
    const c: CuentaMqtt = r.mqtt;
    this.clave.set({ codigo: r.nodo.codigo, filas: [
      { etiqueta: 'Usuario', valor: c.usuario }, { etiqueta: 'Clave', valor: r.clave }, { etiqueta: 'Temas', valor: raizTemas(c) },
    ] });
  }

  /** Una acción del Administrador: muestra el error dentro de la ventana y recarga los listados al terminar bien. */
  private async trabajar(accion: () => Promise<void>): Promise<void> {
    this.trabajando.set(true);
    this.errorVentana.set(null);
    try {
      await accion();
      this.recarga.update(n => n + 1);
    } catch (e) {
      // En la Simulación los errores son de las reglas en memoria y traen su propio mensaje
      this.errorVentana.set(e instanceof Error ? e.message : mensajeDe(problemaDe(e)));
    } finally {
      this.trabajando.set(false);
    }
  }
}
