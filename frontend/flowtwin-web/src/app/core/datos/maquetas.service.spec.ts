import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MaquetasService } from './maquetas.service';

describe('MaquetasService', () => {
  it('baja el modelo y los ambientes del piso desde la API (con la sesión del interceptor), no de public/', async () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const http = TestBed.inject(HttpTestingController);

    const promesa = TestBed.inject(MaquetasService).archivos(3);
    const glb = http.expectOne('/api/v1/pisos/3/maqueta/modelo.glb');
    expect(glb.request.responseType).toBe('arraybuffer');
    glb.flush(new ArrayBuffer(8));
    http.expectOne('/api/v1/pisos/3/maqueta/ambientes.json').flush({ ambientes: [{ id: 'ambiente_51', rotulo_en_plano: 'Sala de esperas', area_m2: 30, centro: [0, 0, 0] }] });

    const r = await promesa;
    expect(r.glb.byteLength).toBe(8);
    expect(r.ambientes[0].rotulo_en_plano).toBe('Sala de esperas');
    http.verify();
  });
});
