import { TestBed } from '@angular/core/testing';
import { ClinicaStore } from '../../core/datos/clinica.store';
import { Actor } from '../../core/datos/modelos';
import { EscaneoApi } from './presentacion-admision';
import { RecepcionSimulada, esProblema, tarjetaDelQr } from './simulacion-admision';

/** ADR-31: en la Simulación, Admisión funciona igual que con la API, pero sobre las tarjetas simuladas. */
describe('Admisión en la Simulación', () => {
  const operador: Actor = { nombre: 'Luis Rojas', rol: 'Operador' };
  let clinica: ClinicaStore;
  let recepcion: RecepcionSimulada;

  beforeEach(() => {
    clinica = TestBed.inject(ClinicaStore);
    recepcion = new RecepcionSimulada(clinica, () => operador);
  });

  it('reconoce el QR impreso o el código escrito a mano', () => {
    expect(tarjetaDelQr('CREO-T05')).toBe('T-05');
    expect(tarjetaDelQr('T-05')).toBe('T-05');
    expect(tarjetaDelQr('t5')).toBe('T-05');
    expect(tarjetaDelQr('hola')).toBeNull();
  });

  it('escanear una tarjeta disponible la entrega y el paciente aparece en la clínica simulada', () => {
    expect(recepcion.vistaPrevia('T-05').operacion).toBe('ENTREGA');

    const r = recepcion.escanear('T-05') as EscaneoApi;

    expect(r.operacion).toBe('ENTREGA');
    expect(r.deshacerHasta).not.toBeNull();
    expect(clinica.tarjeta('T-05')!.estado).toBe('En uso');
    expect(clinica.activas().map(a => a.tarjetaId)).toContain('T-05');
    expect(recepcion.resumen().ultimasOperaciones[0]).toMatchObject({ tipo: 'ENTREGADA', tarjeta: 'T-05' });
  });

  it('al volver a escanearla se recibe y queda por desinfectar; antes de desinfectarla no se puede entregar', () => {
    recepcion.escanear('T-05');
    expect(recepcion.vistaPrevia('T-05').operacion).toBe('DEVOLUCION');

    const r = recepcion.escanear('T-05') as EscaneoApi;

    expect(r.operacion).toBe('DEVOLUCION');
    expect(r.atencion!.finEn).not.toBeNull();
    expect(recepcion.vistaPrevia('T-05').codigo).toBe('RN03_POR_DESINFECTAR');
    expect(recepcion.resumen().porDesinfectar.map(p => p.codigo)).toContain('T-05');

    recepcion.desinfectar([5]);
    expect(recepcion.vistaPrevia('T-05').operacion).toBe('ENTREGA');
  });

  it('deshacer una entrega deja la tarjeta disponible y sin paciente', () => {
    const r = recepcion.escanear('T-06') as EscaneoApi;

    const t = recepcion.deshacer(r.atencion!.id);

    expect(esProblema(t)).toBe(false);
    expect(clinica.tarjeta('T-06')!.estado).toBe('Disponible');
    expect(clinica.activas().map(a => a.tarjetaId)).not.toContain('T-06');
  });

  it('una tarjeta que no existe se avisa como no registrada y no cambia nada', () => {
    const antes = clinica.atenciones().length;

    expect(recepcion.vistaPrevia('T-999').codigo).toBe('TARJETA_NO_REGISTRADA');
    expect(esProblema(recepcion.escanear('T-999'))).toBe(true);
    expect(clinica.atenciones().length).toBe(antes);
  });

  it('la tarjeta que quedó sin devolver ayer aparece para revisar, y si vuelve, pasa a desinfección', () => {
    expect(recepcion.resumen().sinDevolver.map(p => p.codigo)).toContain('T-14');

    const r = recepcion.escanear('CREO-T14') as EscaneoApi;

    expect(r.operacion).toBe('RECUPERADA');
    expect(clinica.tarjeta('T-14')!.estado).toBe('Por desinfectar');
  });
});
