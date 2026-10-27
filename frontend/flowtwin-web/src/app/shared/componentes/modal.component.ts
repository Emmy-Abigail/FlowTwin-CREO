import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { IconoComponent } from './icono.component';

/**
 * Ventana emergente. variante="dialogo" se centra; variante="panel" entra por la derecha
 * (como el Detalle de zona, M2). Se cierra con la X, con Escape o al hacer clic fuera.
 *   <app-modal titulo="Pausar zona" (cerrar)="...">contenido<div pie>botones</div></app-modal>
 */
@Component({
  selector: 'app-modal',
  imports: [IconoComponent],
  template: `
    <div class="scrim" [class.panel]="variante() === 'panel'" (click)="$event.target === $event.currentTarget && cerrar.emit()">
      <div [class]="variante() === 'panel' ? 'drawer' : 'modal'" role="dialog" aria-modal="true" [attr.aria-label]="titulo()">
        <div class="m-h">
          <div>
            @if (antetitulo()) { <span class="lbl-up">{{ antetitulo() }}</span> }
            <h2 [style.font-size.px]="variante() === 'panel' ? 22 : null">{{ titulo() }}</h2>
          </div>
          <button class="x" type="button" (click)="cerrar.emit()" aria-label="Cerrar"><app-icono nombre="x" [tamano]="16" /></button>
        </div>
        <div class="m-b"><ng-content /></div>
        <div class="m-f"><ng-content select="[pie]" /></div>
      </div>
    </div>
  `,
  styles: '.m-f:empty { display: none; }',
  host: { '(document:keydown.escape)': 'cerrar.emit()' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ModalComponent {
  readonly titulo = input.required<string>();
  readonly antetitulo = input('');
  readonly variante = input<'dialogo' | 'panel'>('dialogo');
  readonly cerrar = output<void>();
}
