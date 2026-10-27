import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { BotonDirective } from '../componentes/boton.directive';
import { IconoComponent } from '../componentes/icono.component';

/** Se muestra cuando un rol intenta abrir una pantalla que no le corresponde. */
@Component({
  selector: 'app-sin-permiso',
  imports: [IconoComponent, BotonDirective, RouterLink],
  template: `
    <div class="card pad denegado">
      <app-icono nombre="lock" [tamano]="34" />
      <h2>No tienes permiso para ver esta sección</h2>
      <p class="hint">Tu rol ({{ auth.rol() }}) no incluye esta pantalla.</p>
      <a appBoton="oscuro" [routerLink]="auth.rutaInicio()">Ir a mi inicio</a>
    </div>
  `,
  styles: `
    .denegado { max-width: 480px; margin: 60px auto; text-align: center; display: flex; flex-direction: column; gap: 12px; align-items: center; }
    .denegado p { margin: 0; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SinPermisoPage {
  protected auth = inject(AuthService);
}
