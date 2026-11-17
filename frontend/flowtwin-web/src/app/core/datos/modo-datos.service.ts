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
 * - la pueden activar todos los roles (Operador, Gerencia y Administrador), para mostrarle al sponsor
 *   cómo se ve cada pantalla; Admisión siempre es real, porque ahí se entregan tarjetas de verdad;
 * - no se recuerda: al recargar la página se vuelve a los datos reales;
 * - salir y volver a entrar retoma donde quedó; «Reiniciar simulación» empieza el día de cero.
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

  readonly puedeSimular = computed(() => this.disponible() && this.auth.rol() !== null);

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

  /** «Reiniciar simulación»: vuelve a las 07:00 con la clínica vacía, sin salir de la simulación. */
  reiniciar(): void {
    if (!this.simulando()) return;
    this.simulador.detener();
    this.simulador.reiniciar();
    this.simulador.iniciar();
  }
}
