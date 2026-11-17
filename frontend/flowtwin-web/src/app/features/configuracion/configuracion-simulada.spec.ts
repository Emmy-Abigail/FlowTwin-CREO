import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthService } from '../../core/auth/auth.service';
import { ClinicaStore } from '../../core/datos/clinica.store';
import { GeometriaService } from '../../core/datos/geometria.service';
import { Actor } from '../../core/datos/modelos';
import { ConfiguracionSimulada } from './configuracion-simulada';

/** ADR-31: Configuración en la Simulación: cambios en memoria que se ven en el gemelo simulado. */
describe('Configuración en la Simulación', () => {
  let sim: ConfiguracionSimulada;
  let geometria: GeometriaService;
  let clinica: ClinicaStore;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(), provideHttpClientTesting(),
        { provide: AuthService, useValue: { actor: signal<Actor>({ nombre: 'Ana Torres', rol: 'Administrador' }) } },
      ],
    });
    geometria = TestBed.inject(GeometriaService);
    TestBed.inject(HttpTestingController).expectOne('pisos/geometria-piso1.json').flush({
      pisoId: 1, nombre: 'Piso 1', nivel: 1, anchoM: 40, fondoM: 20, sede: 'Sede Principal', nodos: [],
      zonas: [
        { id: 2, nombre: 'Hall principal', tipo: 'TRANSITO', x: 0, y: 0, ancho: 1, fondo: 1, aforo: 30, umbralMedia: 80, umbralAlta: 100 },
        { id: 3, nombre: 'Sala de espera', tipo: 'ESPERA', x: 0, y: 0, ancho: 1, fondo: 1, aforo: 24, umbralMedia: 80, umbralAlta: 100 },
      ],
    });
    sim = TestBed.inject(ConfiguracionSimulada);
    clinica = TestBed.inject(ClinicaStore);
  });

  it('el Piso 1 trae las zonas de la clínica simulada; las de paso no llevan aforo', () => {
    expect(sim.zonas(1)).toEqual([
      { id: 2, nombre: 'Hall principal', tipo: 'TRANSITO', aforo: null, tieneAforo: false },
      { id: 3, nombre: 'Sala de espera', tipo: 'ESPERA', aforo: 24, tieneAforo: true },
    ]);
  });

  it('cambiar el aforo de una zona se ve en el gemelo simulado y queda en la bitácora con su antes y después', () => {
    sim.guardarZona(1, 3, { nombre: 'Sala de espera', tipo: 'ESPERA', aforo: 20 });

    expect(geometria.zona(3)!.aforo).toBe(20);
    expect(clinica.auditoria()[0]).toMatchObject({ tipo: 'Gemelo', accion: 'Editó', elemento: 'Sala de espera', antes: { aforo: 24 }, despues: { aforo: 20 } });
  });

  it('el ámbar de los parámetros se aplica a todas las zonas, con las mismas validaciones que el servidor', () => {
    expect(() => sim.guardarParametros({ ...sim.parametros(), esperaCriticaMin: 30 })).toThrow('mayor que la alerta de espera');

    sim.guardarParametros({ ...sim.parametros(), semaforoAmbarPct: 70 });

    expect(geometria.zonas().every(z => z.umbralMedia === 70)).toBe(true);
  });

  it('un piso nuevo nace en borrador y no se publica sin zonas', () => {
    const id = sim.guardarPiso(1, null, { nombre: 'Piso 2', nivel: 2 });

    expect(sim.arbol()[0].pisos.find(p => p.id === id)).toMatchObject({ nombre: 'Piso 2', publicado: false });
    expect(() => sim.publicar(id)).toThrow('sin zonas');
    sim.guardarZona(id, null, { nombre: 'Sala 2', tipo: 'ESPERA', aforo: 10 });
    sim.publicar(id);
    expect(sim.pisosPublicados().map(p => p.nombre)).toEqual(['Piso 1', 'Piso 2']);
  });

  it('«Reiniciar simulación» deshace los cambios de configuración', () => {
    sim.guardarZona(1, 3, { nombre: 'Sala grande', tipo: 'ESPERA', aforo: 40 });

    clinica.reiniciar();

    expect(geometria.zona(3)).toMatchObject({ nombre: 'Sala de espera', aforo: 24 });
  });
});
