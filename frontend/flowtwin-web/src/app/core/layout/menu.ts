import { NombreIcono } from '../../shared/componentes/icono.component';
import { Rol } from '../datos/modelos';

export interface ItemMenu {
  ruta: string;
  icono: NombreIcono;
  texto: string;
}

/** Menú lateral de cada rol, en el orden del prototipo. El primero es su pantalla de inicio. */
export const MENU_POR_ROL: Record<Rol, ItemMenu[]> = {
  Operador: [
    { ruta: '/admision', icono: 'scan', texto: 'Admisión' },
    { ruta: '/gemelo', icono: 'cube', texto: 'Gemelo en vivo' },
    { ruta: '/historial', icono: 'chart', texto: 'Historial y reportes' },
    { ruta: '/seguimiento', icono: 'pulse', texto: 'Seguimiento' },
  ],
  Gerencia: [
    { ruta: '/gemelo', icono: 'cube', texto: 'Gemelo en vivo' },
    { ruta: '/historial', icono: 'chart', texto: 'Historial y reportes' },
  ],
  Administrador: [
    { ruta: '/gemelo', icono: 'cube', texto: 'Gemelo en vivo' },
    { ruta: '/historial', icono: 'chart', texto: 'Historial y reportes' },
    { ruta: '/construccion', icono: 'ruler', texto: 'Construcción del gemelo' },
    { ruta: '/dispositivos', icono: 'cpu', texto: 'Dispositivos' },
    { ruta: '/usuarios', icono: 'users', texto: 'Usuarios' },
    { ruta: '/auditoria', icono: 'list', texto: 'Auditoría' },
  ],
};
