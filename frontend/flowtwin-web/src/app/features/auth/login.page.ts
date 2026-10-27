import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { CLAVE_DEMO } from '../../core/auth/usuarios-demo';
import { BotonDirective } from '../../shared/componentes/boton.directive';
import { IconoComponent } from '../../shared/componentes/icono.component';
import { LogoComponent } from '../../shared/componentes/logo.component';

/** P1 · Iniciar sesión. Pantalla dividida con accesos rápidos de demo por rol. */
@Component({
  selector: 'app-login',
  imports: [FormsModule, LogoComponent, IconoComponent, BotonDirective],
  templateUrl: './login.page.html',
  styleUrl: './login.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoginPage {
  private auth = inject(AuthService);
  private router = inject(Router);

  protected correo = signal('');
  protected clave = signal('');
  protected error = signal('');
  protected mostrarAyuda = signal(false);

  protected readonly accesosDemo = [
    { rol: 'Operador', correo: 'operador@creo.pe' },
    { rol: 'Gerencia', correo: 'gerencia@creo.pe' },
    { rol: 'Administrador', correo: 'admin@creo.pe' },
  ];

  protected ingresar(): void {
    this.entrar(this.correo(), this.clave());
  }

  protected entrar(correo: string, clave = CLAVE_DEMO): void {
    const r = this.auth.iniciarSesion(correo, clave);
    if (r.ok) this.router.navigateByUrl(r.destino);
    else this.error.set(r.error);
  }
}
