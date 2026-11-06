import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SeleccionTarjetasComponent } from './seleccion-tarjetas.component';

/** El modal de tandas (R4 desinfectar, R5 extraviar). */
describe('SeleccionTarjetasComponent', () => {
  let fixture: ComponentFixture<SeleccionTarjetasComponent>;
  let elegidas: number[] | null;
  const pagina = () => fixture.nativeElement as HTMLElement;
  const casillas = () => [...pagina().querySelectorAll<HTMLInputElement>('.lista input')];
  const botonAccion = () => [...pagina().querySelectorAll('button')].find(b => b.textContent?.includes('Marcar'))!;

  async function crear(marcadasAlInicio: boolean) {
    fixture = TestBed.createComponent(SeleccionTarjetasComponent);
    fixture.componentRef.setInput('titulo', 'Desinfectar tarjetas');
    fixture.componentRef.setInput('texto', 'Marca las que ya limpiaste.');
    fixture.componentRef.setInput('tarjetas', [
      { id: 11, codigo: 'T-11', detalle: 'hace 42 min' }, { id: 3, codigo: 'T-03', detalle: 'hace 31 min' }, { id: 5, codigo: 'T-05', detalle: 'hace 9 min' },
    ]);
    fixture.componentRef.setInput('accion', (n: number) => `Marcar ${n} como desinfectadas`);
    fixture.componentRef.setInput('marcadasAlInicio', marcadasAlInicio);
    elegidas = null;
    fixture.componentInstance.confirmar.subscribe(ids => (elegidas = ids));
    await fixture.whenStable();
  }

  beforeEach(() => TestBed.configureTestingModule({ imports: [SeleccionTarjetasComponent] }));

  it('desinfectar: todas marcadas al abrir, y el botón dice cuántas', async () => {
    await crear(true);

    expect(casillas().every(c => c.checked)).toBe(true);
    expect(botonAccion().textContent?.trim()).toBe('Marcar 3 como desinfectadas');
  });

  it('desmarcar una cambia el conteo y solo envía las marcadas', async () => {
    await crear(true);
    casillas()[1].click();
    await fixture.whenStable();

    expect(botonAccion().textContent?.trim()).toBe('Marcar 2 como desinfectadas');
    botonAccion().click();
    expect(elegidas).toEqual([11, 5]);
  });

  it('extraviar: ninguna marcada al abrir', async () => {
    await crear(false);

    expect(casillas().some(c => c.checked)).toBe(false);
  });

  it('confirmar sin ninguna marcada no envía nada y dice qué falta (el botón nunca se deshabilita)', async () => {
    await crear(false);
    botonAccion().click();
    await fixture.whenStable();

    expect(elegidas).toBeNull();
    expect(pagina().textContent).toContain('Marca al menos una tarjeta.');
  });

  it('«Todas» marca y desmarca la lista completa', async () => {
    await crear(false);
    pagina().querySelector<HTMLInputElement>('.todas input')!.click();
    await fixture.whenStable();

    expect(casillas().every(c => c.checked)).toBe(true);
  });
});
