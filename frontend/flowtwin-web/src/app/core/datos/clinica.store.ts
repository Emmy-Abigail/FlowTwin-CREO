import { Injectable, computed, signal } from '@angular/core';
import { inicioDelDia, minutos, MIN } from '../utils/tiempo';
import { zonaActual } from './calculos';
import { PISO_PILOTO, ZONAS } from './clinica.config';
import { crearDatosSemilla, idAtencion } from './datos-semilla';
import {
  Actor, Alerta, Atencion, EntradaAuditoria, MotivoCierre, Nodo, Operacion, PausaZona, Tarjeta, TipoAuditoria,
} from './modelos';

/** Cómo estaban la tarjeta y la atención antes de una operación de Admisión (para deshacerla). */
export interface EstadoPrevio {
  tarjeta: Tarjeta;
  atencion?: Atencion;
  /** Atención que creó la operación (en una entrega): al deshacer se elimina. */
  atencionCreadaId?: string;
}

/**
 * Estado compartido de la clínica: tarjetas, atenciones, nodos, auditoría y alertas.
 * Es la "base de datos" en memoria de la demo. Todas las pantallas leen de aquí, por eso
 * una entrega en Admisión aparece al instante en el feed y en el gemelo.
 *
 * Las listas son inmutables: cada cambio crea un arreglo nuevo con signal.update(),
 * así los computed() que dependen de ellas se recalculan solos.
 */
@Injectable({ providedIn: 'root' })
export class ClinicaStore {
  private semilla = crearDatosSemilla(inicioDelDia(Date.now()));
  private siguienteId = this.semilla.siguienteId;

  /** Reloj de la clínica (simulado: avanza 1 min en cada tick del simulador). */
  readonly ahora = signal(this.semilla.ahora);
  readonly simulacionActiva = signal(true);

  readonly tarjetas = signal(this.semilla.tarjetas);
  readonly atenciones = signal(this.semilla.atenciones);
  readonly historial = signal(this.semilla.historial).asReadonly();
  readonly nodos = signal<Nodo[]>(this.semilla.nodos);
  readonly operaciones = signal(this.semilla.operaciones);
  readonly auditoria = signal(this.semilla.auditoria);
  readonly alertas = signal(this.semilla.alertas);
  readonly pausas = signal<Record<number, PausaZona>>({});

  readonly activas = computed(() => this.atenciones().filter(a => a.estado === 'activa'));

  /** Atenciones activas agrupadas por la zona donde están ahora. */
  readonly porZona = computed(() => {
    const mapa = new Map<number, Atencion[]>();
    for (const a of this.activas()) {
      const z = zonaActual(a);
      if (z != null) mapa.set(z, [...(mapa.get(z) ?? []), a]);
    }
    return mapa;
  });

  readonly alertasSinLeer = computed(() => this.alertas().filter(a => !a.leida).length);

  enZona(zonaId: number): Atencion[] {
    return this.porZona().get(zonaId) ?? [];
  }

  nodoDeZona(zonaId: number): Nodo | undefined {
    return this.nodos().find(n => n.zonaId === zonaId && n.estado !== 'Baja');
  }

  tarjeta(id: string): Tarjeta | undefined {
    return this.tarjetas().find(t => t.id === id);
  }

  // ---------------------------------------------------------------- Admisión

  /** Entrega una tarjeta Disponible: crea la atención y empieza el recorrido. */
  entregarTarjeta(tarjetaId: string, quien: Actor): Atencion {
    const t = this.ahora();
    const a: Atencion = {
      id: idAtencion(this.siguienteId++), tarjetaId, pisoId: PISO_PILOTO, inicio: t, fin: null,
      estado: 'activa', motivo: null, eventos: [{ zonaId: ZONAS.admision, entrada: t, salida: null }],
    };
    this.atenciones.update(lista => [...lista, a]);
    this.cambiarTarjeta(tarjetaId, { estado: 'En uso', atencionId: a.id, desde: t });
    this.registrarOperacion({ t, tipo: 'Entregada', tarjetaId, atencionId: a.id });
    this.auditar('Admisión', 'Entregó tarjeta', tarjetaId, a.id, quien);
    return a;
  }

  /** Recibe una tarjeta En uso: cierra la atención y la tarjeta pasa a Por desinfectar. */
  recibirTarjeta(tarjetaId: string, quien: Actor): Atencion | undefined {
    const tarjeta = this.tarjeta(tarjetaId);
    if (!tarjeta?.atencionId) return undefined;
    const cerrada = this.cerrarAtencion(tarjeta.atencionId, 'Devuelta');
    if (!cerrada) return undefined;
    const totalMin = minutos(cerrada.fin! - cerrada.inicio);
    this.cambiarTarjeta(tarjetaId, { estado: 'Por desinfectar', atencionId: null, desde: this.ahora() });
    this.registrarOperacion({ t: this.ahora(), tipo: 'Recibida', tarjetaId, atencionId: cerrada.id, totalMin });
    this.auditar('Admisión', 'Recibió tarjeta', tarjetaId, `${cerrada.id} · total ${totalMin} min`, quien);
    return cerrada;
  }

  marcarDesinfectada(tarjetaId: string, quien: Actor): void {
    this.cambiarTarjeta(tarjetaId, { estado: 'Disponible', desde: this.ahora() });
    this.registrarOperacion({ t: this.ahora(), tipo: 'Desinfectada', tarjetaId });
    this.auditar('Admisión', 'Marcó desinfectada', tarjetaId, 'Por desinfectar → Disponible', quien);
  }

  /** Una tarjeta Sin devolver o Extraviada que vuelve a aparecer. */
  recuperarTarjeta(tarjetaId: string, quien: Actor): void {
    this.cambiarTarjeta(tarjetaId, { estado: 'Por desinfectar', atencionId: null, desde: this.ahora() });
    this.registrarOperacion({ t: this.ahora(), tipo: 'Recuperada', tarjetaId });
    this.auditar('Admisión', 'Recuperó tarjeta', tarjetaId, '→ Por desinfectar', quien);
    this.marcarAlertasLeidas(tarjetaId);
  }

  marcarExtraviada(tarjetaId: string, quien: Actor): void {
    this.cambiarTarjeta(tarjetaId, { estado: 'Extraviada', desde: this.ahora() });
    this.auditar('Admisión', 'Marcó extraviada', tarjetaId, 'Sin devolver → Extraviada', quien);
    this.marcarAlertasLeidas(tarjetaId);
  }

  /**
   * Deshace una operación de Admisión: deja la tarjeta y la atención como estaban antes.
   * Solo toca esa tarjeta y esa atención, así no se pierde lo que el resto de pacientes
   * avanzó mientras tanto.
   */
  revertir(antes: EstadoPrevio, quien: Actor): void {
    const { tarjeta, atencion, atencionCreadaId } = antes;
    this.tarjetas.update(lista => lista.map(t => (t.id === tarjeta.id ? tarjeta : t)));
    this.atenciones.update(lista => lista
      .filter(a => a.id !== atencionCreadaId)
      .map(a => (atencion && a.id === atencion.id ? atencion : a)));
    this.operaciones.update(lista => {
      const i = lista.findIndex(o => o.tarjetaId === tarjeta.id);
      return i < 0 ? lista : lista.filter((_, k) => k !== i);
    });
    this.auditar('Admisión', 'Deshizo la última operación', tarjeta.id, atencionCreadaId ?? atencion?.id ?? '', quien);
  }

  // ---------------------------------------------------------------- Recorrido

  /** El paciente pasa a otra zona: cierra el evento actual y abre uno nuevo. */
  moverAtencion(id: string, zonaId: number, cambios: Partial<Atencion> = {}): void {
    const t = this.ahora();
    this.atenciones.update(lista => lista.map(a => {
      if (a.id !== id) return a;
      const eventos = a.eventos.map(e => (e.salida == null ? { ...e, salida: t } : e));
      return { ...a, ...cambios, eventos: [...eventos, { zonaId, entrada: t, salida: null }] };
    }));
  }

  actualizarAtencion(id: string, cambios: Partial<Atencion>): void {
    this.atenciones.update(lista => lista.map(a => (a.id === id ? { ...a, ...cambios } : a)));
  }

  cerrarAtencion(id: string, motivo: MotivoCierre): Atencion | undefined {
    const t = this.ahora();
    let cerrada: Atencion | undefined;
    this.atenciones.update(lista => lista.map(a => {
      if (a.id !== id) return a;
      cerrada = {
        ...a, estado: 'cerrada', motivo, fin: t,
        eventos: a.eventos.map(e => (e.salida == null ? { ...e, salida: t } : e)),
      };
      return cerrada;
    }));
    return cerrada;
  }

  // ---------------------------------------------------------------- Reloj, nodos, auditoría y alertas

  avanzarReloj(min: number): void {
    this.ahora.update(t => t + min * MIN);
  }

  latidoNodos(): void {
    const t = this.ahora();
    this.nodos.update(lista => lista.map(n => (n.estado === 'En línea' ? { ...n, ultimoLatido: t } : n)));
  }

  auditar(tipo: TipoAuditoria, accion: string, elemento: string, detalle: string, quien: Actor): void {
    const entrada: EntradaAuditoria = { t: this.ahora(), usuario: quien.nombre, rol: quien.rol, tipo, accion, elemento, detalle };
    this.auditoria.update(lista => [entrada, ...lista]);
  }

  /** Agrega una alerta a la campana, salvo que ya haya una igual sin leer. */
  alertar(nivel: Alerta['nivel'], texto: string, detalle: string, ruta: string): void {
    if (this.alertas().some(a => a.texto === texto && !a.leida)) return;
    const alerta: Alerta = { id: Date.now() + Math.random(), nivel, texto, detalle, t: this.ahora(), ruta, leida: false };
    this.alertas.update(lista => [alerta, ...lista]);
  }

  marcarTodasLeidas(): void {
    this.alertas.update(lista => lista.map(a => ({ ...a, leida: true })));
  }

  private marcarAlertasLeidas(texto: string): void {
    this.alertas.update(lista => lista.map(a => (a.texto.includes(texto) ? { ...a, leida: true } : a)));
  }

  private cambiarTarjeta(id: string, cambios: Partial<Tarjeta>): void {
    this.tarjetas.update(lista => lista.map(t => (t.id === id ? { ...t, ...cambios } : t)));
  }

  private registrarOperacion(op: Operacion): void {
    this.operaciones.update(lista => [op, ...lista].slice(0, 12));
  }
}
