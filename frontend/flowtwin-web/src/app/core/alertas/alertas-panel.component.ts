import { ChangeDetectionStrategy, Component, computed, inject, output, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { mensajeDe, problemaDe } from '../api/problema';
import { GeometriaService } from '../datos/geometria.service';
import { ModoDatosService } from '../datos/modo-datos.service';
import { ToastService } from '../ui/toast.service';
import { IconoComponent } from '../../shared/componentes/icono.component';
import { ModalComponent } from '../../shared/componentes/modal.component';
import { AlertasService } from './alertas.service';
import { AlertaVista, vista } from './modelo-alertas';

/** Panel de la campana: lo urgente primero, una línea por alerta y la acción a mano. */
@Component({
  selector: 'app-alertas-panel',
  imports: [RouterLink, IconoComponent, ModalComponent],
  template: `
    <app-modal variante="panel" [titulo]="'Alertas · ' + vistas().length" (cerrar)="cerrar.emit()">
      <button type="button" class="sonido" [attr.aria-pressed]="!alertas.silencio()" (click)="alertas.alternarSilencio()">
        <app-icono nombre="bell" [tamano]="15" />{{ alertas.silencio() ? 'Sonido apagado' : 'Sonido activado' }}
      </button>
      @for (g of grupos(); track g.nombre) {
        <h3 class="grupo">{{ g.nombre }}</h3>
        <ul class="lista">
          @for (a of g.alertas; track a.clave) {
            <li [class]="'alerta ' + a.tono">
              <span class="ic"><app-icono [nombre]="a.icono" [tamano]="17" /></span>
              <div class="texto">
                <b>{{ a.titulo }}</b>
                @if (a.detalle) { <span>{{ a.detalle }}</span> }
                @if (a.atencion || (a.puedeCerrar && !modo.simulacion())) {
                  <div class="acciones">
                    @if (a.atencion) {
                      <a class="btn btn-o btn-sm" [routerLink]="['/atencion', a.atencion]" (click)="cerrar.emit()">Ver recorrido</a>
                    }
                    @if (a.puedeCerrar && a.atencionId !== null && !modo.simulacion()) {
                      @if (confirmando() === a.clave) {
                        <button type="button" class="btn btn-p btn-sm" [disabled]="trabajando()" (click)="cerrarSinDevolver(a.atencionId)">Confirmar cierre</button>
                      } @else {
                        <button type="button" class="btn btn-o btn-sm" (click)="confirmando.set(a.clave)">No devolvió la tarjeta</button>
                      }
                    }
                  </div>
                }
              </div>
            </li>
          }
        </ul>
      } @empty {
        <p class="vacio"><app-icono nombre="check" [tamano]="18" />Todo en orden</p>
      }
    </app-modal>
  `,
  styles: `
    .sonido { align-self: flex-start; display: inline-flex; align-items: center; gap: 6px; border: 0; background: var(--fondo-2); border-radius: 999px; padding: 5px 12px; font-size: 13px; font-weight: 700; color: var(--gris); }
    .sonido[aria-pressed="true"] { color: var(--tinta); }
    .grupo { font-size: 12px; font-weight: 700; letter-spacing: .06em; text-transform: uppercase; color: var(--gris-2); margin: 4px 0 0; }
    .lista { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 10px; }
    .alerta { display: grid; grid-template-columns: auto 1fr; gap: 12px; border: 1px solid var(--linea); border-left-width: 4px; border-radius: 12px; padding: 12px 14px; animation: entrar .28s ease-out both; }
    .alerta .ic { width: 34px; height: 34px; border-radius: 50%; display: grid; place-items: center; }
    .alerta.roja { border-left-color: var(--alta); } .alerta.roja .ic { background: var(--alta-s); color: #B3261E; }
    .alerta.ambar { border-left-color: var(--media); } .alerta.ambar .ic { background: var(--amarillo-suave); color: var(--ambar-t); }
    .alerta.gris { border-left-color: var(--gris-3); } .alerta.gris .ic { background: var(--fondo-2); color: var(--gris); }
    .texto { display: flex; flex-direction: column; gap: 2px; min-width: 0; }
    .texto b { font-size: 15px; }
    .texto span { color: var(--gris); font-size: 13.5px; }
    .acciones { display: flex; gap: 8px; margin-top: 8px; flex-wrap: wrap; }
    .vacio { display: flex; align-items: center; gap: 8px; color: var(--gris); margin: 12px 0; }
    @keyframes entrar { from { opacity: 0; transform: translateX(16px); } to { opacity: 1; transform: none; } }
    @media (prefers-reduced-motion: reduce) { .alerta { animation: none; } }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AlertasPanelComponent {
  protected alertas = inject(AlertasService);
  protected modo = inject(ModoDatosService);
  private geo = inject(GeometriaService);
  private toast = inject(ToastService);
  readonly cerrar = output<void>();

  protected confirmando = signal<string | null>(null);
  protected trabajando = signal(false);

  protected vistas = computed<AlertaVista[]>(() => {
    const ahora = this.alertas.ahora();
    return this.alertas.lista().map(a => vista(a, ahora, id => this.geo.nombreZona(id)));
  });

  protected grupos = computed(() => (['Pacientes', 'Equipos'] as const)
    .map(nombre => ({ nombre, alertas: this.vistas().filter(a => a.grupo === nombre) }))
    .filter(g => g.alertas.length));

  protected async cerrarSinDevolver(atencionId: number): Promise<void> {
    this.trabajando.set(true);
    try {
      await this.alertas.cerrarSinDevolver(atencionId);
      this.toast.mostrar('Atención cerrada · la tarjeta quedó en «Sin devolver»');
    } catch (e) {
      this.toast.mostrar(mensajeDe(problemaDe(e)), 'bad');
    } finally {
      this.trabajando.set(false);
      this.confirmando.set(null);
    }
  }
}
