import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TonoChip } from '../../core/datos/modelos';

/** Etiqueta de estado: <app-chip tono="baja">En consulta</app-chip>. */
@Component({
  selector: 'app-chip',
  template: '<ng-content />',
  host: { class: 'chip', '[class]': '"chip " + tono()' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChipComponent {
  readonly tono = input<TonoChip>('');
}
