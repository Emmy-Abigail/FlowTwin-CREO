/**
 * Estado inicial de la Simulación: 6 días de historial y la clínica de hoy a la hora de apertura,
 * vacía, para que los pacientes vayan llegando de a poco como en un día real (el simulador los trae
 * según la hora). Incluye las tarjetas, los nodos, la auditoría y las alertas. Usa una semilla fija,
 * así la simulación arranca siempre igual.
 */
import { Aleatorio } from '../utils/aleatorio';
import { DIA, MIN, pad2 } from '../utils/tiempo';
import { HORA_APERTURA, LLEGADAS_POR_HORA, PACIENTES_POR_PESO, PISO_PILOTO, ZONAS } from './clinica.config';
import { Alerta, Atencion, EntradaAuditoria, Nodo, Operacion, Tarjeta } from './modelos';
import { crearRuta, recorrer } from './recorrido';

export interface EstadoInicial { 
  ahora: number;
  siguienteId: number;
  tarjetas: Tarjeta[];
  atenciones: Atencion[];   // hoy
  historial: Atencion[];    // los 6 días anteriores
  nodos: Nodo[];
  operaciones: Operacion[];
  auditoria: EntradaAuditoria[];
  alertas: Alerta[];
}

export const TOTAL_TARJETAS = 50;
/** Hace 4 días hubo un día de alta demanda, para que los reportes muestren picos de concentración. */
const DIA_ALTA_DEMANDA = -4;

export const idAtencion = (n: number) => 'A-' + String(n).padStart(4, '0');
export const idTarjeta = (n: number) => 'T-' + pad2(n);

export function crearDatosSemilla(hoy0: number): EstadoInicial {
  const rnd = new Aleatorio(20261012);
  const at = (h: number, m: number, dia = 0) => hoy0 + dia * DIA + (h * 60 + m) * MIN;
  // Hoy recién abre: nadie llegó todavía; el simulador trae a los pacientes según la hora
  const ahora = at(HORA_APERTURA, 0);

  // ---- Horas de llegada de los 6 días anteriores
  const llegadas: number[] = [];
  for (let dia = -6; dia <= -1; dia++) {
    const factor = dia === DIA_ALTA_DEMANDA ? 1.6 : 0.85 + rnd.siguiente() * 0.3;
    LLEGADAS_POR_HORA.forEach((peso, i) => {
      const n = Math.max(0, Math.round(rnd.gauss(peso * PACIENTES_POR_PESO * factor, 1.5)));
      for (let k = 0; k < n; k++) llegadas.push(at(HORA_APERTURA + i, rnd.entero(0, 59), dia));
    });
  }
  llegadas.sort((a, b) => a - b);

  // ---- Tarjetas: todas disponibles al abrir, salvo la que quedó sin devolver ayer
  const tarjetas: Tarjeta[] = Array.from({ length: TOTAL_TARJETAS }, (_, i) => ({
    id: idTarjeta(i + 1), estado: 'Disponible', atencionId: null,
    ble: '0x' + (0xC0A100 + i * 37).toString(16).toUpperCase(), desde: at(HORA_APERTURA, 0, -1),
  }));
  const tarjeta = (id: string) => tarjetas.find(t => t.id === id)!;

  // ---- Atenciones de los días anteriores: todas terminadas (hoy nadie llegó aún)
  const historial: Atencion[] = [];
  const hoy: Atencion[] = [];
  let n = 1;
  for (const inicio of llegadas) {
    const ruta = crearRuta(rnd);
    const av = recorrer(inicio, ruta);
    historial.push({ id: idAtencion(n++), pisoId: PISO_PILOTO, inicio, eventos: av.eventos,
      tarjetaId: idTarjeta(rnd.entero(1, TOTAL_TARJETAS)), fin: av.fin, estado: 'cerrada', motivo: 'Devuelta' });
  }

  // ---- Caso de Admisión: ayer un paciente se fue con la tarjeta (el sistema cerró su atención por inactividad)
  const perdida = historial.find(a => a.inicio >= at(15, 0, -1))!;
  perdida.tarjetaId = 'T-14';
  perdida.motivo = 'Sin lecturas';
  perdida.eventos = perdida.eventos.slice(0, 3);
  perdida.fin = perdida.eventos[2].salida = perdida.eventos[2].entrada + 18 * MIN;
  Object.assign(tarjeta('T-14'), { estado: 'Sin devolver', atencionId: perdida.id, desde: perdida.fin });

  // ---- Nodos ESP32 instalados (coinciden con los nodos de la geometría)
  const nodos: Nodo[] = [
    { codigo: 'N-01', mac: '24:6F:28:AA:01:01', pisoId: PISO_PILOTO, zonaId: ZONAS.admision, estado: 'En línea', ultimoLatido: ahora },
    { codigo: 'N-02', mac: '24:6F:28:AA:01:02', pisoId: PISO_PILOTO, zonaId: ZONAS.sala, estado: 'En línea', ultimoLatido: ahora },
    { codigo: 'N-03', mac: '24:6F:28:AA:01:03', pisoId: PISO_PILOTO, zonaId: ZONAS.modulo2, estado: 'En línea', ultimoLatido: ahora },
  ];

  // ---- Últimas operaciones de Admisión: vacía al abrir; se llena con las entregas del día
  const operaciones: Operacion[] = [];

  // ---- Auditoría inicial
  const auditoria: EntradaAuditoria[] = [];
  const A = (t: number, usuario: string, rol: EntradaAuditoria['rol'], tipo: EntradaAuditoria['tipo'], accion: string, elemento: string, detalle: string) =>
    auditoria.push({ t, usuario, rol, tipo, accion, elemento, detalle });
  A(at(9, 40, -6), 'Ana Torres', 'Administrador', 'Gemelo', 'Publicó cambios', 'Piso 1', '8 zonas · 3 nodos');
  A(at(9, 52, -6), 'Ana Torres', 'Administrador', 'Dispositivos', 'Registró nodo', 'N-03', 'Módulo 2 (consultorio)');
  A(at(10, 5, -6), 'Ana Torres', 'Administrador', 'Usuarios', 'Creó usuario', 'Luis Rojas', 'Rol Operador · Piso 1');
  A(at(10, 9, -6), 'Ana Torres', 'Administrador', 'Usuarios', 'Creó usuario', 'Juan Morales', 'Rol Gerencia');
  A(at(18, 1, -5), 'Ana Torres', 'Administrador', 'Usuarios', 'Desactivó usuario', 'Pedro Salas', 'Activo → Desactivado');
  A(at(8, 30, -2), 'Ana Torres', 'Administrador', 'Gemelo', 'Cambió aforo', 'Sala de espera', '20 → 24');
  A(at(12, 10, -2), 'Luis Rojas', 'Operador', 'Seguimiento', 'Pausó zona', 'Módulo 2 (consultorio)', 'Motivo: Limpieza');
  A(at(12, 40, -2), 'Luis Rojas', 'Operador', 'Seguimiento', 'Reactivó zona', 'Módulo 2 (consultorio)', 'Pausado → Activo');
  A(at(9, 15, -1), 'Juan Morales', 'Gerencia', 'Sesión', 'Inició sesión', '—', 'Navegador web');
  A(at(16, 22, -1), 'Juan Morales', 'Gerencia', 'Reportes', 'Exportó reporte', 'Historial · 7 días', 'CSV');
  A(at(17, 5, -1), 'Ana Torres', 'Administrador', 'Usuarios', 'Creó usuario', 'Carla Díaz', 'Rol Operador · Piso 1');
  A(perdida.fin, 'Sistema', '—', 'Admisión', 'Cerró atención por inactividad', perdida.id, 'T-14 · sin lecturas');
  auditoria.sort((a, b) => b.t - a.t);

  const alertas: Alerta[] = [{
    id: 1, nivel: 'alta', texto: 'Tarjeta T-14 sin devolver',
    detalle: `${perdida.id} · sin lecturas desde ayer, ${pad2(new Date(perdida.fin).getHours())}:${pad2(new Date(perdida.fin).getMinutes())}`,
    t: perdida.fin, ruta: '/admision', leida: false,
  }];

  return {
    ahora, siguienteId: n, tarjetas, atenciones: hoy, historial, nodos, operaciones, auditoria, alertas,
  };
}
