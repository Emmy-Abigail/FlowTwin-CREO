import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { PisoActualService } from '../../core/datos/piso-actual.service';

/**
 * Sede y piso que se están mirando (Gemelo, Historial). Selects nativos: funcionan con teclado y lector
 * de pantalla sin código extra. Con una sola sede, la sede se muestra como texto (no hay nada que elegir).
 */
@Component({
  selector: 'app-selector-piso',
  template: `
    @if (piso.sedes().length > 1) {
      <label class="sr-only" for="sel-sede">Sede</label>
      <select id="sel-sede" class="sel" [value]="piso.sede()?.id" (change)="piso.seleccionarSede(+$any($event.target).value)">
        @for (s of piso.sedes(); track s.id) { <option [value]="s.id">{{ s.nombre }}</option> }
      </select>
    } @else if (piso.sede(); as s) {
      <span class="sede">{{ s.nombre }}</span>
    }
    @if (piso.pisosDeSede().length) {
      <label class="sr-only" for="sel-piso">Piso</label>
      <select id="sel-piso" class="sel piso" [value]="piso.piso()?.id" (change)="piso.seleccionarPiso(+$any($event.target).value)">
        @for (p of piso.pisosDeSede(); track p.id) { <option [value]="p.id">{{ p.nombre }}</option> }
      </select>
    } @else if (piso.cargando()) {
      <span class="sede">Cargando pisos…</span>
    } @else if (piso.error()) {
      <span class="sede error">No se pudieron cargar los pisos</span>
    }
  `,
  styles: `
    :host { display: inline-flex; align-items: center; gap: 10px; flex-wrap: wrap; }
    .sede { font-size: 15px; color: var(--gris); }
    .sede.error { color: var(--error); }
    .sel {
      height: 40px; padding: 0 36px 0 14px; border-radius: 10px; border: 1px solid var(--linea-2); background: var(--fondo)
        url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='16' height='16' fill='none' stroke='%231F1B1C' stroke-width='2' viewBox='0 0 24 24'%3E%3Cpath d='m6 9 6 6 6-6'/%3E%3C/svg%3E")
        no-repeat right 12px center;
      appearance: none; font-size: 15px; color: var(--tinta); cursor: pointer;
    }
    .sel:hover { border-color: var(--gris-2); }
    .sel.piso { font-weight: 700; border-width: 2px; border-color: var(--tinta); }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SelectorPisoComponent {
  protected piso = inject(PisoActualService);
}
