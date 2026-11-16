import { HttpClient, httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, linkedSignal, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API, mensajeDe, problemaDe } from '../../core/api/problema';
import { AuthService } from '../../core/auth/auth.service';
import { PisoActualService } from '../../core/datos/piso-actual.service';
import { ToastService } from '../../core/ui/toast.service';
import { EstadoVacioComponent } from '../../shared/componentes/estado-vacio.component';
import { IconoComponent } from '../../shared/componentes/icono.component';
import { ModalComponent } from '../../shared/componentes/modal.component';
import {
  DatosUsuario, ROLES, RolCodigo, UsuarioAdmin, UsuarioConTemporal, datosParaEnviar, estadoUsuario, filtrarUsuarios,
  formularioValido, nombreCompleto, rolTienePisos, textoPisos, textoRol, textoUltimoIngreso,
} from './modelo-usuarios';

interface Confirmacion { titulo: string; boton: string; accion: () => Promise<void> }

/**
 * P10 · Usuarios (solo Administrador). La lista llega completa (decenas de usuarios) y se busca y filtra aquí.
 * Las reglas de seguridad (no desactivarse, un último Administrador, pisos del Operador) las aplica el
 * servidor; la pantalla solo evita ofrecer lo que sabe que será rechazado.
 */
@Component({
  selector: 'app-usuarios',
  imports: [IconoComponent, ModalComponent, EstadoVacioComponent],
  templateUrl: './usuarios.page.html',
  styleUrl: './usuarios.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UsuariosPage {
  private http = inject(HttpClient);
  private auth = inject(AuthService);
  private pisosServicio = inject(PisoActualService);
  private toast = inject(ToastService);

  protected readonly roles = ROLES;
  protected readonly textoRol = textoRol;
  protected readonly nombreCompleto = nombreCompleto;
  protected readonly estadoUsuario = estadoUsuario;
  protected readonly rolTienePisos = rolTienePisos;

  protected buscar = signal('');
  protected rolFiltro = signal<RolCodigo | ''>('');
  protected inactivos = signal(false);
  private recarga = signal(0);
  protected ahora = signal(Date.now());

  private recurso = httpResource<UsuarioAdmin[]>(() => {
    this.recarga();
    return { url: `${API}/usuarios`, params: { incluirInactivos: this.inactivos() } };
  });
  /** Se conserva la lista anterior mientras se recarga: la tabla no parpadea al editar. */
  private lista = linkedSignal<UsuarioAdmin[] | undefined, UsuarioAdmin[] | null>({
    source: () => this.recurso.hasValue() ? this.recurso.value() : undefined,
    computation: (nueva, anterior) => nueva ?? anterior?.value ?? null,
  });
  protected visibles = computed(() => filtrarUsuarios(this.lista() ?? [], this.buscar(), this.rolFiltro()));
  protected error = computed(() => { const e = this.recurso.error(); return e ? mensajeDe(problemaDe(e)) : null; });

  /** Todos los pisos publicados (el Administrador los ve todos); con su sede si hay más de una. */
  protected pisosDisponibles = computed(() => {
    const sedes = this.pisosServicio.sedes();
    return sedes.flatMap(s => [...s.pisos].sort((a, b) => a.nivel - b.nivel)
      .map(p => ({ id: p.id, nombre: sedes.length > 1 ? `${s.nombre} · ${p.nombre}` : p.nombre })));
  });
  private nombrePiso = (id: number) => this.pisosDisponibles().find(p => p.id === id)?.nombre ?? `Piso ${id}`;
  protected pisosDe = (u: UsuarioAdmin) => textoPisos(u, this.nombrePiso);
  protected ultimoIngreso = (u: UsuarioAdmin) => textoUltimoIngreso(u, this.ahora());
  protected esYo = (u: UsuarioAdmin) => u.id === this.auth.usuario()?.id;

  // ---------- Menú ⋯ y ventanas
  protected menu = signal<number | null>(null);
  protected confirmacion = signal<Confirmacion | null>(null);
  protected clave = signal<{ nombre: string; valor: string } | null>(null);
  protected formulario = signal<{ id: number | null } | null>(null);
  protected trabajando = signal(false);
  protected errorVentana = signal<string | null>(null);

  protected datos = signal<DatosUsuario>({ nombres: '', apellidos: '', correo: '', rol: 'OPERADOR', pisos: [] });
  protected valido = computed(() => formularioValido(this.datos()));

  constructor() {
    if (this.pisosServicio.sedes().length === 0) void this.pisosServicio.cargar();
    const reloj = setInterval(() => this.ahora.set(Date.now()), 60_000);
    inject(DestroyRef).onDestroy(() => clearInterval(reloj));
  }

  protected campo<K extends keyof DatosUsuario>(nombre: K, valor: DatosUsuario[K]): void {
    this.datos.update(d => ({ ...d, [nombre]: valor }));
  }

  protected alternarPiso(id: number): void {
    this.datos.update(d => ({ ...d, pisos: d.pisos.includes(id) ? d.pisos.filter(p => p !== id) : [...d.pisos, id] }));
  }

  protected abrirNuevo(): void {
    this.errorVentana.set(null);
    this.datos.set({ nombres: '', apellidos: '', correo: '', rol: 'OPERADOR', pisos: this.pisosServicio.piso() ? [this.pisosServicio.piso()!.id] : [] });
    this.formulario.set({ id: null });
  }

  protected abrirEditar(u: UsuarioAdmin): void {
    this.menu.set(null); this.errorVentana.set(null);
    this.datos.set({ nombres: u.nombres, apellidos: u.apellidos, correo: u.correo, rol: u.rol, pisos: [...u.pisos] });
    this.formulario.set({ id: u.id });
  }

  protected async guardar(id: number | null): Promise<void> {
    const cuerpo = datosParaEnviar(this.datos());
    await this.trabajar(async () => {
      if (id === null) {
        const r = await firstValueFrom(this.http.post<UsuarioConTemporal>(`${API}/usuarios`, cuerpo));
        this.formulario.set(null);
        this.mostrarClave(r);
      } else {
        await firstValueFrom(this.http.put(`${API}/usuarios/${id}`, cuerpo));
        this.formulario.set(null);
        this.toast.mostrar('Cambios guardados');
      }
    });
  }

  protected confirmarRestablecer(u: UsuarioAdmin): void {
    this.confirmar({
      titulo: `¿Restablecer la contraseña de ${nombreCompleto(u)}?`, boton: 'Restablecer',
      accion: async () => this.mostrarClave(await firstValueFrom(this.http.post<UsuarioConTemporal>(`${API}/usuarios/${u.id}/restablecer-contrasena`, null))),
    });
  }

  protected confirmarDesactivar(u: UsuarioAdmin): void {
    this.confirmar({
      titulo: `¿Desactivar a ${nombreCompleto(u)}?`, boton: 'Desactivar',
      accion: async () => { await firstValueFrom(this.http.post(`${API}/usuarios/${u.id}/desactivar`, null)); this.toast.mostrar(`${nombreCompleto(u)} desactivado`); },
    });
  }

  protected async activar(u: UsuarioAdmin): Promise<void> {
    this.menu.set(null);
    try {
      await firstValueFrom(this.http.post(`${API}/usuarios/${u.id}/activar`, null));
      this.toast.mostrar(`${nombreCompleto(u)} activado`);
      this.recarga.update(n => n + 1);
    } catch (e) {
      this.toast.mostrar(mensajeDe(problemaDe(e)), 'bad');
    }
  }

  protected async ejecutarConfirmacion(c: Confirmacion): Promise<void> {
    await this.trabajar(async () => { await c.accion(); this.confirmacion.set(null); });
  }

  protected async copiar(texto: string): Promise<void> {
    try { await navigator.clipboard.writeText(texto); this.toast.mostrar('Copiado'); }
    catch { this.toast.mostrar('No se pudo copiar: selecciona el texto y cópialo a mano', 'warn'); }
  }

  private confirmar(c: Confirmacion): void {
    this.menu.set(null); this.errorVentana.set(null);
    this.confirmacion.set(c);
  }

  private mostrarClave(r: UsuarioConTemporal): void {
    this.clave.set({ nombre: nombreCompleto(r.usuario), valor: r.contrasenaTemporal });
  }

  /** Una acción del Administrador: el error se muestra dentro de la ventana y la lista se recarga al terminar bien. */
  private async trabajar(accion: () => Promise<void>): Promise<void> {
    this.trabajando.set(true);
    this.errorVentana.set(null);
    try {
      await accion();
      this.recarga.update(n => n + 1);
    } catch (e) {
      this.errorVentana.set(mensajeDe(problemaDe(e)));
    } finally {
      this.trabajando.set(false);
    }
  }
}
