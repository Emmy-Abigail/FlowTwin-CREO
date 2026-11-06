import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { BotonDirective } from '../../shared/componentes/boton.directive';
import { ModalComponent } from '../../shared/componentes/modal.component';

export interface TarjetaSeleccionable { id: number; codigo: string; detalle: string; }

/**
 * Modal para confirmar una tanda de tarjetas (R4 desinfectar, R5 extraviar). El operador marca cuáles
 * y confirma de una vez. «marcadasAlInicio»: en la desinfección vienen todas marcadas (es la tarea de
 * siempre); al extraviar ninguna (saca tarjetas del inventario: debe ser una decisión explícita).
 */
@Component({
  selector: 'app-seleccion-tarjetas',
  imports: [BotonDirective, ModalComponent],
  template: `
    <app-modal [titulo]="titulo()" (cerrar)="cancelar.emit()">
      <p class="texto">{{ texto() }}</p>
      @if (tarjetas().length > 1) {
        <label class="todas">
          <input type="checkbox" [checked]="todas()" [indeterminate]="algunas()" (change)="alternarTodas()"> Todas ({{ tarjetas().length }})
        </label>
      }
      <ul class="lista">
        @for (t of tarjetas(); track t.id) {
          <li>
            <label>
              <input type="checkbox" [checked]="marcadas().has(t.id)" (change)="alternar(t.id)">
              <code>{{ t.codigo }}</code><span>{{ t.detalle }}</span>
            </label>
          </li>
        }
      </ul>
      @if (avisoVacio()) { <p class="error" role="alert">Marca al menos una tarjeta.</p> }
      <div pie>
        <button appBoton="borde" type="button" (click)="cancelar.emit()">Cancelar</button>
        <button appBoton="oscuro" type="button" (click)="aceptar()">{{ textoAccion() }}</button>
      </div>
    </app-modal>
  `,
  styles: `
    .texto { margin: 0 0 12px; color: var(--gris); }
    .todas { display: flex; align-items: center; gap: 10px; font-weight: 700; font-size: 14px; margin-bottom: 8px; }
    .lista { list-style: none; margin: 0; padding: 0; border: 1px solid var(--linea); border-radius: 12px; max-height: 260px; overflow-y: auto; }
    .lista li + li { border-top: 1px solid var(--linea); }
    .lista label { display: flex; align-items: center; gap: 12px; padding: 10px 14px; cursor: pointer; }
    .lista code { font-family: var(--f-mono); font-weight: 600; }
    .lista span { margin-left: auto; color: var(--gris); font-size: 13px; text-align: right; }
    input[type=checkbox] { width: 18px; height: 18px; accent-color: var(--negro); flex: none; }
    .error { margin: 10px 0 0; color: var(--error); font-weight: 600; font-size: 14px; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SeleccionTarjetasComponent {
  readonly titulo = input.required<string>();
  readonly texto = input.required<string>();
  readonly tarjetas = input.required<TarjetaSeleccionable[]>();
  /** «Marcar {n} como desinfectadas»: el texto del botón según cuántas están marcadas. */
  readonly accion = input.required<(n: number) => string>();
  readonly marcadasAlInicio = input(true);
  readonly confirmar = output<number[]>();
  readonly cancelar = output<void>();

  private eleccion = signal<Set<number> | null>(null);
  protected avisoVacio = signal(false);
  /** Hasta que el operador toca algo, la selección es la inicial (todas o ninguna). */
  protected marcadas = computed(() => this.eleccion() ?? new Set(this.marcadasAlInicio() ? this.tarjetas().map(t => t.id) : []));
  protected todas = computed(() => this.marcadas().size === this.tarjetas().length);
  protected algunas = computed(() => this.marcadas().size > 0 && !this.todas());
  protected textoAccion = computed(() => this.accion()(this.marcadas().size));

  protected alternar(id: number): void {
    const s = new Set(this.marcadas());
    if (s.has(id)) s.delete(id); else s.add(id);
    this.eleccion.set(s);
    this.avisoVacio.set(false);
  }

  protected alternarTodas(): void {
    this.eleccion.set(new Set(this.todas() ? [] : this.tarjetas().map(t => t.id)));
    this.avisoVacio.set(false);
  }

  /** El botón no se deshabilita: si no hay ninguna marcada, dice qué falta (mismo criterio que el login). */
  protected aceptar(): void {
    if (this.marcadas().size === 0) { this.avisoVacio.set(true); return; }
    this.confirmar.emit([...this.marcadas()]);
  }
}
