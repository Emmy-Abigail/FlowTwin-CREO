import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API } from '../api/problema';

/** Un piso publicado, tal como lo devuelve GET /sedes. */
export interface PisoApi {
  id: number;
  nombre: string;
  nivel: number;
  anchoM: number | null;
  fondoM: number | null;
  publicado: boolean;
}

/** Una sede con sus pisos visibles para el usuario (RN-24: el servidor ya filtró los que no tiene asignados). */
export interface SedeApi {
  id: number;
  nombre: string;
  direccion: string | null;
  pisos: PisoApi[];
}

const CLAVE = 'flowtwin.piso';

/**
 * Qué sede y qué piso está mirando la persona. Lo comparten el Gemelo y el Historial: si cambia de piso
 * en uno, el otro muestra el mismo. Se recuerda en el navegador (es solo una comodidad: si el piso ya no
 * existe o no lo tiene asignado, se elige otro).
 */
@Injectable({ providedIn: 'root' })
export class PisoActualService {
  private http = inject(HttpClient);

  readonly sedes = signal<SedeApi[]>([]);
  readonly cargando = signal(false);
  readonly error = signal(false);
  private readonly sedeId = signal<number | null>(null);
  private readonly pisoId = signal<number | null>(null);

  readonly sede = computed(() => this.sedes().find(s => s.id === this.sedeId()) ?? null);
  readonly piso = computed(() => this.sede()?.pisos.find(p => p.id === this.pisoId()) ?? null);
  /** De arriba hacia abajo, como en el edificio: el selector los muestra en ese orden. */
  readonly pisosDeSede = computed(() => [...(this.sede()?.pisos ?? [])].sort((a, b) => b.nivel - a.nivel));

  async cargar(): Promise<void> {
    this.cargando.set(true);
    this.error.set(false);
    try {
      const sedes = await firstValueFrom(this.http.get<SedeApi[]>(`${API}/sedes`));
      this.sedes.set(sedes);
      const guardado = leer();
      const sede = sedes.find(s => s.id === guardado?.sedeId) ?? sedes[0];
      const piso = sede?.pisos.find(p => p.id === guardado?.pisoId) ?? porDefecto(sede);
      this.sedeId.set(sede?.id ?? null);
      this.pisoId.set(piso?.id ?? null);
    } catch {
      this.error.set(true);
    } finally {
      this.cargando.set(false);
    }
  }

  seleccionarSede(id: number): void {
    const sede = this.sedes().find(s => s.id === id);
    if (!sede) return;
    this.sedeId.set(sede.id);
    this.pisoId.set(porDefecto(sede)?.id ?? null);
    this.guardar();
  }

  seleccionarPiso(id: number): void {
    if (!this.sede()?.pisos.some(p => p.id === id)) return;
    this.pisoId.set(id);
    this.guardar();
  }

  private guardar(): void {
    try { localStorage.setItem(CLAVE, JSON.stringify({ sedeId: this.sedeId(), pisoId: this.pisoId() })); }
    catch { /* sin almacenamiento: no se recuerda */ }
  }
}

/** El piso de entrada: el de nivel más bajo desde 1 (el sótano, si existe, no suele ser donde llegan los pacientes). */
function porDefecto(sede: SedeApi | undefined): PisoApi | undefined {
  const pisos = [...(sede?.pisos ?? [])].sort((a, b) => a.nivel - b.nivel);
  return pisos.find(p => p.nivel >= 1) ?? pisos[0];
}

function leer(): { sedeId: number; pisoId: number } | null {
  try { return JSON.parse(localStorage.getItem(CLAVE) ?? 'null'); } catch { return null; }
}
