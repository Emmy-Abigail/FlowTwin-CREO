import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AdmisionPage } from './admision.page';

/** P3 · Admisión en la pantalla (reglas R1–R9). La API se simula con HttpTestingController. */
describe('AdmisionPage', () => {
  let fixture: ComponentFixture<AdmisionPage>;
  let http: HttpTestingController;
  const pagina = () => fixture.nativeElement as HTMLElement;
  const campo = () => pagina().querySelector<HTMLInputElement>('#scan')!;
  const botonRegistrar = () => pagina().querySelector<HTMLButtonElement>('button.registrar')!;
  const aviso = () => pagina().querySelector('#scan-aviso')?.textContent?.replace(/\s+/g, ' ').trim() ?? '';
  const modal = () => pagina().querySelector('[role=dialog]');
  const boton = (texto: string) => [...pagina().querySelectorAll('button')].find(b => b.textContent?.trim().startsWith(texto))!;
  const RESUMEN = {
    contadores: { DISPONIBLE: 11, EN_USO: 2, POR_DESINFECTAR: 1, SIN_DEVOLVER: 1 },
    porDesinfectar: [{ tarjetaId: 11, codigo: 'T-11', desde: '2026-11-06T15:05:00Z', atencionCodigo: null }],
    sinDevolver: [{ tarjetaId: 14, codigo: 'T-14', desde: '2026-11-06T13:47:00Z', atencionCodigo: 'A-0128' }],
    ultimasOperaciones: [],
  };
  let RESUMEN_ACTUAL: typeof RESUMEN = RESUMEN;
  const hace = (min: number) => new Date(Date.now() - min * 60_000).toISOString();
  const PREVIA = (operacion: string, desde = hace(30)) => ({ tarjetaId: 7, tarjeta: 'T-07', estado: 'X', desde, operacion, codigo: null, mensaje: null });
  const ENTREGA = {
    operacion: 'ENTREGA', tarjeta: { id: 7, codigo: 'T-07', estado: 'EN_USO', estadoNombre: 'En uso' },
    atencion: { id: 1, codigo: 'A-0143', inicioEn: new Date().toISOString(), finEn: null, esperaMin: null, consultaMin: null, totalMin: null },
    deshacerHasta: new Date(Date.now() + 30_000).toISOString(),
  };

  async function estable() {
    await fixture.whenStable();
    http.match('/api/v1/admision/resumen').forEach(r => r.flush(RESUMEN_ACTUAL));
    await new Promise(r => setTimeout(r));   // deja que las respuestas lleguen al store antes de mirar
    await fixture.whenStable();
  }
  /** Lo que hace el lector: escribe el contenido del QR en el campo y manda Enter. */
  async function leer(qr: string) {
    campo().value = qr;
    campo().dispatchEvent(new Event('input'));
    campo().dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true }));
    await fixture.whenStable();
  }
  async function leerYResponder(qr: string, respuesta: object) {
    await leer(qr);
    http.expectOne(r => r.url === '/api/v1/admision/vista-previa').flush(respuesta);
    await estable();
  }
  async function clic(b: HTMLButtonElement) {
    b.click();
    await estable();
  }

  beforeEach(async () => {
    RESUMEN_ACTUAL = RESUMEN;
    await TestBed.configureTestingModule({
      imports: [AdmisionPage],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(AdmisionPage);
    await estable();
  });

  it('R1 · el Enter del lector no registra: muestra qué pasará y pasa el foco a Registrar', async () => {
    await leerYResponder('CREO-T07', PREVIA('ENTREGA'));

    expect(aviso()).toContain('T-07 · se entregará');
    expect(document.activeElement).toBe(botonRegistrar());
    http.expectNone('/api/v1/admision/escaneos');
  });

  it('R1 · Registrar confirma en modo AUTO y deja una línea de confirmación', async () => {
    await leerYResponder('CREO-T07', PREVIA('ENTREGA'));
    botonRegistrar().click();
    await fixture.whenStable();
    const r = http.expectOne('/api/v1/admision/escaneos');
    expect(r.request.body).toEqual({ codigoQr: 'CREO-T07', modo: 'AUTO' });
    r.flush(ENTREGA);
    await estable();

    const conf = pagina().querySelector('.confirmacion.entrega')!;
    expect(conf.textContent).toMatch(/T-07 entregada · \d\d:\d\d/);
    expect(conf.textContent).not.toContain('Di al paciente');
    expect(conf.textContent).toMatch(/Deshacer entrega · (30|29) s/);
    expect(campo().value).toBe('');
  });

  it('R2 · una tarjeta por desinfectar no llega al servidor al pulsar Registrar', async () => {
    await leerYResponder('T-11', { tarjetaId: 11, tarjeta: 'T-11', estado: 'POR_DESINFECTAR', desde: hace(20), operacion: null, codigo: 'RN03_POR_DESINFECTAR', mensaje: 'x' });
    await clic(botonRegistrar());

    expect(aviso()).toContain('T-11 · aún no está desinfectada');
    http.expectNone('/api/v1/admision/escaneos');
  });

  it('R3 · devolver una tarjeta entregada hace 1 min abre un modal; cancelar no registra', async () => {
    await leerYResponder('T-07', PREVIA('DEVOLUCION', hace(1)));
    await clic(botonRegistrar());

    expect(modal()?.textContent).toContain('¿Recibir T-07?');
    await clic(boton('No, cancelar'));
    expect(modal()).toBeNull();
    http.expectNone('/api/v1/admision/escaneos');
  });

  it('R3 · confirmar en el modal sí registra', async () => {
    await leerYResponder('T-07', PREVIA('DEVOLUCION', hace(1)));
    await clic(botonRegistrar());
    boton('Sí, recibirla').click();
    await fixture.whenStable();

    http.expectOne('/api/v1/admision/escaneos');
  });

  it('R3 · el modal recibe el foco: Enter confirma y el lector no escribe detrás', async () => {
    await leerYResponder('T-07', PREVIA('DEVOLUCION', hace(1)));
    await clic(botonRegistrar());
    await new Promise(r => setTimeout(r));

    expect(document.activeElement?.textContent?.trim()).toBe('Sí, recibirla');
    document.activeElement!.dispatchEvent(new KeyboardEvent('keydown', { key: 'C', bubbles: true, cancelable: true }));
    expect(campo().value).toBe('T-07');
  });

  it('R4 · desinfectar abre la tanda con todas marcadas y las envía juntas', async () => {
    await clic(boton('Desinfectar…'));
    expect(modal()?.textContent).toContain('Desinfectar tarjetas');
    http.expectNone('/api/v1/tarjetas/desinfectar');

    boton('Marcar 1 como desinfectada').click();
    await fixture.whenStable();
    expect(http.expectOne('/api/v1/tarjetas/desinfectar').request.body).toEqual({ tarjetaIds: [11] });
  });

  it('R5 · revisar las sin devolver empieza sin ninguna marcada: extraviar es una decisión explícita', async () => {
    await clic(boton('Revisar…'));
    expect(modal()?.textContent).toContain('Tarjetas sin devolver');

    await clic(boton('Marcar 0 como extraviadas'));
    expect(modal()?.textContent).toContain('Marca al menos una tarjeta.');
    http.expectNone('/api/v1/tarjetas/extraviar');
  });

  it('R6 · deshacer no pide confirmación: llama directo al servidor', async () => {
    await leerYResponder('CREO-T07', PREVIA('ENTREGA'));
    await clic(botonRegistrar());
    http.expectOne('/api/v1/admision/escaneos').flush(ENTREGA);
    await estable();

    boton('Deshacer entrega').click();
    await fixture.whenStable();
    expect(modal()).toBeNull();
    http.expectOne('/api/v1/admision/atenciones/1/deshacer');
  });

  it('R7 · Registrar con el campo vacío dice qué hacer y no llama al servidor', async () => {
    await clic(botonRegistrar());

    expect(aviso()).toContain('No hay tarjeta leída');
    http.expectNone('/api/v1/admision/escaneos');
  });

  it('R8 · al leer otra tarjeta, la confirmación anterior desaparece', async () => {
    await leerYResponder('CREO-T07', PREVIA('ENTREGA'));
    await clic(botonRegistrar());
    http.expectOne('/api/v1/admision/escaneos').flush(ENTREGA);
    await estable();
    expect(pagina().querySelector('.confirmacion')).not.toBeNull();

    await leer('CREO-T08');

    expect(pagina().querySelector('.confirmacion')).toBeNull();
  });

  it('R9 · dos clics seguidos en Registrar registran una sola vez', async () => {
    await leerYResponder('CREO-T07', PREVIA('ENTREGA'));
    botonRegistrar().click();
    botonRegistrar().click();
    await fixture.whenStable();

    expect(http.match('/api/v1/admision/escaneos').length).toBe(1);
  });

  it('a la derecha: la barra de tarjetas y lo que requiere acción, agrupado (una fila por tipo)', () => {
    expect(pagina().textContent).toContain('Tarjetas de la sede · 15');
    expect(pagina().querySelectorAll('.grupos li').length).toBe(2);
    expect(pagina().textContent).toContain('Por desinfectar · 1');
    expect(pagina().textContent).toContain('Sin devolver · 1');
  });

  it('sin pendientes dice «Todo al día» en vez de dejar un hueco', async () => {
    RESUMEN_ACTUAL = { ...RESUMEN, porDesinfectar: [], sinDevolver: [] };
    fixture = TestBed.createComponent(AdmisionPage);
    await estable();

    expect(pagina().textContent).toContain('Todo al día');
  });
});
