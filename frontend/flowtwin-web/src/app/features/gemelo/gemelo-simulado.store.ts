import { Injectable, computed, inject, signal } from '@angular/core';
import { consulta, espera, esperaCorriendo, nivelOcupacion, total, zonaActual } from '../../core/datos/calculos';
import { ClinicaStore } from '../../core/datos/clinica.store';
import { GeometriaService } from '../../core/datos/geometria.service';
import { inicioDelDia, promedio } from '../../core/utils/tiempo';
import { EstadoConexion, FuenteGemelo, KpisVista, NodoVista, PacienteVista, ZonaVista } from './modelo-gemelo';

/**
 * Fuente SIMULADA del gemelo, solo para presentar al sponsor (ADR-22). Traduce el simulador al mismo
 * modelo que la fuente real, con las mismas reglas (RN-13: Tránsito y Salida sin semáforo). Cuando
 * llegue el hardware, se borra este archivo y nada más cambia.
 */
@Injectable()
export class GemeloSimuladoStore implements FuenteGemelo {
  private clinica = inject(ClinicaStore);
  private geo = inject(GeometriaService);

  readonly geometria = this.geo.piso;
  readonly conexion = signal<EstadoConexion>('simulacion').asReadonly();
  readonly error = signal<string | null>(null).asReadonly();
  readonly ahora = this.clinica.ahora;

  private sinDatos(zonaId: number): boolean {
    const nodo = this.clinica.nodoDeZona(zonaId);
    return zonaId in this.clinica.pausas() || (nodo != null && nodo.estado !== 'En línea');
  }

  readonly zonas = computed<ZonaVista[]>(() => this.geo.zonas().map(z => {
    const personas = this.clinica.enZona(z.id).length;
    const sinAforo = z.tipo === 'TRANSITO' || z.tipo === 'SALIDA';
    return {
      zonaId: z.id, personas, aforo: sinAforo ? null : z.aforo,
      nivel: sinAforo ? 'ninguno' : nivelOcupacion(personas, z, this.sinDatos(z.id)),
    };
  }));

  private tipoDe = computed(() => new Map(this.geo.zonas().map(z => [z.id, z.tipo])));

  readonly pacientes = computed<PacienteVista[]>(() => {
    const ahora = this.ahora();
    const hoy0 = inicioDelDia(ahora);
    return this.clinica.atenciones().filter(a => a.inicio >= hoy0).map(a => {
      if (a.estado === 'cerrada') {
        return { clave: a.id, codigo: a.id, estado: 'finalizado' as const, zonaId: null, zonaDesde: null, esperaMin: espera(a) ?? 0, finEn: a.fin!, totalMin: total(a, ahora) };
      }
      const z = zonaActual(a);
      const esp = espera(a);
      const estado = z !== null && this.tipoDe().get(z) === 'SERVICIO' ? 'en-consulta' as const : esp === null ? 'esperando' as const : 'atendido' as const;
      return { clave: a.id, codigo: a.id, estado, zonaId: z, zonaDesde: a.eventos.at(-1)?.entrada ?? null, esperaMin: esp ?? esperaCorriendo(a, ahora) };
    });
  });

  readonly kpis = computed<KpisVista>(() => {
    const ahora = this.ahora();
    const hoy = this.clinica.atenciones().filter(a => a.inicio >= inicioDelDia(ahora));
    return {
      enClinica: this.clinica.activas().length,
      enEspera: this.pacientes().filter(p => p.estado === 'esperando').length,
      esperaPromMin: promedio(hoy.map(espera)),
      consultaPromMin: promedio(hoy.map(a => consulta(a, ahora))),
    };
  });

  readonly nodos = computed<NodoVista[]>(() => this.clinica.nodos().map(n => ({
    codigo: n.codigo, zonaId: n.zonaId, enLinea: n.estado === 'En línea', ultimoLatido: n.ultimoLatido,
  })));
}
