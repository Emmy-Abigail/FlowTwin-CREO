/**
 * P11 · Auditoría, sin Angular: tipos de la API, categorías, nombres de los campos y la comparación «antes y
 * después» que se muestra en el detalle. Todo es de solo lectura: la bitácora no se edita.
 */
import { DIA } from '../../core/utils/tiempo';
import { Rango, diaDe } from '../historial/modelo-historial';

export interface FilaAuditoria {
  id: number; cuando: string; usuarioId: number; usuario: string; accion: string; accionTexto: string;
  entidad: string; entidadId: string | null; objeto: string | null; categoria: string;
}
export interface PaginaAuditoria { filas: FilaAuditoria[]; total: number; pagina: number; tamano: number; conteos: Record<string, number> }
export interface DetalleAuditoria { fila: FilaAuditoria; ip: string | null; traceId: string | null; antes: unknown; despues: unknown }
export interface ResultadoIntegridad {
  configurada: boolean; revisados: number; sinSellar: number; integra: boolean;
  primeraAlteracionId: number | null; motivo: string | null; ultimoSelloEn: string | null;
}

/** Operación (entregas, devoluciones, pausas) es casi todo el volumen: va aparte para que no tape lo demás. */
export const CATEGORIAS: { id: string; texto: string }[] = [
  { id: 'ACCESOS', texto: 'Accesos' }, { id: 'USUARIOS', texto: 'Usuarios' }, { id: 'CONFIGURACION', texto: 'Configuración' },
  { id: 'DISPOSITIVOS', texto: 'Dispositivos' }, { id: 'OPERACION', texto: 'Operación' },
];

const ENTIDADES: Record<string, string> = {
  Usuario: 'Usuario', Sede: 'Sede', Piso: 'Piso', Zona: 'Zona', Parametros: 'Parámetros', Nodo: 'Nodo',
  Tarjeta: 'Tarjeta', Atencion: 'Atención', Reporte: 'Reporte',
};
export const textoEntidad = (e: string) => ENTIDADES[e] ?? e;

/** Los nombres de los campos como los dice la clínica; los que no estén aquí se muestran legibles («ajusteRssiDb» → «Ajuste rssi db»). */
const CAMPOS: Record<string, string> = {
  nombres: 'Nombres', apellidos: 'Apellidos', correo: 'Correo', rol: 'Rol', pisos: 'Pisos', estadoregistro: 'Activo',
  nombre: 'Nombre', tipo: 'Tipo', aforo: 'Aforo', direccion: 'Dirección', prefijoatencion: 'Letra de los códigos', nivel: 'Nivel',
  publicado: 'Publicado', esperaaltamin: 'Alerta de espera (min)', esperacriticamin: 'Alerta crítica (min)',
  sinlecturasmin: 'Sin lecturas (min)', horacierre: 'Hora de cierre', semaforoambarpct: 'Ámbar del semáforo (%)',
  codigo: 'Código', mac: 'MAC', zonaid: 'Zona', posx: 'Posición X', posy: 'Posición Y', ajusterssidb: 'Ajuste de señal (dB)',
  motivo: 'Motivo', detalle: 'Detalle', formato: 'Formato', desde: 'Desde', hasta: 'Hasta', filas: 'Filas', categoria: 'Categoría',
  buscar: 'Búsqueda', minutos: 'Minutos de bloqueo', estado: 'Estado', tarjeta: 'Tarjeta', pisoid: 'Piso', sedeid: 'Sede',
  usuarioid: 'Usuario', cantidad: 'Cantidad', totalmin: 'Tiempo total (min)', origen: 'Origen', primeringreso: 'Primer ingreso',
};

export function etiquetaCampo(clave: string): string {
  const conocida = CAMPOS[clave.toLowerCase()];
  if (conocida) return conocida;
  const palabras = clave.replace(/([a-z0-9])([A-Z])/g, '$1 $2').replace(/[_-]+/g, ' ').trim().toLowerCase();
  return palabras.charAt(0).toUpperCase() + palabras.slice(1);
}

const MOTIVOS_INGRESO: Record<string, string> = {
  DESCONOCIDO: 'Correo que no existe', CLAVE: 'Contraseña incorrecta', BLOQUEADA: 'Cuenta bloqueada', TEMPORAL_VENCIDA: 'Contraseña temporal vencida',
};

/** Un valor guardado, en texto para la persona: «—» si no hay, Sí/No, listas separadas por coma. */
export function textoValor(v: unknown, clave = ''): string {
  if (v === null || v === undefined || v === '') return '—';
  if (typeof v === 'boolean') return v ? 'Sí' : 'No';
  if (Array.isArray(v)) return v.length ? v.map(x => textoValor(x)).join(', ') : '—';
  if (typeof v === 'object') return JSON.stringify(v);
  if (clave.toLowerCase() === 'motivo' && typeof v === 'string') return MOTIVOS_INGRESO[v] ?? v;
  return String(v);
}

export interface Cambio { campo: string; antes: string; despues: string; cambio: boolean }

/**
 * Antes y después, campo por campo. Al crear solo hay «después»; al dar de baja, solo «antes». Los campos que
 * cambiaron van primero; los que siguen igual se muestran para dar contexto.
 */
export function cambios(antes: unknown, despues: unknown): Cambio[] {
  const a = objeto(antes), d = objeto(despues);
  const claves = [...new Set([...Object.keys(a), ...Object.keys(d)])];
  const lista = claves.map(k => {
    const va = textoValor(a[k], k), vd = textoValor(d[k], k);
    return { campo: etiquetaCampo(k), antes: antes == null ? '' : va, despues: despues == null ? '' : vd, cambio: va !== vd };
  });
  return [...lista.filter(c => c.cambio), ...lista.filter(c => !c.cambio)];
}

const objeto = (v: unknown): Record<string, unknown> => v && typeof v === 'object' && !Array.isArray(v) ? v as Record<string, unknown> : {};

/** Lo mismo que valida el backend: dos fechas, en orden, no en el futuro y como máximo un año. */
export const MAXIMO_DIAS = 366;
export function problemaPeriodo(r: Rango, hoy: string): string | null {
  if (!r.desde || !r.hasta) return 'Elige las dos fechas.';
  if (r.hasta < r.desde) return 'La fecha final no puede ser anterior a la inicial.';
  if (r.hasta > hoy) return 'La fecha final no puede ser futura.';
  if (Math.round((diaDe(r.hasta) - diaDe(r.desde)) / DIA) + 1 > MAXIMO_DIAS) return `El período máximo es de ${MAXIMO_DIAS} días.`;
  return null;
}

/** Texto del resultado de la verificación de integridad. */
export function textoIntegridad(r: ResultadoIntegridad): { titulo: string; texto: string; tono: 'ok' | 'mal' | 'aviso' } {
  if (!r.configurada) return { titulo: 'Sello no configurado', texto: 'El servidor no tiene la clave del sello de integridad: la bitácora no se puede verificar.', tono: 'aviso' };
  if (r.primeraAlteracionId !== null) return { titulo: 'Se detectó una alteración', texto: `Registro #${r.primeraAlteracionId}: ${r.motivo ?? ''}`.trim(), tono: 'mal' };
  if (r.sinSellar > 0) return { titulo: 'Hay registros sin sello', texto: r.motivo ?? `${r.sinSellar} registro(s) sin sello.`, tono: 'aviso' };
  return { titulo: 'Bitácora íntegra', texto: r.revisados === 1 ? '1 registro del período verificado.' : `${r.revisados.toLocaleString('es-PE')} registros del período verificados.`, tono: 'ok' };
}

