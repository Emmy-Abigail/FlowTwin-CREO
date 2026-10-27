import { Directive, input } from '@angular/core';

export type VarianteBoton = 'primario' | 'oscuro' | 'borde' | 'fantasma';

/**
 * Botón de CREO+. Es una directiva (no un componente) para que siga siendo un <button>
 * nativo, con su teclado, foco y type="submit" intactos:
 *   <button appBoton="primario">Guardar</button>
 * Regla de marca: un solo botón primario (rojo) por pantalla.
 */
@Directive({
  selector: 'button[appBoton], a[appBoton]',
  host: {
    class: 'btn',
    '[class.btn-p]': 'appBoton() === "primario"',
    '[class.btn-n]': 'appBoton() === "oscuro"',
    '[class.btn-o]': 'appBoton() === "borde"',
    '[class.btn-g]': 'appBoton() === "fantasma"',
    '[class.btn-sm]': 'chico()',
    '[class.btn-block]': 'bloque()',
  },
})
export class BotonDirective {
  readonly appBoton = input<VarianteBoton | ''>('borde');
  readonly chico = input(false);
  readonly bloque = input(false);
}
