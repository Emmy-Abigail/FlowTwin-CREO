import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { IconoComponent } from '../componentes/icono.component';

/** Lugar reservado para las pantallas de la Fase B (P6 a P11). Lee el título de la ruta. */
@Component({
  selector: 'app-proximamente',
  imports: [IconoComponent],
  template: `
    <div class="page-head"><div><h1>{{ titulo() }}</h1><p>{{ descripcion() }}</p></div></div>
    <div class="card pad aviso">
      <app-icono nombre="layers" [tamano]="34" />
      <h2>Esta pantalla llega en la Fase B</h2>
      <p class="hint">Ya está en el menú y protegida por rol. Su diseño está aprobado en el prototipo.</p>
    </div>
  `,
  styles: `
    :host { display: contents; }
    .aviso { max-width: 520px; margin: 40px auto; text-align: center; display: flex; flex-direction: column; gap: 10px; align-items: center; }
    .aviso p { margin: 0; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProximamentePage {
  private datos = toSignal(inject(ActivatedRoute).data);
  protected titulo = computed(() => (this.datos()?.['titulo'] as string) ?? '');
  protected descripcion = computed(() => (this.datos()?.['descripcion'] as string) ?? '');
}
