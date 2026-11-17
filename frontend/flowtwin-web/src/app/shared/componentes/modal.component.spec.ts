import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ModalComponent } from './modal.component';

@Component({
  imports: [ModalComponent],
  template: `<app-modal titulo="Prueba" (cerrar)="cerrado.set(true)">
    <label><input type="checkbox" id="casilla"> Opción</label>
    <div pie><button type="button">Cancelar</button><button type="button" id="principal">Aceptar</button></div>
  </app-modal>`,
})
class Anfitrion { cerrado = signal(false); }

/** El modal compartido: foco, cierre y que su contenido funcione. */
describe('ModalComponent', () => {
  async function crear() {
    const f = TestBed.createComponent(Anfitrion);
    await f.whenStable();
    await new Promise(r => setTimeout(r));
    return f;
  }

  it('al abrirse, el foco va al botón principal (el último del pie)', async () => {
    const f = await crear();

    expect(document.activeElement?.id).toBe('principal');
    f.destroy();
  });

  it('una casilla dentro del modal se puede marcar (el clic no se cancela)', async () => {
    const f = await crear();
    const casilla = (f.nativeElement as HTMLElement).querySelector<HTMLInputElement>('#casilla')!;

    casilla.click();

    expect(casilla.checked).toBe(true);
    expect(f.componentInstance.cerrado()).toBe(false);
  });

  it('un clic en el fondo oscuro lo cierra', async () => {
    const f = await crear();

    (f.nativeElement as HTMLElement).querySelector<HTMLElement>('.scrim')!.click();

    expect(f.componentInstance.cerrado()).toBe(true);
  });
});
