import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TonoChip } from '../../core/datos/modelos';
import { ChipComponent } from './chip.component';

/** Una celda es texto, o un chip si la columna devuelve { chip, tono }. */
export type Celda = string | number | { chip: string; tono: TonoChip };

export interface Columna<T> {
  titulo: string;
  valor: (fila: T) => Celda;
  /** 'mono' = número en negrita tabular; 'num' = número tabular. */
  clase?: 'mono' | 'num';
}

/**
 * Tabla simple con estilo de CREO+. Las columnas dicen cómo leer cada celda:
 *   <app-tabla [columnas]="cols" [filas]="atenciones" (filaClic)="abrir($event)" />
 */
@Component({
  selector: 'app-tabla',
  imports: [ChipComponent],
  template: `
    <div class="tbl-wrap">
      <table class="tbl">
        <thead><tr>@for (c of columnas(); track c.titulo) { <th>{{ c.titulo }}</th> }</tr></thead>
        <tbody>
          @for (fila of filas(); track $index) {
            <tr [class.click]="clicable()" (click)="clicable() && filaClic.emit(fila)">
              @for (c of columnas(); track c.titulo) {
                @let v = c.valor(fila);
                <td [class]="c.clase ?? ''">
                  @if (esChip(v)) { <app-chip [tono]="v.tono">{{ v.chip }}</app-chip> } @else { {{ v }} }
                </td>
              }
            </tr>
          } @empty {
            <tr><td [attr.colspan]="columnas().length"><div class="empty">{{ vacio() }}</div></td></tr>
          }
        </tbody>
      </table>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TablaComponent<T> {
  readonly columnas = input.required<Columna<T>[]>();
  readonly filas = input.required<T[]>();
  readonly vacio = input('No hay datos con estos filtros.');
  readonly clicable = input(false);
  readonly filaClic = output<T>();

  protected esChip(v: Celda): v is { chip: string; tono: TonoChip } {
    return typeof v === 'object';
  }
}
