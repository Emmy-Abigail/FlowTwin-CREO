/**
 * Reglas del formulario «Cambiar contraseña»: funciones puras, una por campo. Devuelven el mensaje que se
 * muestra debajo del campo, o null si está bien. Cada mensaje es un caso de prueba (validacion-contrasena.spec.ts).
 *
 * Son las mismas reglas que valida el servidor (PoliticaContrasena.cs, OWASP ASVS): lo que importa es la
 * longitud, no combinar mayúsculas, números y símbolos; una frase con espacios vale. El navegador avisa antes para
 * ahorrar un viaje, pero la verdad final la tiene el servidor: él revisa además que no sea una contraseña filtrada
 * en ataques conocidos y que no repita las 3 últimas.
 */

export type CampoClave = 'actual' | 'nueva' | 'confirmacion';

export const LONGITUD_MINIMA = 12;
export const LONGITUD_MAXIMA = 128;

const variada = (clave: string) => new Set(clave).size >= 4;

/** Requisitos de la política, para la lista que se va marcando mientras se escribe. */
export function requisitosClave(clave: string): { ok: boolean; texto: string }[] {
  return [
    { ok: clave.length >= LONGITUD_MINIMA, texto: `Al menos ${LONGITUD_MINIMA} caracteres (puede ser una frase)` },
    { ok: variada(clave), texto: 'Sin repetir lo mismo (aaaa…, 1212…)' },
  ];
}

export function errorActual(actual: string, primerIngreso: boolean): string | null {
  if (actual) return null;
  return primerIngreso ? 'Escribe la contraseña temporal que te dieron.' : 'Escribe tu contraseña actual.';
}

/** Palabras del usuario que no pueden ir en su contraseña: lo de antes de la @ y cada nombre de 3+ letras. */
function palabrasPersonales(correo: string, nombre: string): string[] {
  return [correo.split('@')[0], ...nombre.split(/\s+/)].filter(p => p.length >= 3);
}

export function errorNueva(nueva: string, actual: string, correo: string, nombre: string): string | null {
  if (!nueva) return 'Escribe la nueva contraseña.';
  if (nueva.length < LONGITUD_MINIMA) return `Debe tener al menos ${LONGITUD_MINIMA} caracteres. Puede ser una frase con espacios.`;
  if (nueva.length > LONGITUD_MAXIMA) return `Puede tener como máximo ${LONGITUD_MAXIMA} caracteres.`;
  if (!variada(nueva)) return 'Es demasiado repetitiva: usa una frase o más variedad de caracteres.';
  const minuscula = nueva.toLowerCase();
  if (palabrasPersonales(correo, nombre).some(p => minuscula.includes(p.toLowerCase())))
    return 'No puede contener tu correo ni tu nombre.';
  if (nueva === actual) return 'Debe ser distinta de la contraseña actual.';
  return null;
}

export function errorConfirmacion(confirmacion: string, nueva: string): string | null {
  if (!confirmacion) return 'Repite la nueva contraseña.';
  return confirmacion === nueva ? null : 'No coincide con la nueva contraseña.';
}
