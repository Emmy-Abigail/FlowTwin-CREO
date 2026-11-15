import { Injectable, inject, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { AuthService } from '../../core/auth/auth.service';

/** Los mensajes que envía el hub /hubs/gemelo (backend: Diferencias.Entre). */
export interface ManejadoresGemelo {
  ocupacionZona(m: { zonaId: number; ocupacion: number; aforo: number | null; nivel: string; pausada: boolean }): void;
  atencion(m: { id: number; codigo: string; zonaActual: number | null; estado: string; inicioEn: string; esperaMin: number; zonaDesdeEn?: string | null }): void;
  kpis(m: { enClinica: number; enEspera: number; esperaPromMin: number | null; consultaPromMin: number | null }): void;
  nodo(m: { nodoId: number; codigo: string; estado: string; ultimoLatidoEn: string | null }): void;
  /** Tras reconectarse se pudieron perder mensajes: hay que volver a pedir la foto completa. */
  reconectado(): void;
}

export type EstadoHub = 'conectando' | 'en-vivo' | 'reconectando' | 'sin-conexion';

/** Si la conexión se cae del todo, cada cuánto se vuelve a intentar. */
const REINTENTO_MS = 15_000;
/** Renovar el token si le queda menos que esto: dura 15 min y una conexión puede durar horas. */
const MARGEN_TOKEN_MS = 60_000;

/**
 * La conexión SignalR del gemelo. Une al usuario al grupo del piso (el servidor revisa el permiso,
 * RN-24), reparte los mensajes a los manejadores y se recupera sola: reintenta, vuelve a unirse al
 * piso y avisa «reconectado» para que se pida la foto completa (lo que pasó mientras estaba caída).
 */
@Injectable()
export class ConexionGemelo {
  private auth = inject(AuthService);
  readonly estado = signal<EstadoHub>('conectando');
  private hub?: HubConnection;
  private pisoId: number | null = null;
  private reintento?: ReturnType<typeof setTimeout>;
  private detenida = false;

  async iniciar(pisoId: number, m: ManejadoresGemelo): Promise<void> {
    this.detenida = false;
    this.pisoId = pisoId;
    this.hub = new HubConnectionBuilder()
      .withUrl('/hubs/gemelo', { accessTokenFactory: () => this.tokenVigente() })
      .withAutomaticReconnect([0, 2_000, 5_000, 10_000, 30_000])
      .configureLogging(LogLevel.Warning)
      .build();

    this.hub.on('OcupacionZonaCambiada', m.ocupacionZona);
    this.hub.on('AtencionActualizada', m.atencion);
    this.hub.on('KpisActualizados', m.kpis);
    this.hub.on('NodoEstadoCambiado', m.nodo);
    this.hub.onreconnecting(() => this.estado.set('reconectando'));
    this.hub.onreconnected(async () => {
      await this.unirse();
      this.estado.set('en-vivo');
      m.reconectado();
    });
    // Se rindió el reintento automático: sigue intentando cada 15 s mientras la pantalla esté abierta
    this.hub.onclose(() => {
      if (this.detenida) return;
      this.estado.set('sin-conexion');
      this.reintento = setTimeout(() => this.arrancar(m), REINTENTO_MS);
    });
    await this.arrancar(m);
  }

  /** Cambia de piso sin cortar la conexión. */
  async cambiarPiso(pisoId: number): Promise<void> {
    if (this.hub?.state === HubConnectionState.Connected && this.pisoId !== null) {
      await this.hub.invoke('SalirPiso', this.pisoId).catch(() => undefined);
    }
    this.pisoId = pisoId;
    await this.unirse();
  }

  async detener(): Promise<void> {
    this.detenida = true;
    clearTimeout(this.reintento);
    await this.hub?.stop();
  }

  private async arrancar(m: ManejadoresGemelo): Promise<void> {
    if (this.detenida || !this.hub) return;
    try {
      this.estado.set('conectando');
      await this.hub.start();
      await this.unirse();
      this.estado.set('en-vivo');
      m.reconectado();
    } catch {
      this.estado.set('sin-conexion');
      this.reintento = setTimeout(() => this.arrancar(m), REINTENTO_MS);
    }
  }

  private async unirse(): Promise<void> {
    if (this.hub?.state === HubConnectionState.Connected && this.pisoId !== null) await this.hub.invoke('UnirsePiso', this.pisoId);
  }

  private async tokenVigente(): Promise<string> {
    if (vence(this.auth.token()) - Date.now() < MARGEN_TOKEN_MS) await this.auth.refrescar();
    return this.auth.token() ?? '';
  }
}

/** Cuándo vence un JWT (ms), leyendo su «exp». 0 si no se puede leer: así se renueva. */
export function vence(token: string | null): number {
  try {
    const payload = JSON.parse(atob(token!.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')));
    return payload.exp * 1000;
  } catch {
    return 0;
  }
}
