import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter, map } from 'rxjs';
import { BotonDirective } from '../../shared/componentes/boton.directive';
import { IconoComponent } from '../../shared/componentes/icono.component';
import { LogoComponent } from '../../shared/componentes/logo.component';
import { AuthService } from '../auth/auth.service';
import { ClinicaStore } from '../datos/clinica.store';
import { SEDE } from '../datos/clinica.config';
import { Alerta } from '../datos/modelos';
import { SimuladorService } from '../datos/simulador.service';
import { ToastService } from '../ui/toast.service';
import { hhmm } from '../utils/tiempo';
import { MENU_POR_ROL } from './menu';

/**
 * Marco de la app con sesión iniciada: barra lateral negra con el menú del rol,
 * barra superior y el pie de demo. Las pantallas se dibujan en <router-outlet>.
 */
@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, LogoComponent, IconoComponent, BotonDirective],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss',
  host: {
    '(document:click)': 'clicFuera($event)',
    '(document:pointerdown)': 'auth.registrarActividad()',
    '(document:keydown)': 'auth.registrarActividad()',
  },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ShellComponent {
  protected auth = inject(AuthService);
  protected clinica = inject(ClinicaStore);
  private router = inject(Router);
  private ruta = inject(ActivatedRoute);
  private toast = inject(ToastService);

  protected readonly sede = SEDE;
  protected readonly hhmm = hhmm;
  protected menu = computed(() => MENU_POR_ROL[this.auth.rol() ?? 'Gerencia']);
  protected iniciales = computed(() =>
    (this.auth.usuario()?.nombre ?? '').split(' ').map(p => p[0]).join('').slice(0, 2));
  protected primerNombre = computed(() => this.auth.usuario()?.nombre.split(' ')[0] ?? '');

  /** Título de la pantalla actual, tomado de data.titulo de la ruta, para las migas de pan. */
  protected titulo = toSignal(
    this.router.events.pipe(filter(e => e instanceof NavigationEnd), map(() => this.tituloActual())),
    { initialValue: this.tituloActual() },
  );

  protected campanaAbierta = signal(false);
  protected menuUsuarioAbierto = signal(false);

  constructor() {
    inject(SimuladorService).iniciar();
  }

  protected alternarCampana(): void {
    if (this.campanaAbierta()) this.clinica.marcarTodasLeidas();
    this.campanaAbierta.update(v => !v);
    this.menuUsuarioAbierto.set(false);
  }

  protected alternarMenuUsuario(): void {
    this.menuUsuarioAbierto.update(v => !v);
    this.campanaAbierta.set(false);
  }

  protected irAAlerta(alerta: Alerta): void {
    this.clinica.marcarTodasLeidas();
    this.campanaAbierta.set(false);
    this.router.navigateByUrl(alerta.ruta);
  }

  protected alternarSimulacion(): void {
    this.clinica.simulacionActiva.update(v => !v);
    this.toast.mostrar(this.clinica.simulacionActiva() ? 'Simulación reanudada' : 'Simulación en pausa');
  }

  /** Cierra los menús desplegables al hacer clic fuera de ellos. */
  protected clicFuera(evento: MouseEvent): void {
    if ((evento.target as HTMLElement).closest('.dd-wrap')) return;
    if (this.campanaAbierta()) this.clinica.marcarTodasLeidas();
    this.campanaAbierta.set(false);
    this.menuUsuarioAbierto.set(false);
  }

  private tituloActual(): string {
    let r = this.ruta.snapshot;
    while (r.firstChild) r = r.firstChild;
    return (r.data['titulo'] as string) ?? '';
  }
}
