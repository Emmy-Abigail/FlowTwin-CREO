import { HttpClient } from '@angular/common/http';
import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API, mensajeDe, problemaDe } from '../../core/api/problema';
import { TonoChip } from '../../core/datos/modelos';
import { ToastService } from '../../core/ui/toast.service';
import { duracion, hhmm, minutos } from '../../core/utils/tiempo';

/** Modo de Admisión. En "auto" el sistema deduce si es entrega o devolución. */
export type ModoAdmision = 'auto' | 'entregar' | 'recibir';

/** Lo que muestra el recuadro de confirmación (M1) después de escanear. */
export interface ResultadoEscaneo {
  tipo: 'ok' | 'info' | 'error';
  /** Línea pequeña en mayúsculas, por ejemplo "Tarjeta T-07 entregada · 10:42". */
  antetitulo?: string;
  /** El ID de la atención en letra enorme, para leerlo en voz alta. */
  grande?: string;
  titulo?: string;
  texto: string;
  tiempos?: { espera: string; consulta: string; total: string };
  /** Si viene, se muestra el botón "Marcar desinfectada" para esa tarjeta. */
  desinfectar?: { id: number; codigo: string };
}

// ---- Respuestas de la API (sección API → Atenciones e ingesta)
interface TarjetaApi { id: number; codigo: string; estado: string; estadoNombre: string; desde?: string | null; }
interface AtencionApi { id: number; codigo: string; inicioEn: string; finEn: string | null; esperaMin: number | null; consultaMin: number | null; totalMin: number | null; }
interface EscaneoApi { operacion: 'ENTREGA' | 'DEVOLUCION' | 'RECUPERADA'; tarjeta: TarjetaApi; atencion: AtencionApi | null; deshacerHasta: string | null; }
interface PendienteApi { tarjetaId: number; codigo: string; desde: string | null; atencionCodigo: string | null; }
interface OperacionApi { fecha: string; tipo: string; tarjeta: string; atencion: string | null; totalMin: number | null; }
interface ResumenApi {
  contadores: Record<string, number>;
  porDesinfectar: PendienteApi[];
  sinDevolver: PendienteApi[];
  ultimasOperaciones: OperacionApi[];
}

const MODO_API: Record<ModoAdmision, string> = { auto: 'AUTO', entregar: 'ENTREGA', recibir: 'DEVOLUCION' };

/** Nombre y color de cada operación en "Últimas operaciones". */
const OPERACIONES: Record<string, { texto: string; tono: TonoChip }> = {
  ENTREGADA: { texto: 'Entregada', tono: 'baja' },
  RECIBIDA: { texto: 'Recibida', tono: '' },
  DESINFECTADA: { texto: 'Desinfectada', tono: 'media' },
  RECUPERADA: { texto: 'Recuperada', tono: 'media' },
  ANULADA: { texto: 'Anulada', tono: 'nd' },
  SIN_DEVOLVER: { texto: 'Sin devolver', tono: 'alta' },
  EXTRAVIADA: { texto: 'Extraviada', tono: 'alta' },
  BAJA: { texto: 'De baja', tono: 'dark' },
  REGISTRADA: { texto: 'Registrada', tono: 'nd' },
};

/** Cada cuánto se actualiza el panel derecho mientras no hay tiempo real (SignalR llega en el Avance 2). */
const REFRESCO_MS = 15_000;

/**
 * Estado de la pantalla Admisión (P3) contra la API real: escanear (F-06, F-08), deshacer (RN-04),
 * desinfectar (F-09), extraviar (F-10) y el resumen. Las reglas las aplica el backend; aquí solo
 * se muestran sus respuestas.
 */
@Injectable()
export class AdmisionStore {
  private http = inject(HttpClient);
  private toast = inject(ToastService);

  readonly modo = signal<ModoAdmision>('auto');
  readonly resultado = signal<ResultadoEscaneo | null>(null);
  readonly ocupado = signal(false);
  /** Entrega que aún se puede deshacer (null cuando pasan los 30 s). */
  readonly deshacible = signal<{ atencionId: number; hasta: number } | null>(null);
  private resumen = signal<ResumenApi | null>(null);
  private tarjetas = signal<TarjetaApi[]>([]);
  private temporizadorDeshacer: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    this.cargar();
    const intervalo = setInterval(() => this.cargar(), REFRESCO_MS);
    inject(DestroyRef).onDestroy(() => {
      clearInterval(intervalo);
      if (this.temporizadorDeshacer) clearTimeout(this.temporizadorDeshacer);
    });
  }

  readonly contadores = computed(() => {
    const c = this.resumen()?.contadores ?? {};
    return {
      disponibles: c['DISPONIBLE'] ?? 0,
      enUso: c['EN_USO'] ?? 0,
      porDesinfectar: c['POR_DESINFECTAR'] ?? 0,
      sinDevolver: c['SIN_DEVOLVER'] ?? 0,
    };
  });

  readonly porDesinfectar = computed(() => (this.resumen()?.porDesinfectar ?? []).map(t => ({
    id: t.tarjetaId, codigo: t.codigo, desde: t.desde ? hhmm(Date.parse(t.desde)) : '—',
  })));

  readonly pendientes = computed(() => (this.resumen()?.sinDevolver ?? []).map(t => ({
    id: t.tarjetaId, codigo: t.codigo, atencion: t.atencionCodigo ?? '—',
    sinLecturas: t.desde ? duracion(minutos(Date.now() - Date.parse(t.desde))) : '—',
  })));

  readonly operaciones = computed(() => (this.resumen()?.ultimasOperaciones ?? []).slice(0, 7).map(o => ({
    hora: hhmm(Date.parse(o.fecha)),
    tipo: OPERACIONES[o.tipo]?.texto ?? o.tipo,
    tono: OPERACIONES[o.tipo]?.tono ?? '',
    tarjeta: o.tarjeta, atencion: o.atencion, totalMin: o.totalMin,
  })));

  /** Tarjetas de ejemplo para la caja "Ayuda de demo". En uso: primero las que llevan más tiempo. */
  readonly ayuda = computed(() => {
    const de = (estado: string) => this.tarjetas().filter(t => t.estado === estado);
    return {
      disponibles: de('DISPONIBLE').slice(0, 8).map(t => t.codigo),
      enUso: de('EN_USO').sort((a, b) => (a.desde ?? '').localeCompare(b.desde ?? '')).slice(0, 8).map(t => t.codigo),
      porDesinfectar: de('POR_DESINFECTAR').map(t => t.codigo),
      sinDevolver: de('SIN_DEVOLVER').map(t => t.codigo),
    };
  });

  alternarModo(modo: Exclude<ModoAdmision, 'auto'>): void {
    this.modo.update(actual => (actual === modo ? 'auto' : modo));
  }

  /** Recarga el panel derecho y la lista de tarjetas. */
  async cargar(): Promise<void> {
    try {
      const [resumen, tarjetas] = await Promise.all([
        firstValueFrom(this.http.get<ResumenApi>(`${API}/admision/resumen`)),
        firstValueFrom(this.http.get<TarjetaApi[]>(`${API}/tarjetas`)),
      ]);
      this.resumen.set(resumen);
      this.tarjetas.set(tarjetas);
    } catch {
      /* el error de sesión lo maneja el interceptor; el panel conserva lo último que se cargó */
    }
  }

  /** Devuelve true si la operación salió bien (para el sonido de éxito). */
  async escanear(texto: string): Promise<boolean> {
    const codigoQr = texto.trim();
    if (!codigoQr || this.ocupado()) return false;
    this.ocupado.set(true);
    try {
      const r = await firstValueFrom(this.http.post<EscaneoApi>(`${API}/admision/escaneos`, { codigoQr, modo: MODO_API[this.modo()] }));
      this.resultado.set(this.resultadoDe(r));
      this.recordarDeshacer(r);
      return true;
    } catch (e) {
      const p = problemaDe(e);
      this.resultado.set(this.errorDe(p.codigo, mensajeDe(p), p.datos));
      return false;
    } finally {
      this.ocupado.set(false);
      await this.cargar();
    }
  }

  async deshacer(): Promise<void> {
    const pendiente = this.deshacible();
    if (!pendiente) return;
    try {
      const t = await firstValueFrom(this.http.post<TarjetaApi>(`${API}/admision/atenciones/${pendiente.atencionId}/deshacer`, {}));
      this.resultado.set({ tipo: 'info', titulo: 'Entrega deshecha', texto: `La atención quedó anulada y ${t.codigo} está disponible otra vez.` });
    } catch (e) {
      this.resultado.set({ tipo: 'error', titulo: 'No se pudo deshacer', texto: mensajeDe(problemaDe(e)) });
    }
    this.olvidarDeshacer();
    await this.cargar();
  }

  async marcarDesinfectada(tarjetaId: number, codigo: string): Promise<void> {
    try {
      await firstValueFrom(this.http.post(`${API}/tarjetas/${tarjetaId}/desinfectar`, {}));
      this.resultado.set({ tipo: 'ok', titulo: `${codigo} desinfectada`, texto: 'Ya está disponible para entregar.' });
      this.toast.mostrar(`${codigo} disponible`);
    } catch (e) {
      this.toast.mostrar(mensajeDe(problemaDe(e)), 'bad');
    }
    await this.cargar();
  }

  async marcarExtraviada(tarjetaId: number, codigo: string): Promise<void> {
    try {
      await firstValueFrom(this.http.post(`${API}/tarjetas/${tarjetaId}/extraviar`, {}));
      this.toast.mostrar(`${codigo} marcada como extraviada`, 'warn');
    } catch (e) {
      this.toast.mostrar(mensajeDe(problemaDe(e)), 'bad');
    }
    await this.cargar();
  }

  private resultadoDe(r: EscaneoApi): ResultadoEscaneo {
    const a = r.atencion;
    switch (r.operacion) {
      case 'ENTREGA':
        return {
          tipo: 'ok', antetitulo: `Tarjeta ${r.tarjeta.codigo} entregada · ${hhmm(Date.parse(a!.inicioEn))}`, grande: a!.codigo,
          texto: 'Lea el código en voz alta al paciente. El recorrido y el tiempo de espera ya empezaron.',
        };
      case 'DEVOLUCION':
        return {
          tipo: 'ok', antetitulo: `Tarjeta ${r.tarjeta.codigo} recibida · recorrido terminado`, grande: a!.codigo,
          tiempos: { espera: duracion(a!.esperaMin), consulta: duracion(a!.consultaMin), total: duracion(a!.totalMin) },
          texto: 'La tarjeta pasó a «Por desinfectar». Espera y consulta se medirán con los nodos (Avance 2).',
        };
      default:
        return { tipo: 'info', titulo: `Tarjeta ${r.tarjeta.codigo} recuperada`, texto: 'Pasa a «Por desinfectar». Desinféctela antes de volver a entregarla.' };
    }
  }

  /** Mensajes del prototipo para cada código de error de la API. */
  private errorDe(codigo: string, mensaje: string, datos?: Record<string, unknown>): ResultadoEscaneo {
    if (codigo === 'RN03_POR_DESINFECTAR') {
      const codigoTarjeta = String(datos?.['tarjeta'] ?? '');
      const desde = datos?.['desde'] ? ` se devolvió a las ${hhmm(Date.parse(String(datos['desde'])))}` : '';
      return {
        tipo: 'error', titulo: 'Esta tarjeta aún no está desinfectada',
        texto: `${codigoTarjeta}${desde}. Desinféctela con alcohol al 70% antes de entregarla.`,
        desinfectar: { id: Number(datos?.['tarjetaId']), codigo: codigoTarjeta },
      };
    }
    return { tipo: 'error', titulo: mensaje, texto: codigo === 'SIN_CONEXION' ? '' : 'Revise el código e intente de nuevo.' };
  }

  /** RN-04: el botón Deshacer vive los 30 s que dice la API (deshacerHasta). */
  private recordarDeshacer(r: EscaneoApi): void {
    this.olvidarDeshacer();
    if (r.operacion !== 'ENTREGA' || !r.deshacerHasta || !r.atencion) return;
    const hasta = Date.parse(r.deshacerHasta);
    this.deshacible.set({ atencionId: r.atencion.id, hasta });
    this.temporizadorDeshacer = setTimeout(() => this.deshacible.set(null), Math.max(0, hasta - Date.now()));
  }

  private olvidarDeshacer(): void {
    if (this.temporizadorDeshacer) clearTimeout(this.temporizadorDeshacer);
    this.deshacible.set(null);
  }
}
