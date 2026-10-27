import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { GeometriaPiso, ZonaGeometria } from '../../gemelo-render/geometria';

/**
 * Carga la geometría real del Piso 1 desde public/pisos/. Hoy es un archivo estático;
 * mañana será un GET al backend (por ejemplo /api/pisos/1/geometria) y nada más cambia.
 */
@Injectable({ providedIn: 'root' })
export class GeometriaService {
  private http = inject(HttpClient);

  /** null mientras se descarga. */
  readonly piso = toSignal(this.http.get<GeometriaPiso>('pisos/geometria-piso1.json'), { initialValue: null });
  readonly planoUrl = 'pisos/piso1.jpg';

  readonly zonas = computed(() => this.piso()?.zonas ?? []);
  private porId = computed(() => new Map(this.zonas().map(z => [z.id, z])));

  zona(id: number | null | undefined): ZonaGeometria | undefined {
    return id == null ? undefined : this.porId().get(id);
  }

  nombreZona(id: number | null | undefined): string {
    return this.zona(id)?.nombre ?? (id == null ? '—' : `Zona ${id}`);
  }
}
