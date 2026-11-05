/**
 * Reglas del formulario de login: funciones puras, una por campo. Devuelven el mensaje que se muestra
 * debajo del campo, o null si está bien. Cada mensaje es un caso de prueba (validacion-login.spec.ts).
 *
 * A propósito, el login NO valida la política de contraseñas: daría pistas a quien prueba claves y
 * bloquearía a quien tiene una contraseña válida anterior a la política. Esa regla vive donde se crea
 * una contraseña (cambiar contraseña, alta de usuario).
 */

/** Forma básica de un correo: algo@algo.algo, sin espacios. La verdad final la tiene el servidor. */
const FORMA_CORREO = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export function errorCorreo(correo: string): string | null {
  const valor = correo.trim();
  if (!valor) return 'Escribe tu correo institucional.';
  if (!FORMA_CORREO.test(valor)) return 'Escribe un correo con la forma nombre@creo.pe';
  return null;
}

export function errorClave(clave: string): string | null {
  return clave ? null : 'Escribe tu contraseña.';
}
