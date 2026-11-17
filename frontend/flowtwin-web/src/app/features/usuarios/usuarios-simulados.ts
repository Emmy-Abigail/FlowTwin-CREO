import { Injectable, inject, linkedSignal } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';
import { ClinicaStore } from '../../core/datos/clinica.store';
import { DIA, MIN, inicioDelDia } from '../../core/utils/tiempo';
import { DatosUsuario, UsuarioAdmin, UsuarioConTemporal, nombreCompleto, textoRol } from './modelo-usuarios';

/**
 * Usuarios en la Simulación (ADR-31): el personal ficticio de la clínica simulada (el mismo que aparece en su
 * bitácora), en memoria. Crear, editar, restablecer y desactivar funcionan con las mismas reglas que el servidor;
 * las contraseñas temporales son ficticias y no sirven para entrar. Vuelve al inicio con «Reiniciar simulación».
 */
@Injectable({ providedIn: 'root' })
export class UsuariosSimulados {
  private clinica = inject(ClinicaStore);
  private auth = inject(AuthService);

  readonly lista = linkedSignal<number, UsuarioAdmin[]>({ source: this.clinica.reinicios, computation: () => this.iniciales() });

  crear(d: DatosUsuario): UsuarioConTemporal {
    this.validar(d, null);
    const u: UsuarioAdmin = {
      id: Math.max(0, ...this.lista().map(x => x.id)) + 1, ...d, correo: d.correo.trim().toLowerCase(),
      estadoRegistro: true, debeCambiarContrasena: true, ultimoIngresoEn: null,
    };
    this.lista.update(l => [...l, u]);
    this.auditar('Creó', u, undefined, this.datosDe(u));
    return { usuario: u, contrasenaTemporal: temporalFicticia() };
  }

  editar(id: number, d: DatosUsuario): void {
    this.validar(d, id);
    const antes = this.buscar(id);
    if (antes.rol === 'ADMIN' && d.rol !== 'ADMIN' && this.adminsActivos() === 1) throw new Error('Debe quedar al menos un Administrador activo.');
    const despues = { ...antes, ...d, correo: d.correo.trim().toLowerCase() };
    this.lista.update(l => l.map(u => (u.id === id ? despues : u)));
    this.auditar('Editó', despues, this.datosDe(antes), this.datosDe(despues));
  }

  restablecer(id: number): UsuarioConTemporal {
    const u = { ...this.buscar(id), debeCambiarContrasena: true };
    this.lista.update(l => l.map(x => (x.id === id ? u : x)));
    this.auditar('Restableció la contraseña', u, undefined, undefined);
    return { usuario: u, contrasenaTemporal: temporalFicticia() };
  }

  desactivar(id: number): void {
    const u = this.buscar(id);
    if (u.rol === 'ADMIN' && this.adminsActivos() === 1) throw new Error('Debe quedar al menos un Administrador activo.');
    this.cambiarEstado(u, false);
  }

  activar(id: number): void {
    this.cambiarEstado(this.buscar(id), true);
  }

  private cambiarEstado(u: UsuarioAdmin, activo: boolean): void {
    this.lista.update(l => l.map(x => (x.id === u.id ? { ...x, estadoRegistro: activo } : x)));
    this.auditar(activo ? 'Activó' : 'Desactivó', u, { activo: !activo }, { activo });
  }

  /** Las reglas del servidor: correo único, nombre completo y pisos para el Operador. */
  private validar(d: DatosUsuario, id: number | null): void {
    const correo = d.correo.trim().toLowerCase();
    if (!d.nombres.trim() || !d.apellidos.trim() || !/^\S+@\S+\.\S+$/.test(correo)) throw new Error('Completa nombres, apellidos y un correo válido.');
    if (this.lista().some(u => u.id !== id && u.correo === correo)) throw new Error(`Ya existe un usuario con el correo ${correo}.`);
    if (d.rol === 'OPERADOR' && d.pisos.length === 0) throw new Error('Un Operador debe tener al menos un piso.');
  }

  private buscar(id: number): UsuarioAdmin {
    const u = this.lista().find(x => x.id === id);
    if (!u) throw new Error('El usuario no existe.');
    return u;
  }

  private adminsActivos = () => this.lista().filter(u => u.rol === 'ADMIN' && u.estadoRegistro).length;
  private datosDe = (u: UsuarioAdmin) => ({ nombres: u.nombres, apellidos: u.apellidos, correo: u.correo, rol: textoRol(u.rol), pisos: u.pisos.map(p => `Piso ${p}`) });

  private auditar(accion: string, u: UsuarioAdmin, antes?: Record<string, unknown>, despues?: Record<string, unknown>): void {
    this.clinica.auditar('Usuarios', accion, nombreCompleto(u), `Rol ${textoRol(u.rol)}`, this.auth.actor(), { antes, despues });
  }

  /** El personal de la clínica simulada, con sus últimos ingresos relativos a la hora simulada. */
  private iniciales(): UsuarioAdmin[] {
    const hoy = inicioDelDia(this.clinica.ahora());
    const a = (dia: number, h: number, m: number) => new Date(hoy + dia * DIA + (h * 60 + m) * MIN).toISOString();
    const u = (id: number, nombres: string, apellidos: string, correo: string, rol: UsuarioAdmin['rol'], pisos: number[],
      ultimo: string | null, extra: Partial<UsuarioAdmin> = {}): UsuarioAdmin =>
      ({ id, nombres, apellidos, correo, rol, pisos, estadoRegistro: true, debeCambiarContrasena: false, ultimoIngresoEn: ultimo, ...extra });
    return [
      u(1, 'Ana', 'Torres', 'admin@creo.pe', 'ADMIN', [], a(-1, 17, 5)),
      u(2, 'Luis', 'Rojas', 'operador@creo.pe', 'OPERADOR', [1], a(-1, 18, 2)),
      u(3, 'Juan', 'Morales', 'gerencia@creo.pe', 'GERENCIA', [], a(-1, 16, 22)),
      u(4, 'Carla', 'Díaz', 'carla.diaz@creo.pe', 'OPERADOR', [1], null, { debeCambiarContrasena: true }),
      u(5, 'Pedro', 'Salas', 'pedro.salas@creo.pe', 'OPERADOR', [1], a(-5, 17, 40), { estadoRegistro: false }),
    ];
  }
}

/** Ficticia a propósito (no sirve para entrar): se ve como una temporal real, de 12 caracteres. */
function temporalFicticia(): string {
  const letras = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789';
  return Array.from({ length: 12 }, () => letras[Math.floor(Math.random() * letras.length)]).join('');
}
