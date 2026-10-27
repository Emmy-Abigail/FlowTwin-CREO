import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ToastsComponent } from './shared/componentes/toasts.component';

/** Raíz de la app: solo el router y los toasts. El marco con menú está en core/layout. */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, ToastsComponent],
  template: '<router-outlet /><app-toasts />',
})
export class App {}
