import { Atencion } from '../../core/datos/modelos';
import { MIN } from '../../core/utils/tiempo';
import {
  FilaDatos, OcupacionApi, celdas, cifras, cola, diasDe, filaDesdeApi, filaVista, horasDe, mediana, paginar,
  percentil90, problemaRango, rangoDe, resumenDe, textoPeriodo,
} from './modelo-historial';
import { filasSimuladas, ocupacionSimulada } from './simulacion-historial';

const HOY = '2026-11-15';

describe('P5 · historial', () => {
  describe('periodo', () => {
    it('hoy, 7 y 30 días cuentan hoy', () => {
      expect(rangoDe('hoy', HOY, null)).toEqual({ desde: HOY, hasta: HOY });
      expect(rangoDe('7', HOY, null)).toEqual({ desde: '2026-11-09', hasta: HOY });
      expect(diasDe(rangoDe('30', HOY, null))).toBe(30);
    });

    it('un rango elegido se valida igual que en el backend', () => {
      expect(problemaRango({ desde: '2026-10-16', hasta: HOY }, HOY)).toBeNull();          // 31 días
      expect(problemaRango({ desde: '2026-10-15', hasta: HOY }, HOY)).toContain('31');     // 32 días
      expect(problemaRango({ desde: HOY, hasta: '2026-11-14' }, HOY)).toContain('anterior');
      expect(problemaRango({ desde: HOY, hasta: '2026-11-16' }, HOY)).toContain('hasta hoy');
    });

    it('el texto del periodo se lee como una frase', () => {
      expect(textoPeriodo({ desde: '2026-11-09', hasta: HOY }, HOY)).toBe('Del lunes 9 al domingo 15 de noviembre');
      expect(textoPeriodo({ desde: HOY, hasta: HOY }, HOY)).toBe('Hoy, domingo 15 de noviembre');
      expect(textoPeriodo({ desde: '2026-10-20', hasta: HOY }, HOY)).toBe('Del martes 20 de octubre al domingo 15 de noviembre');
    });
  });

  describe('cifras', () => {
    it('mediana y percentil 90 como en el backend', () => {
      expect(mediana([10, 15, 120])).toBe(15);
      expect(mediana([10, 15, 14, 11])).toBe(12);
      expect(percentil90(Array.from({ length: 10 }, (_, i) => i + 1))).toBe(9);
      expect(mediana([])).toBeNull();
    });

    it('«9 de cada 10» solo con 10 casos o más; con menos, se dice de cuántas sale', () => {
      expect(cola(12, 31, 'esperaron', '')).toBe('9 de cada 10 esperaron 31 min o menos');
      expect(cola(3, 31, 'esperaron', '')).toBe('mediana de 3 atenciones terminadas');
      expect(cola(0, null, 'esperaron', 'nada aún')).toBe('nada aún');
    });

    it('sin respuesta todavía, las cifras son «—» y no ceros', () => {
      expect(cifras(null).espera.valor).toBeNull();
    });
  });

  const fila = (extra: Partial<FilaDatos>): FilaDatos => ({
    clave: '1', codigo: 'A-0001', fecha: HOY, inicio: new Date(2026, 10, 15, 15, 48).getTime(), fin: new Date(2026, 10, 15, 16, 21).getTime(),
    estado: 'FINALIZADA', motivo: 'DEVUELTA', esperaMin: 14, consultaMin: 12, totalMin: 33, ...extra,
  });

  it('el resumen cuenta todas y mide solo las terminadas (la espera, solo si llegó a consulta)', () => {
    const r = resumenDe([fila({ esperaMin: 10 }), fila({ esperaMin: 20 }), fila({ esperaMin: 45, consultaMin: null }), fila({ fin: null, estado: 'EN_ESPERA' })]);
    expect(r).toMatchObject({ atenciones: 4, terminadas: 3, conEspera: 2, esperaMediana: 15 });
  });

  describe('tabla', () => {
    it('una abierta dice dónde está y sus minutos «en curso»; el cierre dice si el tiempo es exacto', () => {
      const abierta = filaVista(fila({ fin: null, estado: 'EN_ESPERA', motivo: null, consultaMin: null }));
      expect(abierta).toMatchObject({ fin: '—', consulta: '—', enCurso: true, cierre: { texto: 'En espera', tono: 'media' } });
      expect(filaVista(fila({})).cierre).toEqual({ texto: 'Devuelta', tono: 'nd' });
      expect(filaVista(fila({ motivo: 'SIN_LECTURAS' })).cierre).toEqual({ texto: 'Sin lecturas', tono: 'media' });
      expect(filaVista(fila({}))).toMatchObject({ entrega: '15:48', fecha: 'dom 15 nov' });
    });

    it('la API se traduce con el Id como clave para abrir el detalle', () => {
      const f = filaDesdeApi({ id: 42, codigo: 'A-0041', fecha: HOY, inicioEn: '2026-11-15T20:48:00Z', finEn: null, estado: 'EN_ATENCION', motivo: null, esperaMin: 14, consultaMin: 2, totalMin: 16 });
      expect(f).toMatchObject({ clave: '42', fin: null, inicio: Date.parse('2026-11-15T20:48:00Z') });
    });

    it('la búsqueda filtra por código y la página nunca se sale del rango', () => {
      const filas = Array.from({ length: 20 }, (_, i) => fila({ clave: String(i), codigo: `A-${String(i).padStart(4, '0')}` }));
      expect(paginar(filas, 'a-0012', 1).filas.map(f => f.codigo)).toEqual(['A-0012']);
      expect(paginar(filas, '', 9)).toMatchObject({ pagina: 3, total: 20 });
      expect(paginar(filas, '', 3).filas).toHaveLength(4);
    });
  });

  describe('mapa', () => {
    const ocupacion: OcupacionApi = {
      zonas: [], zonaId: 3, agrupacion: 'FECHA', horaDesde: 9, horaHasta: 11,
      filas: [{ fecha: '2026-11-09', diaSemana: null, celdas: [
        { hora: 9, personas: 0, estado: 'OK' }, { hora: 10, personas: 10.5, estado: 'OK' }, { hora: 11, personas: null, estado: 'SIN_DATOS' },
      ] }],
    };

    it('horas con dos dígitos y filas con su día', () => {
      expect(horasDe(ocupacion)).toEqual(['09', '10', '11']);
      expect(celdas(ocupacion)[0].etiqueta).toBe('lun 9');
      expect(celdas({ ...ocupacion, agrupacion: 'DIA_SEMANA', filas: [{ ...ocupacion.filas[0], fecha: null, diaSemana: 3 }] })[0].etiqueta).toBe('Miércoles');
    });

    it('la escala va hasta el máximo; un cero es un dato y «sin datos» no tiene número', () => {
      const [c0, c1, c2] = celdas(ocupacion)[0].celdas;
      expect(c0).toMatchObject({ estado: 'OK', texto: '0', intensidad: 0 });
      expect(c1).toMatchObject({ estado: 'OK', texto: '10.5', intensidad: 1 });
      expect(c2).toMatchObject({ estado: 'SIN_DATOS', texto: '' });
      expect(c2.titulo).toContain('sin datos');
    });
  });

  describe('simulación', () => {
    const t = (h: number, m = 0) => new Date(2026, 10, 15, h, m).getTime();
    const zona = (id: number) => id === 7 ? { nombre: 'Módulo 2', tipo: 'SERVICIO' } : { nombre: 'Sala de espera', tipo: 'ESPERA' };
    // Tres pacientes de 20 min en la Sala entre las 10 y las 11 → 1 persona en promedio
    const atenciones: Atencion[] = [0, 20, 40].map((m, i) => ({
      id: `A-000${i}`, tarjetaId: 'T-01', pisoId: 1, inicio: t(10, m), fin: t(10, m + 20) + 10 * MIN, estado: 'cerrada', motivo: 'Devuelta',
      eventos: [{ zonaId: 3, entrada: t(10, m), salida: t(10, m + 20) }, { zonaId: 7, entrada: t(10, m + 20), salida: t(10, m + 20) + 10 * MIN }],
    }));

    it('las filas usan la misma regla que el detalle simulado (P6)', () => {
      const filas = filasSimuladas(atenciones, { desde: HOY, hasta: HOY }, t(20), zona);
      expect(filas.map(f => f.codigo)).toEqual(['A-0002', 'A-0001', 'A-0000']);   // la más reciente primero
      expect(filas[0]).toMatchObject({ clave: 'A-0002', esperaMin: 20, consultaMin: 10, totalMin: 30, motivo: 'DEVUELTA' });
    });

    it('la ocupación son minutos-paciente entre 60, y sin nodo es «sin datos»', () => {
      const zonas = [{ id: 3, nombre: 'Sala de espera', tipo: 'ESPERA', aforo: null }, { id: 2, nombre: 'Hall', tipo: 'TRANSITO', aforo: null }];
      const o = ocupacionSimulada(atenciones, { desde: HOY, hasta: HOY }, t(20), zonas, new Set([3]), null);
      expect(o.zonas.map(z => z.id)).toEqual([3]);   // por el Hall solo se pasa: sin mapa
      expect(o.filas[0].celdas[0]).toEqual({ hora: 10, personas: 1, estado: 'OK' });
      const sinNodo = ocupacionSimulada(atenciones, { desde: HOY, hasta: HOY }, t(20), zonas, new Set(), null);
      expect(sinNodo.filas[0].celdas[0].estado).toBe('SIN_DATOS');
    });
  });
});
