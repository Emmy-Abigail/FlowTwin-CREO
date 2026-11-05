import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { PisoActualService, SedeApi } from './piso-actual.service';

/** La sede y el piso que se están mirando: el selector del Gemelo y del Historial. */
describe('PisoActualService', () => {
  const piso = (id: number, nivel: number) => ({ id, nombre: nivel < 1 ? 'Sótano' : `Piso ${nivel}`, nivel, anchoM: 40, fondoM: 20, publicado: true });
  const SEDES: SedeApi[] = [
    { id: 1, nombre: 'Sede Principal', direccion: null, pisos: [piso(12, 3), piso(10, 1), piso(9, 0), piso(11, 2)] },
    { id: 2, nombre: 'Sede Norte', direccion: null, pisos: [piso(20, 1)] },
  ];
  let servicio: PisoActualService;
  let http: HttpTestingController;

  async function cargar() {
    const carga = servicio.cargar();
    http.expectOne('/api/v1/sedes').flush(SEDES);
    await carga;
  }

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    servicio = TestBed.inject(PisoActualService);
    http = TestBed.inject(HttpTestingController);
  });

  it('empieza en el Piso 1 de la primera sede, no en el sótano', async () => {
    await cargar();

    expect(servicio.sede()?.nombre).toBe('Sede Principal');
    expect(servicio.piso()?.nombre).toBe('Piso 1');
  });

  it('lista los pisos de arriba hacia abajo, como en el edificio', async () => {
    await cargar();

    expect(servicio.pisosDeSede().map(p => p.nombre)).toEqual(['Piso 3', 'Piso 2', 'Piso 1', 'Sótano']);
  });

  it('recuerda el último piso elegido', async () => {
    localStorage.setItem('flowtwin.piso', JSON.stringify({ sedeId: 1, pisoId: 12 }));

    await cargar();

    expect(servicio.piso()?.nombre).toBe('Piso 3');
  });

  it('si el piso recordado ya no está disponible, elige otro sin fallar', async () => {
    localStorage.setItem('flowtwin.piso', JSON.stringify({ sedeId: 1, pisoId: 999 }));

    await cargar();

    expect(servicio.piso()?.nombre).toBe('Piso 1');
  });

  it('al cambiar de sede va a su piso de entrada y lo guarda', async () => {
    await cargar();

    servicio.seleccionarSede(2);

    expect(servicio.piso()?.id).toBe(20);
    expect(JSON.parse(localStorage.getItem('flowtwin.piso')!)).toEqual({ sedeId: 2, pisoId: 20 });
  });

  it('no acepta un piso de otra sede', async () => {
    await cargar();

    servicio.seleccionarPiso(20);

    expect(servicio.piso()?.id).toBe(10);
  });

  it('si la API falla, lo informa en vez de quedar cargando', async () => {
    const carga = servicio.cargar();
    http.expectOne('/api/v1/sedes').flush(null, { status: 500, statusText: 'Error' });
    await carga;

    expect(servicio.error()).toBe(true);
    expect(servicio.cargando()).toBe(false);
  });
});
