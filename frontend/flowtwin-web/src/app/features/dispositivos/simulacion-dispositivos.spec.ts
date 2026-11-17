import { TestBed } from '@angular/core/testing';
import { ClinicaStore } from '../../core/datos/clinica.store';
import { Actor } from '../../core/datos/modelos';
import { InventarioSimulado, TAMANO_PAGINA } from './simulacion-dispositivos';

/** ADR-31: Dispositivos en la Simulación, con el mismo formato que la API y sin tocar la base. */
describe('Dispositivos en la Simulación', () => {
  const admin: Actor = { nombre: 'Ana Torres', rol: 'Administrador' };
  let clinica: ClinicaStore;
  let inv: InventarioSimulado;

  beforeEach(() => {
    clinica = TestBed.inject(ClinicaStore);
    inv = new InventarioSimulado(clinica, id => `Zona ${id}`, () => admin);
  });

  it('muestra los nodos de la clínica simulada en línea, con su zona', () => {
    const p = inv.nodos('TODAS', '', 1);

    expect(p.filas.map(n => n.codigo)).toEqual(['N-01', 'N-02', 'N-03']);
    expect(p.filas.every(n => n.estado === 'EN_LINEA')).toBe(true);
    expect(p.conteos).toMatchObject({ ATENCION: 0, EN_LINEA: 3, TODAS: 3 });
  });

  it('registrar un nodo lo agrega «sin señal» y muestra una clave ficticia; un código repetido se rechaza', () => {
    const r = inv.registrarNodo('n-04', 'A4:CF:12:9B:3E:04', 4);

    expect(r.clave).toContain('SIMULACION');
    expect(r.mqtt.usuario).toBe('flowtwin-a4cf129b3e04');
    expect(inv.nodos('ATENCION', '', 1).filas.map(n => n.codigo)).toEqual(['N-04']);
    expect(() => inv.registrarNodo('N-04', 'A4:CF:12:9B:3E:05', 4)).toThrow('Ya existe el nodo N-04.');
    expect(clinica.auditoria()[0]).toMatchObject({ tipo: 'Dispositivos', accion: 'Registró nodo', elemento: 'N-04' });
  });

  it('dar de baja un nodo lo saca del inventario', () => {
    inv.darDeBajaNodo('N-02');

    expect(inv.nodos('TODAS', '', 1).filas.map(n => n.codigo)).toEqual(['N-01', 'N-03']);
  });

  it('las tarjetas se paginan como en la API y «Requieren atención» trae la que quedó sin devolver', () => {
    const todas = inv.tarjetas('TODAS', '', 1);
    const atencion = inv.tarjetas('ATENCION', '', 1);

    expect(todas.total).toBe(50);
    expect(todas.filas.length).toBe(TAMANO_PAGINA);
    expect(todas.conteos['SIN_DEVOLVER']).toBe(1);
    expect(atencion.filas.map(t => t.codigo)).toContain('T-14');
    expect(inv.tarjetas('TODAS', 't-07', 1).filas.map(t => t.codigo)).toEqual(['T-07']);
  });

  it('registrar tarjetas por rango crea todas o ninguna', () => {
    expect(inv.registrarTarjetas(51, 53).map(t => t.codigoQr)).toEqual(['CREO-T51', 'CREO-T52', 'CREO-T53']);
    expect(() => inv.registrarTarjetas(50, 54)).toThrow(/Ya existen: T-50, T-51/);
    expect(inv.tarjetas('TODAS', '', 1).total).toBe(53);
  });
});
