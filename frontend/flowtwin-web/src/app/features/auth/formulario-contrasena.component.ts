import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/auth/auth.service';
import { IconoComponent } from '../../shared/componentes/icono.component';
import { CampoClave, errorActual, errorConfirmacion, errorNueva, requisitosClave } from './validacion-contrasena';

/**
 * Formulario de cambio de contraseña (F-02). Lo usan el primer ingreso y «Mi cuenta».
 *
 * Mismo patrón que el login («premiar pronto, castigar tarde»):
 * - la lista de requisitos se marca en verde MIENTRAS se escribe (es ayuda, no un error);
 * - el error de un campo aparece al salir de él o al enviar, y se oculta mientras se corrige;
 * - los errores del servidor (contraseña actual incorrecta, repetida) van debajo de su campo.
 */
@Component({
  selector: 'app-formulario-contrasena',
  imports: [FormsModule, IconoComponent],
  template: `
    <form class="formulario" (ngSubmit)="guardar()" novalidate>
      @for (c of campos; track c.id) {
        <div class="campo" [class.con-error]="mensaje(c.id)">
          <label [for]="'cc-' + c.id">{{ c.id === 'actual' && primerIngreso() ? 'Contraseña temporal' : c.etiqueta }}</label>
          <div class="entrada">
            <app-icono nombre="lock" [tamano]="18" />
            <input [id]="'cc-' + c.id" [name]="c.id" [type]="ver() ? 'text' : 'password'" [autocomplete]="c.autocompletar"
                   [attr.aria-invalid]="!!mensaje(c.id)" [attr.aria-describedby]="mensaje(c.id) ? 'cc-' + c.id + '-error' : null"
                   [ngModel]="valor(c.id)" (ngModelChange)="escribir(c.id, $event)" (blur)="salir(c.id)">
            @if (c.id === 'actual') {
              <button class="ver" type="button" (click)="ver.set(!ver())"
                      [attr.aria-label]="ver() ? 'Ocultar contraseñas' : 'Mostrar contraseñas'" [attr.aria-pressed]="ver()">
                <app-icono [nombre]="ver() ? 'ojoNo' : 'ojo'" [tamano]="19" />
              </button>
            }
          </div>
          @if (mensaje(c.id); as m) {
            <span [id]="'cc-' + c.id + '-error'" class="msg-error"><app-icono nombre="alert" [tamano]="15" />{{ m }}</span>
          }
          @if (c.id === 'nueva') {
            <ul class="requisitos" aria-label="Requisitos de la contraseña">
              @for (r of requisitos(); track r.texto) {
                <li [class.ok]="r.ok"><app-icono [nombre]="r.ok ? 'check' : 'punto'" [tamano]="14" />{{ r.texto }}</li>
              }
            </ul>
          }
        </div>
      }

      @if (errorGeneral(); as m) {
        <div class="aviso-err" role="alert"><app-icono nombre="alert" [tamano]="18" />{{ m }}</div>
      }

      <div class="acciones">
        <button class="guardar" type="submit" [attr.aria-busy]="guardando()">
          @if (guardando()) { Guardando… } @else { Guardar contraseña }
        </button>
        <p>Al guardarla se cerrarán tus sesiones en otros equipos.</p>
      </div>
    </form>
  `,
  styles: `
    .formulario { display: flex; flex-direction: column; gap: 16px; }
    .requisitos { list-style: none; margin: 4px 0 0; padding: 0; display: grid; grid-template-columns: 1fr 1fr; gap: 4px 14px; font-size: 13px; color: var(--gris); }
    .requisitos li { display: flex; align-items: center; gap: 6px; transition: color .15s; }
    .requisitos li.ok { color: #1E6E4A; font-weight: 600; }
    .aviso-err { display: flex; gap: 10px; padding: 12px 14px; border-radius: 12px; background: var(--durazno); color: var(--error); font-weight: 600; }
    .acciones { display: flex; align-items: center; gap: 16px; flex-wrap: wrap; margin-top: 4px; }
    .acciones p { margin: 0; font-size: 13px; color: var(--gris); }
    .guardar { height: 46px; padding: 0 20px; border: 0; border-radius: 12px; background: var(--rojo); color: #fff; font-size: 15px; font-weight: 700; }
    .guardar:hover { filter: brightness(1.08); }
    .guardar[aria-busy="true"] { opacity: .75; cursor: progress; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FormularioContrasenaComponent {
  private auth = inject(AuthService);

  /** En el primer ingreso el primer campo es la contraseña temporal que dio el Administrador. */
  readonly primerIngreso = input(false);
  /** Se emite cuando el servidor aceptó la nueva contraseña. */
  readonly guardada = output<void>();

  protected readonly campos: { id: CampoClave; etiqueta: string; autocompletar: string }[] = [
    { id: 'actual', etiqueta: 'Contraseña actual', autocompletar: 'current-password' },
    { id: 'nueva', etiqueta: 'Nueva contraseña', autocompletar: 'new-password' },
    { id: 'confirmacion', etiqueta: 'Repetir nueva contraseña', autocompletar: 'new-password' },
  ];

  private valores = { actual: signal(''), nueva: signal(''), confirmacion: signal('') };
  /** Qué campos ya se pueden juzgar (se salió de ellos o se envió). Se apaga al volver a escribir. */
  private revisar = { actual: signal(false), nueva: signal(false), confirmacion: signal(false) };
  private errorServidor = signal<Partial<Record<CampoClave, string>>>({});
  protected errorGeneral = signal<string | null>(null);
  protected guardando = signal(false);
  protected ver = signal(false);

  protected requisitos = computed(() => requisitosClave(this.valores.nueva()));

  private errorLocal(campo: CampoClave): string | null {
    const u = this.auth.usuario();
    const v = { actual: this.valores.actual(), nueva: this.valores.nueva(), confirmacion: this.valores.confirmacion() };
    switch (campo) {
      case 'actual': return errorActual(v.actual, this.primerIngreso());
      case 'nueva': return errorNueva(v.nueva, v.actual, u?.correo ?? '', u?.nombre ?? '');
      case 'confirmacion': return errorConfirmacion(v.confirmacion, v.nueva);
    }
  }

  protected valor(campo: CampoClave): string {
    return this.valores[campo]();
  }

  protected mensaje(campo: CampoClave): string | null {
    return this.errorServidor()[campo] ?? (this.revisar[campo]() ? this.errorLocal(campo) : null);
  }

  protected escribir(campo: CampoClave, valor: string): void {
    this.valores[campo].set(valor);
    this.revisar[campo].set(false);
    this.errorServidor.update(e => ({ ...e, [campo]: undefined }));
    this.errorGeneral.set(null);
  }

  protected salir(campo: CampoClave): void {
    // Solo se juzga un campo que ya tiene algo: salir de uno vacío al recorrer con Tab no es un error
    if (this.valores[campo]()) this.revisar[campo].set(true);
  }

  protected async guardar(): Promise<void> {
    if (this.guardando()) return;
    for (const c of this.campos) this.revisar[c.id].set(true);
    if (this.campos.some(c => this.errorLocal(c.id))) return;

    this.guardando.set(true);
    const r = await this.auth.cambiarClave(this.valores.actual(), this.valores.nueva(), this.valores.confirmacion());
    this.guardando.set(false);
    if (r.ok) {
      for (const c of this.campos) { this.valores[c.id].set(''); this.revisar[c.id].set(false); }
      this.guardada.emit();
      return;
    }
    this.errorServidor.set(r.campos);
    this.errorGeneral.set(r.error);
  }
}
