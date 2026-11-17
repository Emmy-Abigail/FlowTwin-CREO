/**
 * Auditoría en la Simulación (ADR-31): la bitácora de la clínica simulada (lo que hacen la recepción simulada y
 * quien presenta) con el mismo formato que la API: filtros, categorías con conteo, página de 25 y detalle.
 * No tiene sello de integridad: es ficticia y no se guarda.
 */
import { ClinicaStore } from '../../core/datos/clinica.store';
import { EntradaAuditoria, TipoAuditoria } from '../../core/datos/modelos';
import { isoFecha } from '../../core/utils/tiempo';
import { Rango } from '../historial/modelo-historial';
import { DetalleAuditoria, FilaAuditoria, PaginaAuditoria, etiquetaCampo, textoEntidad } from './modelo-auditoria';

/** Lo mismo que la API (AuditoriaConsultaServicio.TamanoPagina). */
export const TAMANO_PAGINA = 25;

export interface FiltrosAuditoria { rango: Rango; usuarioId: number | null; categoria: string | null; buscar: string }

const CATEGORIA: Record<TipoAuditoria, string> = {
  'Sesión': 'ACCESOS', 'Usuarios': 'USUARIOS', 'Gemelo': 'CONFIGURACION', 'Dispositivos': 'DISPOSITIVOS',
  'Admisión': 'OPERACION', 'Seguimiento': 'OPERACION', 'Reportes': 'OPERACION',
};

function entidadDe(e: EntradaAuditoria): string {
  switch (e.tipo) {
    case 'Sesión': case 'Usuarios': return 'Usuario';
    case 'Admisión': return 'Tarjeta';
    case 'Seguimiento': return 'Zona';
    case 'Reportes': return 'Reporte';
    case 'Dispositivos': return e.elemento.startsWith('N-') ? 'Nodo' : 'Tarjeta';
    case 'Gemelo':
      // Los cambios hechos en la Simulación dicen su entidad en el detalle; los de ejemplo se deducen del nombre
      if (['Sede', 'Piso', 'Zona', 'Parametros'].includes(e.detalle)) return e.detalle;
      return e.elemento === 'Parámetros' ? 'Parametros' : e.elemento.startsWith('Piso') ? 'Piso' : 'Zona';
  }
}

/** «Entregó tarjeta» → «ENTREGO_TARJETA»: el código de la acción, como lo manda la API. */
const codigoDe = (texto: string) => texto.normalize('NFD').replace(/[̀-ͯ]/g, '').toUpperCase().replace(/[^A-Z0-9]+/g, '_');

export class BitacoraSimulada {
  constructor(private clinica: ClinicaStore) {}

  /**
   * Personas que aparecen en la bitácora, con un id fijo por nombre (para el filtro de usuario). «Sistema» no va:
   * la pantalla ya tiene su opción fija con el id 1, como en la API.
   */
  usuarios(): { id: number; nombre: string }[] {
    return [...new Set(this.clinica.auditoria().map(e => e.usuario))].filter(n => n !== 'Sistema')
      .sort((a, b) => a.localeCompare(b, 'es')).map((nombre, i) => ({ id: i + 2, nombre }));
  }

  pagina(f: FiltrosAuditoria, pagina: number): PaginaAuditoria {
    const { filas, conteos } = this.filtrar(f);
    const p = Math.max(1, pagina);
    return { filas: filas.slice((p - 1) * TAMANO_PAGINA, p * TAMANO_PAGINA), total: filas.length, pagina: p, tamano: TAMANO_PAGINA, conteos };
  }

  detalle(id: number): DetalleAuditoria | null {
    const x = this.filas().find(({ fila }) => fila.id === id);
    if (!x) return null;
    const { antes, despues } = cambioDe(x.entrada);
    return { fila: x.fila, ip: 'Simulación', traceId: null, antes, despues };
  }

  /** El CSV de lo filtrado, con las columnas que se ven en la tabla. */
  csv(f: FiltrosAuditoria): Blob {
    return new Blob(['\uFEFF' + this.textoCsv(f)], { type: 'text/csv;charset=utf-8' });
  }

  textoCsv(f: FiltrosAuditoria): string {
    const celda = (v: string | null) => `"${(v ?? '').replace(/"/g, '""')}"`;
    const lineas = [
      ['Fecha y hora', 'Usuario', 'Acción', 'Sobre', 'Categoría'].map(celda).join(';'),
      ...this.filtrar(f).filas.map(r => [r.cuando.replace('T', ' '), r.usuario, r.accionTexto, `${textoEntidad(r.entidad)} ${r.objeto ?? ''}`.trim(), r.categoria].map(celda).join(';')),
    ];
    return lineas.join('\r\n');
  }

  /** Período, usuario y búsqueda; los conteos por categoría se calculan antes de filtrar por categoría (como la API). */
  private filtrar(f: FiltrosAuditoria): { filas: FilaAuditoria[]; conteos: Record<string, number> } {
    const base = this.filas()
      .filter(({ fila }) => fila.cuando.slice(0, 10) >= f.rango.desde && fila.cuando.slice(0, 10) <= f.rango.hasta)
      .filter(({ fila }) => f.usuarioId === null || fila.usuarioId === f.usuarioId)
      .filter(({ fila, entrada }) => !f.buscar || [fila.objeto, fila.accionTexto, entrada.detalle].some(t => t?.toLowerCase().includes(f.buscar.toLowerCase())));
    const conteos: Record<string, number> = {};
    for (const { fila } of base) conteos[fila.categoria] = (conteos[fila.categoria] ?? 0) + 1;
    return { filas: base.filter(({ fila }) => !f.categoria || fila.categoria === f.categoria).map(x => x.fila), conteos };
  }

  /** Cada entrada con su fila: id fijo (la más antigua es la 1), de la más reciente a la más antigua. */
  private filas(): { entrada: EntradaAuditoria; fila: FilaAuditoria }[] {
    const ids = new Map([['Sistema', 1], ...this.usuarios().map(u => [u.nombre, u.id] as [string, number])]);
    const lista = [...this.clinica.auditoria()].sort((a, b) => b.t - a.t);
    return lista.map((e, i) => ({
      entrada: e,
      fila: {
        id: lista.length - i, cuando: localIso(e.t), usuarioId: ids.get(e.usuario) ?? 0, usuario: e.usuario,
        accion: codigoDe(e.accion), accionTexto: e.accion, entidad: entidadDe(e), entidadId: null,
        objeto: e.elemento === '—' ? null : e.elemento, categoria: CATEGORIA[e.tipo],
      },
    }));
  }
}

/** La hora local con el formato ISO de la API (la pantalla toma el día de los 10 primeros caracteres). */
function localIso(t: number): string {
  return `${isoFecha(t)}T${new Date(t).toTimeString().slice(0, 8)}`;
}

/**
 * Antes y después: los que trae la entrada (cambios hechos en la Simulación) o, en las de ejemplo, el detalle
 * «20 → 24» partido en dos.
 */
function cambioDe(e: EntradaAuditoria): { antes: unknown; despues: unknown } {
  if (e.antes || e.despues) return { antes: e.antes ?? null, despues: e.despues ?? null };
  if (['Sede', 'Piso', 'Zona', 'Parametros'].includes(e.detalle)) return { antes: null, despues: null };
  const campo = etiquetaCampo('detalle');
  const [antes, despues] = e.detalle.split('→').map(s => s.trim());
  return despues !== undefined ? { antes: { [campo]: antes }, despues: { [campo]: despues } } : { antes: null, despues: { [campo]: e.detalle } };
}
