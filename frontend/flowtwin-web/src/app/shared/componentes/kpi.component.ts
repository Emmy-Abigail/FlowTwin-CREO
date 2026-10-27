import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * Indicador grande con su comparación debajo:
 *   <app-kpi etiqueta="Espera promedio hoy" [valor]="23" sufijo=" min">▼ 2 min vs. ayer</app-kpi>
 */
@Component({
  selector: 'app-kpi',
  template: `
    <span class="k">{{ etiqueta() }}</span>
    <span class="v num">{{ valor() ?? '—' }}@if (sufijo()) {<small>{{ sufijo() }}</small>}</span>
    <span class="c"><ng-content /></span>
  `,
  styles: `
    :host { padding: 16px 18px; display: flex; flex-direction: column; gap: 4px; }
    .k { color: var(--gris); font-weight: 700; font-size: 12.5px; }
    .v { font-family: var(--f-disp); font-weight: 900; font-size: 30px; line-height: 1.1; }
    .v small { font-size: 15px; color: var(--gris); font-weight: 700; }
    .c { color: var(--gris-2); font-size: 12px; display: flex; align-items: center; gap: 6px; }
  `,
  host: { class: 'card' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KpiComponent {
  readonly etiqueta = input.required<string>();
  readonly valor = input<string | number | null>(null);
  readonly sufijo = input('');
}
