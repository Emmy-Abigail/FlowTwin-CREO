import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient, withFetch } from '@angular/common/http';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // withComponentInputBinding: los parámetros de la URL (como :id) llegan a la página como input().
    provideRouter(routes, withComponentInputBinding()),
    // HttpClient para cargar la geometría del piso (y, más adelante, la API del backend).
    provideHttpClient(withFetch()),
  ]
};
