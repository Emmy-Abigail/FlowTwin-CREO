import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { NOMBRE_SEDE } from '../../core/datos/clinica.config';
import { ChipComponent } from '../../shared/componentes/chip.component';
import { IconoComponent } from '../../shared/componentes/icono.component';
import { KpiComponent } from '../../shared/componentes/kpi.component';
import { ModalComponent } from '../../shared/componentes/modal.component';
import { Render3dComponent } from '../../gemelo-render/render-3d/render-3d.component';
import { FiltroFeed, GemeloStore } from './gemelo.store';

/**
 * P4 · Gemelo en vivo: 4 KPI, el gemelo 3D del Piso 1 con la ocupación de cada zona,
 * la leyenda, el detalle de zona (M2) y el feed "Actividad en vivo".
 * La página solo conecta: los datos salen de GemeloStore y el dibujo de Render3dComponent.
 */
@Component({
  selector: 'app-gemelo',
  imports: [Render3dComponent, KpiComponent, ChipComponent, IconoComponent, ModalComponent],
  templateUrl: './gemelo.page.html',
  styleUrl: './gemelo.page.scss',
  providers: [GemeloStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GemeloPage {
  protected store = inject(GemeloStore);
  private router = inject(Router);

  protected readonly sede = NOMBRE_SEDE;
  protected readonly filtros: { valor: FiltroFeed; texto: string }[] = [
    { valor: 'activos', texto: 'Activos' },
    { valor: 'atendidos', texto: 'Atendidos' },
    { valor: 'todos', texto: 'Todos' },
  ];

  protected abrirAtencion(id: string): void {
    this.store.zonaSeleccionada.set(null);
    this.router.navigate(['/atencion', id], { state: { desde: 'gemelo' } });
  }
}
