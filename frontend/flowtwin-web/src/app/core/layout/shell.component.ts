import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { IconoComponent } from '../../shared/componentes/icono.component';
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
  imports: [RouterOutlet, RouterLink, RouterLinkActive, IconoComponent],
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

  constructor() {
    this.modo.cargar();
    inject(PisoActualService).cargar();
    // Al salir del sistema (cerrar sesión, sesión vencida) la simulación se apaga: nunca queda prendida
    inject(DestroyRef).onDestroy(() => this.modo.salir());
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
