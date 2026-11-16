/**
 * P10 · Usuarios, sin Angular: tipos de la API, textos y filtros. Una clínica tiene decenas de usuarios,
 * no miles: la lista llega completa y se busca y filtra en la pantalla (si algún día pasa de unos cientos,
 * se pagina en el servidor sin cambiar esto).
 */
import { cuando } from '../dispositivos/modelo-dispositivos';

export type RolCodigo = 'ADMIN' | 'OPERADOR' | 'GERENCIA';

export interface UsuarioAdmin {
  id: number; nombres: string; apellidos: string; correo: string; rol: RolCodigo; pisos: number[];
  estadoRegistro: boolean; debeCambiarContrasena: boolean; ultimoIngresoEn: string | null;
}

/** La contraseña temporal solo llega al crear o restablecer, y no se vuelve a mostrar. */
export interface UsuarioConTemporal { usuario: UsuarioAdmin; contrasenaTemporal: string }

export interface DatosUsuario { nombres: string; apellidos: string; correo: string; rol: RolCodigo; pisos: number[] }

/** ADR-29: Administrador y Gerencia ven todos los pisos; solo el Operador tiene pisos asignados. */
export const ROLES: { codigo: RolCodigo; texto: string; tienePisos: boolean }[] = [
  { codigo: 'ADMIN', texto: 'Administrador', tienePisos: false },
  { codigo: 'OPERADOR', texto: 'Operador', tienePisos: true },
  { codigo: 'GERENCIA', texto: 'Gerencia', tienePisos: false },
];

export const textoRol = (codigo: string) => ROLES.find(r => r.codigo === codigo)?.texto ?? codigo;
export const rolTienePisos = (codigo: string) => ROLES.find(r => r.codigo === codigo)?.tienePisos ?? false;

export const nombreCompleto = (u: Pick<UsuarioAdmin, 'nombres' | 'apellidos'>) => `${u.nombres} ${u.apellidos}`.trim();

/** «Todos» (Administrador y Gerencia) o los nombres de sus pisos; «—» si no tiene ninguno. */
export function textoPisos(u: UsuarioAdmin, nombrePiso: (id: number) => string): string {
  if (!rolTienePisos(u.rol)) return 'Todos';
  return u.pisos.length === 0 ? '—' : u.pisos.map(nombrePiso).join(', ');
}

/** Lo que más le importa al Administrador: quién no ha entrado todavía o ya no tiene acceso. */
export function estadoUsuario(u: UsuarioAdmin): { texto: string; tono: 'ok' | 'aviso' | 'off' } {
  if (!u.estadoRegistro) return { texto: 'Inactivo', tono: 'off' };
  if (u.debeCambiarContrasena) return { texto: 'Debe cambiar su contraseña', tono: 'aviso' };
  return { texto: 'Activo', tono: 'ok' };
}

export function textoUltimoIngreso(u: UsuarioAdmin, ahora: number): string {
  return u.ultimoIngresoEn ? cuando(Date.parse(u.ultimoIngresoEn), ahora) : 'Nunca';
}

const simple = (s: string) => s.normalize('NFD').replace(/\p{Diacritic}/gu, '').toLowerCase();

/** Busca por nombre, apellido o correo (sin importar mayúsculas ni tildes) y por rol. */
export function filtrarUsuarios(lista: UsuarioAdmin[], buscar: string, rol: RolCodigo | ''): UsuarioAdmin[] {
  const q = simple(buscar.trim());
  return lista.filter(u =>
    (!rol || u.rol === rol) && (!q || simple(`${nombreCompleto(u)} ${u.correo}`).includes(q)));
}

/** El formulario está completo y los pisos cumplen con el rol (RN-24): el Operador necesita al menos uno. */
export function formularioValido(d: DatosUsuario): boolean {
  return !!d.nombres.trim() && !!d.apellidos.trim() && /^\S+@\S+\.\S+$/.test(d.correo.trim())
    && (!rolTienePisos(d.rol) || d.pisos.length > 0);
}

/** Los pisos solo viajan si el rol los usa: así no se guardan pisos de un rol que ve todos. */
export const datosParaEnviar = (d: DatosUsuario): DatosUsuario => ({ ...d, pisos: rolTienePisos(d.rol) ? d.pisos : [] });
