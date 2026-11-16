import { errorActual, errorConfirmacion, errorNueva, requisitosClave } from './validacion-contrasena';

/** Cada mensaje del formulario «Cambiar contraseña» es un caso de prueba. Usuaria de ejemplo: Ana Torres. */
describe('Validación de la contraseña', () => {
  const CORREO = 'ana.torres@creo.pe', NOMBRE = 'Ana Torres';
  const nueva = (clave: string, actual = 'mi clave vieja 2025') => errorNueva(clave, actual, CORREO, NOMBRE);

  describe('contraseña actual', () => {
    it('vacía: pide escribirla', () => expect(errorActual('', false)).toBe('Escribe tu contraseña actual.'));
    it('en el primer ingreso habla de la contraseña temporal', () =>
      expect(errorActual('', true)).toBe('Escribe la contraseña temporal que te dieron.'));
    it('con algo escrito no se juzga aquí: la verifica el servidor', () => expect(errorActual('x', false)).toBeNull());
  });

  describe('nueva contraseña', () => {
    it('vacía', () => expect(nueva('')).toBe('Escribe la nueva contraseña.'));
    it('corta: pide 12 caracteres y sugiere una frase', () =>
      expect(nueva('Ab1#')).toBe('Debe tener al menos 12 caracteres. Puede ser una frase con espacios.'));
    it('demasiado larga', () => expect(nueva('x'.repeat(120) + 'abcdefghij')).toBe('Puede tener como máximo 128 caracteres.'));
    it('repetitiva', () => expect(nueva('1212121212121')).toBe('Es demasiado repetitiva: usa una frase o más variedad de caracteres.'));
    it('ya no exige mayúsculas, números ni símbolos (OWASP ASVS)', () => expect(nueva('el gemelo de la sala azul')).toBeNull());
    it('no puede contener el nombre', () => expect(nueva('Torres en la sala 2026')).toBe('No puede contener tu correo ni tu nombre.'));
    it('no puede contener el usuario del correo', () => expect(nueva('Xana.torres#1')).toBe('No puede contener tu correo ni tu nombre.'));
    it('no puede ser igual a la actual', () => expect(nueva('mi clave vieja 2025')).toBe('Debe ser distinta de la contraseña actual.'));
    it('una buena contraseña pasa', () => expect(nueva('Riñón#Azul26')).toBeNull());
    it('acepta tildes, Ñ y espacios', () => expect(requisitosClave('Ñandú en la puna').every(r => r.ok)).toBe(true));
  });

  describe('repetir la nueva', () => {
    it('vacía', () => expect(errorConfirmacion('', 'Riñón#Azul26')).toBe('Repite la nueva contraseña.'));
    it('distinta', () => expect(errorConfirmacion('Riñón#Azul2', 'Riñón#Azul26')).toBe('No coincide con la nueva contraseña.'));
    it('igual', () => expect(errorConfirmacion('Riñón#Azul26', 'Riñón#Azul26')).toBeNull());
  });
});
