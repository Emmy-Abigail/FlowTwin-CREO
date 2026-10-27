import { Rol } from '../datos/modelos';

const TODOS: Rol[] = ['Operador', 'Gerencia', 'Administrador'];

/**
 * Qué roles pueden abrir cada pantalla. Lo usan las rutas (rolGuard) y el menú,
 * así ambos dicen siempre lo mismo. Fuente: version-final-pantallas.md §3.
 */
export const PERMISOS = {
  admision: ['Operador'],
  gemelo: TODOS,
  historial: TODOS,
  atencion: TODOS,
  seguimiento: ['Operador'],
  construccion: ['Administrador'],
  dispositivos: ['Administrador'],
  usuarios: ['Administrador'],
  auditoria: ['Administrador'],
} satisfies Record<string, Rol[]>;

export type Pantalla = keyof typeof PERMISOS;

/** Pantalla de inicio de cada rol después del login. */
export const INICIO_POR_ROL: Record<Rol, string> = {
  Operador: '/admision',
  Gerencia: '/gemelo',
  Administrador: '/gemelo',
};
