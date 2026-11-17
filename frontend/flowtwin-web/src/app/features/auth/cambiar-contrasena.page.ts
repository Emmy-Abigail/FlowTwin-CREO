import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { ToastService } from '../../core/ui/toast.service';
import { FormularioContrasenaComponent } from './formulario-contrasena.component';

/**
 * P2 · Primer ingreso: crear una contraseña propia antes de usar el sistema (F-02).
 * Fuera del primer ingreso, la contraseña se cambia desde «Mi cuenta» (/cuenta).
 */
@Component({
  selector: 'app-cambiar-contrasena',
  imports: [FormularioContrasenaComponent],
  template: `
    <div class="page-head">
      <div>
        <h1>Crea tu contraseña</h1>
        <p>Es tu primer ingreso. Cambia la contraseña temporal por una que solo tú conozcas.</p>
      </div>
    </div>
    <section class="card pad tarjeta">
      <app-formulario-contrasena [primerIngreso]="true" (guardada)="listo()" />
    </section>
  `,
  styles: `.tarjeta { max-width: 520px; }`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CambiarContrasenaPage {
  private auth = inject(AuthService);
  private router = inject(Router);
  private toast = inject(ToastService);

  protected listo(): void {
    this.toast.mostrar('Contraseña creada. ¡Bienvenido!');
    this.router.navigateByUrl(this.auth.rutaInicio());
  }
}
