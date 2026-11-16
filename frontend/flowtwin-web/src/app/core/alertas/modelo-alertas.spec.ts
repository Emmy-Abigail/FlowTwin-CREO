import { Atencion, Nodo } from '../datos/modelos';
import { MIN } from '../utils/tiempo';
import { Alerta, alertasSimuladas, desdeApi, nuevas, vista } from './modelo-alertas';

const T0 = new Date(2026, 10, 15, 15, 0).getTime();
const zona = (id: number) => (id === 3 ? 'Sala de espera' : `Zona ${id}`);

describe('F5 · alertas', () => {
  const espera: Alerta = { clave: 'ESPERA_LARGA:9:CRITICA', tipo: 'ESPERA_LARGA', nivel: 'CRITICA', desde: T0, atencion: '9', atencionId: 9, codigo: 'A-0043', zonaId: 3, nodo: null };

  it('el texto es corto: quién, cuánto y dónde', () => {
    expect(vista(espera, T0 + 62 * MIN, zona)).toMatchObject({ titulo: 'A-0043 · 62 min esperando', detalle: 'Sala de espera', tono: 'roja', puedeCerrar: false });
    const sin = vista({ ...espera, tipo: 'TARJETA_SIN_DEVOLVER', nivel: 'ALTA' }, T0 + 34 * MIN, zona);
    expect(sin).toMatchObject({ titulo: 'A-0043 · sin lecturas hace 34 min', detalle: 'Visto en Sala de espera', puedeCerrar: true });
    const nodo = vista({ ...espera, tipo: 'NODO_SIN_SENAL', nivel: 'ALTA', nodo: 'N-02', codigo: null }, T0, zona);
    expect(nodo).toMatchObject({ titulo: 'Nodo N-02 sin señal', grupo: 'Equipos' });
  });

  it('solo suena lo nuevo; la primera carga no suena', () => {
    expect(nuevas(null, [espera])).toEqual([]);
    expect(nuevas(new Set([espera.clave]), [espera])).toEqual([]);
    const subio = { ...espera, clave: 'ESPERA_LARGA:9:ALTA' };
    expect(nuevas(new Set([espera.clave]), [subio])).toEqual([subio]);
  });

  it('la API se traduce con el Id como clave del recorrido', () => {
    const a = desdeApi({ clave: 'x', tipo: 'TARJETA_SIN_DEVOLVER', nivel: 'ALTA', desde: '2026-11-15T20:00:00Z', atencionId: 42, codigo: 'A-0031', zonaId: 3, nodo: null });
    expect(a).toMatchObject({ atencion: '42', atencionId: 42, desde: Date.parse('2026-11-15T20:00:00Z') });
  });

  it('en la simulación, las mismas reglas: espera sin consulta y nodos sin señal', () => {
    const at = (id: string, min: number, zonaId: number): Atencion => ({
      id, tarjetaId: 'T-01', pisoId: 1, inicio: T0 - min * MIN, fin: null, estado: 'activa', motivo: null,
      eventos: [{ zonaId, entrada: T0 - min * MIN, salida: null }],
    });
    const nodos: Nodo[] = [{ codigo: 'N-02', mac: 'x', pisoId: 1, zonaId: 3, estado: 'Sin señal', ultimoLatido: T0 - 5 * MIN }];
    const lista = alertasSimuladas([at('A-1', 45, 3), at('A-2', 10, 3), at('A-3', 70, 7)], nodos, T0);

    expect(lista.map(a => a.clave)).toEqual(['ESPERA_LARGA:A-1:ALTA', 'NODO_SIN_SENAL:N-02']);   // A-3 está en consulta
  });
});
