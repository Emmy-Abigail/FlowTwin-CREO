import {
  DatosUsuario, UsuarioAdmin, datosParaEnviar, estadoUsuario, filtrarUsuarios, formularioValido, textoPisos, textoRol, textoUltimoIngreso,
} from './modelo-usuarios';

const usuario = (extra: Partial<UsuarioAdmin> = {}): UsuarioAdmin => ({
  id: 1, nombres: 'Rosa', apellidos: 'Quispe', correo: 'rosa@creo.pe', rol: 'OPERADOR', pisos: [1],
  estadoRegistro: true, debeCambiarContrasena: false, ultimoIngresoEn: null, ...extra,
});

describe('P10 · Usuarios', () => {
  it('solo el Operador tiene pisos; Administrador y Gerencia ven «Todos» (ADR-29)', () => {
    const nombre = (id: number) => `Piso ${id}`;
    expect(textoPisos(usuario({ pisos: [1, 2] }), nombre)).toBe('Piso 1, Piso 2');
    expect(textoPisos(usuario({ rol: 'GERENCIA', pisos: [] }), nombre)).toBe('Todos');
    expect(textoPisos(usuario({ rol: 'ADMIN', pisos: [] }), nombre)).toBe('Todos');
    expect(textoPisos(usuario({ pisos: [] }), nombre)).toBe('—');
    expect(textoRol('GERENCIA')).toBe('Gerencia');
  });

  it('el estado destaca a quien aún no cambió su contraseña y a quien está inactivo', () => {
    expect(estadoUsuario(usuario())).toEqual({ texto: 'Activo', tono: 'ok' });
    expect(estadoUsuario(usuario({ debeCambiarContrasena: true })).tono).toBe('aviso');
    expect(estadoUsuario(usuario({ estadoRegistro: false, debeCambiarContrasena: true }))).toEqual({ texto: 'Inactivo', tono: 'off' });
  });

  it('el último ingreso dice «Nunca» si no ha entrado', () => {
    expect(textoUltimoIngreso(usuario(), Date.now())).toBe('Nunca');
    const ahora = Date.parse('2026-11-16T15:00:00');
    expect(textoUltimoIngreso(usuario({ ultimoIngresoEn: '2026-11-16T09:41:00' }), ahora)).toBe('hoy 09:41');
  });

  it('busca por nombre o correo sin importar tildes ni mayúsculas, y filtra por rol', () => {
    const lista = [usuario(), usuario({ id: 2, nombres: 'Marta', apellidos: 'Díaz', correo: 'gerencia@creo.pe', rol: 'GERENCIA' })];
    expect(filtrarUsuarios(lista, 'DIAZ', '').map(u => u.id)).toEqual([2]);
    expect(filtrarUsuarios(lista, 'rosa@', '').map(u => u.id)).toEqual([1]);
    expect(filtrarUsuarios(lista, '', 'OPERADOR').map(u => u.id)).toEqual([1]);
    expect(filtrarUsuarios(lista, '  ', '')).toHaveLength(2);
  });

  describe('formulario', () => {
    const datos = (extra: Partial<DatosUsuario> = {}): DatosUsuario =>
      ({ nombres: 'Luis', apellidos: 'Ramos', correo: 'luis@creo.pe', rol: 'OPERADOR', pisos: [1], ...extra });

    it('el Operador necesita al menos un piso (RN-24); los demás roles no', () => {
      expect(formularioValido(datos())).toBe(true);
      expect(formularioValido(datos({ pisos: [] }))).toBe(false);
      expect(formularioValido(datos({ rol: 'GERENCIA', pisos: [] }))).toBe(true);
      expect(formularioValido(datos({ rol: 'ADMIN', pisos: [] }))).toBe(true);
    });

    it('pide nombre, apellido y un correo con forma de correo', () => {
      expect(formularioValido(datos({ nombres: ' ' }))).toBe(false);
      expect(formularioValido(datos({ apellidos: '' }))).toBe(false);
      expect(formularioValido(datos({ correo: 'luis' }))).toBe(false);
    });

    it('no envía pisos si el rol los ve todos', () => {
      expect(datosParaEnviar(datos({ rol: 'GERENCIA', pisos: [1, 2] })).pisos).toEqual([]);
      expect(datosParaEnviar(datos({ pisos: [1, 2] })).pisos).toEqual([1, 2]);
    });
  });
});
