import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, signal, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter, map } from 'rxjs';
import { IconoComponent } from '../../shared/componentes/icono.component';
import { AlertasPanelComponent } from '../alertas/alertas-panel.component';
import { AlertasService } from '../alertas/alertas.service';
import { AuthService } from '../auth/auth.service';
import { ModoDatosService } from '../datos/modo-datos.service';
import { PisoActualService } from '../datos/piso-actual.service';
import { menuDe } from './menu';

/** Clave de localStorage: la barra colapsada o expandida es una preferencia de cada persona. */
const CLAVE_COLAPSADA = 'flowtwin.menuColapsado';

/**
 * Marco de la app con sesión iniciada: barra lateral oscura (colapsable) con el menú del rol, la franja
 * de simulación cuando está activa y el área de cada pantalla. El título y los filtros (sede, piso) van
 * en el encabezado de cada pantalla, no aquí: así no se repiten.
 */
@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, IconoComponent, AlertasPanelComponent],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss',
  host: {
    '(document:pointerdown)': 'auth.registrarActividad()',
    '(document:keydown)': 'auth.registrarActividad()',
  },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ShellComponent {
  protected auth = inject(AuthService);
  protected modo = inject(ModoDatosService);

  protected menu = computed(() => {
    const rol = this.auth.rol();
    return rol ? menuDe(rol) : [];
  });
  protected iniciales = computed(() => {
    const u = this.auth.usuario();
    return u ? `${u.nombre.charAt(0)}${u.nombre.split(' ')[1]?.charAt(0) ?? ''}`.toUpperCase() : '';
  });

  protected colapsada = signal(leerColapsada());

  protected alertas = inject(AlertasService);
  protected panelAlertas = signal(false);
  /** Mientras es true, la campana se mueve: llegó una alerta nueva. */
  protected sonando = signal(false);
  private finSonando?: ReturnType<typeof setTimeout>;

  private router = inject(Router);
  private url = toSignal(this.router.events.pipe(filter(e => e instanceof NavigationEnd), map(() => this.router.url)),
    { initialValue: this.router.url });
  /** Admisión y Dispositivos nunca se simulan (ADR-22): ahí la franja aclara que lo que se ve es real. */
  protected pantallaReal = computed(() =>
    this.url().startsWith('/admision') ? 'Admisión' : this.url().startsWith('/dispositivos') ? 'Dispositivos' : null);

  /**
   * Con la contraseña temporal el servidor solo permite cambiarla (F-02) y responde 403 a todo lo demás: los pisos y
   * la configuración se piden cuando ya no es temporal. Así también se cargan solos justo después de cambiarla.
   */
  private sesionCompleta = computed(() => this.auth.usuario()?.debeCambiarContrasena === false);

  constructor() {
    const pisos = inject(PisoActualService);
    effect(() => {
      if (!this.sesionCompleta()) return;
      untracked(() => { this.modo.cargar(); pisos.cargar(); });
    });
    // Al salir del sistema (cerrar sesión, sesión vencida) la simulación se apaga: nunca queda prendida
    inject(DestroyRef).onDestroy(() => { this.modo.salir(); clearTimeout(this.finSonando); });
    effect(() => {
      if (!this.alertas.pulso()) return;
      this.sonando.set(true);
      clearTimeout(this.finSonando);
      this.finSonando = setTimeout(() => this.sonando.set(false), 1_200);
    });
  }

  protected alternarColapsada(): void {
    this.colapsada.update(v => !v);
    try { localStorage.setItem(CLAVE_COLAPSADA, String(this.colapsada())); } catch { /* sin almacenamiento */ }
  }

  protected alternarSimulacion(): void {
    if (this.modo.simulacion()) this.modo.salir();
    else this.modo.activar();
  }
}

function leerColapsada(): boolean {
  try { return localStorage.getItem(CLAVE_COLAPSADA) === 'true'; } catch { return false; }
}
