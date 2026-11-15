import { HttpClient } from '@angular/common/http';
import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API, mensajeDe, problemaDe } from '../../core/api/problema';
import { GeometriaPiso } from '../../gemelo-render/geometria';
import { ConexionGemelo, ManejadoresGemelo } from './conexion-gemelo';
import {
  EstadoConexion, FuenteGemelo, GeometriaApi, KpisVista, NodoVista, PacienteVista, ZonaVista,
  estadoDeApi, geometriaDesdeApi, nivelDeApi,
} from './modelo-gemelo';

// ---- GET /pisos/{id}/estado y /atendidas-hoy (backend: EstadoPiso, AtendidaHoy)
interface EstadoApi {
  zonas: { zonaId: number; ocupacion: number; aforo: number | null; nivel: string; pausada: boolean }[];
  atenciones: { id: number; codigo: string; zonaActual: number | null; estado: string; inicioEn: string; esperaMin: number; zonaDesdeEn: string | null }[];
  kpis: { enClinica: number; enEspera: number; esperaPromMin: number | null; consultaPromMin: number | null };
  nodos: { nodoId: number; codigo: string; zonaId: number; estado: string; ultimoLatidoEn: string | null }[];
}
interface AtendidaApi { id: number; codigo: string; inicioEn: string; finEn: string; motivo: string; esperaMin: number; totalMin: number; }

/** El reloj de la pantalla: las esperas y los «hace X min» avanzan solos entre mensajes. */
const TIC_MS = 15_000;
const ms = (iso: string | null | undefined) => (iso ? Date.parse(iso) : null);

/**
 * Fuente de datos REAL del gemelo: la foto del piso por HTTP y, encima, los cambios que manda el hub
 * SignalR. Si la conexión se corta y vuelve, se pide la foto otra vez (pudieron perderse mensajes).
 * Nada se inventa: lo que el backend no sabe (zona sin nodo) llega como «sin datos» y así se muestra.
 */
@Injectable()
export class GemeloEnVivoStore implements FuenteGemelo {
  private http = inject(HttpClient);
  private hub = inject(ConexionGemelo);

  readonly geometria = signal<GeometriaPiso | null>(null);
  readonly kpis = signal<KpisVista | null>(null);
  readonly error = signal<string | null>(null);
  readonly ahora = signal(Date.now());
  readonly conexion = computed<EstadoConexion>(() => this.hub.estado());

  private zonasPorId = signal(new Map<number, ZonaVista>());
  private abiertas = signal(new Map<string, { id: number; zonaId: number | null; estado: string; inicio: number; esperaMin: number; zonaDesde: number | null }>());
  private atendidas = signal<PacienteVista[]>([]);
  private nodosPorId = signal(new Map<number, NodoVista>());
  private pisoId: number | null = null;
  private activa = false;
  private reloj?: ReturnType<typeof setInterval>;

  readonly zonas = computed(() => [...this.zonasPorId().values()]);
  readonly nodos = computed(() => [...this.nodosPorId().values()]);

  /** Abiertas + atendidas hoy. La espera de quien aún espera se calcula con su inicio: corre sola. */
  readonly pacientes = computed<PacienteVista[]>(() => {
    const ahora = this.ahora();
    const abiertas = [...this.abiertas()].map(([codigo, a]) => {
      const estado = estadoDeApi(a.estado);
      return {
        clave: String(a.id), codigo, estado, zonaId: a.zonaId, zonaDesde: a.zonaDesde,
        esperaMin: estado === 'esperando' ? Math.max(a.esperaMin, Math.floor((ahora - a.inicio) / 60_000)) : a.esperaMin,
      };
    });
    return [...abiertas, ...this.atendidas()];
  });

  constructor() {
    inject(DestroyRef).onDestroy(() => this.desactivar());
  }

  /** Empieza a mirar un piso: geometría, estado, atendidas y la conexión en vivo. */
  async activar(pisoId: number, sede: string): Promise<void> {
    const cambio = this.activa && this.pisoId !== pisoId;
    this.pisoId = pisoId;
    this.error.set(null);
    this.geometria.set(null);
    if (!this.activa) {
      this.activa = true;
      this.reloj = setInterval(() => this.ahora.set(Date.now()), TIC_MS);
      this.hub.iniciar(pisoId, this.manejadores);
    } else if (cambio) {
      await this.hub.cambiarPiso(pisoId);
    }
    await Promise.all([this.cargarGeometria(pisoId, sede), this.cargarEstado(), this.cargarAtendidas()]);
  }

  desactivar(): void {
    if (!this.activa) return;
    this.activa = false;
    clearInterval(this.reloj);
    this.hub.detener();
  }

  // ------------------------------------------------------------------ cargas por HTTP

  private async cargarGeometria(pisoId: number, sede: string): Promise<void> {
    try {
      const g = await firstValueFrom(this.http.get<GeometriaApi>(`${API}/pisos/${pisoId}/geometria`));
      if (pisoId !== this.pisoId) return;   // el usuario ya cambió de piso
      this.geometria.set(geometriaDesdeApi(g, sede));
    } catch (e) {
      this.error.set(mensajeDe(problemaDe(e)));
    }
  }

  /** La foto completa. Se pide al entrar y cada vez que el hub se reconecta. */
  async cargarEstado(): Promise<void> {
    const pisoId = this.pisoId;
    if (pisoId === null) return;
    try {
      const e = await firstValueFrom(this.http.get<EstadoApi>(`${API}/pisos/${pisoId}/estado`));
      if (pisoId !== this.pisoId) return;
      this.zonasPorId.set(new Map(e.zonas.map(z => [z.zonaId, { zonaId: z.zonaId, personas: z.ocupacion, aforo: z.aforo, nivel: nivelDeApi(z.nivel) }])));
      this.abiertas.set(new Map(e.atenciones.map(a => [a.codigo,
        { id: a.id, zonaId: a.zonaActual, estado: a.estado, inicio: Date.parse(a.inicioEn), esperaMin: a.esperaMin, zonaDesde: ms(a.zonaDesdeEn) }])));
      this.kpis.set(e.kpis);
      this.nodosPorId.set(new Map(e.nodos.map(n => [n.nodoId, { codigo: n.codigo, zonaId: n.zonaId, enLinea: n.estado === 'EN_LINEA', ultimoLatido: ms(n.ultimoLatidoEn) }])));
      this.ahora.set(Date.now());
    } catch (e) {
      this.error.set(mensajeDe(problemaDe(e)));
    }
  }

  private async cargarAtendidas(): Promise<void> {
    const pisoId = this.pisoId;
    if (pisoId === null) return;
    try {
      const lista = await firstValueFrom(this.http.get<AtendidaApi[]>(`${API}/pisos/${pisoId}/atendidas-hoy`));
      if (pisoId !== this.pisoId) return;
      this.atendidas.set(lista.map(a => ({
        clave: String(a.id), codigo: a.codigo, estado: 'finalizado' as const, zonaId: null, zonaDesde: null,
        esperaMin: a.esperaMin, finEn: Date.parse(a.finEn), totalMin: a.totalMin,
      })));
    } catch {
      /* «Atendidos» es secundario: si falla, la pestaña queda como estaba */
    }
  }

  // ------------------------------------------------------------------ mensajes del hub

  private manejadores: ManejadoresGemelo = {
    ocupacionZona: m => this.zonasPorId.update(z => new Map(z).set(m.zonaId,
      { zonaId: m.zonaId, personas: m.ocupacion, aforo: m.aforo, nivel: nivelDeApi(m.nivel) })),
    atencion: m => {
      const abiertas = new Map(this.abiertas());
      if (m.estado === 'FINALIZADA') {
        abiertas.delete(m.codigo);
        this.cargarAtendidas();   // devolvió la tarjeta (o se anuló): «Atendidos» cambia
      } else {
        abiertas.set(m.codigo, { id: m.id, zonaId: m.zonaActual, estado: m.estado, inicio: Date.parse(m.inicioEn), esperaMin: m.esperaMin, zonaDesde: ms(m.zonaDesdeEn) });
      }
      this.abiertas.set(abiertas);
    },
    kpis: m => this.kpis.set(m),
    nodo: m => this.nodosPorId.update(n => {
      const previo = n.get(m.nodoId);
      return new Map(n).set(m.nodoId, { codigo: m.codigo, zonaId: previo?.zonaId ?? 0, enLinea: m.estado === 'EN_LINEA', ultimoLatido: ms(m.ultimoLatidoEn) });
    }),
    reconectado: () => { this.cargarEstado(); this.cargarAtendidas(); },
  };
}
