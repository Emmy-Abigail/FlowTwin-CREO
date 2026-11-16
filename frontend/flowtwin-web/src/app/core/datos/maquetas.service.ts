import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API } from '../api/problema';
import { AmbienteMaqueta } from '../../gemelo-render/maqueta';

/**
 * Descarga los archivos de la maqueta 3D de un piso desde la API (ADR-28). Va por HttpClient a propósito:
 * así pasa por el interceptor que agrega la sesión y la renueva si venció. El navegador guarda una copia
 * y, con el ETag, solo vuelve a bajar los MB del .glb si el equipo técnico instaló una versión nueva.
 */
@Injectable({ providedIn: 'root' })
export class MaquetasService {
  private http = inject(HttpClient);

  async archivos(pisoId: number): Promise<{ glb: ArrayBuffer; ambientes: AmbienteMaqueta[] }> {
    const base = `${API}/pisos/${pisoId}/maqueta`;
    const [glb, lista] = await Promise.all([
      firstValueFrom(this.http.get(`${base}/modelo.glb`, { responseType: 'arraybuffer' })),
      firstValueFrom(this.http.get<{ ambientes: AmbienteMaqueta[] }>(`${base}/ambientes.json`)),
    ]);
    return { glb, ambientes: lista.ambientes };
  }
}
