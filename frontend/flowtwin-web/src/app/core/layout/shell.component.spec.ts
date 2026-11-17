import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { AlertasService } from '../alertas/alertas.service';
import { Alerta } from '../alertas/modelo-alertas';
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
  const alertasDisponibles = signal(false);
  const alertas = signal<Alerta[]>([]);
  const pulso = signal(0);
  const usuario = signal({ id: 1, nombre: 'Ana Torres', correo: 'admin@creo.pe', rol: 'Administrador' as Rol, pisos: [1], debeCambiarContrasena: false });
  const cargarPisos = vi.fn();
  const cargarModo = vi.fn();
  const reiniciar = vi.fn();
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
    alertasDisponibles.set(false);
    alertas.set([]);
    usuario.update(u => ({ ...u, debeCambiarContrasena: false }));
    cargarPisos.mockClear();
    cargarModo.mockClear();
    reiniciar.mockClear();
    await TestBed.configureTestingModule({
      imports: [ShellComponent],
      providers: [
        provideRouter([{ path: 'admision', children: [] }, { path: 'gemelo', children: [] }]),
        {
          provide: AuthService, useValue: {
            rol, usuario,
            registrarActividad: vi.fn(), cerrarSesion: vi.fn(),
          },
        },
        { provide: ModoDatosService, useValue: { cargar: cargarModo, puedeSimular, simulacion, activar: vi.fn(), salir: vi.fn(), reiniciar } },
        { provide: PisoActualService, useValue: { cargar: cargarPisos } },
        { provide: AlertasService, useValue: { disponible: alertasDisponibles, lista: alertas, pendientes: alertas, pulso, silencio: signal(false), ahora: signal(0) } },
      ],
    }).compileComponents();
  });

  it('con la contraseña temporal no pide pisos ni configuración, y los pide en cuanto se cambia (F-02)', async () => {
    usuario.update(u => ({ ...u, debeCambiarContrasena: true }));
    await crear();
    expect(cargarPisos).not.toHaveBeenCalled();   // el servidor respondería 403 y la app quedaría sin pisos
    expect(cargarModo).not.toHaveBeenCalled();

    usuario.update(u => ({ ...u, debeCambiarContrasena: false }));
    await fixture.whenStable();
    expect(cargarPisos).toHaveBeenCalledTimes(1);
    expect(cargarModo).toHaveBeenCalledTimes(1);
  });

  it('el Administrador solo ve las pantallas que existen', async () => {
    await crear();

    expect(textoMenu()).toEqual(['Gemelo en vivo', 'Historial y reportes', 'Dispositivos', 'Usuarios', 'Auditoría', 'Configuración']);
  });

  it('sin simulación disponible no aparece el botón', async () => {
    puedeSimular.set(false);
    await crear();

    expect(pagina().querySelector('.simulacion')).toBeNull();
  });

  it('en Admisión la franja aclara que esa pantalla es real', async () => {
    rol.set('Operador');
    simulacion.set(true);
    await crear();

    await TestBed.inject(Router).navigateByUrl('/admision');
    await fixture.whenStable();
    expect(pagina().querySelector('.franja-simulacion')?.textContent).toContain('Admisión no se simula');

    await TestBed.inject(Router).navigateByUrl('/gemelo');
    await fixture.whenStable();
    expect(pagina().querySelector('.franja-simulacion')?.textContent).toContain('datos ficticios');
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

  it('la campana muestra cuántas alertas hay y solo aparece para quien las atiende', async () => {
    await crear();
    expect(pagina().querySelector('.item.alertas')).toBeNull();   // Gerencia, o sin piso: no hay campana

    alertasDisponibles.set(true);
    alertas.set([{ clave: 'ESPERA_LARGA:1:CRITICA', tipo: 'ESPERA_LARGA', nivel: 'CRITICA', desde: 0, atencion: '1', atencionId: 1, codigo: 'A-0001', zonaId: 3, nodo: null }]);
    await fixture.whenStable();

    expect(pagina().querySelector('.item.alertas .contador')?.textContent?.trim()).toBe('1');
    expect(pagina().querySelector('.item.alertas')?.getAttribute('aria-label')).toBe('Alertas: 1');
  });

  it('la franja de simulación ofrece reiniciar (salir ya está en el menú)', async () => {
    simulacion.set(true);
    await crear();
    const boton = pagina().querySelector<HTMLButtonElement>('.franja-simulacion button')!;

    expect(boton.textContent?.trim()).toBe('Reiniciar simulación');
    boton.click();
    expect(reiniciar).toHaveBeenCalledTimes(1);
  });
});
