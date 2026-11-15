import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ConexionGemelo, ManejadoresGemelo, vence } from './conexion-gemelo';
import { GemeloEnVivoStore } from './gemelo-en-vivo.store';

/** La fuente real: la foto por HTTP y, encima, lo que manda el hub (aquí, un hub falso). */
describe('GemeloEnVivoStore', () => {
  let store: GemeloEnVivoStore;
  let http: HttpTestingController;
  let hub: ManejadoresGemelo;
  const conexion = { estado: signal('en-vivo'), iniciar: vi.fn(), cambiarPiso: vi.fn(), detener: vi.fn() };
  const ahora = Date.now();
  const iso = (minAtras: number) => new Date(ahora - minAtras * 60_000).toISOString();
  const ESTADO = {
    zonas: [{ zonaId: 3, ocupacion: 2, aforo: 24, nivel: 'BAJA', pausada: false }, { zonaId: 2, ocupacion: 0, aforo: null, nivel: 'NINGUNO', pausada: false }],
    atenciones: [{ codigo: 'A-0137', zonaActual: 3, estado: 'EN_ESPERA', inicioEn: iso(34), esperaMin: 34, zonaDesdeEn: iso(21) }],
    kpis: { enClinica: 1, enEspera: 1, esperaPromMin: 34, consultaPromMin: null },
    nodos: [{ nodoId: 1, codigo: 'N-02', zonaId: 3, estado: 'EN_LINEA', ultimoLatidoEn: iso(0) }],
  };

  async function activar() {
    const carga = store.activar(1, 'Sede Principal');
    http.expectOne('/api/v1/pisos/1/geometria').flush({ pisoId: 1, nombre: 'Piso 1', nivel: 1, anchoM: 40, fondoM: 20, altoMuroM: 2.7, planoUrl: '/planos/piso1.jpg', zonas: [], elementos: [], nodos: [] });
    http.expectOne('/api/v1/pisos/1/estado').flush(ESTADO);
    http.expectOne('/api/v1/pisos/1/atendidas-hoy').flush([{ codigo: 'A-0130', inicioEn: iso(90), finEn: iso(10), motivo: 'DEVUELTA', esperaMin: 20, totalMin: 80 }]);
    await carga;
  }

  beforeEach(() => {
    vi.clearAllMocks();
    conexion.iniciar.mockImplementation((_p: number, m: ManejadoresGemelo) => { hub = m; return Promise.resolve(); });
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), GemeloEnVivoStore, { provide: ConexionGemelo, useValue: conexion }],
    });
    store = TestBed.inject(GemeloEnVivoStore);
    http = TestBed.inject(HttpTestingController);
  });

  it('carga la foto del piso: zonas, KPI, pacientes (abiertos y atendidos hoy) y nodos', async () => {
    await activar();

    expect(store.zonas().find(z => z.zonaId === 2)).toEqual({ zonaId: 2, personas: 0, aforo: null, nivel: 'ninguno' });
    expect(store.kpis()?.consultaPromMin).toBeNull();
    expect(store.pacientes().map(p => [p.codigo, p.estado])).toEqual([['A-0137', 'esperando'], ['A-0130', 'finalizado']]);
    expect(store.nodos()[0].enLinea).toBe(true);
    expect(store.geometria()?.anchoM).toBe(40);
  });

  it('un cambio de ocupación por el hub actualiza solo esa zona', async () => {
    await activar();

    hub.ocupacionZona({ zonaId: 3, ocupacion: 20, aforo: 24, nivel: 'ALTA', pausada: false });

    expect(store.zonas().find(z => z.zonaId === 3)).toMatchObject({ personas: 20, nivel: 'alta' });
  });

  it('un paciente que entra a consulta cambia de pestaña', async () => {
    await activar();

    hub.atencion({ codigo: 'A-0137', zonaActual: 7, estado: 'EN_ATENCION', inicioEn: iso(34), esperaMin: 34, zonaDesdeEn: iso(0) });

    expect(store.pacientes().find(p => p.codigo === 'A-0137')).toMatchObject({ estado: 'en-consulta', zonaId: 7 });
  });

  it('al devolver la tarjeta sale de los abiertos y se vuelven a pedir los atendidos', async () => {
    await activar();

    hub.atencion({ codigo: 'A-0137', zonaActual: null, estado: 'FINALIZADA', inicioEn: iso(34), esperaMin: 34 });

    expect(store.pacientes().some(p => p.codigo === 'A-0137' && p.estado !== 'finalizado')).toBe(false);
    http.expectOne('/api/v1/pisos/1/atendidas-hoy');
  });

  it('al reconectarse pide la foto completa otra vez (pudieron perderse mensajes)', async () => {
    await activar();

    hub.reconectado();

    http.expectOne('/api/v1/pisos/1/estado');
    http.expectOne('/api/v1/pisos/1/atendidas-hoy');
  });

  it('un piso sin permiso muestra el mensaje del servidor', async () => {
    const carga = store.activar(9, 'Sede');
    http.expectOne('/api/v1/pisos/9/geometria').flush({ codigo: 'SIN_PERMISO', title: 'No tienes asignado este piso.' }, { status: 403, statusText: 'Forbidden' });
    http.expectOne('/api/v1/pisos/9/estado').flush({ codigo: 'SIN_PERMISO', title: 'No tienes asignado este piso.' }, { status: 403, statusText: 'Forbidden' });
    http.expectOne('/api/v1/pisos/9/atendidas-hoy').flush(null, { status: 403, statusText: 'Forbidden' });
    await carga;

    expect(store.error()).toBe('No tienes asignado este piso.');
  });
});

describe('vence (token)', () => {
  const token = (exp: number) => `x.${btoa(JSON.stringify({ exp }))}.y`;

  it('lee el vencimiento del token', () => expect(vence(token(2_000_000_000))).toBe(2_000_000_000_000));
  it('si no se puede leer devuelve 0: así se renueva', () => expect([vence(null), vence('basura')]).toEqual([0, 0]));
});
