import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { ModoDatosService } from '../../core/datos/modo-datos.service';
import { PisoActualService } from '../../core/datos/piso-actual.service';
import { ToastService } from '../../core/ui/toast.service';
import { ChipComponent } from '../../shared/componentes/chip.component';
import { EstadoVacioComponent } from '../../shared/componentes/estado-vacio.component';
import { IconoComponent } from '../../shared/componentes/icono.component';
import { SelectorPisoComponent } from '../../shared/componentes/selector-piso.component';
import { HistorialStore, Pestana } from './historial.store';
import { MapaCalorComponent } from './mapa-calor.component';
import { FilaVista, PeriodoId, cifras, filaVista, textoPeriodo } from './modelo-historial';

/** Espera antes de buscar mientras se escribe el código: no se pide al servidor en cada tecla. */
const ESPERA_BUSQUEDA_MS = 300;

/**
 * P5 · Historial y reportes. Lo esencial (como en los tableros de flujo de pacientes): cuántas
 * atenciones, la espera y el tiempo total típicos (mediana) con su «cola», cuándo se llena cada zona
 * y la lista para revisar un caso. Gerencia lo ve igual: no tiene acciones que cambien datos.
 */
@Component({
  selector: 'app-historial',
  imports: [RouterLink, ChipComponent, EstadoVacioComponent, IconoComponent, SelectorPisoComponent, MapaCalorComponent],
  templateUrl: './historial.page.html',
  styleUrl: './historial.page.scss',
  providers: [HistorialStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HistorialPage {
  protected store = inject(HistorialStore);
  protected modo = inject(ModoDatosService);
  protected pisos = inject(PisoActualService);
  private router = inject(Router);
  private toast = inject(ToastService);
  private temporizador?: ReturnType<typeof setTimeout>;

  protected readonly periodos: { id: PeriodoId; texto: string }[] = [
    { id: 'hoy', texto: 'Hoy' }, { id: '7', texto: '7 días' }, { id: '30', texto: '30 días' }, { id: 'elegir', texto: 'Elegir' },
  ];

  /** El selector de piso solo aparece si hay entre qué elegir. */
  protected variosPisos = computed(() => this.pisos.sedes().length > 1 || this.pisos.pisosDeSede().length > 1);
  protected subtitulo = computed(() => {
    const partes = [textoPeriodo(this.store.rango(), this.store.hoy()), this.pisos.sede()?.nombre, this.pisos.piso()?.nombre];
    return partes.filter(Boolean).join(' · ');
  });
  protected k = computed(() => cifras(this.store.resumen()));
  protected filas = computed<FilaVista[]>(() => this.store.atenciones()?.filas.map(filaVista) ?? []);
  protected zona = computed(() => {
    const o = this.store.ocupacion();
    return o?.zonas.find(z => z.id === o.zonaId) ?? null;
  });

  constructor() {
    inject(DestroyRef).onDestroy(() => clearTimeout(this.temporizador));
  }

  protected cambiarPestana(p: Pestana): void {
    this.store.pestana.set(p);
  }

  protected escribir(texto: string): void {
    clearTimeout(this.temporizador);
    this.temporizador = setTimeout(() => this.store.buscar(texto), ESPERA_BUSQUEDA_MS);
  }

  protected abrir(f: FilaVista): void {
    this.router.navigate(['/atencion', f.clave], { queryParams: { desde: 'historial' } });
  }

  protected async exportar(): Promise<void> {
    const r = await this.store.exportarCsv();
    if ('error' in r) this.toast.mostrar(`No se pudo exportar: ${r.error}`, 'bad');
    else this.toast.mostrar(`Se descargó ${r.archivo}. La exportación quedó registrada en la auditoría.`);
  }
}
