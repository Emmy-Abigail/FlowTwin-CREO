import {
  DEVOLUCION_RAPIDA_MIN, EscaneoApi, VistaPreviaApi, avisoDeProblema, confirmacionDe, haceCuanto, vistaPreviaDe,
} from './presentacion-admision';

/** Reglas de Admisión (R1–R9, ver presentacion-admision.ts). Cada mensaje es un caso de prueba. */
describe('Reglas de Admisión', () => {
  const AHORA = Date.parse('2026-11-06T15:30:00Z');
  const minAntes = (m: number) => new Date(AHORA - m * 60_000).toISOString();
  const previa = (operacion: VistaPreviaApi['operacion'], extra: Partial<VistaPreviaApi> = {}): VistaPreviaApi =>
    ({ tarjetaId: 7, tarjeta: 'T-07', estado: 'X', desde: minAntes(30), operacion, codigo: null, mensaje: null, ...extra });
  const atencion = { id: 1, codigo: 'A-0143', inicioEn: '2026-11-06T15:31:00Z', finEn: '2026-11-06T16:23:00Z', esperaMin: null, consultaMin: null, totalMin: 52 };
  const escaneo = (operacion: EscaneoApi['operacion']): EscaneoApi => ({
    operacion, tarjeta: { id: 7, codigo: 'T-07', estado: 'X', estadoNombre: 'X' },
    atencion: operacion === 'RECUPERADA' ? null : atencion, deshacerHasta: null,
  });

  describe('R1 · antes de registrar se ve qué va a pasar', () => {
    it('disponible → se entregará', () => expect(vistaPreviaDe(previa('ENTREGA'), AHORA)).toMatchObject({ tono: 'verde', titulo: 'T-07 · se entregará' }));
    it('en uso → se recibirá', () => expect(vistaPreviaDe(previa('DEVOLUCION'), AHORA)).toMatchObject({ tono: 'celeste', titulo: 'T-07 · se recibirá' }));
    it('perdida → volvió', () => expect(vistaPreviaDe(previa('RECUPERADA'), AHORA).titulo).toBe('T-07 · volvió una tarjeta perdida'));
  });

  describe('R2 · si una regla lo impide, se avisa y no se registra (tono rojo)', () => {
    it('por desinfectar: ofrece desinfectarla', () => {
      const a = vistaPreviaDe(previa(null, { codigo: 'RN03_POR_DESINFECTAR', mensaje: 'x' }), AHORA);
      expect(a).toMatchObject({ tono: 'rojo', titulo: 'T-07 · aún no está desinfectada', desinfectar: { id: 7, codigo: 'T-07' } });
    });
    it('de baja u otra sede: el mensaje del servidor', () =>
      expect(vistaPreviaDe(previa(null, { codigo: 'RN_TARJETA_DE_BAJA', mensaje: 'La tarjeta T-07 está dada de baja.' }), AHORA))
        .toMatchObject({ tono: 'rojo', titulo: 'La tarjeta T-07 está dada de baja.' }));
    it('no registrada: dice a quién pedírselo', () =>
      expect(avisoDeProblema('TARJETA_NO_REGISTRADA', '')).toMatchObject({ tono: 'rojo', texto: expect.stringContaining('Administrador') }));
    it('sin conexión: deja claro que no se registró nada', () => expect(avisoDeProblema('SIN_CONEXION', '').texto).toContain('No se registró'));
  });

  describe(`R3 · devolver antes de ${DEVOLUCION_RAPIDA_MIN} min pide confirmación`, () => {
    it('entregada hace 1 min: aviso ámbar y pregunta antes de registrar', () => {
      const a = vistaPreviaDe(previa('DEVOLUCION', { desde: minAntes(1) }), AHORA);
      expect(a.tono).toBe('ambar');
      expect(a.texto).toBe('Se entregó hace 1 min. ¿Es la tarjeta correcta?');
      expect(a.confirmar).toMatchObject({ titulo: '¿Recibir T-07?', accion: 'Sí, recibirla' });
    });
    it('hace segundos: «hace menos de 1 min»', () =>
      expect(vistaPreviaDe(previa('DEVOLUCION', { desde: minAntes(0.2) }), AHORA).texto).toContain('hace menos de 1 min'));
    it(`justo a los ${DEVOLUCION_RAPIDA_MIN} min ya no pregunta`, () =>
      expect(vistaPreviaDe(previa('DEVOLUCION', { desde: minAntes(DEVOLUCION_RAPIDA_MIN) }), AHORA).confirmar).toBeUndefined());
    it('una entrega nunca pregunta', () => expect(vistaPreviaDe(previa('ENTREGA', { desde: minAntes(0) }), AHORA).confirmar).toBeUndefined());
  });

  describe('después de registrar: confirmación compacta', () => {
    it('entrega: una línea; al paciente no se le dicta ningún código (lo siguen los nodos)', () => {
      const c = confirmacionDe(escaneo('ENTREGA'));
      expect(c.titulo).toMatch(/^T-07 entregada · \d\d:\d\d$/);
      expect(c.detalle).toBe('Su recorrido empezó');   // el código de la atención ya está en «Últimas operaciones»
    });
    it('devolución: cuánto estuvo y qué hacer con la tarjeta', () =>
      expect(confirmacionDe(escaneo('DEVOLUCION')).detalle).toBe('52 min en la clínica · déjala para desinfectar'));
    it('recuperada: ofrece desinfectarla', () => expect(confirmacionDe(escaneo('RECUPERADA')).desinfectar).toEqual({ id: 7, codigo: 'T-07' }));
  });

  describe('hace cuánto: la unidad cambia con el tiempo, nunca «hace 225 h»', () => {
    const local = (d: number, h: number, m = 0) => new Date(2026, 10, d, h, m).getTime();   // noviembre, hora local
    const AHORA_LOCAL = local(6, 15, 30);
    it('menos de 1 min: recién', () => expect(haceCuanto(AHORA_LOCAL - 30_000, AHORA_LOCAL)).toBe('recién'));
    it('minutos', () => expect(haceCuanto(local(6, 15, 23), AHORA_LOCAL)).toBe('hace 7 min'));
    it('horas del mismo día', () => expect(haceCuanto(local(6, 14, 25), AHORA_LOCAL)).toBe('hace 1 h 05 min'));
    it('anoche ya es «ayer» aunque no hayan pasado 24 h', () => expect(haceCuanto(local(5, 23, 50), AHORA_LOCAL)).toBe('ayer, 23:50'));
    it('días de la semana', () => expect(haceCuanto(local(3, 9), AHORA_LOCAL)).toBe('hace 3 días'));
    it('más de una semana: la fecha', () => expect(haceCuanto(new Date(2026, 9, 28, 9).getTime(), AHORA_LOCAL)).toBe('el 28/10'));
  });
});
