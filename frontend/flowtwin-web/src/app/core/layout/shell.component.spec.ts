import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { Rol } from '../datos/modelos';
import { ModoDatosService } from '../datos/modo-datos.service';
import { PisoActualService } from '../datos/piso-actual.service';
import { ShellComponent } from './shell.component';

/** El marco común: menú por rol, barra colapsable y la franja de simulación (ADR-22). */
describe('ShellComponent', () => {
  let fixture: ComponentFixture<ShellComponent>;
  const rol = signal<Rol | null>('Administrador');
  const puedeSimular = signal(true);
  const simulacion = signal(false);
  const pagina = () => fixture.nativeElement as HTMLElement;
  const textoMenu = () => [...pagina().querySelectorAll('.nav .item')].map(a => a.textContent?.trim());

  async function crear() {
    fixture = TestBed.createComponent(ShellComponent);
    await fixture.whenStable();
  }

  beforeEach(async () => {
    localStorage.clear();
    rol.set('Administrador');
    puedeSimular.set(true);
    simulacion.set(false);
    await TestBed.configureTestingModule({
      imports: [ShellComponent],
      providers: [
        provideRouter([]),
        {
          provide: AuthService, useValue: {
            rol, usuario: signal({ id: 1, nombre: 'Ana Torres', correo: 'admin@creo.pe', rol: 'Administrador', pisos: [1], debeCambiarContrasena: false }),
            registrarActividad: vi.fn(), cerrarSesion: vi.fn(),
          },
        },
        { provide: ModoDatosService, useValue: { cargar: vi.fn(), puedeSimular, simulacion, activar: vi.fn(), salir: vi.fn() } },
        { provide: PisoActualService, useValue: { cargar: vi.fn() } },
      ],
    }).compileComponents();
  });

  it('el Administrador solo ve las pantallas que existen', async () => {
    await crear();

    expect(textoMenu()).toEqual(['Gemelo en vivo', 'Historial y reportes']);
  });

  it('el Operador no tiene el botón de Simulación', async () => {
    rol.set('Operador');
    puedeSimular.set(false);
    await crear();

    expect(pagina().querySelector('.simulacion')).toBeNull();
  });

  it('colapsar el menú deja solo íconos y se recuerda', async () => {
    await crear();

    pagina().querySelector<HTMLButtonElement>('[aria-label="Colapsar el menú"]')!.click();
    await fixture.whenStable();

    expect(pagina().querySelector('.app')!.classList).toContain('colapsada');
    expect(localStorage.getItem('flowtwin.menuColapsado')).toBe('true');
  });

  it('en simulación la franja violeta lo avisa en toda la pantalla', async () => {
    await crear();
    expect(pagina().querySelector('.franja-simulacion')).toBeNull();

    simulacion.set(true);
    await fixture.whenStable();

    expect(pagina().querySelector('.franja-simulacion')?.textContent).toContain('datos ficticios');
    expect(pagina().querySelector('.app')!.classList).toContain('simulando');
  });

  it('el avatar lleva a «Mi cuenta» y cerrar sesión está a un clic, al lado', async () => {
    await crear();

    expect(pagina().querySelector('a.yo')!.getAttribute('href')).toBe('/cuenta');
    pagina().querySelector<HTMLButtonElement>('[aria-label="Cerrar sesión"]')!.click();
    expect(TestBed.inject(AuthService).cerrarSesion).toHaveBeenCalled();
  });

  it('ya no muestra datos falsos en el marco: ni reloj «En vivo» ni campana inventada', async () => {
    await crear();

    expect(pagina().textContent).not.toContain('En vivo');
    expect(pagina().querySelector('[aria-label="Alertas"]')).toBeNull();
  });
});
