import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, linkedSignal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { GeometriaPiso, ZonaGeometria } from '../../gemelo-render/geometria';
import { ClinicaStore } from './clinica.store';

/** Lo que la Configuración simulada cambia de una zona (ADR-31). */
export type AjusteZona = Partial<Pick<ZonaGeometria, 'nombre' | 'tipo' | 'aforo' | 'umbralMedia'>>;

/**
 * Carga la geometría real del Piso 1 desde public/pisos/. Hoy es un archivo estático;
 * mañana será un GET al backend (por ejemplo /api/pisos/1/geometria) y nada más cambia.
 */
@Injectable({ providedIn: 'root' })
export class GeometriaService {
  private http = inject(HttpClient);

  /** null mientras se descarga. */
  readonly piso = toSignal(this.http.get<GeometriaPiso>('pisos/geometria-piso1.json'), { initialValue: null });

  /** Cambios hechos en la Configuración simulada; «Reiniciar simulación» los borra. */
  private ajustes = linkedSignal<number, Record<number, AjusteZona>>({ source: inject(ClinicaStore).reinicios, computation: () => ({}) });

  readonly zonas = computed(() => (this.piso()?.zonas ?? []).map(z => ({ ...z, ...this.ajustes()[z.id] })));

  ajustarZona(id: number, ajuste: AjusteZona): void {
    this.ajustes.update(a => ({ ...a, [id]: { ...a[id], ...ajuste } }));
  }
  private porId = computed(() => new Map(this.zonas().map(z => [z.id, z])));

  zona(id: number | null | undefined): ZonaGeometria | undefined {
    return id == null ? undefined : this.porId().get(id);
  }

  nombreZona(id: number | null | undefined): string {
    return this.zona(id)?.nombre ?? (id == null ? '—' : `Zona ${id}`);
  }
}
