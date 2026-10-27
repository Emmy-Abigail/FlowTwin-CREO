import { Injectable, inject } from '@angular/core';
import { Aleatorio } from '../utils/aleatorio';
import { minutos } from '../utils/tiempo';
import { esperaCorriendo, zonaActual } from './calculos';
import { ClinicaStore } from './clinica.store';
import { ESPERA_CRITICA_MIN, HORA_APERTURA, LLEGADAS_POR_HORA, PACIENTES_POR_PESO, ZONAS, ZONAS_CON_CUPO } from './clinica.config';
import { GeometriaService } from './geometria.service';
import { Actor } from './modelos';
import { crearRuta } from './recorrido';

/** Quien firma en la auditoría las acciones automáticas de la simulación. */
const RECEPCION_SIMULADA: Actor = { nombre: 'Recepción (simulación)', rol: 'Operador' };

/**
 * Simula lo que en el sistema real llegará por SignalR desde los nodos ESP32:
 * pacientes que llegan, cambian de zona y devuelven su tarjeta.
 *
 * Está aislado a propósito: para conectar el backend se reemplaza este servicio por uno
 * que escuche el hub de SignalR y llame a los mismos métodos de ClinicaStore
 * (moverAtencion, entregarTarjeta, recibirTarjeta…). Las pantallas no cambian.
 */
@Injectable({ providedIn: 'root' })
export class SimuladorService {
  /** Cada 5 s reales pasa 1 minuto en la clínica. */
  static readonly INTERVALO_MS = 5000;

  private store = inject(ClinicaStore);
  private geometria = inject(GeometriaService);
  private rnd = new Aleatorio(20261026);
  private temporizador: ReturnType<typeof setInterval> | null = null;

  iniciar(): void {
    this.temporizador ??= setInterval(() => this.tick(), SimuladorService.INTERVALO_MS);
  }

  detener(): void {
    if (this.temporizador) clearInterval(this.temporizador);
    this.temporizador = null;
  }

  /** Un minuto de la clínica. */
  tick(): void {
    if (!this.store.simulacionActiva()) return;
    this.store.avanzarReloj(1);
    this.avanzarPacientes();
    this.llegadas();
    this.desinfeccionAutomatica();
    this.revisarAlertas();
    this.store.latidoNodos();
  }

  private avanzarPacientes(): void {
    const ahora = this.store.ahora();
    for (const a of this.store.activas()) {
      // Pacientes entregados desde Admisión aún no tienen ruta: se les asigna una.
      const ruta = a.ruta ?? crearRuta(this.rnd);
      const paso = a.paso ?? 0;
      const actual = a.eventos.at(-1)!;
      if (!a.ruta) this.store.actualizarAtencion(a.id, { ruta, paso });
      if (minutos(ahora - actual.entrada) < ruta[paso].minutos) continue;

      const siguiente = ruta[paso + 1];
      if (!siguiente) {
        this.store.recibirTarjeta(a.tarjetaId, RECEPCION_SIMULADA);
      } else if (this.hayCupo(siguiente.zonaId)) {
        this.store.moverAtencion(a.id, siguiente.zonaId, { ruta, paso: paso + 1 });
      }
      // Si no hay cupo, el paciente sigue esperando donde está.
    }
  }

  private hayCupo(zonaId: number): boolean {
    if (!ZONAS_CON_CUPO.includes(zonaId)) return true;
    const zona = this.geometria.zona(zonaId);
    return !zona || this.store.enZona(zonaId).length < zona.aforo;
  }

  /** Llegan pacientes según la hora: más entre 9 y 11 y entre 15 y 16. */
  private llegadas(): void {
    const hora = new Date(this.store.ahora()).getHours() - HORA_APERTURA;
    const peso = LLEGADAS_POR_HORA[hora] ?? 0;
    const probabilidad = Math.min(0.9, (peso * PACIENTES_POR_PESO) / 60);
    const disponibles = this.store.tarjetas().filter(t => t.estado === 'Disponible');
    if (disponibles.length && this.rnd.siguiente() < probabilidad) {
      this.store.entregarTarjeta(this.rnd.elegir(disponibles).id, RECEPCION_SIMULADA);
    }
  }

  /** Si quedan pocas tarjetas, la recepción desinfecta la más antigua. */
  private desinfeccionAutomatica(): void {
    const tarjetas = this.store.tarjetas();
    if (tarjetas.filter(t => t.estado === 'Disponible').length >= 4) return;
    const ahora = this.store.ahora();
    const lista = tarjetas.find(t => t.estado === 'Por desinfectar' && t.desde != null && minutos(ahora - t.desde) > 8);
    if (lista) this.store.marcarDesinfectada(lista.id, RECEPCION_SIMULADA);
  }

  private revisarAlertas(): void {
    const sala = this.geometria.zona(ZONAS.sala);
    const enSala = this.store.enZona(ZONAS.sala).length;
    if (sala && enSala > sala.aforo) {
      this.store.alertar('alta', 'Aforo superado en Sala de espera', `${enSala}/${sala.aforo} · Piso 1`, '/gemelo');
    }
    const ahora = this.store.ahora();
    for (const a of this.store.activas()) {
      const z = zonaActual(a);
      if ((z === ZONAS.sala || z === ZONAS.admision) && esperaCorriendo(a, ahora) > ESPERA_CRITICA_MIN) {
        this.store.alertar('media', `Espera de ${a.id} supera ${ESPERA_CRITICA_MIN} min`, 'Sala de espera · Piso 1', '/gemelo');
      }
    }
  }
}
