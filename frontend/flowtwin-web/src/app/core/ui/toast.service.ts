import { Injectable, signal } from '@angular/core';

export type TipoToast = '' | 'warn' | 'bad';

export interface Toast {
  id: number;
  texto: string;
  tipo: TipoToast;
}

/** Mensajes breves de confirmación abajo a la derecha. Desaparecen solos a los 3.8 s. */
@Injectable({ providedIn: 'root' })
export class ToastService {
  readonly toasts = signal<Toast[]>([]);
  private id = 0;

  mostrar(texto: string, tipo: TipoToast = ''): void {
    const toast = { id: ++this.id, texto, tipo };
    this.toasts.update(lista => [...lista, toast]);
    setTimeout(() => this.toasts.update(lista => lista.filter(t => t !== toast)), 3800);
  }
}
