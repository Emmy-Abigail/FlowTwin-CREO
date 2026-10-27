import { Injectable, computed, inject, signal } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';
import { consulta, espera, total } from '../../core/datos/calculos';
import { ClinicaStore, EstadoPrevio } from '../../core/datos/clinica.store';
import { EstadoTarjeta, TipoOperacion, TonoChip } from '../../core/datos/modelos';
import { ToastService } from '../../core/ui/toast.service';
import { duracion, hhmm, minutos, pad2 } from '../../core/utils/tiempo';

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
  desinfectar?: string;
}

const DESHACER_MS = 30_000;

export const TONO_OPERACION: Record<TipoOperacion, TonoChip> = {
  Entregada: 'baja', Recibida: '', Desinfectada: 'media', Recuperada: 'media',
};

/**
 * Estado de la pantalla Admisión (P3): escaneo de tarjetas BLE, contadores, listas de
 * pendientes y "Deshacer" durante 30 s. Las operaciones se hacen sobre ClinicaStore,
 * por eso una entrega aparece al instante en el gemelo y en el feed.
 */
@Injectable()
export class AdmisionStore {
  private clinica = inject(ClinicaStore);
  private auth = inject(AuthService);
  private toast = inject(ToastService);

  readonly modo = signal<ModoAdmision>('auto');
  readonly resultado = signal<ResultadoEscaneo | null>(null);
  /** Operación que aún se puede deshacer (null cuando pasan los 30 s). */
  readonly deshacible = signal<EstadoPrevio | null>(null);
  private temporizador: ReturnType<typeof setTimeout> | null = null;

  private conEstado = (estado: EstadoTarjeta) => this.clinica.tarjetas().filter(t => t.estado === estado);

  readonly contadores = computed(() => ({
    disponibles: this.conEstado('Disponible').length,
    enUso: this.conEstado('En uso').length,
    porDesinfectar: this.conEstado('Por desinfectar').length,
    sinDevolver: this.conEstado('Sin devolver').length,
  }));

  readonly porDesinfectar = computed(() =>
    this.conEstado('Por desinfectar').map(t => ({ id: t.id, desde: t.desde ? hhmm(t.desde) : '—' })));

  readonly pendientes = computed(() => {
    const ahora = this.clinica.ahora();
    return this.conEstado('Sin devolver').map(t => ({
      id: t.id, atencionId: t.atencionId ?? '',
      sinLecturas: duracion(t.desde ? minutos(ahora - t.desde) : null),
    }));
  });

  readonly operaciones = computed(() => this.clinica.operaciones().slice(0, 7).map(o => ({
    ...o, hora: hhmm(o.t), tono: TONO_OPERACION[o.tipo],
  })));

  /** Tarjetas de ejemplo para la caja "Ayuda de demo". En uso: primero las de pacientes que llevan más tiempo. */
  readonly ayuda = computed(() => ({
    disponibles: this.conEstado('Disponible').slice(0, 8).map(t => t.id),
    enUso: this.conEstado('En uso').sort((a, b) => (a.desde ?? 0) - (b.desde ?? 0)).slice(0, 8).map(t => t.id),
    porDesinfectar: this.conEstado('Por desinfectar').map(t => t.id),
  }));

  alternarModo(modo: Exclude<ModoAdmision, 'auto'>): void {
    this.modo.update(actual => (actual === modo ? 'auto' : modo));
  }

  /** Acepta "T-07", "t7", "T 7" o solo "7". Devuelve true si la operación salió bien. */
  escanear(texto: string): boolean {
    const codigo = normalizarCodigo(texto);
    if (!codigo) return false;
    const r = this.procesar(codigo);
    this.resultado.set(r);
    return r.tipo !== 'error';
  }

  private procesar(codigo: string): ResultadoEscaneo {
    const quien = this.auth.actor();
    const t = this.clinica.tarjeta(codigo);
    const modo = this.modo();

    if (!t) {
      return { tipo: 'error', titulo: `Tarjeta ${codigo} no registrada`, texto: 'Pide al Administrador que la registre en Dispositivos.' };
    }
    if (t.estado === 'Baja') {
      return { tipo: 'error', titulo: `La tarjeta ${t.id} está dada de baja`, texto: 'Use otra tarjeta disponible.' };
    }
    if (t.estado === 'Por desinfectar') {
      return {
        tipo: 'error', titulo: 'Esta tarjeta aún no está desinfectada', desinfectar: t.id,
        texto: `${t.id} se devolvió a las ${t.desde ? hhmm(t.desde) : '—'}. Desinféctela con alcohol al 70% antes de entregarla.`,
      };
    }
    if (t.estado === 'Sin devolver' || t.estado === 'Extraviada') {
      this.recordar({ tarjeta: t });
      this.clinica.recuperarTarjeta(t.id, quien);
      return { tipo: 'info', titulo: `Tarjeta ${t.id} recuperada`, texto: 'Pasa a «Por desinfectar». Desinféctela antes de volver a entregarla.' };
    }
    if (t.estado === 'Disponible') {
      if (modo === 'recibir') {
        return { tipo: 'error', titulo: `${t.id} no está en uso`, texto: 'Esta tarjeta está disponible: no hay nada que recibir. Para entregarla, vuelva al modo automático.' };
      }
      const a = this.clinica.entregarTarjeta(t.id, quien);
      this.recordar({ tarjeta: t, atencionCreadaId: a.id });
      return {
        tipo: 'ok', antetitulo: `Tarjeta ${t.id} entregada · ${hhmm(a.inicio)}`, grande: a.id,
        texto: 'Lea el código en voz alta al paciente. El recorrido y el tiempo de espera ya empezaron.',
      };
    }
    // En uso
    if (modo === 'entregar') {
      return { tipo: 'error', titulo: `${t.id} ya está en uso (${t.atencionId})`, texto: 'Para entregarla primero hay que recibirla. Vuelva al modo automático.' };
    }
    const antes = this.clinica.atenciones().find(a => a.id === t.atencionId);
    const a = this.clinica.recibirTarjeta(t.id, quien);
    if (!a) return { tipo: 'error', titulo: `No se pudo recibir ${t.id}`, texto: 'La tarjeta no tiene una atención abierta.' };
    this.recordar({ tarjeta: t, atencion: antes });
    const ahora = this.clinica.ahora();
    return {
      tipo: 'ok', antetitulo: `Tarjeta ${t.id} recibida · recorrido terminado`, grande: a.id,
      tiempos: { espera: duracion(espera(a)), consulta: duracion(consulta(a, ahora)), total: duracion(total(a, ahora)) },
      texto: 'La tarjeta pasó a «Por desinfectar».',
    };
  }

  marcarDesinfectada(tarjetaId: string): void {
    this.clinica.marcarDesinfectada(tarjetaId, this.auth.actor());
    this.resultado.set({ tipo: 'ok', titulo: `${tarjetaId} desinfectada`, texto: 'Ya está disponible para entregar.' });
    this.toast.mostrar(`${tarjetaId} disponible`);
  }

  marcarExtraviada(tarjetaId: string): void {
    this.clinica.marcarExtraviada(tarjetaId, this.auth.actor());
    this.toast.mostrar(`${tarjetaId} marcada como extraviada`, 'warn');
  }

  deshacer(): void {
    const previo = this.deshacible();
    if (!previo) return;
    this.clinica.revertir(previo, this.auth.actor());
    this.olvidar();
    this.resultado.set({ tipo: 'info', titulo: 'Operación deshecha', texto: 'Todo volvió al estado anterior.' });
  }

  /** Guarda cómo estaba todo para poder deshacer durante 30 s. */
  private recordar(previo: EstadoPrevio): void {
    this.olvidar();
    this.deshacible.set(previo);
    this.temporizador = setTimeout(() => this.deshacible.set(null), DESHACER_MS);
  }

  private olvidar(): void {
    if (this.temporizador) clearTimeout(this.temporizador);
    this.deshacible.set(null);
  }
}

/** "7", "t7", "T 7" o "T-07" → "T-07". Otros textos se devuelven en mayúsculas tal cual. */
export function normalizarCodigo(texto: string): string {
  const limpio = texto.trim().toUpperCase();
  const m = limpio.match(/^T?\s*-?\s*(\d{1,3})$/);
  return m ? `T-${pad2(+m[1])}` : limpio;
}
