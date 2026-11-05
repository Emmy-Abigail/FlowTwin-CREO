import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AuthService, ResultadoCambioClave } from '../../core/auth/auth.service';
import { FormularioContrasenaComponent } from './formulario-contrasena.component';

/** Cuándo aparece cada mensaje del formulario y qué hace con la respuesta del servidor. */
describe('FormularioContrasenaComponent', () => {
  let fixture: ComponentFixture<FormularioContrasenaComponent>;
  let cambiarClave: ReturnType<typeof vi.fn>;
  const pagina = () => fixture.nativeElement as HTMLElement;
  const campo = (id: string) => pagina().querySelector<HTMLInputElement>(`#cc-${id}`)!;
  const error = (id: string) => pagina().querySelector(`#cc-${id}-error`)?.textContent?.trim() ?? null;

  async function escribir(id: string, valor: string) {
    campo(id).value = valor;
    campo(id).dispatchEvent(new Event('input'));
    await fixture.whenStable();
  }
  async function salir(id: string) {
    campo(id).dispatchEvent(new Event('blur'));
    await fixture.whenStable();
  }
  async function enviar() {
    pagina().querySelector<HTMLButtonElement>('button[type=submit]')!.click();
    await fixture.whenStable();
  }
  async function llenarBien() {
    await escribir('actual', 'Vieja#2025');
    await escribir('nueva', 'Riñón#Azul26');
    await escribir('confirmacion', 'Riñón#Azul26');
  }

  beforeEach(async () => {
    cambiarClave = vi.fn<(a: string, n: string, c: string) => Promise<ResultadoCambioClave>>();
    await TestBed.configureTestingModule({
      imports: [FormularioContrasenaComponent],
      providers: [{
        provide: AuthService,
        useValue: { cambiarClave, usuario: signal({ nombre: 'Ana Torres', correo: 'ana.torres@creo.pe' }) },
      }],
    }).compileComponents();
    fixture = TestBed.createComponent(FormularioContrasenaComponent);
    await fixture.whenStable();
  });

  it('no muestra errores mientras se escribe por primera vez, pero sí marca los requisitos cumplidos', async () => {
    await escribir('nueva', 'abc');

    expect(error('nueva')).toBeNull();
    expect(pagina().querySelectorAll('.requisitos li.ok').length).toBe(1);
  });

  it('al salir de la nueva contraseña dice qué le falta, debajo del campo', async () => {
    await escribir('nueva', 'clinica1');
    await salir('nueva');

    expect(error('nueva')).toBe('Le falta: una letra mayúscula, un símbolo (# $ % & * ! ?).');
  });

  it('pasar con Tab por un campo vacío no es un error', async () => {
    await salir('actual');

    expect(error('actual')).toBeNull();
  });

  it('al enviar vacío marca los tres campos y no llama al servidor', async () => {
    await enviar();

    expect(error('actual')).toBe('Escribe tu contraseña actual.');
    expect(error('nueva')).toBe('Escribe la nueva contraseña.');
    expect(error('confirmacion')).toBe('Repite la nueva contraseña.');
    expect(cambiarClave).not.toHaveBeenCalled();
  });

  it('mientras se corrige un campo, su error se oculta', async () => {
    await enviar();
    await escribir('confirmacion', 'R');

    expect(error('confirmacion')).toBeNull();
    expect(error('actual')).toBe('Escribe tu contraseña actual.');
  });

  it('si el servidor dice que la actual es incorrecta, el error va debajo de ese campo', async () => {
    cambiarClave.mockResolvedValue({ ok: false, error: null, campos: { actual: 'La contraseña actual no es correcta.' } });
    await llenarBien();
    await enviar();

    expect(error('actual')).toBe('La contraseña actual no es correcta.');
    expect(error('nueva')).toBeNull();
  });

  it('si todo está bien, guarda, avisa y limpia el formulario', async () => {
    cambiarClave.mockResolvedValue({ ok: true });
    const guardada = vi.fn();
    fixture.componentInstance.guardada.subscribe(guardada);
    await llenarBien();
    await enviar();

    expect(cambiarClave).toHaveBeenCalledWith('Vieja#2025', 'Riñón#Azul26', 'Riñón#Azul26');
    expect(guardada).toHaveBeenCalled();
    expect(campo('nueva').value).toBe('');
  });
});
