import { ApplicationConfig, inject, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { routes } from './app.routes';
import { authInterceptor } from './core/auth/auth.interceptor';
import { AuthService } from './core/auth/auth.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // withComponentInputBinding: los parámetros de la URL (como :id) llegan a la página como input().
    provideRouter(routes, withComponentInputBinding()),
    // HttpClient con el interceptor que agrega el token y renueva la sesión.
    provideHttpClient(withFetch(), withInterceptors([authInterceptor])),
    // Antes de la primera ruta: si la cookie de refresco sigue vigente, la sesión vuelve sola.
    provideAppInitializer(() => inject(AuthService).restaurarSesion()),
  ]
};
