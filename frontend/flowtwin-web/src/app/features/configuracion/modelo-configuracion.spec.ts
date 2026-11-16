import { aforoDesdeTexto, llevaAforo, textoTipo } from './modelo-configuracion';

describe('Configuración', () => {
  it('solo espera e ingreso llevan aforo, y los tipos se dicen como en la clínica', () => {
    expect(['ESPERA', 'INGRESO', 'TRIAJE', 'SERVICIO', 'TRANSITO'].map(llevaAforo)).toEqual([true, true, false, false, false]);
    expect(textoTipo('SERVICIO')).toBe('Consulta');
  });

  it('el aforo vacío queda por definir y uno inválido no se guarda', () => {
    expect(aforoDesdeTexto(' 24 ')).toBe(24);
    expect(aforoDesdeTexto('')).toBeNull();
    expect(aforoDesdeTexto('0')).toBeUndefined();
    expect(aforoDesdeTexto('2.5')).toBeUndefined();
  });
});
