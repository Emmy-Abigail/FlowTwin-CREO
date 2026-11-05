/**
 * Reglas del formulario «Cambiar contraseña»: funciones puras, una por campo. Devuelven el mensaje que se
 * muestra debajo del campo, o null si está bien. Cada mensaje es un caso de prueba (validacion-contrasena.spec.ts).
 *
 * Son las mismas reglas que valida el servidor (PoliticaContrasena.cs): el navegador avisa antes para
 * ahorrar un viaje, pero la verdad final la tiene el servidor (por ejemplo, no repetir las 3 últimas).
 */

export type CampoClave = 'actual' | 'nueva' | 'confirmacion';

/** Requisitos de la política, para la lista que se va marcando mientras se escribe. */
export function requisitosClave(clave: string): { ok: boolean; texto: string }[] {
  return [
    { ok: clave.length >= 8, texto: 'Al menos 8 caracteres' },
    { ok: /\p{Lu}/u.test(clave), texto: 'Una letra mayúscula' },
    { ok: /\p{Ll}/u.test(clave), texto: 'Una letra minúscula' },
    { ok: /\d/.test(clave), texto: 'Un número' },
    { ok: /[^\p{L}\d]/u.test(clave), texto: 'Un símbolo (# $ % & * ! ?)' },
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
  const faltan = requisitosClave(nueva).filter(r => !r.ok).map(r => r.texto.toLowerCase());
  if (faltan.length) return `Le falta: ${faltan.join(', ')}.`;
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
