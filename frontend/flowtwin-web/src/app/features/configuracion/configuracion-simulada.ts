import { Injectable, computed, inject, linkedSignal } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';
import { ClinicaStore } from '../../core/datos/clinica.store';
import { GeometriaService } from '../../core/datos/geometria.service';
import { TipoZona } from '../../gemelo-render/geometria';
import { Parametros, SedeConfig, ZonaConfig, llevaAforo } from './modelo-configuracion';

/** Los mismos valores iniciales que el sistema real (ParametrosSistema). */
const PARAMETROS_INICIALES: Parametros = { esperaAltaMin: 40, esperaCriticaMin: 60, sinLecturasMin: 30, horaCierre: 22, semaforoAmbarPct: 80 };
const SEDES_INICIALES: SedeConfig[] = [{
  id: 1, nombre: 'Sede Principal', direccion: 'Lima', prefijoAtencion: 'A',
  pisos: [{ id: 1, nombre: 'Piso 1', nivel: 1, publicado: true }],
}];
/** Las zonas creadas en la Simulación empiezan en este id, para no chocar con las del Piso 1. */
const PRIMER_ID_NUEVO = 100;

/**
 * Configuración en la Simulación (ADR-31): sede, pisos, zonas y parámetros ficticios, en memoria. Las zonas del
 * Piso 1 son las de la clínica simulada: cambiar su aforo, su nombre o el ámbar del semáforo se ve al instante en
 * el gemelo simulado. Todo vuelve al inicio con «Reiniciar simulación» y nada llega a la base.
 */
@Injectable({ providedIn: 'root' })
export class ConfiguracionSimulada {
  private clinica = inject(ClinicaStore);
  private geometria = inject(GeometriaService);
  private auth = inject(AuthService);

  private sedes = linkedSignal({ source: this.clinica.reinicios, computation: () => structuredClone(SEDES_INICIALES) });
  /** Zonas de los pisos nuevos y las nuevas del Piso 1 (las demás del Piso 1 salen de la geometría simulada). */
  private zonasNuevas = linkedSignal<number, Record<number, ZonaConfig[]>>({ source: this.clinica.reinicios, computation: () => ({}) });
  private bajas = linkedSignal<number, number[]>({ source: this.clinica.reinicios, computation: () => [] });
  readonly parametros = linkedSignal({ source: this.clinica.reinicios, computation: () => ({ ...PARAMETROS_INICIALES }) });

  readonly arbol = computed(() => this.sedes());
  readonly pisosPublicados = computed(() => this.sedes().flatMap(s => s.pisos.filter(p => p.publicado)));

  zonas(pisoId: number): ZonaConfig[] {
    const delPiso1 = pisoId === 1
      ? this.geometria.zonas().map(z => ({ id: z.id, nombre: z.nombre, tipo: z.tipo, aforo: llevaAforo(z.tipo) ? z.aforo : null, tieneAforo: llevaAforo(z.tipo) }))
      : [];
    return [...delPiso1, ...(this.zonasNuevas()[pisoId] ?? [])].filter(z => !this.bajas().includes(z.id));
  }

  guardarSede(id: number | null, c: { nombre: string; direccion: string | null; prefijoAtencion: string }): void {
    if (!c.nombre.trim() || !/^[A-Z]$/.test(c.prefijoAtencion)) throw new Error('Escribe el nombre y una sola letra para los códigos.');
    if (this.sedes().some(s => s.id !== id && s.prefijoAtencion === c.prefijoAtencion)) throw new Error(`La letra ${c.prefijoAtencion} ya la usa otra sede.`);
    const antes = id === null ? undefined : this.sedes().find(s => s.id === id);
    this.sedes.update(l => id === null
      ? [...l, { id: Math.max(...l.map(s => s.id)) + 1, ...c, pisos: [] }]
      : l.map(s => (s.id === id ? { ...s, ...c } : s)));
    this.auditar(id === null ? 'Creó' : 'Editó', 'Sede', c.nombre, antes && { nombre: antes.nombre, direccion: antes.direccion, prefijoAtencion: antes.prefijoAtencion }, c);
  }

  guardarPiso(sedeId: number, id: number | null, c: { nombre: string; nivel: number }): number {
    if (!c.nombre.trim()) throw new Error('Escribe el nombre del piso.');
    const todos = this.sedes().flatMap(s => s.pisos);
    const nuevoId = id ?? Math.max(...todos.map(p => p.id)) + 1;
    const antes = todos.find(p => p.id === id);
    this.sedes.update(l => l.map(s => s.id !== sedeId ? s : {
      ...s, pisos: id === null ? [...s.pisos, { id: nuevoId, ...c, publicado: false }] : s.pisos.map(p => (p.id === id ? { ...p, ...c } : p)),
    }));
    this.auditar(id === null ? 'Creó' : 'Editó', 'Piso', c.nombre, antes && { nombre: antes.nombre, nivel: antes.nivel }, c);
    return nuevoId;
  }

  publicar(pisoId: number): void {
    if (this.zonas(pisoId).length === 0) throw new Error('Un piso sin zonas no se puede publicar.');
    this.sedes.update(l => l.map(s => ({ ...s, pisos: s.pisos.map(p => (p.id === pisoId ? { ...p, publicado: true } : p)) })));
    const piso = this.sedes().flatMap(s => s.pisos).find(p => p.id === pisoId)!;
    this.auditar('Publicó', 'Piso', piso.nombre, { publicado: false }, { publicado: true });
  }

  guardarZona(pisoId: number, id: number | null, c: { nombre: string; tipo: string; aforo: number | null }): void {
    if (!c.nombre.trim()) throw new Error('Escribe el nombre de la zona.');
    const antes = id === null ? undefined : this.zonas(pisoId).find(z => z.id === id);
    const desdeGeometria = id !== null && pisoId === 1 && this.geometria.zona(id);
    if (desdeGeometria) {
      this.geometria.ajustarZona(id, { nombre: c.nombre, tipo: c.tipo as TipoZona, ...(c.aforo !== null ? { aforo: c.aforo } : {}) });
    } else {
      const zona: ZonaConfig = { id: id ?? this.siguienteIdZona(), nombre: c.nombre, tipo: c.tipo, aforo: c.aforo, tieneAforo: llevaAforo(c.tipo) };
      this.zonasNuevas.update(m => ({
        ...m, [pisoId]: id === null ? [...(m[pisoId] ?? []), zona] : (m[pisoId] ?? []).map(z => (z.id === id ? zona : z)),
      }));
    }
    this.auditar(id === null ? 'Creó' : 'Editó', 'Zona', c.nombre,
      antes && { nombre: antes.nombre, tipo: antes.tipo, aforo: antes.aforo }, { nombre: c.nombre, tipo: c.tipo, aforo: c.aforo });
  }

  darDeBajaZona(id: number): void {
    if (this.clinica.nodoDeZona(id)) throw new Error('La zona tiene un nodo instalado: primero cámbialo de zona o dalo de baja.');
    const nombre = [...this.zonas(1), ...Object.values(this.zonasNuevas()).flat()].find(z => z.id === id)?.nombre ?? String(id);
    this.bajas.update(l => [...l, id]);
    this.auditar('Dio de baja', 'Zona', nombre);
  }

  /** Las mismas validaciones que el servidor; el ámbar se aplica a todas las zonas del gemelo simulado. */
  guardarParametros(p: Parametros): void {
    if (p.esperaCriticaMin <= p.esperaAltaMin) throw new Error('La alerta crítica debe ser mayor que la alerta de espera.');
    if (p.semaforoAmbarPct < 10 || p.semaforoAmbarPct > 100) throw new Error('El ámbar debe estar entre 10 % y 100 %.');
    const antes = { ...this.parametros() };
    this.parametros.set({ ...p });
    for (const z of this.geometria.zonas()) this.geometria.ajustarZona(z.id, { umbralMedia: p.semaforoAmbarPct });
    this.auditar('Editó', 'Parametros', 'Parámetros', { ...antes }, { ...p });
  }

  private siguienteIdZona(): number {
    const usados = Object.values(this.zonasNuevas()).flat().map(z => z.id);
    return Math.max(PRIMER_ID_NUEVO - 1, ...usados) + 1;
  }

  /** En la bitácora simulada: la entidad (Sede, Piso, Zona, Parametros) va en el detalle, el nombre en el elemento. */
  private auditar(accion: string, entidad: string, nombre: string, antes?: object, despues?: object): void {
    this.clinica.auditar('Gemelo', accion, nombre, entidad, this.auth.actor(),
      { antes: antes as Record<string, unknown> | undefined, despues: despues as Record<string, unknown> | undefined });
  }
}
