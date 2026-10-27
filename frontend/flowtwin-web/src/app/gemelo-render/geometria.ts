/**
 * Tipos del archivo de geometría de un piso (public/pisos/geometria-piso1.json),
 * extraído del plano de AutoCAD. Todo está en metros. El origen es la esquina de los
 * ejes 1 y D: x crece hacia la derecha e y hacia el fondo del plano.
 */
export type TipoZona = 'INGRESO' | 'ESPERA' | 'SERVICIO' | 'TRANSITO' | 'SALIDA';

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

/** Un muro es un polígono exterior "o" con agujeros "h" (por ejemplo, vanos o columnas). */
export interface MuroGeometria {
  o: Punto[];
  h: Punto[][];
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
  muros: MuroGeometria[];
}

/** Lo que el render necesita saber de cada zona en cada momento. */
export type NivelOcupacion = 'alta' | 'media' | 'baja' | 'nd';

export interface OcupacionZona {
  zonaId: number;
  personas: number;
  aforo: number;
  nivel: NivelOcupacion;
  /** Texto corto bajo el nombre; por defecto "8/20". */
  etiqueta?: string;
}

/** Zona bajo el cursor, con su posición en pantalla para dibujar un tooltip. */
export interface ZonaHover {
  zonaId: number;
  clientX: number;
  clientY: number;
}
