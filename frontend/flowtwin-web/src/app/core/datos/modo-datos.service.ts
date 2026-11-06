import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API } from '../api/problema';
import { AuthService } from '../auth/auth.service';
import { SimuladorService } from './simulador.service';

interface ConfiguracionApi {
  simulacionDisponible: boolean;
}

/**
 * ¿Datos reales o simulación? (ADR-22). Por defecto, siempre reales. La simulación es temporal, para
 * presentar el sistema antes de que lleguen los nodos:
 * - solo existe si el servidor lo permite (Demo:SimulacionDisponible, falso en la clínica);
 * - solo la usan Administrador y Gerencia: la pantalla del Operador es operativa, nunca ficticia;
 * - no se recuerda: al recargar la página se vuelve a los datos reales.
 */
@Injectable({ providedIn: 'root' })
export class ModoDatosService {
  private http = inject(HttpClient);
  private auth = inject(AuthService);
  private simulador = inject(SimuladorService);

  private disponible = signal(false);
  private readonly simulando = signal(false);

  /** true mientras se muestran datos ficticios. Las pantallas lo usan para decidir qué mostrar. */
  readonly simulacion = this.simulando.asReadonly();

  readonly puedeSimular = computed(() => {
    const rol = this.auth.rol();
    return this.disponible() && (rol === 'Administrador' || rol === 'Gerencia');
  });

  /** Pregunta al servidor si en esta instalación existe la simulación. Si falla, no existe. */
  async cargar(): Promise<void> {
    try {
      const c = await firstValueFrom(this.http.get<ConfiguracionApi>(`${API}/configuracion`));
      this.disponible.set(c.simulacionDisponible === true);
    } catch {
      this.disponible.set(false);
    }
  }

  activar(): void {
    if (!this.puedeSimular()) return;
    this.simulando.set(true);
    this.simulador.iniciar();
  }

  salir(): void {
    this.simulando.set(false);
    this.simulador.detener();
  }
}
