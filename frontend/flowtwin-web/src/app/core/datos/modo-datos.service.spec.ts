import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthService } from '../auth/auth.service';
import { Rol } from './modelos';
import { ModoDatosService } from './modo-datos.service';
import { SimuladorService } from './simulador.service';

/** ADR-22: la simulación es temporal, solo si el servidor la permite y solo para Administrador y Gerencia. */
describe('ModoDatosService', () => {
  const rol = signal<Rol | null>('Gerencia');
  const simulador = { iniciar: vi.fn(), detener: vi.fn() };
  let modo: ModoDatosService;
  let http: HttpTestingController;

  async function cargarCon(respuesta: { simulacionDisponible: boolean } | 'error') {
    const carga = modo.cargar();
    const peticion = http.expectOne('/api/v1/configuracion');
    if (respuesta === 'error') peticion.flush(null, { status: 500, statusText: 'Error' });
    else peticion.flush(respuesta);
    await carga;
  }

  beforeEach(() => {
    rol.set('Gerencia');
    simulador.iniciar.mockClear();
    simulador.detener.mockClear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(), provideHttpClientTesting(),
        { provide: AuthService, useValue: { rol } },
        { provide: SimuladorService, useValue: simulador },
      ],
    });
    modo = TestBed.inject(ModoDatosService);
    http = TestBed.inject(HttpTestingController);
  });

  it('por defecto muestra datos reales', () => {
    expect(modo.simulacion()).toBe(false);
  });

  it('Gerencia puede simular si el servidor lo permite', async () => {
    await cargarCon({ simulacionDisponible: true });

    expect(modo.puedeSimular()).toBe(true);
  });

  it('en la clínica (servidor sin simulación) nadie puede simular', async () => {
    await cargarCon({ simulacionDisponible: false });

    expect(modo.puedeSimular()).toBe(false);
  });

  it('el Operador nunca simula: su pantalla es operativa', async () => {
    rol.set('Operador');
    await cargarCon({ simulacionDisponible: true });

    modo.activar();

    expect(modo.puedeSimular()).toBe(false);
    expect(modo.simulacion()).toBe(false);
    expect(simulador.iniciar).not.toHaveBeenCalled();
  });

  it('si no se puede saber la configuración, la simulación no existe', async () => {
    await cargarCon('error');

    expect(modo.puedeSimular()).toBe(false);
  });

  it('activar enciende el simulador y salir lo apaga', async () => {
    await cargarCon({ simulacionDisponible: true });

    modo.activar();
    expect(modo.simulacion()).toBe(true);
    expect(simulador.iniciar).toHaveBeenCalled();

    modo.salir();
    expect(modo.simulacion()).toBe(false);
    expect(simulador.detener).toHaveBeenCalled();
  });
});
