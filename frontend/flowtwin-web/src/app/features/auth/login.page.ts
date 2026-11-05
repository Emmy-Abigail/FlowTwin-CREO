import { ChangeDetectionStrategy, Component, ElementRef, afterNextRender, computed, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { IconoComponent } from '../../shared/componentes/icono.component';
import { errorClave, errorCorreo } from './validacion-login';

/** Clave de localStorage para "Recordar mi correo". Solo guarda el correo, nunca la contraseña. */
const CLAVE_CORREO = 'flowtwin.correo';

/**
 * P1 · Iniciar sesión (F-01). Validación por campo: el mensaje aparece debajo del campo al salir de él
 * o al intentar enviar, nunca mientras se escribe por primera vez. El botón siempre está activo: al
 * presionarlo dice qué falta, en vez de deshabilitarse sin explicar por qué.
 */
@Component({
  selector: 'app-login',
  imports: [FormsModule, IconoComponent],
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
  protected enviando = signal(false);
  protected mostrarAyuda = signal(false);
  /** Sacude el formulario un instante cuando el servidor rechaza el ingreso (se apaga si se pide menos movimiento). */
  protected sacudir = signal(false);

  /**
   * «Premia rápido, corrige tarde»: cada campo se revisa al salir de él o al enviar, y mientras la
   * persona escribe en él su error se oculta (ya está corrigiendo). Es un estado POR CAMPO: escribir
   * en el correo no oculta el error de la contraseña.
   */
  private revisarCorreo = signal(false);
  private revisarClave = signal(false);

  /** Errores que devolvió el servidor: generales (credenciales, bloqueo, conexión) y por campo. */
  protected errorServidor = signal('');
  private errorServidorCampo = signal<Partial<Record<'correo' | 'clave', string>>>({});

  protected mensajeCorreo = computed(() =>
    this.errorServidorCampo().correo ?? (this.revisarCorreo() ? errorCorreo(this.correo()) : null));
  protected mensajeClave = computed(() =>
    this.errorServidorCampo().clave ?? (this.revisarClave() ? errorClave(this.clave()) : null));

  private campoCorreo = viewChild.required<ElementRef<HTMLInputElement>>('campoCorreo');
  private campoClave = viewChild.required<ElementRef<HTMLInputElement>>('campoClave');

  constructor() {
    // Foco en el primer campo vacío: si recuerda el correo, va directo a la contraseña.
    afterNextRender(() => (this.correoGuardado ? this.campoClave() : this.campoCorreo()).nativeElement.focus());
  }

  protected escribir(campo: 'correo' | 'clave', valor: string): void {
    (campo === 'correo' ? this.correo : this.clave).set(valor);
    // Está corrigiendo: el error de ESTE campo se oculta hasta que salga de él o vuelva a enviar
    (campo === 'correo' ? this.revisarCorreo : this.revisarClave).set(false);
    // El error del servidor se va en cuanto la persona corrige algo
    this.errorServidor.set('');
    this.errorServidorCampo.set({});
  }

  protected salir(campo: 'correo' | 'clave'): void {
    (campo === 'correo' ? this.revisarCorreo : this.revisarClave).set(true);
    if (campo === 'clave') this.mayusculas.set(false);
  }

  /** Avisa si el Bloq Mayús está activo mientras escribe la contraseña. */
  protected revisarMayusculas(e: KeyboardEvent): void {
    this.mayusculas.set(e.getModifierState?.('CapsLock') ?? false);
  }

  protected async ingresar(): Promise<void> {
    if (this.enviando()) return;
    this.revisarCorreo.set(true);
    this.revisarClave.set(true);
    // Lleva el foco al primer campo con problema, así el lector de pantalla lee su mensaje
    if (errorCorreo(this.correo())) { this.campoCorreo().nativeElement.focus(); return; }
    if (errorClave(this.clave())) { this.campoClave().nativeElement.focus(); return; }

    this.enviando.set(true);
    const correo = this.correo().trim();
    const r = await this.auth.iniciarSesion(correo, this.clave());
    this.enviando.set(false);
    if (r.ok) {
      guardarCorreo(this.recordar() ? correo : '');
      await this.router.navigateByUrl(r.destino);
      return;
    }
    this.errorServidor.set(r.error);
    this.errorServidorCampo.set(r.campos);
    this.sacudir.set(true);
    // La contraseña se vacía por seguridad; no se le dice «escribe tu contraseña» por algo que no hizo mal
    this.clave.set('');
    this.revisarClave.set(false);
    this.campoClave().nativeElement.focus();
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
