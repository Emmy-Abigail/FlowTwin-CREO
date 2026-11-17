import { MIN } from '../../core/utils/tiempo';
import { AtencionDetalleApi, desdeApi, desdeSimulacion } from './fuentes-atencion';
import { DetalleVista, EstadiaVista, aclaraciones, carrilDe, chipEstado, marcas, pasos, sigueAbierta, tramos } from './modelo-atencion';

const T0 = new Date(2026, 10, 15, 15, 23).getTime();
const m = (min: number) => T0 + min * MIN;
const en = (zona: string, tipo: string, desde: number, hasta: number | null): EstadiaVista =>
  ({ zona, tipo, entrada: m(desde), salida: hasta === null ? null : m(hasta) });

function detalle(extra: Partial<DetalleVista> = {}): DetalleVista {
  return {
    codigo: 'A-0012', sede: 'Sede Principal', tarjeta: 'T-01', inicio: T0, fin: m(35), estado: 'FINALIZADA', motivo: 'DEVUELTA',
    esperaMin: 19, consultaMin: 14, totalMin: 35,
    estadias: [en('Admisión y caja', 'INGRESO', 0, 2), en('Sala de espera', 'ESPERA', 2, 18), en('Hall principal', 'TRANSITO', 18, 19),
      en('Módulo 2', 'SERVICIO', 19, 33), en('Hall principal', 'TRANSITO', 33, 35)],
    ...extra,
  };
}

describe('P6 · detalle de atención', () => {
  it('cada tipo de zona va a su carril; lo desconocido, a «Paso»', () => {
    expect(['INGRESO', 'TRIAJE', 'ESPERA', 'SERVICIO', 'TRANSITO', 'SALIDA', 'DESCONOCIDO'].map(carrilDe))
      .toEqual(['admision', 'admision', 'espera', 'consulta', 'paso', 'paso', 'paso']);   // ADR-26: el triaje no es consulta
  });

  describe('tramos', () => {
    it('un recorrido continuo no tiene huecos', () => {
      const t = tramos(detalle(), m(60));
      expect(t.map(x => x.carril)).toEqual(['admision', 'espera', 'paso', 'consulta', 'paso']);
      expect(t.every(x => !x.enCurso)).toBe(true);
    });

    it('un minuto o más sin estadía es «Sin detectar», también al principio y al final', () => {
      const d = detalle({ estadias: [en('Sala de espera', 'ESPERA', 2, 10), en('Módulo 2', 'SERVICIO', 12, 30)] });
      const t = tramos(d, m(60));
      expect(t.map(x => [x.carril, (x.desde - T0) / MIN, (x.hasta - T0) / MIN])).toEqual([
        ['sin-detectar', 0, 2], ['espera', 2, 10], ['sin-detectar', 10, 12], ['consulta', 12, 30], ['sin-detectar', 30, 35],
      ]);
    });

    it('un hueco de segundos (lo que tarda el motor en confirmar) no se muestra', () => {
      const d = detalle({ fin: m(10), estadias: [{ zona: 'Sala', tipo: 'ESPERA', entrada: T0 + 45_000, salida: m(10) }] });
      expect(tramos(d, m(60)).map(x => x.carril)).toEqual(['espera']);
    });

    it('si sigue abierta, la zona actual termina en «ahora» y está en curso', () => {
      const d = detalle({ fin: null, motivo: null, estado: 'EN_ESPERA', estadias: [en('Sala de espera', 'ESPERA', 0, null)] });
      const t = tramos(d, m(16));
      expect(t).toHaveLength(1);
      expect(t[0]).toMatchObject({ hasta: m(16), enCurso: true });
    });

    it('una entrega anulada no tiene recorrido', () => {
      expect(tramos(detalle({ estado: 'ANULADA', motivo: 'ANULADA', fin: T0 + 10_000, estadias: [] }), m(1))).toEqual([]);
    });
  });

  describe('pasos', () => {
    it('entrega, cada tramo con su hora y el cierre con el total', () => {
      const p = pasos(detalle(), m(60));
      expect(p.map(x => x.titulo)).toEqual([
        'Se entregó la tarjeta T-01', 'Admisión y caja', 'Sala de espera', 'Hall principal', 'Módulo 2', 'Hall principal', 'Devolvió la tarjeta T-01',
      ]);
      expect(p[2].detalle).toBe('15:25–15:41 · 16 min');
      expect(p.at(-1)?.detalle).toBe('Total 35 min');
    });

    it('un hueco se explica y lo que sigue abierto dice «hasta ahora»', () => {
      const d = detalle({ fin: null, motivo: null, estado: 'EN_ESPERA', estadias: [en('Admisión y caja', 'INGRESO', 0, 2), en('Sala de espera', 'ESPERA', 3, null)] });
      const p = pasos(d, m(19));
      expect(p.map(x => x.titulo)).toEqual(['Se entregó la tarjeta T-01', 'Admisión y caja', 'No se detectó la tarjeta', 'Sala de espera']);
      expect(p[2].detalle).toBe('15:25–15:26 · 1 min sin lecturas de ningún nodo');
      expect(p[3].detalle).toBe('desde las 15:26 · 16 min hasta ahora');
    });

    it('el cierre automático dice por qué se cerró', () => {
      expect(pasos(detalle({ motivo: 'SIN_LECTURAS' }), m(60)).at(-1)?.titulo).toBe('Se cerró sola: la tarjeta dejó de detectarse');
    });
  });

  it('el eje marca inicio, fin y horas redondas sin encimarse', () => {
    expect(marcas(T0, m(35)).map(x => x.texto)).toEqual(['15:23', '15:30', '15:40', '15:50', '15:58']);
    expect(marcas(T0, m(19), true).at(-1)?.texto).toBe('15:42 ahora');
    expect(marcas(T0, m(300)).length).toBeLessThanOrEqual(6);
  });

  it('estado y aclaraciones: lo que aún no se puede calcular se explica', () => {
    const abierta = detalle({ fin: null, motivo: null, estado: 'EN_ESPERA', consultaMin: null });
    expect(chipEstado(abierta)).toEqual({ texto: 'En espera', tono: 'ambar' });
    expect(aclaraciones(abierta).espera).toBe('sigue corriendo: aún no entra a consulta');
    expect(aclaraciones(abierta).consulta).toBe('todavía no entra a una zona de atención');
    expect(sigueAbierta(abierta)).toBe(true);
    expect(chipEstado(detalle())).toEqual({ texto: 'Finalizada · tarjeta devuelta', tono: 'gris' });
    expect(sigueAbierta(detalle())).toBe(false);
  });

  describe('fuentes', () => {
    it('la API se traduce con las fechas en milisegundos', () => {
      const api: AtencionDetalleApi = {
        id: 42, codigo: 'A-0012', fecha: '2026-11-15', sede: 'Sede Principal', tarjeta: 'T-01',
        inicioEn: '2026-11-15T20:23:00Z', finEn: null, estado: 'EN_ATENCION', motivo: null, esperaMin: 10, consultaMin: 2, totalMin: 12,
        estadias: [{ zonaId: 7, zona: 'Módulo 2', tipo: 'SERVICIO', entradaEn: '2026-11-15T20:33:00Z', salidaEn: null }],
      };
      const d = desdeApi(api);
      expect(d.inicio).toBe(Date.parse('2026-11-15T20:23:00Z'));
      expect(d.fin).toBeNull();
      expect(d.estadias[0]).toEqual({ zona: 'Módulo 2', tipo: 'SERVICIO', entrada: Date.parse('2026-11-15T20:33:00Z'), salida: null });
    });

    it('la simulación usa el mismo modelo y sus mismas reglas', () => {
      const zona = (id: number) => id === 7 ? { nombre: 'Módulo 2', tipo: 'SERVICIO' } : { nombre: 'Sala de espera', tipo: 'ESPERA' };
      const d = desdeSimulacion({
        id: 'A-1828', tarjetaId: 'T-05', pisoId: 1, inicio: T0, fin: null, estado: 'activa', motivo: null,
        eventos: [{ zonaId: 3, entrada: m(1), salida: m(11) }, { zonaId: 7, entrada: m(11), salida: null }],
      }, m(20), zona, 'Sede Principal');
      expect(d).toMatchObject({ codigo: 'A-1828', tarjeta: 'T-05', estado: 'EN_ATENCION', esperaMin: 11, consultaMin: 9, totalMin: 20 });
    });
  });
});
