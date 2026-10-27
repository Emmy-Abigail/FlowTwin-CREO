import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { HORA_APERTURA } from '../../core/datos/clinica.config';
import { pad2 } from '../../core/utils/tiempo';
import { ZonaGeometria } from '../../gemelo-render/geometria';

const COLOR = { alta: '#E53935', media: '#F2A900', baja: '#2E9E6B' };

export interface FilaMapa {
  etiqueta: string;
  valores: (number | null)[];
}

/**
 * Mapa "Día × hora": cada celda es la ocupación promedio de esa hora en % del aforo,
 * coloreada con el semáforo según los umbrales de la zona. Muestra los picos que se repiten.
 */
@Component({
  selector: 'app-mapa-calor',
  template: `
    <div class="hm" [style.grid-template-columns]="'54px repeat(' + horas().length + ', minmax(0, 1fr))'">
      <span></span>
      @for (h of horas(); track h.indice) { <span class="lab centro">{{ h.indice % 2 === 0 ? h.texto : '' }}</span> }
      @for (f of celdas(); track f.etiqueta) {
        <span class="lab">{{ f.etiqueta }}</span>
        @for (c of f.celdas; track $index) {
          @if (c) {
            <span class="c" [title]="c.titulo" [style.background]="c.color" [style.opacity]="c.opacidad" [style.color]="c.opacidad > 0.6 ? '#fff' : '#1F1B1C'">{{ c.pct }}</span>
          } @else {
            <span class="c futuro"></span>
          }
        }
      }
    </div>
  `,
  styles: `
    .hm { display: grid; gap: 3px; font-size: 11px; }
    .c { border-radius: 4px; height: 28px; display: flex; align-items: center; justify-content: center; font-weight: 700; font-variant-numeric: tabular-nums; }
    .c.futuro { background: #F4F1ED; }
    .lab { color: var(--gris); display: flex; align-items: center; font-weight: 600; }
    .lab.centro { justify-content: center; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MapaCalorComponent {
  readonly filas = input.required<FilaMapa[]>();
  readonly zona = input.required<ZonaGeometria>();

  protected horas = computed(() =>
    (this.filas()[0]?.valores ?? []).map((_, i) => ({ indice: i, texto: pad2(i + HORA_APERTURA) })));

  protected celdas = computed(() => {
    const z = this.zona();
    return this.filas().map(f => ({
      etiqueta: f.etiqueta,
      celdas: f.valores.map((v, i) => {
        if (v == null) return null;
        const pct = Math.round((v / Math.max(1, z.aforo)) * 100);
        const nivel = pct >= z.umbralAlta ? 'alta' : pct >= z.umbralMedia ? 'media' : 'baja';
        return {
          pct, color: COLOR[nivel],
          opacidad: +(0.18 + Math.min(1, pct / 110) * 0.82).toFixed(2),
          titulo: `${f.etiqueta} ${pad2(i + HORA_APERTURA)}:00 · ${v} pacientes (${pct} %)`,
        };
      }),
    }));
  });
}
