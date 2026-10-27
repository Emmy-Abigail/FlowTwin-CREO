import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ClinicaStore } from '../datos/clinica.store';
import { Actor, Rol, Usuario } from '../datos/modelos';
import { ToastService } from '../ui/toast.service';
import { MIN } from '../utils/tiempo';
import { INICIO_POR_ROL } from './permisos';
import { USUARIOS_DEMO } from './usuarios-demo';

const CLAVE_SESION = 'flowtwin.sesion';
const MAX_INTENTOS = 5;
const BLOQUEO_MS = 5 * MIN;
const INACTIVIDAD_MS = 15 * MIN;

export type ResultadoLogin = { ok: true; destino: string } | { ok: false; error: string };

/**
 * Login SIMULADO: valida contra los usuarios demo en memoria. En el sistema real
 * este servicio llamará a POST /api/auth/login y guardará el token que devuelva;
 * el resto de la app (guards, menú, pantallas) solo usa usuario() y rol().
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private router = inject(Router);
  private clinica = inject(ClinicaStore);
  private toast = inject(ToastService);

  readonly usuarios = signal<Usuario[]>(USUARIOS_DEMO);
  private correo = signal<string | null>(leerSesion());
  private intentos = new Map<string, { fallos: number; hasta: number }>();
  private temporizadorInactividad: ReturnType<typeof setTimeout> | null = null;

  readonly usuario = computed(() => this.usuarios().find(u => u.correo === this.correo()) ?? null);
  readonly rol = computed<Rol | null>(() => this.usuario()?.rol ?? null);
  readonly soloLectura = computed(() => this.rol() === 'Gerencia');

  /** Para firmar las acciones en la auditoría. */
  readonly actor = computed<Actor>(() => {
    const u = this.usuario();
    return u ? { nombre: u.nombre, rol: u.rol } : { nombre: 'Sistema', rol: '—' };
  });

  iniciarSesion(correoIngresado: string, clave: string): ResultadoLogin {
    const correo = correoIngresado.trim().toLowerCase();
    const intento = this.intentos.get(correo) ?? { fallos: 0, hasta: 0 };
    if (intento.hasta > Date.now()) {
      return { ok: false, error: 'Cuenta bloqueada 5 min por demasiados intentos. Pide ayuda al Administrador.' };
    }

    const u = this.usuarios().find(x => x.correo === correo);
    if (!u || u.clave !== clave) {
      intento.fallos++;
      if (intento.fallos >= MAX_INTENTOS) intento.hasta = Date.now() + BLOQUEO_MS;
      this.intentos.set(correo, intento);
      return {
        ok: false,
        error: intento.hasta > Date.now()
          ? 'Cuenta bloqueada 5 min por demasiados intentos.'
          : `Correo o contraseña incorrectos. Intento ${intento.fallos} de ${MAX_INTENTOS}.`,
      };
    }
    if (u.estado === 'Desactivado') {
      return { ok: false, error: 'Esta cuenta está desactivada. Pide ayuda al Administrador.' };
    }

    this.intentos.delete(correo);
    this.actualizarUsuario(u.id, { ultimoIngreso: this.clinica.ahora() });
    this.correo.set(correo);
    guardarSesion(correo);
    this.clinica.auditar('Sesión', 'Inició sesión', '—', 'Navegador web', this.actor());
    this.registrarActividad();
    return { ok: true, destino: u.estado === 'Primer ingreso' ? '/cambiar-contrasena' : this.rutaInicio() };
  }

  cerrarSesion(motivo?: string): void {
    if (this.usuario()) this.clinica.auditar('Sesión', 'Cerró sesión', '—', motivo ?? '', this.actor());
    this.correo.set(null);
    guardarSesion(null);
    if (this.temporizadorInactividad) clearTimeout(this.temporizadorInactividad);
    if (motivo) this.toast.mostrar(motivo, 'warn');
    this.router.navigateByUrl('/login');
  }

  /** Devuelve un mensaje de error, o null si la contraseña se cambió. */
  cambiarClave(actual: string, nueva: string, confirmacion: string): string | null {
    const u = this.usuario();
    if (!u) return 'No hay una sesión activa.';
    if (actual !== u.clave) return 'La contraseña actual no es correcta.';
    if (!cumpleRequisitos(nueva).every(r => r.ok)) return 'La nueva contraseña no cumple los requisitos.';
    if (nueva !== confirmacion) return 'Las contraseñas no coinciden.';
    const primerIngreso = u.estado === 'Primer ingreso';
    this.actualizarUsuario(u.id, { clave: nueva, estado: 'Activo' });
    this.clinica.auditar('Sesión', 'Cambió su contraseña', '—', primerIngreso ? 'Primer ingreso' : '', this.actor());
    return null;
  }

  rutaInicio(): string {
    const rol = this.rol();
    return rol ? INICIO_POR_ROL[rol] : '/login';
  }

  puede(roles: readonly Rol[]): boolean {
    const rol = this.rol();
    return rol != null && roles.includes(rol);
  }

  /** La sesión se cierra tras 15 min sin actividad (flujo F1). */
  registrarActividad(): void {
    if (this.temporizadorInactividad) clearTimeout(this.temporizadorInactividad);
    if (!this.usuario()) return;
    this.temporizadorInactividad = setTimeout(
      () => this.cerrarSesion('Sesión cerrada por inactividad'), INACTIVIDAD_MS);
  }

  private actualizarUsuario(id: string, cambios: Partial<Usuario>): void {
    this.usuarios.update(lista => lista.map(u => (u.id === id ? { ...u, ...cambios } : u)));
  }
}

/** Requisitos visibles de la nueva contraseña (P2). */
export function cumpleRequisitos(clave: string): { ok: boolean; texto: string }[] {
  return [
    { ok: clave.length >= 8, texto: 'Al menos 8 caracteres' },
    { ok: /[A-Z]/.test(clave), texto: 'Una letra mayúscula' },
    { ok: /\d/.test(clave), texto: 'Un número' },
  ];
}

// La sesión se recuerda en sessionStorage para que recargar la página no saque al usuario.
// Puede fallar en modo privado: en ese caso simplemente no se recuerda.
function leerSesion(): string | null {
  try { return sessionStorage.getItem(CLAVE_SESION); } catch { return null; }
}

function guardarSesion(correo: string | null): void {
  try {
    if (correo) sessionStorage.setItem(CLAVE_SESION, correo);
    else sessionStorage.removeItem(CLAVE_SESION);
  } catch { /* sin almacenamiento disponible */ }
}
