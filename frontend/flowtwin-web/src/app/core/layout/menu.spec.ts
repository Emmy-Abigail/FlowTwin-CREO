import { menuDe } from './menu';

describe('Menú por rol', () => {
  it('solo muestra las pantallas construidas: nada de callejones sin salida', () => {
    const rutas = menuDe('Administrador').map(i => i.ruta);

    expect(rutas).toEqual(['/gemelo', '/historial']);
  });

  it('el Operador empieza por Admisión, su pantalla de trabajo', () => {
    expect(menuDe('Operador')[0].ruta).toBe('/admision');
  });

  it('Gerencia no ve Admisión: no entrega tarjetas', () => {
    expect(menuDe('Gerencia').map(i => i.ruta)).not.toContain('/admision');
  });
});
