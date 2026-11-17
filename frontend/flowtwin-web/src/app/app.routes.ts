import { Routes } from '@angular/router';
import { autenticadoGuard, inicioGuard, invitadoGuard, primerIngresoGuard, rolGuard, soloPrimerIngresoGuard } from './core/auth/guards';
import { PERMISOS } from './core/auth/permisos';

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
        title: 'Admisión · CREO FlowTwin',
        loadComponent: () => import('./features/admision/admision.page').then(m => m.AdmisionPage),
      },
      {
        path: 'gemelo',
        canActivate: [rolGuard(PERMISOS.gemelo)],
        title: 'Gemelo en vivo · CREO FlowTwin',
        loadComponent: () => import('./features/gemelo/gemelo.page').then(m => m.GemeloPage),
      },
      {
        path: 'historial',
        canActivate: [rolGuard(PERMISOS.historial)],
        title: 'Historial y reportes · CREO FlowTwin',
        loadComponent: () => import('./features/historial/historial.page').then(m => m.HistorialPage),
      },
      {
        path: 'atencion/:id',
        canActivate: [rolGuard(PERMISOS.atencion)],
        title: 'Detalle de atención · CREO FlowTwin',
        loadComponent: () => import('./features/atencion/atencion.page').then(m => m.AtencionPage),
      },
      {
        path: 'dispositivos',
        canActivate: [rolGuard(PERMISOS.dispositivos)],
        title: 'Dispositivos · CREO FlowTwin',
        loadComponent: () => import('./features/dispositivos/dispositivos.page').then(m => m.DispositivosPage),
      },
      {
        path: 'usuarios',
        canActivate: [rolGuard(PERMISOS.usuarios)],
        title: 'Usuarios · CREO FlowTwin',
        loadComponent: () => import('./features/usuarios/usuarios.page').then(m => m.UsuariosPage),
      },
      {
        path: 'auditoria',
        canActivate: [rolGuard(PERMISOS.auditoria)],
        title: 'Auditoría · CREO FlowTwin',
        loadComponent: () => import('./features/auditoria/auditoria.page').then(m => m.AuditoriaPage),
      },
      {
        path: 'configuracion',
        canActivate: [rolGuard(PERMISOS.configuracion)],
        title: 'Configuración · CREO FlowTwin',
        loadComponent: () => import('./features/configuracion/configuracion.page').then(m => m.ConfiguracionPage),
      },
      {
        path: 'cuenta',
        title: 'Mi cuenta · CREO FlowTwin',
        loadComponent: () => import('./features/cuenta/cuenta.page').then(m => m.CuentaPage),
      },
      {
        // Solo para el primer ingreso; el resto de veces la contraseña se cambia en «Mi cuenta»
        path: 'cambiar-contrasena',
        canActivate: [soloPrimerIngresoGuard],
        title: 'Crea tu contraseña · CREO FlowTwin',
        loadComponent: () => import('./features/auth/cambiar-contrasena.page').then(m => m.CambiarContrasenaPage),
      },
      {
        path: 'sin-permiso',
        title: 'Sin permiso · CREO FlowTwin',
        loadComponent: () => import('./shared/paginas/sin-permiso.page').then(m => m.SinPermisoPage),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
