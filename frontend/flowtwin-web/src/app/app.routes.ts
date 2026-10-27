import { Routes } from '@angular/router';
import { autenticadoGuard, inicioGuard, invitadoGuard, primerIngresoGuard, rolGuard } from './core/auth/guards';
import { PERMISOS } from './core/auth/permisos';

/** Página temporal para las pantallas de la Fase B. */
const proximamente = () => import('./shared/paginas/proximamente.page').then(m => m.ProximamentePage);

/**
 * Mapa de rutas. Cada pantalla se carga "lazy" (solo cuando se visita) y está protegida
 * por rol con rolGuard(PERMISOS.x), la misma tabla que arma el menú lateral.
 */
export const routes: Routes = [
  {
    path: 'login',
    canActivate: [invitadoGuard],
    title: 'Iniciar sesión · CREO FlowTwin',
    loadComponent: () => import('./features/auth/login.page').then(m => m.LoginPage),
  },
  {
    path: '',
    canActivate: [autenticadoGuard],
    canActivateChild: [primerIngresoGuard],
    loadComponent: () => import('./core/layout/shell.component').then(m => m.ShellComponent),
    children: [
      { path: '', pathMatch: 'full', canActivate: [inicioGuard], children: [] },
      {
        path: 'admision',
        canActivate: [rolGuard(PERMISOS.admision)],
        data: { titulo: 'Admisión' },
        loadComponent: () => import('./features/admision/admision.page').then(m => m.AdmisionPage),
      },
      {
        path: 'gemelo',
        canActivate: [rolGuard(PERMISOS.gemelo)],
        data: { titulo: 'Gemelo en vivo' },
        loadComponent: () => import('./features/gemelo/gemelo.page').then(m => m.GemeloPage),
      },
      {
        path: 'historial',
        canActivate: [rolGuard(PERMISOS.historial)],
        data: { titulo: 'Historial y reportes' },
        loadComponent: () => import('./features/historial/historial.page').then(m => m.HistorialPage),
      },
      {
        path: 'atencion/:id',
        canActivate: [rolGuard(PERMISOS.atencion)],
        data: { titulo: 'Detalle de atención', descripcion: 'Línea de tiempo del recorrido del paciente (P6).' },
        loadComponent: proximamente,
      },
      {
        path: 'seguimiento',
        canActivate: [rolGuard(PERMISOS.seguimiento)],
        data: { titulo: 'Seguimiento', descripcion: 'Active o pause el monitoreo de sus zonas y revise sus nodos (P7).' },
        loadComponent: proximamente,
      },
      {
        path: 'construccion',
        canActivate: [rolGuard(PERMISOS.construccion)],
        data: { titulo: 'Construcción del gemelo', descripcion: 'Sedes, pisos, zonas y nodos sobre el plano (P8).' },
        loadComponent: proximamente,
      },
      {
        path: 'dispositivos',
        canActivate: [rolGuard(PERMISOS.dispositivos)],
        data: { titulo: 'Dispositivos', descripcion: 'Nodos ESP32 y tarjetas BLE (P9).' },
        loadComponent: proximamente,
      },
      {
        path: 'usuarios',
        canActivate: [rolGuard(PERMISOS.usuarios)],
        data: { titulo: 'Usuarios', descripcion: 'Cuentas, roles y pisos asignados (P10).' },
        loadComponent: proximamente,
      },
      {
        path: 'auditoria',
        canActivate: [rolGuard(PERMISOS.auditoria)],
        data: { titulo: 'Auditoría', descripcion: 'Bitácora de solo lectura de todas las acciones (P11).' },
        loadComponent: proximamente,
      },
      {
        path: 'cambiar-contrasena',
        data: { titulo: 'Cambiar contraseña' },
        loadComponent: () => import('./features/auth/cambiar-contrasena.page').then(m => m.CambiarContrasenaPage),
      },
      {
        path: 'sin-permiso',
        data: { titulo: 'Sin permiso' },
        loadComponent: () => import('./shared/paginas/sin-permiso.page').then(m => m.SinPermisoPage),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
