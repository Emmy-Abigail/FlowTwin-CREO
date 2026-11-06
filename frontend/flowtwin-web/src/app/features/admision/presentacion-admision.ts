import { duracion, hhmm } from '../../core/utils/tiempo';

/**
 * Reglas de la pantalla Admisión y lo que muestra en cada caso. Funciones puras (sin Angular ni HTTP):
 * cada regla tiene su prueba en presentacion-admision.spec.ts con el mismo identificador.
 *
 * Las reglas de negocio las decide el servidor (AdmisionServicio.Decidir); aquí solo se decide CÓMO
 * se muestran y cuándo pedir una confirmación:
 *
 *  R1  Antes de registrar se muestra qué va a pasar (entrega, devolución, recuperación).
 *  R2  Si una regla lo impide (por desinfectar, de baja, otra sede, no registrada, sin conexión),
 *      se avisa debajo del campo y NO se llama al servidor para registrar.
 *  R3  Devolver una tarjeta entregada hace menos de DEVOLUCION_RAPIDA_MIN pide confirmación (modal):
 *      casi siempre es un doble escaneo y cerraría la atención de un paciente que acaba de llegar.
 *  R4  Marcar desinfectada pide confirmación (modal): es una declaración sanitaria auditada.
 *  R5  Marcar extraviada pide confirmación (modal): saca la tarjeta del inventario.
 *  R6  Deshacer una entrega NO pide confirmación: ya es la corrección (RN-04, 30 s).
 *  R7  Registrar con el campo vacío dice qué hacer y no llama al servidor.
 *  R8  Al leer otra tarjeta, la confirmación anterior desaparece: nunca se mezclan dos pacientes.
 *  R9  Un segundo clic en Registrar mientras se registra se ignora: no se registra dos veces.
 */

/** R3: menos de esto entre la entrega y la devolución parece un doble escaneo. */
export const DEVOLUCION_RAPIDA_MIN = 2;

// ---- Respuestas de la API
export interface TarjetaApi { id: number; codigo: string; estado: string; estadoNombre: string; desde?: string | null; }
export interface AtencionApi {
  id: number; codigo: string; inicioEn: string; finEn: string | null;
  esperaMin: number | null; consultaMin: number | null; totalMin: number | null;
}
export interface EscaneoApi {
  operacion: 'ENTREGA' | 'DEVOLUCION' | 'RECUPERADA';
  tarjeta: TarjetaApi; atencion: AtencionApi | null; deshacerHasta: string | null;
}
export interface VistaPreviaApi {
  tarjetaId: number; tarjeta: string; estado: string; desde: string | null;
  operacion: EscaneoApi['operacion'] | null; codigo: string | null; mensaje: string | null;
}

export type Tono = 'verde' | 'celeste' | 'ambar' | 'gris' | 'rojo';

/** Lo que se pregunta en un modal antes de una acción con consecuencias. */
export interface Pregunta { titulo: string; texto: string; accion: string; }

/** Aviso debajo del campo (antes de registrar). tono 'rojo' = no se puede registrar (R2). */
export interface Aviso {
  tono: Tono;
  titulo: string;
  texto: string;
  desinfectar?: { id: number; codigo: string };
  /** R3: si viene, Registrar abre un modal con esta pregunta antes de registrar. */
  confirmar?: Pregunta;
}

/**
 * Confirmación después de registrar: una línea con color. El paciente no necesita ningún código (lo
 * siguen los nodos por su tarjeta); el de la atención va en el detalle, para la trazabilidad.
 */
export interface Confirmacion {
  tipo: 'entrega' | 'devolucion' | 'recuperada' | 'desinfectada' | 'anulada' | 'extraviada' | 'error';
  /** «T-07 entregada · 10:31» */
  titulo: string;
  detalle?: string;
  desinfectar?: { id: number; codigo: string };
}

/** R1 y R3: qué va a pasar con la tarjeta leída. */
export function vistaPreviaDe(v: VistaPreviaApi, ahora: number): Aviso {
  if (v.codigo) return avisoDeProblema(v.codigo, v.mensaje ?? '', { tarjetaId: v.tarjetaId, tarjeta: v.tarjeta });
  switch (v.operacion) {
    case 'ENTREGA':
      return { tono: 'verde', titulo: `${v.tarjeta} · se entregará`, texto: 'Empieza el recorrido del paciente.' };
    case 'DEVOLUCION': {
      const min = v.desde ? Math.floor((ahora - Date.parse(v.desde)) / 60_000) : null;
      if (min !== null && min < DEVOLUCION_RAPIDA_MIN) {
        const hace = min < 1 ? 'hace menos de 1 min' : `hace ${min} min`;
        return {
          tono: 'ambar', titulo: `${v.tarjeta} · se recibirá`, texto: `Se entregó ${hace}. ¿Es la tarjeta correcta?`,
          confirmar: {
            titulo: `¿Recibir ${v.tarjeta}?`,
            texto: `Se entregó ${hace}, a las ${hhmm(Date.parse(v.desde!))}. Si la recibes, la atención de ese paciente termina ahora.`,
            accion: 'Sí, recibirla',
          },
        };
      }
      return { tono: 'celeste', titulo: `${v.tarjeta} · se recibirá`, texto: 'Termina el recorrido del paciente.' };
    }
    default:
      return { tono: 'ambar', titulo: `${v.tarjeta} · volvió una tarjeta perdida`, texto: 'Al registrarla pasará a desinfección.' };
  }
}

/** Después de registrar. */
export function confirmacionDe(r: EscaneoApi): Confirmacion {
  const t = r.tarjeta.codigo;
  const a = r.atencion;
  switch (r.operacion) {
    case 'ENTREGA':
      return { tipo: 'entrega', titulo: `${t} entregada · ${hhmm(Date.parse(a!.inicioEn))}`, detalle: 'Su recorrido empezó' };
    case 'DEVOLUCION':
      return {
        tipo: 'devolucion', titulo: `${t} recibida · ${hhmm(Date.parse(a!.finEn ?? a!.inicioEn))}`,
        detalle: `${duracion(a!.totalMin)} en la clínica · déjala para desinfectar`,
      };
    case 'RECUPERADA':
      return {
        tipo: 'recuperada', titulo: `${t} recuperada`, detalle: 'Volvió una tarjeta perdida. Desinféctala antes de entregarla.',
        desinfectar: { id: r.tarjeta.id, codigo: t },
      };
  }
}

/** R2: una regla o un error impide registrar. El mismo aviso en la vista previa y al intentar registrar. */
export function avisoDeProblema(codigo: string, mensaje: string, datos?: Record<string, unknown>): Aviso {
  const tarjeta = String(datos?.['tarjeta'] ?? '');
  switch (codigo) {
    case 'RN03_POR_DESINFECTAR':
      return {
        tono: 'rojo', titulo: `${tarjeta} · aún no está desinfectada`, texto: 'Límpiala con alcohol al 70\u00a0% antes de entregarla.',
        desinfectar: { id: Number(datos?.['tarjetaId']), codigo: tarjeta },
      };
    case 'TARJETA_NO_REGISTRADA':
      return { tono: 'rojo', titulo: 'Tarjeta no registrada', texto: 'Pide al Administrador que la registre antes de usarla.' };
    case 'SIN_CONEXION':
      return { tono: 'rojo', titulo: 'Sin conexión con el servidor', texto: 'No se registró nada. Vuelve a escanear en un momento.' };
    default:
      return { tono: 'rojo', titulo: mensaje || 'No se puede registrar', texto: 'Usa otra tarjeta o avisa al Administrador.' };
  }
}

/** R7 */
export const AVISO_SIN_TARJETA: Aviso = { tono: 'rojo', titulo: 'No hay tarjeta leída', texto: 'Acerca la tarjeta al lector.' };

/**
 * Cuánto pasó desde una operación, en la unidad que se lee de un vistazo:
 * «recién» · «hace 7 min» · «hace 3 h 05 min» (hoy) · «ayer, 18:40» · «hace 3 días» · «el 28/10».
 * Los días son de calendario (medianoche local), no bloques de 24 h: algo de anoche es «ayer».
 */
export function haceCuanto(fecha: number, ahora: number): string {
  const min = Math.max(0, Math.floor((ahora - fecha) / 60_000));
  if (min < 1) return 'recién';
  const dias = diasDeCalendario(fecha, ahora);
  if (dias === 0) return min < 60 ? `hace ${min} min` : `hace ${Math.floor(min / 60)} h ${String(min % 60).padStart(2, '0')} min`;
  if (dias === 1) return `ayer, ${hhmm(fecha)}`;
  if (dias < 7) return `hace ${dias} días`;
  const d = new Date(fecha);
  return `el ${String(d.getDate()).padStart(2, '0')}/${String(d.getMonth() + 1).padStart(2, '0')}`;
}

/** Cuántas medianoches (hora local) hay entre las dos fechas. */
function diasDeCalendario(desde: number, hasta: number): number {
  const dia = (t: number) => { const d = new Date(t); return new Date(d.getFullYear(), d.getMonth(), d.getDate()).getTime(); };
  return Math.round((dia(hasta) - dia(desde)) / 86_400_000);
}
