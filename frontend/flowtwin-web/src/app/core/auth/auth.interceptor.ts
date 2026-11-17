import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

/** Rutas que nunca llevan token ni reintentan: si fallan, fallan (si no, habría un bucle). */
const RUTAS_DE_SESION = /\/api\/v1\/auth\/(login|refresh|logout)$/;

/**
 * Interceptor de la API (sección Arquitectura del frontend → Interceptores):
 * 1. agrega el token de acceso a cada petición a /api;
 * 2. ante un 401, pide un token nuevo con la cookie UNA vez y repite la petición;
 * 3. ante 403 CAMBIO_CONTRASENA_REQUERIDO, lleva a la pantalla de cambio de contraseña.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith('/api/')) return next(req);

  const auth = inject(AuthService);
  const router = inject(Router);
  const conToken = (r: HttpRequest<unknown>) => {
    const token = auth.token();
    return token && !RUTAS_DE_SESION.test(r.url) ? r.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : r;
  };

  return next(conToken(req)).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401 && !RUTAS_DE_SESION.test(req.url)) {
        return from(auth.refrescar()).pipe(
          switchMap(renovado => {
            if (renovado) return next(conToken(req));
            auth.sesionExpirada();
            return throwError(() => error);
          }),
        );
      }
      if (error.status === 403 && error.error?.codigo === 'CAMBIO_CONTRASENA_REQUERIDO') {
        router.navigateByUrl('/cambiar-contrasena');
      }
      return throwError(() => error);
    }),
  );
};
