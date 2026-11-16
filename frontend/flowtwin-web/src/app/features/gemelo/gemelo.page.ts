import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API, mensajeDe, problemaDe } from '../../core/api/problema';
import { AuthService } from '../../core/auth/auth.service';
import { ClinicaStore } from '../../core/datos/clinica.store';
import { ToastService } from '../../core/ui/toast.service';
import { RouterLink } from '@angular/router';
import { ModoDatosService } from '../../core/datos/modo-datos.service';
import { PisoActualService } from '../../core/datos/piso-actual.service';
import { fechaLarga } from '../../core/utils/tiempo';
import { Render3dComponent } from '../../gemelo-render/render-3d/render-3d.component';
import { OcupacionZona, TipoZona } from '../../gemelo-render/geometria';
import { maquetaDe } from '../../gemelo-render/maqueta';
import { EstadoVacioComponent } from '../../shared/componentes/estado-vacio.component';
import { IconoComponent, NombreIcono } from '../../shared/componentes/icono.component';
import { ModalComponent } from '../../shared/componentes/modal.component';
import { SelectorPisoComponent } from '../../shared/componentes/selector-piso.component';
import { haceCuanto } from '../admision/presentacion-admision';
import { ConexionGemelo } from './conexion-gemelo';
import { GemeloEnVivoStore } from './gemelo-en-vivo.store';
import { GemeloSimuladoStore } from './gemelo-simulado.store';
import {
  MOTIVOS_FUERA, textoMotivo,
  EstadoPaciente, FuenteGemelo, PacienteVista, detallePaciente, etiquetaZona, ocupacionParaRender, pacientesPorPestana, resumenZona, textoConexion,
} from './modelo-gemelo';

type Pestana = 'esperando' | 'enConsulta' | 'atendidos';

const NOMBRE_TIPO: Record<TipoZona, string> = {
  INGRESO: 'Ingreso', ESPERA: 'Espera', TRIAJE: 'Triaje', SERVICIO: 'Servicio', TRANSITO: 'Tránsito', SALIDA: 'Salida',
};

/** Etiqueta, color e ícono de cada estado de paciente en sus tarjetas. */
const PRESENTACION: Record<EstadoPaciente, { texto: string; tono: string; icono: NombreIcono }> = {
  'esperando': { texto: 'En espera', tono: 'ambar', icono: 'sofa' },
  'en-consulta': { texto: 'En consulta', tono: 'verde', icono: 'steth' },
  'atendido': { texto: 'Hacia la salida', tono: 'gris', icono: 'walk' },
  'finalizado': { texto: 'Finalizada', tono: 'gris', icono: 'check' },
};

/**
 * P4 · Gemelo en vivo. Una sola pantalla para dos fuentes (ADR-22): los datos reales (API + SignalR)
 * o, si alguien la activa desde el menú, la simulación para el sponsor. La página no sabe de cuál
 * viene cada dato: solo dibuja el modelo común (modelo-gemelo.ts).
 */
@Component({
  selector: 'app-gemelo',
  imports: [RouterLink, Render3dComponent, IconoComponent, ModalComponent, EstadoVacioComponent, SelectorPisoComponent],
  templateUrl: './gemelo.page.html',
  styleUrl: './gemelo.page.scss',
  providers: [ConexionGemelo, GemeloEnVivoStore, GemeloSimuladoStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GemeloPage {
  protected modo = inject(ModoDatosService);
  protected pisos = inject(PisoActualService);
  private http = inject(HttpClient);
  private auth = inject(AuthService);
  private clinica = inject(ClinicaStore);
  private toast = inject(ToastService);
  private enVivo = inject(GemeloEnVivoStore);
  private simulado = inject(GemeloSimuladoStore);

  /** La fuente de datos de ahora: la simulación solo si está activa. */
  protected f = computed<FuenteGemelo>(() => this.modo.simulacion() ? this.simulado : this.enVivo);

  protected pestana = signal<Pestana>('esperando');
  protected zonaSeleccionada = signal<number | null>(null);

  constructor() {
    // Datos reales: se conecta al piso elegido y se desconecta al pasar a la simulación
    effect(() => {
      const piso = this.pisos.piso(), sede = this.pisos.sede();
      const simulando = this.modo.simulacion();
      untracked(() => {
        if (simulando) this.enVivo.desactivar();
        else if (piso && sede) this.enVivo.activar(piso.id, sede.nombre);
      });
    });
  }

  protected fecha = computed(() => fechaLarga(this.f().ahora()));
  protected conexion = computed(() => textoConexion(this.f().conexion(), this.f().nodos()));
  protected hace = (t: number) => haceCuanto(t, this.f().ahora());

  private zonasPorId = computed(() => new Map(this.f().zonas().map(z => [z.zonaId, z])));
  private geoPorId = computed(() => new Map((this.f().geometria()?.zonas ?? []).map(z => [z.id, z])));
  protected nombreZona = (id: number) => this.geoPorId().get(id)?.nombre ?? `Zona ${id}`;
  private tipoDe = (id: number) => this.geoPorId().get(id)?.tipo;

  /** Lo que recibe el render 3D: las zonas sin nodo, solo con su nombre. */
  protected ocupacion = computed<OcupacionZona[]>(() => ocupacionParaRender(this.f().zonas(), this.f().nodos(), this.tipoDe));

  /** La maqueta 3D del piso que se mira; null si ese piso todavía no la tiene. */
  protected maqueta = computed(() => {
    const geo = this.f().geometria();
    return geo ? maquetaDe(geo.pisoId) : null;
  });

  protected grupos = computed(() => pacientesPorPestana(this.f().pacientes()));
  protected lista = computed(() => this.grupos()[this.pestana()]);
  protected readonly pestanas: { id: Pestana; texto: string }[] = [
    { id: 'esperando', texto: 'Esperando' }, { id: 'enConsulta', texto: 'En consulta' }, { id: 'atendidos', texto: 'Atendidos' },
  ];

  protected tarjeta(p: PacienteVista) {
    return { ...PRESENTACION[p.estado], detalle: detallePaciente(p, this.nombreZona, this.hace) };
  }

  /** Detalle de zona (M2): quiénes están ahí ahora. */
  protected detalle = computed(() => {
    const id = this.zonaSeleccionada();
    const z = id === null ? undefined : this.zonasPorId().get(id);
    const geo = id === null ? undefined : this.geoPorId().get(id);
    if (!z || !geo) return null;
    const aqui = this.f().pacientes().filter(p => p.zonaId === id && p.estado !== 'finalizado')
      .sort((a, b) => (a.zonaDesde ?? 0) - (b.zonaDesde ?? 0));
    const monitoreada = this.f().nodos().some(n => n.zonaId === id);
    return {
      zona: z, nombre: geo.nombre, tipo: NOMBRE_TIPO[geo.tipo], monitoreada,
      cifra: etiquetaZona(z, geo.tipo),
      resumen: monitoreada ? resumenZona(z, geo.tipo) : 'Aún no se monitorea: no tiene un nodo instalado.',
      nodo: this.textoNodo(z.zonaId), pacientes: aqui,
    };
  });

  private textoNodo(zonaId: number): string {
    const n = this.f().nodos().find(x => x.zonaId === zonaId);
    if (!n) return 'Sin nodo en esta zona';
    if (!n.enLinea) return `Nodo ${n.codigo} · sin señal${n.ultimoLatido ? ` (último latido ${this.hace(n.ultimoLatido)})` : ''}`;
    return `Nodo ${n.codigo} · en línea`;
  }

  // ------------------------------------------------------------------ zona fuera de servicio (RF09, ADR-25)

  /** Gerencia solo mira: el interruptor es del Operador y del Administrador. */
  protected puedeCambiarServicio = computed(() => this.auth.rol() === 'Operador' || this.auth.rol() === 'Administrador');
  protected readonly motivos = MOTIVOS_FUERA;
  protected readonly textoMotivo = textoMotivo;
  /** Al apagar el interruptor se eligen el motivo (y el detalle si es «Otro») antes de confirmar. */
  protected eligiendo = signal(false);
  protected motivo = signal('LIMPIEZA');
  protected detalleOtro = signal('');
  protected guardandoServicio = signal(false);

  protected cerrarZona(): void {
    this.zonaSeleccionada.set(null);
    this.eligiendo.set(false);
  }

  protected async ponerFuera(zonaId: number, nombre: string): Promise<void> {
    if (this.motivo() === 'OTRO' && !this.detalleOtro().trim()) return;
    await this.cambiarServicio(async () => {
      if (this.modo.simulacion()) this.clinica.ponerFueraDeServicio(zonaId, this.motivo(), nombre, this.auth.actor());
      else await firstValueFrom(this.http.post(`${API}/zonas/${zonaId}/pausas`, { motivo: this.motivo(), detalle: this.detalleOtro().trim() || null }));
      this.eligiendo.set(false);
      this.detalleOtro.set('');
    });
  }

  protected async ponerEnServicio(zonaId: number, nombre: string): Promise<void> {
    await this.cambiarServicio(async () => {
      if (this.modo.simulacion()) this.clinica.ponerEnServicio(zonaId, nombre, this.auth.actor());
      else await firstValueFrom(this.http.post(`${API}/zonas/${zonaId}/pausas/actual/finalizar`, null));
    });
  }

  /** El gemelo se actualiza solo por el hub; aquí solo se avisa si falló. */
  private async cambiarServicio(accion: () => Promise<void>): Promise<void> {
    this.guardandoServicio.set(true);
    try { await accion(); }
    catch (e) { this.toast.mostrar(mensajeDe(problemaDe(e)), 'bad'); }
    finally { this.guardandoServicio.set(false); }
  }
}
