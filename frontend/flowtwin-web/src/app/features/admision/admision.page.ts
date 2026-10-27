import { ChangeDetectionStrategy, Component, ElementRef, afterNextRender, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { BotonDirective } from '../../shared/componentes/boton.directive';
import { ChipComponent } from '../../shared/componentes/chip.component';
import { IconoComponent } from '../../shared/componentes/icono.component';
import { ModalComponent } from '../../shared/componentes/modal.component';
import { AdmisionStore } from './admision.store';

/**
 * P3 · Admisión. Un solo campo para escanear el QR de la tarjeta BLE: el sistema deduce
 * si es entrega (empieza el recorrido) o devolución (termina). Todo se puede hacer con
 * teclado: el foco siempre vuelve al campo de escaneo y Enter confirma.
 */
@Component({
  selector: 'app-admision',
  imports: [FormsModule, BotonDirective, ChipComponent, IconoComponent, ModalComponent],
  templateUrl: './admision.page.html',
  styleUrl: './admision.page.scss',
  providers: [AdmisionStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdmisionPage {
  protected store = inject(AdmisionStore);
  protected codigo = signal('');
  /** Tarjeta que se va a marcar como extraviada (abre la confirmación). */
  protected confirmarExtraviada = signal<string | null>(null);
  private campo = viewChild.required<ElementRef<HTMLInputElement>>('campo');

  constructor() {
    afterNextRender(() => this.enfocar());
  }

  protected registrar(): void {
    const ok = this.store.escanear(this.codigo());
    if (ok) pitido();
    this.codigo.set('');
    this.enfocar();
  }

  /** Botones de la ayuda de demo: rellenan el campo como si se hubiera escaneado. */
  protected rellenar(id: string): void {
    this.codigo.set(id);
    this.enfocar();
  }

  protected alternarModo(modo: 'entregar' | 'recibir'): void {
    this.store.alternarModo(modo);
    this.enfocar();
  }

  protected desinfectar(id: string): void {
    this.store.marcarDesinfectada(id);
    this.enfocar();
  }

  protected extraviar(): void {
    const id = this.confirmarExtraviada();
    if (id) this.store.marcarExtraviada(id);
    this.confirmarExtraviada.set(null);
    this.enfocar();
  }

  protected deshacer(): void {
    this.store.deshacer();
    this.enfocar();
  }

  private enfocar(): void {
    this.campo().nativeElement.focus({ preventScroll: true });
  }
}

/** Sonido corto de éxito al entregar o recibir (si el navegador lo permite). */
function pitido(): void {
  try {
    const ctx = new AudioContext();
    const osc = ctx.createOscillator();
    const vol = ctx.createGain();
    osc.frequency.value = 880;
    vol.gain.setValueAtTime(0.08, ctx.currentTime);
    vol.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.18);
    osc.connect(vol).connect(ctx.destination);
    osc.start();
    osc.stop(ctx.currentTime + 0.18);
    osc.onended = () => ctx.close();
  } catch { /* sin audio disponible */ }
}
