import { aforoDesdeTexto, llevaAforo, textoTipo } from './modelo-configuracion';

describe('Configuración', () => {
  it('llevan aforo las zonas donde el paciente permanece, no las de paso, y los tipos se dicen como en la clínica', () => {
    expect(['ESPERA', 'INGRESO', 'TRIAJE', 'SERVICIO', 'TRANSITO', 'SALIDA'].map(llevaAforo)).toEqual([true, true, true, true, false, false]);
    expect(textoTipo('SERVICIO')).toBe('Consulta');
  });

  it('el aforo vacío queda por definir y uno inválido no se guarda', () => {
    expect(aforoDesdeTexto(' 24 ')).toBe(24);
    expect(aforoDesdeTexto('')).toBeNull();
    expect(aforoDesdeTexto('0')).toBeUndefined();
    expect(aforoDesdeTexto('2.5')).toBeUndefined();
  });
});
