import { cambios, etiquetaCampo, problemaPeriodo, textoEntidad, textoIntegridad, textoValor } from './modelo-auditoria';

describe('P11 · Auditoría', () => {
  it('los campos se dicen como en la clínica; los desconocidos, legibles', () => {
    expect(etiquetaCampo('aforo')).toBe('Aforo');
    expect(etiquetaCampo('EsperaAltaMin')).toBe('Alerta de espera (min)');   // da igual la mayúscula inicial
    expect(etiquetaCampo('versionFirmware')).toBe('Version firmware');
    expect(textoEntidad('Atencion')).toBe('Atención');
  });

  it('los valores se muestran para la persona', () => {
    expect(textoValor(null)).toBe('—');
    expect(textoValor(true)).toBe('Sí');
    expect(textoValor([1, 2])).toBe('1, 2');
    expect(textoValor([])).toBe('—');
    expect(textoValor('CLAVE', 'motivo')).toBe('Contraseña incorrecta');
  });

  it('antes y después: lo que cambió primero, lo demás como contexto', () => {
    const c = cambios({ nombre: 'Módulo 2', aforo: 2 }, { nombre: 'Módulo 2', aforo: 1 });
    expect(c).toEqual([
      { campo: 'Aforo', antes: '2', despues: '1', cambio: true },
      { campo: 'Nombre', antes: 'Módulo 2', despues: 'Módulo 2', cambio: false },
    ]);
  });

  it('al crear solo hay «después» y al dar de baja solo «antes»', () => {
    expect(cambios(null, { codigo: 'N-09' })).toEqual([{ campo: 'Código', antes: '', despues: 'N-09', cambio: true }]);
    expect(cambios({ codigo: 'N-09' }, null)).toEqual([{ campo: 'Código', antes: 'N-09', despues: '', cambio: true }]);
    expect(cambios(null, null)).toEqual([]);
  });

  it('el período sigue las reglas del backend', () => {
    expect(problemaPeriodo({ desde: '2026-11-10', hasta: '2026-11-01' }, '2026-11-16')).toContain('anterior');
    expect(problemaPeriodo({ desde: '2026-11-10', hasta: '2026-11-17' }, '2026-11-16')).toContain('futura');
    expect(problemaPeriodo({ desde: '2025-01-01', hasta: '2026-11-16' }, '2026-11-16')).toContain('366');
    expect(problemaPeriodo({ desde: '2026-11-01', hasta: '2026-11-16' }, '2026-11-16')).toBeNull();
  });

  it('la verificación dice claramente si la bitácora está íntegra, alterada o sin sello', () => {
    const base = { configurada: true, revisados: 1248, sinSellar: 0, integra: true, primeraAlteracionId: null, motivo: null, ultimoSelloEn: null };
    expect(textoIntegridad(base).tono).toBe('ok');
    expect(textoIntegridad({ ...base, integra: false, primeraAlteracionId: 42, motivo: 'El sello no coincide.' })).toEqual(
      { titulo: 'Se detectó una alteración', texto: 'Registro #42: El sello no coincide.', tono: 'mal' });
    expect(textoIntegridad({ ...base, integra: false, sinSellar: 3, motivo: '3 sin sello' }).tono).toBe('aviso');
    expect(textoIntegridad({ ...base, configurada: false }).titulo).toBe('Sello no configurado');
  });
});
