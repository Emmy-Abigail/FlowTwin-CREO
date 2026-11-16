import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { PisoActualService } from '../../core/datos/piso-actual.service';
import { SelectorPisoComponent } from './selector-piso.component';

describe('SelectorPisoComponent', () => {
  it('muestra el piso elegido aunque no sea el primero de la lista (antes mostraba siempre el primero)', async () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const pisos = TestBed.inject(PisoActualService);
    const piso = (id: number, nivel: number) => ({ id, nombre: `Piso ${nivel}`, nivel, anchoM: null, fondoM: null, publicado: true });
    pisos.sedes.set([
      { id: 1, nombre: 'Sede Principal', direccion: null, pisos: [piso(1, 1), piso(3, 3)] },
      { id: 2, nombre: 'Sede Norte', direccion: null, pisos: [piso(5, 1)] },
    ]);
    pisos.seleccionarSede(1);
    pisos.seleccionarPiso(1);   // el Piso 1 queda último: la lista va de arriba hacia abajo

    const fixture = TestBed.createComponent(SelectorPisoComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    const el: HTMLElement = fixture.nativeElement;

    expect(el.querySelector<HTMLSelectElement>('#sel-piso')!.selectedOptions[0].text).toBe('Piso 1');
    expect(el.querySelector<HTMLSelectElement>('#sel-sede')?.selectedOptions[0].text ?? 'Sede Principal').toBe('Sede Principal');
  });
});
