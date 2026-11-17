import { HttpErrorResponse } from '@angular/common/http';

/** Base de todas las rutas de la API (sección API → Convenciones). En desarrollo, el proxy la reenvía al puerto 5080. */
export const API = '/api/v1';

/**
 * Error de la API en formato ProblemDetails (sección "Traducción de errores").
 * codigo es estable (RN03_POR_DESINFECTAR, SIN_PERMISO…): el frontend decide con él, no con el texto.
 */
export interface Problema {
  status: number;
  codigo: string;
  titulo: string;
  datos?: Record<string, unknown>;
  errores?: Record<string, string[]>;
  traceId?: string;
}

export function problemaDe(error: unknown): Problema {
  if (error instanceof HttpErrorResponse) {
    const cuerpo = (error.error ?? {}) as Record<string, unknown>;
    if (error.status === 0) {
      return { status: 0, codigo: 'SIN_CONEXION', titulo: 'No hay conexión con el servidor. Revisa que la API esté encendida.' };
    }
    return {
      status: error.status,
      codigo: (cuerpo['codigo'] as string) ?? 'ERROR_INTERNO',
      titulo: (cuerpo['title'] as string) ?? 'Ocurrió un error inesperado.',
      datos: cuerpo['datos'] as Record<string, unknown> | undefined,
      errores: cuerpo['errores'] as Record<string, string[]> | undefined,
      traceId: cuerpo['traceId'] as string | undefined,
    };
  }
  return { status: 0, codigo: 'ERROR_INTERNO', titulo: 'Ocurrió un error inesperado.' };
}

/** Mensaje para mostrar: el título y, si vienen, los errores por campo. */
export function mensajeDe(p: Problema): string {
  const detalles = p.errores ? Object.values(p.errores).flat() : [];
  return detalles.length ? detalles.join(' ') : p.titulo;
}

/**
 * Errores de validación del servidor por campo, con el nombre del campo en minúscula inicial
 * ("Correo" → "correo"), como lo usan los formularios. Solo el primer mensaje de cada campo.
 */
export function erroresPorCampo(p: Problema): Record<string, string> {
  return Object.fromEntries(
    Object.entries(p.errores ?? {})
      .filter(([, mensajes]) => mensajes.length > 0)
      .map(([campo, mensajes]) => [campo.charAt(0).toLowerCase() + campo.slice(1), mensajes[0]]),
  );
}
