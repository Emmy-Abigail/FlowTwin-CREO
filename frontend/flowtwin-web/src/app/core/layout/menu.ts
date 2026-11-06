import { NombreIcono } from '../../shared/componentes/icono.component';
import { Rol } from '../datos/modelos';

export interface ItemMenu {
  ruta: string;
  icono: NombreIcono;
  texto: string;
  /**
   * false mientras la pantalla no existe. El menú solo muestra lo construido: un enlace que lleva a
   * «llega en la próxima fase» es un callejón sin salida (hallazgo de la auditoría UX del 05/11).
   * Al terminar una pantalla, se cambia a true y aparece sola.
   */
  construida: boolean;
}

/** Menú lateral de cada rol. El primero construido es su pantalla de inicio. */
const MENU_COMPLETO: Record<Rol, ItemMenu[]> = {
  Operador: [
    { ruta: '/admision', icono: 'scan', texto: 'Admisión', construida: true },
    { ruta: '/gemelo', icono: 'cube', texto: 'Gemelo en vivo', construida: true },
    { ruta: '/historial', icono: 'chart', texto: 'Historial y reportes', construida: true },
    { ruta: '/seguimiento', icono: 'pulse', texto: 'Seguimiento', construida: false },
  ],
  Gerencia: [
    { ruta: '/gemelo', icono: 'cube', texto: 'Gemelo en vivo', construida: true },
    { ruta: '/historial', icono: 'chart', texto: 'Historial y reportes', construida: true },
  ],
  Administrador: [
    { ruta: '/gemelo', icono: 'cube', texto: 'Gemelo en vivo', construida: true },
    { ruta: '/historial', icono: 'chart', texto: 'Historial y reportes', construida: true },
    { ruta: '/construccion', icono: 'ruler', texto: 'Construcción del gemelo', construida: false },
    { ruta: '/dispositivos', icono: 'cpu', texto: 'Dispositivos', construida: false },
    { ruta: '/usuarios', icono: 'users', texto: 'Usuarios', construida: false },
    { ruta: '/auditoria', icono: 'list', texto: 'Auditoría', construida: false },
  ],
};

export function menuDe(rol: Rol): ItemMenu[] {
  return MENU_COMPLETO[rol].filter(i => i.construida);
}
