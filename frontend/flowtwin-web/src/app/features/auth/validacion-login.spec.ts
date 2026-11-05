import { errorClave, errorCorreo } from './validacion-login';

describe('Reglas del login', () => {
  describe('correo', () => {
    it('vacío pide escribirlo', () => {
      expect(errorCorreo('')).toBe('Escribe tu correo institucional.');
    });

    it('solo espacios cuenta como vacío', () => {
      expect(errorCorreo('   ')).toBe('Escribe tu correo institucional.');
    });

    it.each(['ana.torres', 'ana@', 'ana@creo', '@creo.pe', 'ana torres@creo.pe'])('«%s» no tiene forma de correo', correo => {
      expect(errorCorreo(correo)).toBe('Escribe un correo con la forma nombre@creo.pe');
    });

    it('un correo bien formado es válido, aunque tenga espacios alrededor', () => {
      expect(errorCorreo('  ana.torres@creo.pe ')).toBeNull();
    });
  });

  describe('contraseña', () => {
    it('vacía pide escribirla', () => {
      expect(errorClave('')).toBe('Escribe tu contraseña.');
    });

    it('cualquier contraseña escrita es válida: el login no revela la política', () => {
      expect(errorClave('abc')).toBeNull();
    });
  });
});
