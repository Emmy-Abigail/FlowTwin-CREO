import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { AuthService, ResultadoLogin } from '../../core/auth/auth.service';
import { LoginPage } from './login.page';

/** La pantalla de login: cuándo aparece cada mensaje y qué hace con la respuesta del servidor. */
describe('LoginPage', () => {
  let fixture: ComponentFixture<LoginPage>;
  let iniciarSesion: ReturnType<typeof vi.fn>;
  const pagina = () => fixture.nativeElement as HTMLElement;
  const correo = () => pagina().querySelector<HTMLInputElement>('#lg-mail')!;
  const clave = () => pagina().querySelector<HTMLInputElement>('#lg-pass')!;
  const texto = () => pagina().textContent ?? '';

  async function escribir(input: HTMLInputElement, valor: string) {
    input.value = valor;
    input.dispatchEvent(new Event('input'));
    await fixture.whenStable();
  }
  async function salir(input: HTMLInputElement) {
    input.dispatchEvent(new Event('blur'));
    await fixture.whenStable();
  }
  async function enviar() {
    pagina().querySelector<HTMLButtonElement>('button[type=submit]')!.click();
    await fixture.whenStable();
  }

  beforeEach(async () => {
    localStorage.clear();
    iniciarSesion = vi.fn<(c: string, k: string) => Promise<ResultadoLogin>>();
    await TestBed.configureTestingModule({
      imports: [LoginPage],
      providers: [provideRouter([]), { provide: AuthService, useValue: { iniciarSesion } }],
    }).compileComponents();
    fixture = TestBed.createComponent(LoginPage);
    await fixture.whenStable();
  });

  it('no muestra errores al abrir ni mientras se escribe por primera vez', async () => {
    await escribir(correo(), 'ana');

    expect(texto()).not.toContain('Escribe un correo');
  });

  it('al salir del correo mal escrito, el error aparece debajo del campo', async () => {
    await escribir(correo(), 'ana.torres');
    await salir(correo());

    expect(pagina().querySelector('#lg-mail-error')?.textContent).toContain('Escribe un correo con la forma nombre@creo.pe');
    expect(correo().getAttribute('aria-invalid')).toBe('true');
  });

  it('el botón nunca se deshabilita: al enviar vacío dice qué falta y no llama al servidor', async () => {
    expect(pagina().querySelector<HTMLButtonElement>('button[type=submit]')!.disabled).toBe(false);

    await enviar();

    expect(texto()).toContain('Escribe tu correo institucional.');
    expect(texto()).toContain('Escribe tu contraseña.');
    expect(iniciarSesion).not.toHaveBeenCalled();
    expect(document.activeElement).toBe(correo());   // el foco va al primer campo con problema
  });

  it('con credenciales incorrectas muestra el mensaje del servidor y vacía la contraseña', async () => {
    iniciarSesion.mockResolvedValue({ ok: false, error: 'Correo o contraseña incorrectos.', campos: {} });
    await escribir(correo(), 'ana.torres@creo.pe');
    await escribir(clave(), 'equivocada');

    await enviar();

    expect(pagina().querySelector('[role=alert]')?.textContent).toContain('Correo o contraseña incorrectos.');
    expect(clave().value).toBe('');
    // Se vació por seguridad: no se le reclama «escribe tu contraseña» por algo que no hizo mal
    expect(texto()).not.toContain('Escribe tu contraseña.');
  });

  it('un error del servidor sobre un campo se muestra debajo de ese campo', async () => {
    iniciarSesion.mockResolvedValue({ ok: false, error: '', campos: { correo: 'El correo admite hasta 120 caracteres.' } });
    await escribir(correo(), 'ana.torres@creo.pe');
    await escribir(clave(), 'algo');

    await enviar();

    expect(pagina().querySelector('#lg-mail-error')?.textContent).toContain('El correo admite hasta 120 caracteres.');
    expect(pagina().querySelector('[role=alert]')).toBeNull();
  });

  it('el mensaje del servidor desaparece en cuanto la persona corrige', async () => {
    iniciarSesion.mockResolvedValue({ ok: false, error: 'Correo o contraseña incorrectos.', campos: {} });
    await escribir(correo(), 'ana.torres@creo.pe');
    await escribir(clave(), 'equivocada');
    await enviar();

    await escribir(clave(), 'o');

    expect(pagina().querySelector('[role=alert]')).toBeNull();
  });

  it('con credenciales correctas entra a la pantalla de su rol y recuerda el correo si se pidió', async () => {
    iniciarSesion.mockResolvedValue({ ok: true, destino: '/admision' });
    const navegar = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    await escribir(correo(), ' ana.torres@creo.pe ');
    await escribir(clave(), 'demo123');
    pagina().querySelector<HTMLInputElement>('input[type=checkbox]')!.click();
    await fixture.whenStable();

    await enviar();

    expect(iniciarSesion).toHaveBeenCalledWith('ana.torres@creo.pe', 'demo123');
    expect(navegar).toHaveBeenCalledWith('/admision');
    expect(localStorage.getItem('flowtwin.correo')).toBe('ana.torres@creo.pe');
  });
});
