import { HttpClient } from '@angular/common/http';
import { DestroyRef, Injectable, computed, effect, inject, signal, untracked } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API } from '../api/problema';
import { AuthService } from '../auth/auth.service';
import { ClinicaStore } from '../datos/clinica.store';
import { ModoDatosService } from '../datos/modo-datos.service';
import { PisoActualService } from '../datos/piso-actual.service';
import { Alerta, AlertaApi, alertasSimuladas, desdeApi, esUrgente, nuevas } from './modelo-alertas';
import { sonarAlerta } from './sonido';

/** Cada cuánto se piden las alertas reales. Las de espera cambian de nivel por minuto: 15 s sobra. */
const REFRESCO_MS = 15_000;
/** Preferencias de cada persona (en una computadora de recepción compartida, cada operador tiene las suyas). */
const CLAVE_SILENCIO = (usuario: number) => `flowtwin.alertas.silencio.${usuario}`;
const CLAVE_ENTERADO = (usuario: number) => `flowtwin.alertas.enterado.${usuario}`;

/**
 * La campana (F5, ADR-25): alertas del piso actual para el Operador y el Administrador, en cualquier
 * pantalla. Una alerta nueva suena (salvo que se haya silenciado) y anima la campana.
 */
@Injectable({ providedIn: 'root' })
export class AlertasService {
  private http = inject(HttpClient);
  private auth = inject(AuthService);
  private modo = inject(ModoDatosService);
  private pisos = inject(PisoActualService);
  private clinica = inject(ClinicaStore);

  /** Gerencia no: son operativas. */
  readonly disponible = computed(() => (this.auth.rol() === 'Operador' || this.auth.rol() === 'Administrador') && this.pisos.piso() !== null);

  private reales = signal<Alerta[]>([]);
  private simuladas = computed(() => alertasSimuladas(this.clinica.atenciones(), this.clinica.nodos(), this.clinica.ahora()));
  readonly lista = computed(() => !this.disponible() ? [] : this.modo.simulacion() ? this.simuladas() : this.reales());

  /** Reloj para «hace N min»: el simulado en simulación. */
  private reloj = signal(Date.now());
  readonly ahora = computed(() => this.modo.simulacion() ? this.clinica.ahora() : this.reloj());

  private usuarioId = computed(() => this.auth.usuario()?.id ?? 0);
  readonly silencio = signal(false);
  /**
   * Claves marcadas «Enterado»: dejan de contar y de sonar. La clave incluye el nivel, así que si la
   * situación empeora (40 → 60 min) es otra clave y vuelve a avisar.
   */
  readonly enterado = signal<ReadonlySet<string>>(new Set());
  /** Lo que todavía nadie atendió: es lo que cuenta la campana. */
  readonly pendientes = computed(() => this.lista().filter(a => !this.enterado().has(a.clave)));
  /** Cambia con cada alerta nueva: la campana lo usa para animarse. */
  readonly pulso = signal(0);

  private vistas: Set<string> | null = null;

  constructor() {
    // Sonido y animación cuando aparece una clave que no estaba
    effect(() => {
      const actuales = this.lista();
      untracked(() => {
        const llegaron = nuevas(this.vistas, actuales);
        this.vistas = new Set(actuales.map(a => a.clave));
        if (!llegaron.length) return;
        this.pulso.update(n => n + 1);
        const urgentes = llegaron.filter(esUrgente);
        if (urgentes.length && !this.silencio()) sonarAlerta(urgentes.some(a => a.nivel === 'CRITICA'));
      });
    });
    // Al cambiar de modo, de piso o de usuario, lo que había no cuenta como «nuevo»
    effect(() => {
      this.modo.simulacion(); this.pisos.piso(); this.auth.usuario();
      untracked(() => { this.vistas = null; void this.cargar(); });
    });
    // Preferencias de la persona que entró
    effect(() => {
      const id = this.usuarioId();
      untracked(() => {
        this.silencio.set(leer(CLAVE_SILENCIO(id)) === 'true');
        this.enterado.set(new Set<string>(JSON.parse(leer(CLAVE_ENTERADO(id)) ?? '[]')));
      });
    });
    // Un «Enterado» de algo que ya se resolvió no sirve: se olvida para no crecer sin fin
    effect(() => {
      const actuales = new Set(this.lista().map(a => a.clave));
      untracked(() => {
        const vigentes = [...this.enterado()].filter(c => actuales.has(c));
        if (vigentes.length !== this.enterado().size && this.lista().length) this.guardarEnterado(new Set(vigentes));
      });
    });
    const intervalo = setInterval(() => { this.reloj.set(Date.now()); void this.cargar(); }, REFRESCO_MS);
    inject(DestroyRef).onDestroy(() => clearInterval(intervalo));
  }

  alternarSilencio(): void {
    this.silencio.update(s => !s);
    escribir(CLAVE_SILENCIO(this.usuarioId()), String(this.silencio()));
  }

  marcarEnterado(clave: string): void {
    this.guardarEnterado(new Set([...this.enterado(), clave]));
  }

  private guardarEnterado(claves: Set<string>): void {
    this.enterado.set(claves);
    escribir(CLAVE_ENTERADO(this.usuarioId()), JSON.stringify([...claves]));
  }

  /** Ante «sin lecturas»: el paciente se fue sin devolver la tarjeta. Solo con datos reales. */
  async cerrarSinDevolver(atencionId: number): Promise<void> {
    await firstValueFrom(this.http.post(`${API}/atenciones/${atencionId}/cerrar-sin-devolver`, null));
    await this.cargar();
  }

  private async cargar(): Promise<void> {
    const piso = this.pisos.piso()?.id;
    if (!this.disponible() || this.modo.simulacion() || piso === undefined) return;
    try {
      const r = await firstValueFrom(this.http.get<AlertaApi[]>(`${API}/pisos/${piso}/alertas`));
      if (piso === this.pisos.piso()?.id) this.reales.set(r.map(desdeApi));
    } catch { /* un corte momentáneo no borra lo que se ve: el siguiente ciclo reintenta */ }
  }
}

/** Comodidades de la persona: sin almacenamiento (ventana privada) duran solo la sesión. */
function leer(clave: string): string | null {
  try { return localStorage.getItem(clave); } catch { return null; }
}

function escribir(clave: string, valor: string): void {
  try { localStorage.setItem(clave, valor); } catch { /* sin almacenamiento */ }
}
