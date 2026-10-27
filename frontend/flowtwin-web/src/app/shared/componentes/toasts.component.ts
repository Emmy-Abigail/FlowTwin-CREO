import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ToastService } from '../../core/ui/toast.service';

/** Contenedor de toasts. Va una sola vez, en la raíz de la app. */
@Component({
  selector: 'app-toasts',
  template: `
    @for (t of toasts.toasts(); track t.id) {
      <div class="toast" [class]="'toast ' + t.tipo"><i></i><span>{{ t.texto }}</span></div>
    }
  `,
  styles: `
    :host { position: fixed; right: 20px; bottom: 20px; z-index: 90; display: flex; flex-direction: column; gap: 8px; max-width: calc(100vw - 40px); }
    .toast { background: var(--negro); color: #fff; border-radius: 10px; padding: 12px 16px; font-weight: 600;
      box-shadow: 0 10px 24px rgba(0,0,0,.25); display: flex; gap: 10px; align-items: center; animation: pop .2s ease-out; max-width: 420px; }
    .toast i { width: 8px; height: 8px; border-radius: 50%; background: var(--baja); flex: none; }
    .toast.warn i { background: var(--media); }
    .toast.bad i { background: var(--alta); }
  `,
  host: { 'aria-live': 'polite' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ToastsComponent {
  protected toasts = inject(ToastService);
}
