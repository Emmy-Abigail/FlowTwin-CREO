import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { OcupacionApi, celdas, horasDe } from './modelo-historial';

/**
 * Mapa «Día × hora»: cada celda es cuántas personas había en promedio en la zona durante esa hora.
 * Un solo color (azul) que se oscurece con la cantidad: sin aforo no hay «lleno» que juzgar (ADR-23).
 * El número va escrito en la celda (no depende solo del color) y «sin datos» va rayado, nunca como 0.
 */
@Component({
  selector: 'app-mapa-calor',
  template: `
    <div class="mapa" role="table" [attr.aria-label]="'Personas en promedio por ' + (porDia() ? 'día de la semana' : 'fecha') + ' y hora'"
         [style.grid-template-columns]="'72px repeat(' + horas().length + ', minmax(0, 1fr))'"
         [style.grid-template-rows]="'auto repeat(' + filas().length + ', minmax(24px, 52px))'">
      <span role="columnheader"></span>
      @for (h of horas(); track h) { <span class="h" role="columnheader">{{ h }}</span> }
      @for (f of filas(); track f.etiqueta) {
        <span class="f" role="rowheader">{{ f.etiqueta }}</span>
        @for (c of f.celdas; track $index) {
          <span role="cell" [class]="'c ' + c.estado" [attr.title]="c.titulo" [attr.aria-label]="c.titulo"
                [style.background]="c.estado === 'OK' ? fondo(c.intensidad) : null"
                [class.claro]="c.estado === 'OK' && c.intensidad > 0.55" [class.fuera]="c.fuera">{{ c.texto }}</span>
        }
      }
    </div>
  `,
  styles: `
    :host { display: block; min-height: 0; }
    .mapa { display: grid; gap: 4px; height: 100%; align-content: start; }
    .h { font-family: var(--f-mono); font-size: 12px; color: var(--gris-2); text-align: center; }
    .f { display: flex; align-items: center; font-size: 13px; font-weight: 700; color: var(--gris); }
    .c {
      border-radius: 6px; display: flex; align-items: center; justify-content: center;
      font-family: var(--f-mono); font-weight: 600; font-size: 12.5px; color: var(--celeste-t); font-variant-numeric: tabular-nums;
    }
    .c.claro { color: #fff; }
    /* Fuera de servicio (ADR-25): el dato se ve igual, con un borde que avisa por qué hubo menos gente */
    .c.fuera { outline: 2px dashed var(--gris-2); outline-offset: -3px; }
    .c.FUTURO { background: #FBFAF9; }
    /* Rayado como «Sin datos» en el gemelo: no se sabe cuánta gente hubo */
    .c.SIN_DATOS { background: repeating-linear-gradient(135deg, var(--fondo-2) 0 5px, var(--linea) 5px 10px); }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MapaCalorComponent {
  readonly ocupacion = input.required<OcupacionApi>();

  protected horas = computed(() => horasDe(this.ocupacion()));
  protected filas = computed(() => celdas(this.ocupacion()));
  protected porDia = computed(() => this.ocupacion().agrupacion === 'DIA_SEMANA');

  /** El celeste-texto de la paleta (#1F5F99) con más o menos opacidad; un cero se ve casi blanco. */
  protected fondo(intensidad: number): string {
    return `rgba(31, 95, 153, ${(0.07 + intensidad * 0.85).toFixed(2)})`;
  }
}
