import { TestBed } from '@angular/core/testing';
import { ClinicaStore } from '../../core/datos/clinica.store';
import { DIA, isoFecha } from '../../core/utils/tiempo';
import { cambios } from './modelo-auditoria';
import { BitacoraSimulada, FiltrosAuditoria } from './simulacion-auditoria';

/** ADR-31: la Auditoría en la Simulación muestra la bitácora de la clínica simulada con el formato de la API. */
describe('Auditoría en la Simulación', () => {
  let clinica: ClinicaStore;
  let bitacora: BitacoraSimulada;
  let semana: FiltrosAuditoria;

  beforeEach(() => {
    clinica = TestBed.inject(ClinicaStore);
    bitacora = new BitacoraSimulada(clinica);
    const hoy = isoFecha(clinica.ahora());
    semana = { rango: { desde: isoFecha(clinica.ahora() - 6 * DIA), hasta: hoy }, usuarioId: null, categoria: null, buscar: '' };
  });

  it('trae la bitácora de la semana, de la más reciente a la más antigua, con su categoría', () => {
    const p = bitacora.pagina(semana, 1);

    expect(p.total).toBeGreaterThan(0);
    expect(p.filas[0].cuando >= p.filas.at(-1)!.cuando).toBe(true);
    expect(Object.keys(p.conteos)).toEqual(expect.arrayContaining(['USUARIOS', 'OPERACION']));
  });

  it('lo que hace la recepción simulada aparece en Operación, y se filtra por categoría y por texto', () => {
    clinica.entregarTarjeta('T-05', { nombre: 'Luis Rojas', rol: 'Operador' });

    const operacion = bitacora.pagina({ ...semana, categoria: 'OPERACION' }, 1);
    const buscada = bitacora.pagina({ ...semana, buscar: 't-05' }, 1);

    expect(operacion.filas[0]).toMatchObject({ usuario: 'Luis Rojas', accionTexto: 'Entregó tarjeta', objeto: 'T-05', entidad: 'Tarjeta' });
    expect(buscada.filas.every(f => f.objeto === 'T-05' || f.accionTexto.includes('T-05'))).toBe(true);
  });

  it('un cambio hecho en la Simulación muestra su antes y después en el detalle', () => {
    clinica.auditar('Gemelo', 'Editó', 'Sala de espera', 'Aforo 24 → 20', { nombre: 'Ana Torres', rol: 'Administrador' },
      { antes: { aforo: 24 }, despues: { aforo: 20 } });
    const fila = bitacora.pagina({ ...semana, categoria: 'CONFIGURACION' }, 1).filas[0];

    const d = bitacora.detalle(fila.id)!;

    expect(cambios(d.antes, d.despues)[0]).toMatchObject({ campo: 'Aforo', antes: '24', despues: '20', cambio: true });
  });

  it('el filtro de usuario usa la lista de quienes aparecen en la bitácora', () => {
    const ana = bitacora.usuarios().find(u => u.nombre === 'Ana Torres')!;

    const p = bitacora.pagina({ ...semana, usuarioId: ana.id }, 1);

    expect(p.filas.every(f => f.usuario === 'Ana Torres')).toBe(true);
    expect(bitacora.usuarios().map(u => u.nombre)).not.toContain('Sistema');   // ya tiene su opción fija (id 1)
    expect(bitacora.pagina({ ...semana, usuarioId: 1 }, 1).filas.every(f => f.usuario === 'Sistema')).toBe(true);
  });

  it('exporta lo filtrado en CSV', () => {
    const texto = bitacora.textoCsv(semana);

    expect(texto.split('\r\n')[0]).toContain('Fecha y hora');
    expect(texto.split('\r\n').length).toBe(bitacora.pagina(semana, 1).total + 1);
  });
});
