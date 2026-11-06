import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';
import { Rol } from '../../core/datos/modelos';
import { PisoActualService } from '../../core/datos/piso-actual.service';
import { ToastService } from '../../core/ui/toast.service';
import { IconoComponent } from '../../shared/componentes/icono.component';
import { FormularioContrasenaComponent } from '../auth/formulario-contrasena.component';

/** Qué hace cada rol en FlowTwin, dicho para la persona (no la tabla de permisos). */
const QUE_HACE: Record<Rol, string> = {
  Operador: 'Registras la llegada y la salida de los pacientes y ves el gemelo de tus pisos.',
  Gerencia: 'Consultas el gemelo en vivo y los reportes. Tu acceso es de solo lectura.',
  Administrador: 'Configuras el sistema: sedes, pisos, zonas, nodos y usuarios.',
};

/**
 * Mi cuenta: quién soy en el sistema, qué pisos veo y mi contraseña. Se abre desde el avatar.
 * Los datos del perfil son de solo lectura a propósito: el nombre, el correo, el rol y los pisos los
 * asigna el Administrador (control de acceso), así nadie puede darse más permisos.
 */
@Component({
  selector: 'app-cuenta',
  imports: [IconoComponent, FormularioContrasenaComponent],
  template: `
    <div class="page-head">
      <div>
        <h1>Mi cuenta</h1>
        <p>Tus datos de acceso a FlowTwin.</p>
      </div>
    </div>

    @if (auth.usuario(); as u) {
      <div class="rejilla">
        <section class="card pad perfil" aria-labelledby="t-perfil">
          <div class="quien">
            <span class="avatar" aria-hidden="true">{{ iniciales() }}</span>
            <div>
              <h2 id="t-perfil">{{ u.nombre }}</h2>
              <span class="correo">{{ u.correo }}</span>
            </div>
          </div>

          <dl>
            <div>
              <dt>Rol</dt>
              <dd><span class="rol">{{ u.rol }}</span><small>{{ queHace() }}</small></dd>
            </div>
            <div>
              <dt>Pisos</dt>
              <dd>
                @if (u.rol === 'Operador') {
                  @for (p of misPisos(); track p) { <span class="piso"><app-icono nombre="pin" [tamano]="14" />{{ p }}</span> }
                  @empty { <small>Aún no tienes pisos asignados.</small> }
                } @else {
                  <span class="piso"><app-icono nombre="pin" [tamano]="14" />Todos los pisos</span>
                }
              </dd>
            </div>
            <div>
              <dt>Sesión</dt>
              <dd><small><app-icono nombre="reloj" [tamano]="14" /> Se cierra sola tras 30 minutos sin actividad.</small></dd>
            </div>
          </dl>

          @if (u.rol !== 'Administrador') {
            <p class="nota">¿Algún dato no es correcto? Pídele al Administrador que lo actualice.</p>
          }
        </section>

        <section class="card pad" aria-labelledby="t-clave">
          <h2 id="t-clave">Cambiar contraseña</h2>
          <p class="sub">Usa una contraseña que no uses en otros sitios.</p>
          <app-formulario-contrasena (guardada)="toast.mostrar('Contraseña actualizada')" />
        </section>
      </div>
    }
  `,
  styles: `
    :host { display: flex; flex-direction: column; gap: 20px; }
    .rejilla { display: grid; grid-template-columns: minmax(300px, 400px) minmax(0, 560px); gap: 20px; align-items: start; }
    h2 { font-family: var(--f-disp); font-size: 20px; font-weight: 800; margin: 0; }
    .sub { margin: 4px 0 18px; color: var(--gris); }

    .quien { display: flex; align-items: center; gap: 14px; padding-bottom: 16px; border-bottom: 1px solid var(--linea); }
    .avatar { flex: none; width: 60px; height: 60px; border-radius: 16px; background: var(--amarillo); display: grid; place-items: center; font-size: 22px; font-weight: 800; }
    .correo { color: var(--gris); font-size: 14px; }

    dl { margin: 0; display: flex; flex-direction: column; }
    dl > div { display: grid; grid-template-columns: 70px 1fr; gap: 12px; padding: 14px 0; border-bottom: 1px solid var(--linea); }
    dt { font-size: 13px; font-weight: 700; color: var(--gris); padding-top: 2px; }
    dd { margin: 0; display: flex; flex-wrap: wrap; gap: 6px; align-items: center; }
    dd small { flex-basis: 100%; display: flex; gap: 6px; align-items: center; color: var(--gris); font-size: 13px; line-height: 1.4; }
    .rol { padding: 3px 10px; border-radius: 999px; background: var(--celeste); color: var(--celeste-t); font-size: 13px; font-weight: 700; }
    .piso { display: inline-flex; align-items: center; gap: 5px; padding: 3px 10px; border-radius: 999px; background: var(--fondo-2); font-size: 13px; font-weight: 600; }
    dl > div:last-child { border-bottom: 0; padding-bottom: 0; }
    .nota { margin: 14px 0 0; padding-top: 14px; border-top: 1px solid var(--linea); font-size: 13px; color: var(--gris); }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CuentaPage {
  protected auth = inject(AuthService);
  protected toast = inject(ToastService);
  private pisos = inject(PisoActualService);

  protected iniciales = computed(() => {
    const partes = (this.auth.usuario()?.nombre ?? '').split(' ');
    return `${partes[0]?.charAt(0) ?? ''}${partes[1]?.charAt(0) ?? ''}`.toUpperCase();
  });
  protected queHace = computed(() => {
    const rol = this.auth.rol();
    return rol ? QUE_HACE[rol] : '';
  });
  /** Nombres de los pisos asignados («Sede Principal · Piso 1»), tomados de las sedes ya cargadas. */
  protected misPisos = computed(() => {
    const ids = new Set(this.auth.usuario()?.pisos ?? []);
    const varias = this.pisos.sedes().length > 1;
    return this.pisos.sedes().flatMap(s => s.pisos.filter(p => ids.has(p.id)).map(p => varias ? `${s.nombre} · ${p.nombre}` : p.nombre));
  });
}
