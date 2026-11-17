import { TestBed } from '@angular/core/testing';
import { Aleatorio } from '../utils/aleatorio';
import { DIA, inicioDelDia } from '../utils/tiempo';
import { ClinicaStore } from './clinica.store';
import { HORA_APERTURA, ZONAS } from './clinica.config';
import { crearDatosSemilla } from './datos-semilla';
import { GeometriaService } from './geometria.service';
import { crearRuta } from './recorrido';
import { SimuladorService } from './simulador.service';

/** La Simulación debe parecerse a un día real: la clínica abre vacía y se va llenando de a poco. */
describe('Simulación de la clínica', () => {
  const hoy0 = inicioDelDia(Date.now());

  it('arranca a la hora de apertura, sin pacientes ni operaciones del día', () => {
    const s = crearDatosSemilla(hoy0);

    expect(new Date(s.ahora).getHours()).toBe(HORA_APERTURA);
    expect(new Date(s.ahora).getMinutes()).toBe(0);
    expect(s.atenciones).toEqual([]);
    expect(s.operaciones).toEqual([]);
    expect(s.tarjetas.filter(t => t.estado === 'En uso')).toEqual([]);
    expect(s.auditoria.every(e => e.t <= s.ahora)).toBe(true);   // nada «en el futuro»
  });

  it('trae los 6 días anteriores terminados, para que el Historial tenga datos desde el inicio', () => {
    const s = crearDatosSemilla(hoy0);
    const dias = new Set(s.historial.map(a => Math.floor((inicioDelDia(a.inicio) - hoy0) / DIA)));

    expect([...dias].sort((a, b) => a - b)).toEqual([-6, -5, -4, -3, -2, -1]);
    expect(s.historial.every(a => a.estado === 'cerrada' && a.fin !== null)).toBe(true);
  });

  it('al abrir, recepción encuentra la tarjeta que quedó sin devolver ayer', () => {
    const s = crearDatosSemilla(hoy0);
    const t14 = s.tarjetas.find(t => t.id === 'T-14')!;

    expect(t14.estado).toBe('Sin devolver');
    expect(t14.desde!).toBeLessThan(hoy0);
  });

  it('1 de cada 10 pacientes, aproximadamente, tiene una espera larga en la sala', () => {
    const rnd = new Aleatorio(7);
    const rutas = Array.from({ length: 2000 }, () => crearRuta(rnd));
    const largas = rutas.filter(r => r.at(4)!.zonaId === ZONAS.sala && r.at(4)!.minutos > 14).length / rutas.length;

    expect(largas).toBeGreaterThan(0.07);
    expect(largas).toBeLessThan(0.13);
  });

  it('los pacientes llegan de a poco: en la primera media hora, unos pocos; no todos juntos', () => {
    TestBed.configureTestingModule({ providers: [{ provide: GeometriaService, useValue: { zona: () => undefined } }] });
    const store = TestBed.inject(ClinicaStore);
    const simulador = TestBed.inject(SimuladorService);

    for (let i = 0; i < 30; i++) simulador.tick();
    const enMediaHora = store.atenciones().length;
    for (let i = 0; i < 150; i++) simulador.tick();   // hasta las 10:00, en pleno pico

    expect(enMediaHora).toBeGreaterThan(0);
    expect(enMediaHora).toBeLessThan(15);
    expect(store.atenciones().length).toBeGreaterThan(enMediaHora * 3);
  });

  it('«Reiniciar simulación» vuelve a la clínica recién abierta aunque ya hubiera pacientes', () => {
    TestBed.configureTestingModule({ providers: [{ provide: GeometriaService, useValue: { zona: () => undefined } }] });
    const store = TestBed.inject(ClinicaStore);
    const simulador = TestBed.inject(SimuladorService);
    const apertura = store.ahora();
    for (let i = 0; i < 120; i++) simulador.tick();
    expect(store.atenciones().length).toBeGreaterThan(0);

    simulador.reiniciar();

    expect(store.ahora()).toBe(apertura);
    expect(store.atenciones()).toEqual([]);
    expect(store.operaciones()).toEqual([]);
    expect(store.historial().length).toBeGreaterThan(0);
  });
});
