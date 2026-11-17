import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthService } from '../../core/auth/auth.service';
import { ClinicaStore } from '../../core/datos/clinica.store';
import { Actor } from '../../core/datos/modelos';
import { UsuariosSimulados } from './usuarios-simulados';

/** ADR-31: Usuarios en la Simulación, con las reglas del servidor y sin tocar la base. */
describe('Usuarios en la Simulación', () => {
  let sim: UsuariosSimulados;
  let clinica: ClinicaStore;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [{ provide: AuthService, useValue: { actor: signal<Actor>({ nombre: 'Ana Torres', rol: 'Administrador' }) } }],
    });
    sim = TestBed.inject(UsuariosSimulados);
    clinica = TestBed.inject(ClinicaStore);
  });

  it('trae el personal ficticio de la clínica simulada', () => {
    expect(sim.lista().map(u => `${u.nombres} ${u.apellidos}`)).toEqual(['Ana Torres', 'Luis Rojas', 'Juan Morales', 'Carla Díaz', 'Pedro Salas']);
    expect(sim.lista().find(u => u.nombres === 'Pedro')!.estadoRegistro).toBe(false);
  });

  it('crear un usuario da una contraseña temporal y queda en la bitácora simulada', () => {
    const r = sim.crear({ nombres: 'María', apellidos: 'Quispe', correo: 'Maria.Quispe@creo.pe', rol: 'OPERADOR', pisos: [1] });

    expect(r.contrasenaTemporal).toHaveLength(12);
    expect(r.usuario).toMatchObject({ correo: 'maria.quispe@creo.pe', debeCambiarContrasena: true });
    expect(clinica.auditoria()[0]).toMatchObject({ tipo: 'Usuarios', accion: 'Creó', elemento: 'María Quispe' });
  });

  it('aplica las reglas del servidor: correo único, pisos del Operador y un último Administrador', () => {
    expect(() => sim.crear({ nombres: 'Otra', apellidos: 'Ana', correo: 'admin@creo.pe', rol: 'GERENCIA', pisos: [] })).toThrow('Ya existe un usuario');
    expect(() => sim.crear({ nombres: 'Sin', apellidos: 'Piso', correo: 'sin@creo.pe', rol: 'OPERADOR', pisos: [] })).toThrow('al menos un piso');
    expect(() => sim.desactivar(1)).toThrow('al menos un Administrador');
  });

  it('desactivar y activar cambian el estado; «Reiniciar simulación» devuelve la lista inicial', () => {
    sim.desactivar(2);
    expect(sim.lista().find(u => u.id === 2)!.estadoRegistro).toBe(false);

    clinica.reiniciar();

    expect(sim.lista().find(u => u.id === 2)!.estadoRegistro).toBe(true);
    expect(sim.lista()).toHaveLength(5);
  });
});
