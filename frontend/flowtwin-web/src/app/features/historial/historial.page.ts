import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { ToastService } from '../../core/ui/toast.service';
import { BotonDirective } from '../../shared/componentes/boton.directive';
import { IconoComponent } from '../../shared/componentes/icono.component';
import { KpiComponent } from '../../shared/componentes/kpi.component';
import { ModalComponent } from '../../shared/componentes/modal.component';
import { Columna, TablaComponent } from '../../shared/componentes/tabla.component';
import { AforoChartComponent } from './aforo-chart.component';
import { FilaAtencion, HistorialStore, Rango } from './historial.store';
import { MapaCalorComponent } from './mapa-calor.component';

/** P5 · Historial y reportes. Gerencia lo ve en solo lectura (puede exportar). */
@Component({
  selector: 'app-historial',
  imports: [FormsModule, BotonDirective, IconoComponent, KpiComponent, ModalComponent, TablaComponent, AforoChartComponent, MapaCalorComponent],
  templateUrl: './historial.page.html',
  styleUrl: './historial.page.scss',
  providers: [HistorialStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HistorialPage {
  protected store = inject(HistorialStore);
  protected auth = inject(AuthService);
  private router = inject(Router);
  private toast = inject(ToastService);

  /** Aviso del reporte PDF generado (en la demo no se descarga un PDF real). */
  protected pdfGenerado = signal<{ archivo: string; filas: number } | null>(null);

  protected readonly columnas: Columna<FilaAtencion>[] = [
    { titulo: 'ID', valor: f => f.id, clase: 'mono' },
    { titulo: 'Fecha', valor: f => f.fecha },
    { titulo: 'Ingreso', valor: f => f.ingreso, clase: 'num' },
    { titulo: 'Atendido en', valor: f => f.atendidoEn },
    { titulo: 'Espera', valor: f => f.espera, clase: 'num' },
    { titulo: 'Consulta', valor: f => f.consulta, clase: 'num' },
    { titulo: 'Total', valor: f => (f.activa ? { chip: 'En curso', tono: 'media' } : f.total), clase: 'num' },
    { titulo: 'Cierre', valor: f => f.cierre },
  ];

  protected cambiarRango(rango: Rango): void {
    this.store.cambiarFiltro(() => {
      this.store.rango.set(rango);
      if (rango === 'custom' && !this.store.desde()) {
        this.store.desde.set(this.store.haceSeisIso());
        this.store.hasta.set(this.store.hoyIso());
      }
    });
  }

  protected abrir(fila: FilaAtencion): void {
    this.router.navigate(['/atencion', fila.id], { state: { desde: 'historial' } });
  }

  /** CSV real: se descarga un archivo con las filas filtradas. */
  protected exportarCsv(): void {
    const { nombre, contenido, filas } = this.store.generarCsv();
    const url = URL.createObjectURL(new Blob([contenido], { type: 'text/csv;charset=utf-8' }));
    const enlace = Object.assign(document.createElement('a'), { href: url, download: nombre });
    enlace.click();
    URL.revokeObjectURL(url);
    this.store.registrarExportacion(nombre, 'CSV');
    this.toast.mostrar(`Reporte generado con los filtros actuales · ${filas} atenciones`);
  }

  protected exportarPdf(): void {
    const archivo = this.store.nombreArchivo('pdf');
    this.store.registrarExportacion(archivo, 'PDF');
    this.pdfGenerado.set({ archivo, filas: this.store.filtradas().length });
  }
}
