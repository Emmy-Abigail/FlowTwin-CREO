import { HttpClient, HttpParams } from '@angular/common/http';
import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API, mensajeDe, problemaDe } from '../../core/api/problema';
import { duracion, hhmm } from '../../core/utils/tiempo';
import {
  AVISO_SIN_TARJETA, Aviso, Confirmacion, EscaneoApi, TarjetaApi, VistaPreviaApi, avisoDeProblema, confirmacionDe, vistaPreviaDe,
} from './presentacion-admision';

// ---- Respuestas de la API (sección API → Atenciones e ingesta)
interface PendienteApi { tarjetaId: number; codigo: string; desde: string | null; atencionCodigo: string | null; }
interface OperacionApi { fecha: string; tipo: string; tarjeta: string; atencion: string | null; totalMin: number | null; }
interface ResumenApi {
  contadores: Record<string, number>;
  porDesinfectar: PendienteApi[];
  sinDevolver: PendienteApi[];
  ultimasOperaciones: OperacionApi[];
}

/** Nombre y color de la etiqueta de cada operación en «Últimas operaciones». */
const OPERACIONES: Record<string, { texto: string; tono: string }> = {
  ENTREGADA: { texto: 'Entrega', tono: 'verde' },
  RECIBIDA: { texto: 'Devolución', tono: 'celeste' },
  DESINFECTADA: { texto: 'Desinfectada', tono: 'ambar' },
  RECUPERADA: { texto: 'Recuperada', tono: 'ambar' },
  ANULADA: { texto: 'Anulada', tono: 'gris' },
  SIN_DEVOLVER: { texto: 'Sin devolver', tono: 'rojo' },
  EXTRAVIADA: { texto: 'Extraviada', tono: 'rojo' },
  BAJA: { texto: 'De baja', tono: 'gris' },
  REGISTRADA: { texto: 'Registrada', tono: 'gris' },
};
/** Cuántas operaciones caben sin scroll debajo de la confirmación. */
const OPERACIONES_VISIBLES = 6;

/** Cada cuánto se actualiza el monitoreo: lo cambian también otros operadores y el cierre automático. */
const REFRESCO_MS = 15_000;

/**
 * Estado de Admisión (P3) contra la API real. El flujo del mostrador:
 * 1. el lector escribe el QR → previsualizar(): qué va a pasar (lo decide el servidor, sin registrar);
 * 2. el operador pulsa Registrar → registrar(): entrega, devolución o recuperación (F-06, F-08, F-10);
 * 3. durante 30 s puede deshacer la entrega (RN-04).
 * Además: desinfectar (F-09), extraviar (F-10) y el resumen para el monitoreo.
 */
@Injectable()
export class AdmisionStore {
  private http = inject(HttpClient);

  /** Qué va a pasar con la tarjeta leída (debajo del campo, antes de registrar). */
  readonly vistaPrevia = signal<{ texto: string; aviso: Aviso } | null>(null);
  readonly consultando = signal(false);
  /** Lo último que quedó registrado (la caja de color, compacta). */
  readonly confirmacion = signal<Confirmacion | null>(null);
  readonly registrando = signal(false);
  /** Entrega que aún se puede deshacer (null cuando pasan los 30 s). */
  readonly deshacible = signal<{ atencionId: number; desde: number; hasta: number } | null>(null);
  private resumen = signal<ResumenApi | null>(null);
  private temporizadorDeshacer: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    this.cargar();
    const intervalo = setInterval(() => this.cargar(), REFRESCO_MS);
    inject(DestroyRef).onDestroy(() => {
      clearInterval(intervalo);
      if (this.temporizadorDeshacer) clearTimeout(this.temporizadorDeshacer);
    });
  }

  /** Las tarjetas por estado, para la barra «Tarjetas de la sede». */
  readonly contadores = computed(() => {
    const c = this.resumen()?.contadores ?? {};
    const estados = [
      { clave: 'disponibles', texto: 'disponibles', tono: 'verde', n: c['DISPONIBLE'] ?? 0 },
      { clave: 'enUso', texto: 'en uso', tono: 'negro', n: c['EN_USO'] ?? 0 },
      { clave: 'porDesinfectar', texto: 'por desinfectar', tono: 'ambar', n: c['POR_DESINFECTAR'] ?? 0 },
      { clave: 'sinDevolver', texto: 'sin devolver', tono: 'rojo', n: c['SIN_DEVOLVER'] ?? 0 },
    ];
    return { estados, total: estados.reduce((s, e) => s + e.n, 0) };
  });

  /**
   * Lo que pide una acción del operador, por grupos (nunca un bloque por tarjeta): las devueltas sin
   * desinfectar y las que no volvieron. De la más antigua a la más reciente: la que más espera, primero.
   */
  readonly porDesinfectar = computed(() => ordenarPorAntiguedad((this.resumen()?.porDesinfectar ?? []).map(t => ({
    id: t.tarjetaId, codigo: t.codigo, desde: t.desde ? Date.parse(t.desde) : null,
  }))));

  readonly sinDevolver = computed(() => ordenarPorAntiguedad((this.resumen()?.sinDevolver ?? []).map(t => ({
    id: t.tarjetaId, codigo: t.codigo, desde: t.desde ? Date.parse(t.desde) : null, atencion: t.atencionCodigo ?? '—',
  }))));

  readonly operaciones = computed(() => (this.resumen()?.ultimasOperaciones ?? []).slice(0, OPERACIONES_VISIBLES).map(o => ({
    fecha: Date.parse(o.fecha),
    hora: hhmm(Date.parse(o.fecha)),
    tipo: OPERACIONES[o.tipo]?.texto ?? o.tipo,
    tono: OPERACIONES[o.tipo]?.tono ?? 'gris',
    tarjeta: o.tarjeta, atencion: o.atencion,
    total: o.totalMin !== null ? `${duracion(o.totalMin)} en total` : null,
  })));

  async cargar(): Promise<void> {
    try {
      this.resumen.set(await firstValueFrom(this.http.get<ResumenApi>(`${API}/admision/resumen`)));
    } catch {
      /* el error de sesión lo maneja el interceptor; el monitoreo conserva lo último que se cargó */
    }
  }

  /** Paso 1: qué pasaría con esta tarjeta. No registra nada. */
  async previsualizar(texto: string): Promise<void> {
    const qr = texto.trim();
    if (!qr) { this.vistaPrevia.set(null); return; }
    this.consultando.set(true);
    try {
      const v = await firstValueFrom(this.http.get<VistaPreviaApi>(`${API}/admision/vista-previa`, { params: new HttpParams().set('qr', qr) }));
      this.vistaPrevia.set({ texto: qr, aviso: vistaPreviaDe(v, Date.now()) });
    } catch (e) {
      const p = problemaDe(e);
      this.vistaPrevia.set({ texto: qr, aviso: avisoDeProblema(p.codigo, mensajeDe(p), p.datos) });
    } finally {
      this.consultando.set(false);
    }
  }

  /** R8: se leyó otra tarjeta. Su vista previa y la confirmación anterior ya no valen. */
  nuevaLectura(): void {
    this.vistaPrevia.set(null);
    this.confirmacion.set(null);
    this.olvidarDeshacer();
  }

  /** R7 */
  avisarSinTarjeta(): void {
    this.vistaPrevia.set({ texto: '', aviso: AVISO_SIN_TARJETA });
  }

  /**
   * Paso 2: registrar (la página ya revisó la vista previa: R2, R3, R7). Devuelve true si quedó
   * registrado, para el sonido y para limpiar el campo. R9: si ya se está registrando, no hace nada.
   */
  async registrar(texto: string): Promise<boolean> {
    const qr = texto.trim();
    if (this.registrando() || !qr) return false;
    this.registrando.set(true);
    try {
      const r = await firstValueFrom(this.http.post<EscaneoApi>(`${API}/admision/escaneos`, { codigoQr: qr, modo: 'AUTO' }));
      this.confirmacion.set(confirmacionDe(r));
      this.vistaPrevia.set(null);
      this.recordarDeshacer(r);
      return true;
    } catch (e) {
      const p = problemaDe(e);
      this.vistaPrevia.set({ texto: qr, aviso: avisoDeProblema(p.codigo, mensajeDe(p), p.datos) });
      return false;
    } finally {
      this.registrando.set(false);
      await this.cargar();
    }
  }

  async deshacer(): Promise<void> {
    const pendiente = this.deshacible();
    if (!pendiente) return;
    try {
      const t = await firstValueFrom(this.http.post<TarjetaApi>(`${API}/admision/atenciones/${pendiente.atencionId}/deshacer`, {}));
      this.confirmacion.set({ tipo: 'anulada', titulo: `Entrega de ${t.codigo} deshecha`, detalle: 'La atención quedó anulada y la tarjeta está disponible.' });
    } catch (e) {
      this.confirmacion.set({ tipo: 'error', titulo: 'No se pudo deshacer', detalle: mensajeDe(problemaDe(e)) });
    }
    this.olvidarDeshacer();
    await this.cargar();
  }

  /** F-09 por tandas: todas o ninguna (el servidor lo garantiza en una transacción). */
  async desinfectar(tarjetas: { id: number; codigo: string }[]): Promise<void> {
    try {
      await firstValueFrom(this.http.post(`${API}/tarjetas/desinfectar`, { tarjetaIds: tarjetas.map(t => t.id) }));
      this.confirmacion.set({
        tipo: 'desinfectada',
        titulo: tarjetas.length === 1 ? `${tarjetas[0].codigo} desinfectada` : `${tarjetas.length} tarjetas desinfectadas`,
        detalle: tarjetas.length === 1 ? 'Ya se puede entregar.' : 'Ya se pueden entregar.',
      });
      // Si estaba la tarjeta que se iba a registrar, su vista previa ya no vale: se vuelve a consultar
      const v = this.vistaPrevia();
      if (v?.aviso.desinfectar && tarjetas.some(t => t.id === v.aviso.desinfectar!.id)) await this.previsualizar(v.texto);
    } catch (e) {
      this.confirmacion.set({ tipo: 'error', titulo: 'No se desinfectó ninguna', detalle: mensajeDe(problemaDe(e)) });
    }
    await this.cargar();
  }

  /** F-10 por tandas. */
  async extraviar(tarjetas: { id: number; codigo: string }[]): Promise<void> {
    try {
      await firstValueFrom(this.http.post(`${API}/tarjetas/extraviar`, { tarjetaIds: tarjetas.map(t => t.id) }));
      this.confirmacion.set({
        tipo: 'extraviada',
        titulo: tarjetas.length === 1 ? `${tarjetas[0].codigo} marcada como extraviada` : `${tarjetas.length} tarjetas marcadas como extraviadas`,
        detalle: 'Si aparece, escanéala y pasará a desinfección.',
      });
    } catch (e) {
      this.confirmacion.set({ tipo: 'error', titulo: 'No se marcó ninguna', detalle: mensajeDe(problemaDe(e)) });
    }
    await this.cargar();
  }

  /** RN-04: el botón Deshacer vive los 30 s que dice la API (deshacerHasta). */
  private recordarDeshacer(r: EscaneoApi): void {
    this.olvidarDeshacer();
    if (r.operacion !== 'ENTREGA' || !r.deshacerHasta || !r.atencion) return;
    const hasta = Date.parse(r.deshacerHasta);
    this.deshacible.set({ atencionId: r.atencion.id, desde: Date.now(), hasta });
    this.temporizadorDeshacer = setTimeout(() => this.deshacible.set(null), Math.max(0, hasta - Date.now()));
  }

  private olvidarDeshacer(): void {
    if (this.temporizadorDeshacer) clearTimeout(this.temporizadorDeshacer);
    this.deshacible.set(null);
  }
}

/** La que lleva más tiempo esperando, primero; las sin fecha, al final. */
function ordenarPorAntiguedad<T extends { desde: number | null }>(lista: T[]): T[] {
  return [...lista].sort((a, b) => (a.desde ?? Infinity) - (b.desde ?? Infinity));
}
