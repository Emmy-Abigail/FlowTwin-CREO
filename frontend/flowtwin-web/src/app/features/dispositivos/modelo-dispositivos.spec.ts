import { DIA, MIN } from '../../core/utils/tiempo';
import { NodoInventario, TarjetaInventario, cuando, filtrosTarjetas, puedeDarDeBaja, raizTemas, textoNodo, textoProblema } from './modelo-dispositivos';

const AHORA = new Date(2026, 10, 15, 16, 0).getTime();
const iso = (t: number) => new Date(t).toISOString();
const tarjeta = (extra: Partial<TarjetaInventario>): TarjetaInventario => ({
  id: 1, codigo: 'T-11', estado: 'DISPONIBLE', estadoNombre: 'Disponible', codigoQr: 'CREO-T11',
  problema: null, ultimaDeteccionEn: null, zonaUltimaDeteccion: null, ...extra,
});

describe('P9 · dispositivos', () => {
  it('los filtros abren en lo que requiere atención y solo muestran estados con tarjetas', () => {
    const f = filtrosTarjetas({ ATENCION: 3, DISPONIBLE: 9412, EN_USO: 318, BAJA: 0, TODAS: 10000 });
    expect(f.map(x => x.id)).toEqual(['ATENCION', 'DISPONIBLE', 'EN_USO', 'TODAS']);
    expect(f[0]).toMatchObject({ n: 3, atencion: true });
  });

  it('cada problema dice qué pasa y qué revisar', () => {
    expect(textoProblema(tarjeta({ problema: 'NUNCA_DETECTADA' }), AHORA)?.texto).toBe('Nunca se ha detectado: ¿batería?');
    expect(textoProblema(tarjeta({ problema: 'SIN_DETECCION', ultimaDeteccionEn: iso(AHORA - 3 * DIA) }), AHORA)?.texto).toBe('No se detecta hace 3 días: ¿batería?');
    const vista = tarjeta({ ultimaDeteccionEn: iso(AHORA - 30 * MIN), zonaUltimaDeteccion: 'Sala de espera' });
    expect(textoProblema(vista, AHORA)).toEqual({ texto: 'Vista hoy 15:30 · Sala de espera', aviso: false });
    expect(textoProblema(tarjeta({}), AHORA)).toBeNull();
  });

  it('el estado del nodo en palabras', () => {
    const n = (estado: 'EN_LINEA' | 'SIN_SENAL', latido: number | null): NodoInventario => ({
      id: 1, codigo: 'N-03', mac: '', zonaId: 7, zona: 'Módulo 2', pisoId: 1, piso: 'Piso 1', estado, requiereAtencion: estado !== 'EN_LINEA',
      ultimoLatidoEn: latido === null ? null : iso(latido), posX: null, posY: null, ajusteRssiDb: 0,
    });
    expect(textoNodo(n('EN_LINEA', AHORA), AHORA)).toBe('En línea');
    expect(textoNodo(n('SIN_SENAL', AHORA - 8 * MIN), AHORA)).toBe('Sin señal desde las 15:52');
    expect(textoNodo(n('SIN_SENAL', AHORA - DIA), AHORA)).toBe('Sin señal desde ayer 16:00');
    expect(textoNodo(n('SIN_SENAL', null), AHORA)).toBe('Nunca se ha conectado');
  });

  it('solo se da de baja una tarjeta guardada o extraviada, y las fechas se leen fácil', () => {
    expect(['DISPONIBLE', 'EN_USO', 'SIN_DEVOLVER', 'EXTRAVIADA'].map(puedeDarDeBaja)).toEqual([true, false, false, true]);
    expect(cuando(AHORA - DIA, AHORA)).toBe('ayer 16:00');
    expect(raizTemas({ usuario: 'x', temaLecturas: 'flowtwin/v1/nodos/a4/lecturas', temaLatidos: '', temaRespuestas: '' })).toBe('flowtwin/v1/nodos/a4');
  });
});
