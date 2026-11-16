/**
 * Geometría de un piso: medidas, zonas y nodos (de la API, o de public/pisos/geometria-piso1.json en
 * la simulación). Todo está en metros. El origen es la esquina de los ejes 1 y D: x crece hacia la
 * derecha e y hacia el fondo del plano. Lo que se DIBUJA (muros, muebles) viene de la maqueta 3D.
 */
export type TipoZona = 'INGRESO' | 'ESPERA' | 'TRIAJE' | 'SERVICIO' | 'TRANSITO' | 'SALIDA';

/** Punto [x, y] en metros. */
export type Punto = [number, number];

export interface ZonaGeometria {
  id: number;
  nombre: string;
  tipo: TipoZona;
  /** Esquina superior izquierda del rectángulo de la zona. */
  x: number;
  y: number;
  ancho: number;
  fondo: number;
  aforo: number;
  /** Porcentaje del aforo desde el que la zona pasa a concentración media. */
  umbralMedia: number;
  /** Porcentaje del aforo desde el que la zona pasa a concentración alta. */
  umbralAlta: number;
}

export interface NodoGeometria {
  codigo: string;
  x: number;
  y: number;
  zonaId: number;
}

export interface GeometriaPiso {
  pisoId: number;
  nombre: string;
  nivel: number;
  anchoM: number;
  fondoM: number;
  altoMuroM: number;
  sede: string;
  zonas: ZonaGeometria[];
  nodos: NodoGeometria[];
}

/** Lo que el render necesita saber de cada zona en cada momento. */
/** nd: sin datos (ningún nodo la cubre). ninguno: sin aforo definido, sin semáforo (Tránsito, RN-13). */
export type NivelOcupacion = 'alta' | 'media' | 'baja' | 'nd' | 'ninguno';

export interface OcupacionZona {
  zonaId: number;
  personas: number;
  aforo: number | null;
  nivel: NivelOcupacion;
  /** Texto corto bajo el nombre; por defecto "8/20". */
  etiqueta?: string;
  /** true: la zona aún no se monitorea (sin nodo); se ve solo su nombre, sin cifra ni color. */
  soloNombre?: boolean;
}
