import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * Estado vacío: cuando no hay datos, la pantalla lo dice y explica por qué, en vez de mostrar ceros o
 * datos inventados (principio «solo datos reales»). Lleva el motivo de la cruz de CREO+ en gris.
 * Lo que va entre las etiquetas (acciones, pasos) se muestra debajo del texto.
 */
@Component({
  selector: 'app-estado-vacio',
  template: `
    <svg width="44" height="44" viewBox="0 0 31 31" aria-hidden="true">
      <rect x="12" y="2" width="7" height="27" rx="3.5" fill="#DDD7D3" />
      <rect x="2" y="12" width="27" height="7" rx="3.5" fill="#DDD7D3" />
    </svg>
    <h2>{{ titulo() }}</h2>
    <p>{{ texto() }}</p>
    <ng-content />
  `,
  styles: `
    :host {
      display: flex; flex-direction: column; align-items: center; text-align: center; gap: 12px;
      max-width: 480px; margin: clamp(32px, 10vh, 96px) auto; padding: 0 16px;
    }
    h2 { font-size: 22px; font-weight: 900; }
    p { margin: 0; color: var(--gris); font-size: 15px; line-height: 1.5; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EstadoVacioComponent {
  readonly titulo = input.required<string>();
  readonly texto = input.required<string>();
}
