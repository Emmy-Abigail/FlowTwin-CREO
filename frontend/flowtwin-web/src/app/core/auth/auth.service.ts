import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { API, erroresPorCampo, mensajeDe, problemaDe } from '../api/problema';
import { Actor, Rol } from '../datos/modelos';
import { ToastService } from '../ui/toast.service';
import { MIN } from '../utils/tiempo';
import { INICIO_POR_ROL } from './permisos';

/** Usuario con sesión, tal como lo devuelve la API (/auth/login, /auth/yo). */
export interface UsuarioSesion {
  id: number;
  nombre: string;
  correo: string;
  rol: Rol;
  pisos: number[];
  debeCambiarContrasena: boolean;
}

interface UsuarioApi {
  id: number; correo: string; nombres: string; apellidos: string; rol: string; pisos: number[]; debeCambiarContrasena: boolean;
}
interface SesionApi { accessToken: string; expiraEn: string; usuario: UsuarioApi; }

/**
 * Resultado del login. Si falla: «error» es el mensaje general (credenciales, bloqueo, sin conexión) y
 * «campos» los errores que el servidor atribuye a un campo concreto (correo, clave), para mostrarlos
 * debajo de ese campo.
 */
export type ResultadoLogin =
  | { ok: true; destino: string }
  | { ok: false; error: string; campos: Partial<Record<'correo' | 'clave', string>> };

/** Código de rol de la API → nombre que usan el menú y los permisos del frontend. */
const ROLES: Record<string, Rol> = { ADMIN: 'Administrador', OPERADOR: 'Operador', GERENCIA: 'Gerencia' };

/** 30 minutos sin actividad cierran la sesión (RNF11). */
const INACTIVIDAD_MS = 30 * MIN;

/**
 * Sesión contra la API real (flujos F-01, F-02 y F-03, ADR-13):
 * - el token de acceso (15 min) vive SOLO en memoria, nunca en localStorage;
 * - el token de refresco va en una cookie HttpOnly que JavaScript no puede leer;
 * - al recargar la página, restaurarSesion() pide un token nuevo con esa cookie.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private router = inject(Router);
  private toast = inject(ToastService);

  private sesion = signal<{ token: string; usuario: UsuarioSesion } | null>(null);
  private temporizadorInactividad: ReturnType<typeof setTimeout> | null = null;
  private refrescoEnCurso: Promise<boolean> | null = null;

  readonly usuario = computed(() => this.sesion()?.usuario ?? null);
  readonly token = computed(() => this.sesion()?.token ?? null);
  readonly rol = computed<Rol | null>(() => this.usuario()?.rol ?? null);
  readonly soloLectura = computed(() => this.rol() === 'Gerencia');

  /** Para firmar las acciones de las pantallas que aún son simuladas (gemelo, historial). */
  readonly actor = computed<Actor>(() => {
    const u = this.usuario();
    return u ? { nombre: u.nombre, rol: u.rol } : { nombre: 'Sistema', rol: '—' };
  });

  async iniciarSesion(correo: string, clave: string): Promise<ResultadoLogin> {
    try {
      const r = await firstValueFrom(this.http.post<SesionApi>(`${API}/auth/login`, { correo, clave }));
      this.guardar(r);
      return { ok: true, destino: this.usuario()!.debeCambiarContrasena ? '/cambiar-contrasena' : this.rutaInicio() };
    } catch (e) {
      const p = problemaDe(e);
      const campos = erroresPorCampo(p);
      return { ok: false, error: Object.keys(campos).length ? '' : mensajeDe(p), campos };
    }
  }

  /** Al abrir la app: si la cookie de refresco sigue vigente, la sesión vuelve sin pedir la contraseña. */
  async restaurarSesion(): Promise<void> {
    await this.refrescar();
  }

  /** Pide un token nuevo con la cookie. Si varias peticiones lo piden a la vez, se hace una sola vez. */
  refrescar(): Promise<boolean> {
    this.refrescoEnCurso ??= firstValueFrom(this.http.post<SesionApi>(`${API}/auth/refresh`, {}))
      .then(r => { this.guardar(r); return true; })
      .catch(() => { this.limpiar(); return false; })
      .finally(() => (this.refrescoEnCurso = null));
    return this.refrescoEnCurso;
  }

  async cerrarSesion(motivo?: string): Promise<void> {
    try { await firstValueFrom(this.http.post(`${API}/auth/logout`, {})); } catch { /* la sesión se limpia igual */ }
    this.limpiar();
    if (motivo) this.toast.mostrar(motivo, 'warn');
    await this.router.navigateByUrl('/login');
  }

  /** El servidor rechazó el token y no se pudo renovar: vuelve al login. */
  sesionExpirada(): void {
    if (!this.sesion()) return;
    this.limpiar();
    this.toast.mostrar('Tu sesión expiró. Vuelve a iniciar sesión.', 'warn');
    this.router.navigateByUrl('/login');
  }

  /** Devuelve un mensaje de error, o null si la contraseña se cambió. */
  async cambiarClave(actual: string, nueva: string, confirmacion: string): Promise<string | null> {
    try {
      const r = await firstValueFrom(this.http.post<SesionApi>(`${API}/auth/cambiar-contrasena`, { actual, nueva, confirmacion }));
      this.guardar(r);
      return null;
    } catch (e) {
      return mensajeDe(problemaDe(e));
    }
  }

  rutaInicio(): string {
    const rol = this.rol();
    return rol ? INICIO_POR_ROL[rol] : '/login';
  }

  puede(roles: readonly Rol[]): boolean {
    const rol = this.rol();
    return rol != null && roles.includes(rol);
  }

  /** Cada clic o tecla reinicia la cuenta de 30 minutos de inactividad. */
  registrarActividad(): void {
    if (this.temporizadorInactividad) clearTimeout(this.temporizadorInactividad);
    if (!this.sesion()) return;
    this.temporizadorInactividad = setTimeout(() => this.cerrarSesion('Sesión cerrada por inactividad'), INACTIVIDAD_MS);
  }

  private guardar(r: SesionApi): void {
    const u = r.usuario;
    this.sesion.set({
      token: r.accessToken,
      usuario: {
        id: u.id, correo: u.correo, nombre: `${u.nombres} ${u.apellidos}`.trim(),
        rol: ROLES[u.rol], pisos: u.pisos, debeCambiarContrasena: u.debeCambiarContrasena,
      },
    });
    this.registrarActividad();
  }

  private limpiar(): void {
    this.sesion.set(null);
    if (this.temporizadorInactividad) clearTimeout(this.temporizadorInactividad);
  }
}

/** Requisitos visibles de la nueva contraseña (sección Seguridad → Contraseñas). La API los vuelve a validar. */
export function cumpleRequisitos(clave: string): { ok: boolean; texto: string }[] {
  return [
    { ok: clave.length >= 8, texto: 'Al menos 8 caracteres' },
    { ok: /[A-ZÁÉÍÓÚÑ]/.test(clave), texto: 'Una letra mayúscula' },
    { ok: /[a-záéíóúñ]/.test(clave), texto: 'Una letra minúscula' },
    { ok: /\d/.test(clave), texto: 'Un número' },
    { ok: /[^A-Za-zÁÉÍÓÚÑáéíóúñ0-9]/.test(clave), texto: 'Un símbolo (# $ % & * ! ?)' },
  ];
}
