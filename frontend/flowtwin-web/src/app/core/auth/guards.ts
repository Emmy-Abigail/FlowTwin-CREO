import { inject } from '@angular/core';
import { CanActivateChildFn, CanActivateFn, Router } from '@angular/router';
import { Rol } from '../datos/modelos';
import { AuthService } from './auth.service';

/** Solo deja pasar con sesión iniciada; si no, manda al login. */
export const autenticadoGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.usuario() ? true : inject(Router).parseUrl('/login');
};

/** El login solo se ve sin sesión; con sesión, manda a la pantalla de inicio del rol. */
export const invitadoGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.usuario() ? inject(Router).parseUrl(auth.rutaInicio()) : true;
};

/** En el primer ingreso, cualquier pantalla redirige a Cambiar contraseña (P2). */
export const primerIngresoGuard: CanActivateChildFn = (_ruta, estado) => {
  const auth = inject(AuthService);
  const pendiente = auth.usuario()?.estado === 'Primer ingreso';
  return pendiente && !estado.url.startsWith('/cambiar-contrasena')
    ? inject(Router).parseUrl('/cambiar-contrasena')
    : true;
};

/** Solo deja pasar a los roles indicados; el resto ve "No tienes permiso". */
export function rolGuard(roles: readonly Rol[]): CanActivateFn {
  return () => (inject(AuthService).puede(roles) ? true : inject(Router).parseUrl('/sin-permiso'));
}

/** La ruta vacía lleva a la pantalla de inicio del rol. */
export const inicioGuard: CanActivateFn = () => inject(Router).parseUrl(inject(AuthService).rutaInicio());
