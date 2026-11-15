import {
  PacienteVista, ZonaVista, detallePaciente, estadoDeApi, etiquetaZona, geometriaDesdeApi, nivelDeApi, ocupacionParaRender, pacientesPorPestana, resumenZona, textoConexion,
} from './modelo-gemelo';

/** El gemelo nunca inventa: cada texto dice lo que el sistema sabe, y lo que no. */
describe('Modelo del gemelo', () => {
  const zona = (extra: Partial<ZonaVista>): ZonaVista => ({ zonaId: 3, personas: 13, aforo: 24, nivel: 'baja', ...extra });
  const nodo = (enLinea: boolean) => ({ codigo: 'N-01', zonaId: 1, enLinea, ultimoLatido: null });

  describe('niveles del backend', () => {
    it('traduce el semáforo', () => expect(['BAJA', 'MEDIA', 'ALTA'].map(nivelDeApi)).toEqual(['baja', 'media', 'alta']));
    it('NINGUNO (sin aforo, RN-13) no es «sin datos»', () => expect(nivelDeApi('NINGUNO')).toBe('ninguno'));
    it('SIN_DATOS o cualquier otro valor es «sin datos»: lo seguro', () => expect([nivelDeApi('SIN_DATOS'), nivelDeApi('X')]).toEqual(['nd', 'nd']));
    it('estados de la atención → pestaña', () =>
      expect(['EN_ESPERA', 'EN_ATENCION', 'ATENDIDO', 'FINALIZADA'].map(estadoDeApi)).toEqual(['esperando', 'en-consulta', 'atendido', 'finalizado']));
  });

  describe('etiqueta y resumen de una zona', () => {
    it('con aforo: «13/24»', () => expect(etiquetaZona(zona({}))).toBe('13/24'));
    it('zona de paso (Tránsito): solo cuántos hay', () => expect(etiquetaZona(zona({ aforo: null, nivel: 'ninguno', personas: 2 }), 'TRANSITO')).toBe('2'));
    it('aforo por definir: «2/—» hasta que el Administrador lo configure', () =>
      expect(etiquetaZona(zona({ aforo: null, nivel: 'ninguno', personas: 2 }), 'ESPERA')).toBe('2/—'));
    it('sin datos: nunca «0», dice «Sin datos»', () => expect(etiquetaZona(zona({ personas: 0, nivel: 'nd' }))).toBe('Sin datos'));
    it('resumen con nivel', () => expect(resumenZona(zona({}))).toBe('13 de 24 pacientes · concentración baja'));
    it('resumen con el aforo por definir', () =>
      expect(resumenZona(zona({ aforo: null, nivel: 'ninguno', personas: 1 }), 'SERVICIO')).toBe('1 paciente · aforo por definir'));
    it('resumen de una zona de paso', () =>
      expect(resumenZona(zona({ aforo: null, nivel: 'ninguno', personas: 1 }), 'TRANSITO')).toBe('1 paciente · zona de paso, sin aforo'));
    it('resumen sin datos', () => expect(resumenZona(zona({ nivel: 'nd' }))).toContain('ningún nodo'));
  });

  describe('qué zonas muestran datos en el 3D', () => {
    const zonas = [zona({ zonaId: 3, aforo: null, nivel: 'ninguno', personas: 5 }), zona({ zonaId: 4, nivel: 'nd', personas: 0 })];
    const conNodoEnSala = [{ codigo: 'N-02', zonaId: 3, enLinea: true, ultimoLatido: null }];
    const r = ocupacionParaRender(zonas, conNodoEnSala, () => 'ESPERA');

    it('la zona con nodo muestra su cifra', () => expect(r[0]).toMatchObject({ zonaId: 3, etiqueta: '5/—' }));
    it('la zona sin nodo muestra solo su nombre, sin color', () =>
      expect(r[1]).toEqual({ zonaId: 4, personas: 0, aforo: null, nivel: 'ninguno', soloNombre: true }));
    it('un nodo caído sigue mostrando «Sin datos»: la zona sí se monitorea', () => {
      const caido = ocupacionParaRender([zona({ zonaId: 3, nivel: 'nd' })], [{ ...conNodoEnSala[0], enLinea: false }], () => 'ESPERA');
      expect(caido[0]).toMatchObject({ nivel: 'nd', etiqueta: 'Sin datos' });
    });
  });

  describe('estado de la conexión', () => {
    it('en vivo con todos los nodos', () => expect(textoConexion('en-vivo', [nodo(true), nodo(true)])).toEqual({ texto: 'En vivo · 2 de 2 nodos', tono: 'ok' }));
    it('conectado pero ningún nodo con señal: no dice «en vivo»', () =>
      expect(textoConexion('en-vivo', [nodo(false), nodo(false)])).toEqual({ texto: 'Conectado · 0 de 2 nodos con señal', tono: 'aviso' }));
    it('reconectando avisa que puede estar desactualizado', () => expect(textoConexion('reconectando', []).texto).toContain('desactualizado'));
    it('en simulación lo dice', () => expect(textoConexion('simulacion', []).tono).toBe('sim'));
  });

  describe('pacientes por pestaña', () => {
    const p = (codigo: string, estado: PacienteVista['estado'], extra: Partial<PacienteVista> = {}): PacienteVista =>
      ({ codigo, estado, zonaId: 3, zonaDesde: 0, esperaMin: 0, ...extra });

    it('esperando: el que más espera, primero', () => {
      const g = pacientesPorPestana([p('A', 'esperando', { esperaMin: 5 }), p('B', 'esperando', { esperaMin: 30 })]);
      expect(g.esperando.map(x => x.codigo)).toEqual(['B', 'A']);
    });
    it('atendidos: los que van a la salida y luego los finalizados, el más reciente primero', () => {
      const g = pacientesPorPestana([p('F1', 'finalizado', { finEn: 100 }), p('S', 'atendido'), p('F2', 'finalizado', { finEn: 200 })]);
      expect(g.atendidos.map(x => x.codigo)).toEqual(['S', 'F2', 'F1']);
    });
    it('detalle: dónde está y desde cuándo, o que no se le detecta', () => {
      const nombre = () => 'Sala de espera', hace = () => 'hace 3 min';
      expect(detallePaciente(p('A', 'esperando', { zonaDesde: 1 }), nombre, hace)).toBe('en Sala de espera · hace 3 min');
      expect(detallePaciente(p('A', 'esperando', { zonaId: null }), nombre, hace)).toBe('Sin ubicación: no se le detecta ahora');
    });
  });

  it('la geometría de la API pasa al render: zonas sin aforo con 0 y la sede', () => {
    const g = geometriaDesdeApi({
      pisoId: 1, nombre: 'Piso 1', nivel: 1, anchoM: 40, fondoM: 20, altoMuroM: 2.7,
      zonas: [{ id: 2, nombre: 'Hall', tipo: 'TRANSITO', x: 0, y: 0, ancho: 5, fondo: 5, aforo: null, umbralMedia: null, umbralAlta: null }],
      nodos: [{ codigo: 'N-01', x: 1, y: 2, zonaId: 2 }],
    }, 'Sede Principal');
    expect(g.zonas[0]).toMatchObject({ id: 2, tipo: 'TRANSITO', aforo: 0 });
    expect(g.nodos).toEqual([{ codigo: 'N-01', x: 1, y: 2, zonaId: 2 }]);
    expect(g.sede).toBe('Sede Principal');
  });
});
