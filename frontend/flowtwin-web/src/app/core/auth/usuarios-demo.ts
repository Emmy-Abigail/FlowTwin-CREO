import { Usuario } from '../datos/modelos';

/** Contraseña de todos los usuarios demo. */
export const CLAVE_DEMO = 'demo123';

/** Usuarios de la demo. nuevo@creo.pe entra con "primer ingreso" y debe cambiar su contraseña. */
export const USUARIOS_DEMO: Usuario[] = [
  { id: 'u1', nombre: 'Ana Torres', correo: 'admin@creo.pe', rol: 'Administrador', pisos: [1], estado: 'Activo', clave: CLAVE_DEMO, ultimoIngreso: null },
  { id: 'u2', nombre: 'Luis Rojas', correo: 'operador@creo.pe', rol: 'Operador', pisos: [1], estado: 'Activo', clave: CLAVE_DEMO, ultimoIngreso: null },
  { id: 'u3', nombre: 'Juan Morales', correo: 'gerencia@creo.pe', rol: 'Gerencia', pisos: [1], estado: 'Activo', clave: CLAVE_DEMO, ultimoIngreso: null },
  { id: 'u4', nombre: 'Carla Díaz', correo: 'nuevo@creo.pe', rol: 'Operador', pisos: [1], estado: 'Primer ingreso', clave: CLAVE_DEMO, ultimoIngreso: null },
  { id: 'u5', nombre: 'Pedro Salas', correo: 'psalas@creo.pe', rol: 'Operador', pisos: [1], estado: 'Desactivado', clave: CLAVE_DEMO, ultimoIngreso: null },
];
