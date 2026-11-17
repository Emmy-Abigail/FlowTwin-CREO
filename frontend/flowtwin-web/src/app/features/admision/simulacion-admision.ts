/**
 * Admisión en la Simulación (ADR-31): responde lo mismo que la API real (resumen, vista previa, escaneo,
 * deshacer, desinfectar, extraviar), pero sobre las tarjetas de la clínica simulada. Así la pantalla es la
 * misma y nada de lo que se hace aquí llega a la base: el sponsor puede escanear «T-05» y ver al paciente
 * aparecer en el gemelo.
 */
import { ClinicaStore, EstadoPrevio } from '../../core/datos/clinica.store';
import { Actor, EstadoTarjeta, Operacion, Tarjeta } from '../../core/datos/modelos';
import { minutos, pad2 } from '../../core/utils/tiempo';
import { AtencionApi, EscaneoApi, TarjetaApi, VistaPreviaApi } from './presentacion-admision';

/** Lo que el store de Admisión recibe del servidor: el mismo formato aquí y en la API. */
export interface PendienteSim { tarjetaId: number; codigo: string; desde: string | null; atencionCodigo: string | null; }
export interface ResumenSim {
  contadores: Record<string, number>;
  porDesinfectar: PendienteSim[];
  sinDevolver: PendienteSim[];
  ultimasOperaciones: { fecha: string; tipo: string; tarjeta: string; atencion: string | null; totalMin: number | null }[];
}

/** Un problema de negocio, con el mismo código que usa la API (lo muestra avisoDeProblema). */
export interface ProblemaSim { codigo: string; mensaje: string; datos: Record<string, unknown>; }

const CODIGO_ESTADO: Record<EstadoTarjeta, string> = {
  'Disponible': 'DISPONIBLE', 'En uso': 'EN_USO', 'Por desinfectar': 'POR_DESINFECTAR',
  'Sin devolver': 'SIN_DEVOLVER', 'Extraviada': 'EXTRAVIADA', 'Baja': 'BAJA',
};
const CODIGO_OPERACION: Record<Operacion['tipo'], string> = {
  Entregada: 'ENTREGADA', Recibida: 'RECIBIDA', Desinfectada: 'DESINFECTADA', Recuperada: 'RECUPERADA',
};

/** «T-05» → 5, «A-0012» → 12: el número que la API usa como id. */
export const numero = (codigo: string) => Number(codigo.replace(/\D/g, ''));
const iso = (t: number | null) => (t === null ? null : new Date(t).toISOString());

/** Acepta el texto del QR («CREO-T05») o el código escrito a mano («T-05», «t5»). */
export function tarjetaDelQr(qr: string): string | null {
  const m = /T-?0*(\d{1,4})\s*$/i.exec(qr.trim());
  return m ? 'T-' + pad2(Number(m[1])) : null;
}

export class RecepcionSimulada {
  /** Lo que había antes de cada entrega, para poder deshacerla (RN-04). */
  private antesDeEntregar = new Map<number, EstadoPrevio>();

  constructor(private clinica: ClinicaStore, private quien: () => Actor) {}

  resumen(): ResumenSim {
    const tarjetas = this.clinica.tarjetas();
    const contadores: Record<string, number> = {};
    for (const t of tarjetas) contadores[CODIGO_ESTADO[t.estado]] = (contadores[CODIGO_ESTADO[t.estado]] ?? 0) + 1;
    const pendiente = (t: Tarjeta): PendienteSim => ({ tarjetaId: numero(t.id), codigo: t.id, desde: iso(t.desde), atencionCodigo: t.atencionId });
    return {
      contadores,
      porDesinfectar: tarjetas.filter(t => t.estado === 'Por desinfectar').map(pendiente),
      sinDevolver: tarjetas.filter(t => t.estado === 'Sin devolver').map(pendiente),
      ultimasOperaciones: this.clinica.operaciones().map(o => ({
        fecha: new Date(o.t).toISOString(), tipo: CODIGO_OPERACION[o.tipo], tarjeta: o.tarjetaId,
        atencion: o.atencionId ?? null, totalMin: o.totalMin ?? null,
      })),
    };
  }

  /** Paso 1: qué pasaría con esta tarjeta. No cambia nada. */
  vistaPrevia(qr: string): VistaPreviaApi {
    const id = tarjetaDelQr(qr);
    const t = id ? this.clinica.tarjeta(id) : undefined;
    if (!t) {
      return { tarjetaId: 0, tarjeta: qr, estado: '', desde: null, operacion: null, codigo: 'TARJETA_NO_REGISTRADA', mensaje: 'Tarjeta no registrada' };
    }
    const base = { tarjetaId: numero(t.id), tarjeta: t.id, estado: CODIGO_ESTADO[t.estado], desde: iso(t.desde) };
    const problema = this.problemaDe(t);
    if (problema) return { ...base, operacion: null, codigo: problema.codigo, mensaje: problema.mensaje };
    return { ...base, operacion: this.operacionDe(t), codigo: null, mensaje: null };
  }

  /** Paso 2: registra la entrega, la devolución o la recuperación. */
  escanear(qr: string): EscaneoApi | ProblemaSim {
    const id = tarjetaDelQr(qr);
    const t = id ? this.clinica.tarjeta(id) : undefined;
    if (!t) return { codigo: 'TARJETA_NO_REGISTRADA', mensaje: 'Tarjeta no registrada', datos: {} };
    const problema = this.problemaDe(t);
    if (problema) return problema;

    switch (this.operacionDe(t)) {
      case 'ENTREGA': {
        const antes: EstadoPrevio = { tarjeta: { ...t } };
        const a = this.clinica.entregarTarjeta(t.id, this.quien());
        this.antesDeEntregar.set(numero(a.id), { ...antes, atencionCreadaId: a.id });
        return {
          operacion: 'ENTREGA', tarjeta: this.tarjetaApi(t.id), atencion: atencionApi(a.id, a.inicio, null),
          // La ventana de deshacer es de tiempo real (la cuenta regresiva que ve el operador), no del reloj simulado
          deshacerHasta: new Date(Date.now() + 30_000).toISOString(),
        };
      }
      case 'DEVOLUCION': {
        const a = this.clinica.recibirTarjeta(t.id, this.quien())!;
        return { operacion: 'DEVOLUCION', tarjeta: this.tarjetaApi(t.id), atencion: atencionApi(a.id, a.inicio, a.fin), deshacerHasta: null };
      }
      default:
        this.clinica.recuperarTarjeta(t.id, this.quien());
        return { operacion: 'RECUPERADA', tarjeta: this.tarjetaApi(t.id), atencion: null, deshacerHasta: null };
    }
  }

  deshacer(atencionId: number): TarjetaApi | ProblemaSim {
    const antes = this.antesDeEntregar.get(atencionId);
    if (!antes) return { codigo: 'NO_SE_PUEDE_DESHACER', mensaje: 'Esa entrega ya no se puede deshacer.', datos: {} };
    this.clinica.revertir(antes, this.quien());
    this.antesDeEntregar.delete(atencionId);
    return this.tarjetaApi(antes.tarjeta.id);
  }

  desinfectar(ids: number[]): void {
    for (const id of ids) this.clinica.marcarDesinfectada('T-' + pad2(id), this.quien());
  }

  extraviar(ids: number[]): void {
    for (const id of ids) this.clinica.marcarExtraviada('T-' + pad2(id), this.quien());
  }

  private operacionDe(t: Tarjeta): EscaneoApi['operacion'] {
    return t.estado === 'Disponible' ? 'ENTREGA' : t.estado === 'En uso' ? 'DEVOLUCION' : 'RECUPERADA';
  }

  /** Las mismas reglas que el servidor (AdmisionServicio.Decidir). */
  private problemaDe(t: Tarjeta): ProblemaSim | null {
    const datos = { tarjetaId: numero(t.id), tarjeta: t.id };
    if (t.estado === 'Por desinfectar') return { codigo: 'RN03_POR_DESINFECTAR', mensaje: `${t.id} aún no está desinfectada`, datos };
    if (t.estado === 'Baja') return { codigo: 'RN_TARJETA_DE_BAJA', mensaje: `La tarjeta ${t.id} está dada de baja`, datos };
    return null;
  }

  private tarjetaApi(id: string): TarjetaApi {
    const t = this.clinica.tarjeta(id)!;
    return { id: numero(t.id), codigo: t.id, estado: CODIGO_ESTADO[t.estado], estadoNombre: t.estado, desde: iso(t.desde) };
  }
}

function atencionApi(id: string, inicio: number, fin: number | null): AtencionApi {
  const totalMin = fin === null ? null : minutos(fin - inicio);
  return { id: numero(id), codigo: id, inicioEn: new Date(inicio).toISOString(), finEn: iso(fin), esperaMin: null, consultaMin: null, totalMin };
}

export const esProblema = (r: unknown): r is ProblemaSim => typeof r === 'object' && r !== null && 'mensaje' in r && 'datos' in r;
