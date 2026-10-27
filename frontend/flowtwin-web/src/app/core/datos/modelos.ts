/**
 * Modelos del dominio de CREO FlowTwin. Son los mismos que tendrá la API del backend:
 * cuando exista, los servicios devolverán estos tipos y las pantallas no cambiarán.
 * Nunca hay datos personales del paciente: solo un ID anónimo (A-0143) y su tarjeta.
 */
export type Rol = 'Operador' | 'Gerencia' | 'Administrador';
export type EstadoUsuario = 'Activo' | 'Primer ingreso' | 'Desactivado';

export interface Usuario {
  id: string;
  nombre: string;
  correo: string;
  rol: Rol;
  /** Pisos asignados (solo limitan al Operador). */
  pisos: number[];
  estado: EstadoUsuario;
  /** Solo en la demo: la contraseña vive en memoria. En el sistema real la valida el backend. */
  clave: string;
  ultimoIngreso: number | null;
}

/** Quién hace una acción, para la auditoría. */
export interface Actor {
  nombre: string;
  rol: Rol | '—';
}

export type EstadoTarjeta = 'Disponible' | 'En uso' | 'Por desinfectar' | 'Sin devolver' | 'Extraviada' | 'Baja';

export interface Tarjeta {
  id: string;           // T-07
  estado: EstadoTarjeta;
  atencionId: string | null;
  ble: string;          // identificador BLE vinculado al QR
  desde: number | null; // desde cuándo está en este estado
}

/** Estancia de una atención en una zona. salida = null si sigue ahí. */
export interface EventoZona {
  zonaId: number;
  entrada: number;
  salida: number | null;
}

/** Paso del recorrido planificado. Solo lo usa el SimuladorService. */
export interface PasoRuta {
  zonaId: number;
  minutos: number;
}

export type EstadoAtencion = 'activa' | 'cerrada';
export type MotivoCierre = 'Devuelta' | 'Sin lecturas' | 'Cierre de jornada';

export interface Atencion {
  id: string;           // A-0143
  tarjetaId: string;
  pisoId: number;
  inicio: number;
  fin: number | null;
  estado: EstadoAtencion;
  motivo: MotivoCierre | null;
  eventos: EventoZona[];
  /** Solo en la simulación: recorrido planificado y paso actual. En el sistema real
   *  el recorrido lo dictan las lecturas de los nodos ESP32. */
  ruta?: PasoRuta[];
  paso?: number;
}

export type EstadoNodo = 'En línea' | 'Sin señal' | 'Esperando primera señal' | 'Baja';

export interface Nodo {
  codigo: string;       // N-01
  mac: string;
  pisoId: number;
  zonaId: number;
  estado: EstadoNodo;
  ultimoLatido: number;
}

export type TipoOperacion = 'Entregada' | 'Recibida' | 'Desinfectada' | 'Recuperada';

/** Operación de Admisión, para la lista "Últimas operaciones". */
export interface Operacion {
  t: number;
  tipo: TipoOperacion;
  tarjetaId: string;
  atencionId?: string;
  totalMin?: number;
}

export type TipoAuditoria = 'Sesión' | 'Admisión' | 'Seguimiento' | 'Gemelo' | 'Dispositivos' | 'Usuarios' | 'Reportes';

export interface EntradaAuditoria {
  t: number;
  usuario: string;
  rol: Rol | '—';
  tipo: TipoAuditoria;
  accion: string;
  elemento: string;
  detalle: string;
}

export interface Alerta {
  id: number;
  nivel: 'alta' | 'media';
  texto: string;
  detalle: string;
  t: number;
  /** Pantalla a la que lleva al hacer clic. */
  ruta: string;
  leida: boolean;
}

/** Zona con el seguimiento pausado (P7, Fase B). */
export interface PausaZona {
  motivo: string;
  por: string;
  t: number;
}

/** Tono visual de un chip. Los de semáforo solo se usan para datos. */
export type TonoChip = '' | 'baja' | 'media' | 'alta' | 'nd' | 'dark' | 'red';
