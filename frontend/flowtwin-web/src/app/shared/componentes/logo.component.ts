import { ChangeDetectionStrategy, Component } from '@angular/core';

/** Logo de CREO+: "Creo" en rojo con la cruz amarilla y roja. */
@Component({
  selector: 'app-logo',
  template: `
    <svg viewBox="0 0 150 46" width="138" height="42" role="img" aria-label="Creo+">
      <text x="0" y="36" font-family="Nunito, sans-serif" font-weight="900" font-size="40" fill="#A60437" letter-spacing="-1">Creo</text>
      <g transform="translate(108 8)">
        <rect x="10" y="0" width="11" height="31" rx="5.5" fill="#FBB62F" />
        <rect x="0" y="10" width="31" height="11" rx="5.5" fill="#FBB62F" />
        <rect x="12.5" y="5" width="6" height="21" rx="3" fill="#A60437" />
        <rect x="5" y="12.5" width="21" height="6" rx="3" fill="#A60437" />
      </g>
    </svg>
  `,
  styles: ':host { display: block; line-height: 0; }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LogoComponent {}
