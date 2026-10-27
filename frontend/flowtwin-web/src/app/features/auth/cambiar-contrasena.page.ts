import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService, cumpleRequisitos } from '../../core/auth/auth.service';
import { ToastService } from '../../core/ui/toast.service';
import { BotonDirective } from '../../shared/componentes/boton.directive';
import { IconoComponent } from '../../shared/componentes/icono.component';

/** P2 · Cambiar contraseña. Obligatoria en el primer ingreso; también se abre desde el menú del usuario. */
@Component({
  selector: 'app-cambiar-contrasena',
  imports: [FormsModule, RouterLink, BotonDirective, IconoComponent],
  template: `
    <div class="page-head">
      <div>
        <h1>Cambiar contraseña</h1>
        <p>{{ primerIngreso() ? 'Es tu primer ingreso. Crea una contraseña propia para continuar.' : 'Actualiza tu contraseña de acceso.' }}</p>
      </div>
    </div>
    <form class="card pad formulario" (ngSubmit)="guardar()">
      <div class="field">
        <label for="cp-a">{{ primerIngreso() ? 'Contraseña temporal' : 'Contraseña actual' }}</label>
        <input class="inp" id="cp-a" name="actual" type="password" required [ngModel]="actual()" (ngModelChange)="actual.set($event)">
      </div>
      <div class="field">
        <label for="cp-n">Nueva contraseña</label>
        <input class="inp" id="cp-n" name="nueva" type="password" required [ngModel]="nueva()" (ngModelChange)="nueva.set($event)">
      </div>
      <div class="field">
        <label for="cp-c">Confirmar nueva contraseña</label>
        <input class="inp" id="cp-c" name="confirmacion" type="password" required [ngModel]="confirmacion()" (ngModelChange)="confirmacion.set($event)">
      </div>
      <div class="checks">
        @for (r of requisitos(); track r.texto) {
          <span [class.ok]="r.ok">
            @if (r.ok) { <app-icono nombre="check" [tamano]="14" /> } @else { · } {{ r.texto }}
          </span>
        }
      </div>
      @if (error()) { <div class="err">{{ error() }}</div> }
      <div class="toolbar">
        <button appBoton="primario" type="submit">Guardar contraseña</button>
        @if (!primerIngreso()) { <a appBoton="borde" [routerLink]="auth.rutaInicio()">Cancelar</a> }
      </div>
    </form>
  `,
  styles: `
    .formulario { max-width: 460px; display: flex; flex-direction: column; gap: 14px; }
    .checks { display: flex; flex-direction: column; gap: 4px; font-size: 12.5px; }
    .checks span { display: flex; gap: 6px; align-items: center; color: var(--gris); }
    .checks span.ok { color: #1E6E4A; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CambiarContrasenaPage {
  protected auth = inject(AuthService);
  private router = inject(Router);
  private toast = inject(ToastService);

  protected actual = signal('');
  protected nueva = signal('');
  protected confirmacion = signal('');
  protected error = signal('');
  protected requisitos = computed(() => cumpleRequisitos(this.nueva()));
  protected primerIngreso = computed(() => this.auth.usuario()?.estado === 'Primer ingreso');

  protected guardar(): void {
    const error = this.auth.cambiarClave(this.actual(), this.nueva(), this.confirmacion());
    if (error) {
      this.error.set(error);
      return;
    }
    this.toast.mostrar('Contraseña actualizada');
    this.router.navigateByUrl(this.auth.rutaInicio());
  }
}
