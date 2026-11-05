import { ChangeDetectionStrategy, Component, ElementRef, afterNextRender, computed, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { BotonDirective } from '../../shared/componentes/boton.directive';
import { IconoComponent } from '../../shared/componentes/icono.component';
import { LogoComponent } from '../../shared/componentes/logo.component';

/** Clave de localStorage para "Recordar mi correo". Solo guarda el correo, nunca la contraseña. */
const CLAVE_CORREO = 'flowtwin.correo';

/** P1 · Iniciar sesión (F-01). */
@Component({
  selector: 'app-login',
  imports: [FormsModule, LogoComponent, IconoComponent, BotonDirective],
  templateUrl: './login.page.html',
  styleUrl: './login.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoginPage {
  private auth = inject(AuthService);
  private router = inject(Router);

  private readonly correoGuardado = leerCorreo();
  protected correo = signal(this.correoGuardado);
  protected clave = signal('');
  protected recordar = signal(this.correoGuardado !== '');
  protected verClave = signal(false);
  protected mayusculas = signal(false);
  protected error = signal('');
  protected enviando = signal(false);
  protected mostrarAyuda = signal(false);

  protected completo = computed(() => this.correo().trim() !== '' && this.clave() !== '');

  private campoCorreo = viewChild.required<ElementRef<HTMLInputElement>>('campoCorreo');
  private campoClave = viewChild.required<ElementRef<HTMLInputElement>>('campoClave');

  constructor() {
    // Foco en el primer campo vacío: si recuerda el correo, va directo a la contraseña.
    afterNextRender(() => (this.correoGuardado ? this.campoClave() : this.campoCorreo()).nativeElement.focus());
  }

  protected escribir(campo: 'correo' | 'clave', valor: string): void {
    (campo === 'correo' ? this.correo : this.clave).set(valor);
    this.error.set('');   // el mensaje de error se va en cuanto la persona corrige
  }

  /** Avisa si el Bloq Mayús está activo mientras escribe la contraseña. */
  protected revisarMayusculas(e: KeyboardEvent): void {
    this.mayusculas.set(e.getModifierState?.('CapsLock') ?? false);
  }

  protected async ingresar(): Promise<void> {
    if (this.enviando() || !this.completo()) return;
    this.enviando.set(true);
    this.error.set('');
    const correo = this.correo().trim();
    const r = await this.auth.iniciarSesion(correo, this.clave());
    this.enviando.set(false);
    if (r.ok) {
      guardarCorreo(this.recordar() ? correo : '');
      await this.router.navigateByUrl(r.destino);
    } else {
      this.error.set(r.error);
      this.clave.set('');
      this.campoClave().nativeElement.focus();
    }
  }
}

// localStorage puede fallar (modo privado, almacenamiento bloqueado): el login funciona igual.
function leerCorreo(): string {
  try { return localStorage.getItem(CLAVE_CORREO) ?? ''; } catch { return ''; }
}
function guardarCorreo(correo: string): void {
  try {
    if (correo) localStorage.setItem(CLAVE_CORREO, correo);
    else localStorage.removeItem(CLAVE_CORREO);
  } catch { /* sin almacenamiento: no se recuerda */ }
}
