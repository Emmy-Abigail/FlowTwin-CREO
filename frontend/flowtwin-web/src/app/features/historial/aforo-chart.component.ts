import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { HORA_APERTURA } from '../../core/datos/clinica.config';
import { pad2 } from '../../core/utils/tiempo';

const ANCHO = 620, ALTO = 230, IZQ = 34, DER = 10, ARRIBA = 12, ABAJO = 28;

/**
 * Gráfico "Aforo histórico": pacientes promedio por hora (07:00 a 18:00) frente a la
 * línea punteada del aforo. Las horas que lo superan se marcan con el rojo del semáforo.
 * SVG simple, sin librerías, igual al prototipo aprobado.
 */
@Component({
  selector: 'app-aforo-chart',
  template: `
    @let g = grafico();
    <svg [attr.viewBox]="'0 0 ' + ancho + ' ' + alto" role="img" aria-label="Aforo histórico por hora">
      @for (t of g.marcas; track t.valor) {
        <line [attr.x1]="izq" [attr.x2]="ancho - der" [attr.y1]="t.y" [attr.y2]="t.y" stroke="#EFEAE3" />
        <text [attr.x]="izq - 6" [attr.y]="t.y + 4" text-anchor="end">{{ t.valor }}</text>
      }
      @if (g.area) {
        <path [attr.d]="g.area" fill="rgba(166,4,55,.08)" />
        <path [attr.d]="g.linea" fill="none" stroke="#1A1A1A" stroke-width="2.2" stroke-linejoin="round" />
      }
      <line [attr.x1]="izq" [attr.x2]="ancho - der" [attr.y1]="g.yAforo" [attr.y2]="g.yAforo" stroke="#E53935" stroke-dasharray="5 4" stroke-width="1.5" />
      <text [attr.x]="ancho - der" [attr.y]="g.yAforo - 6" text-anchor="end" class="aforo">Aforo {{ aforo() }}</text>
      @for (p of g.puntos; track p.hora) {
        <circle [attr.cx]="p.x" [attr.cy]="p.y" [attr.r]="p.supera ? 5 : 3.2" [attr.fill]="p.supera ? '#E53935' : '#1A1A1A'">
          <title>{{ p.hora }} · {{ p.valor }} pacientes</title>
        </circle>
      }
      @if (g.pico; as pico) {
        <text [attr.x]="pico.x" [attr.y]="pico.y - 10" text-anchor="middle" class="pico">{{ pico.valor }}</text>
      }
      @for (e of g.ejeX; track e.texto) {
        <text [attr.x]="e.x" [attr.y]="alto - 8" text-anchor="middle">{{ e.texto }}</text>
      }
    </svg>
    <p class="hint nota">
      Hora pico: <b>{{ g.pico?.hora ?? '—' }}</b>. Máximo registrado en 7 días: <b>{{ maximo7Dias() }}</b> pacientes{{ maximo7Dias() > aforo() ? ' (superó el aforo)' : '' }}.
    </p>
  `,
  styles: `
    svg { width: 100%; height: auto; display: block; }
    text { font-family: var(--f-body); font-size: 11px; fill: var(--gris); }
    .aforo { fill: #B3261E; font-weight: 700; }
    .pico { fill: var(--tinta); font-weight: 800; }
    .nota { margin: 6px 0 0; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AforoChartComponent {
  readonly serie = input.required<(number | null)[]>();
  readonly aforo = input.required<number>();
  readonly maximo7Dias = input(0);

  protected readonly ancho = ANCHO;
  protected readonly alto = ALTO;
  protected readonly izq = IZQ;
  protected readonly der = DER;

  protected grafico = computed(() => {
    const v = this.serie(), aforo = this.aforo();
    const tope = Math.max(aforo, Math.ceil(Math.max(1, ...v.filter((x): x is number => x != null)))) * 1.15;
    const x = (i: number) => IZQ + (i * (ANCHO - IZQ - DER)) / (v.length - 1);
    const y = (val: number) => ARRIBA + (ALTO - ARRIBA - ABAJO) * (1 - val / tope);
    const hora = (i: number) => `${pad2(i + HORA_APERTURA)}:00`;

    const puntos = v.flatMap((val, i) => (val == null ? [] : [{ x: x(i), y: y(val), valor: val, hora: hora(i), supera: val > aforo }]));
    const linea = puntos.map((p, i) => `${i ? 'L' : 'M'}${p.x},${p.y}`).join(' ');
    const area = puntos.length
      ? `M${puntos[0].x},${y(0)} ${puntos.map(p => `L${p.x},${p.y}`).join(' ')} L${puntos.at(-1)!.x},${y(0)} Z`
      : '';
    const pico = puntos.reduce<(typeof puntos)[number] | null>((m, p) => (!m || p.valor > m.valor ? p : m), null);

    return {
      puntos, linea, area, pico, yAforo: y(aforo),
      marcas: [0, Math.round(tope / 3), Math.round((2 * tope) / 3)].map(valor => ({ valor, y: y(valor) })),
      ejeX: v.flatMap((_, i) => (i % 2 === 0 ? [{ x: x(i), texto: hora(i) }] : [])),
    };
  });
}
