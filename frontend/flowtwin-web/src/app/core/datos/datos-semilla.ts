/**
 * Estado inicial de la demo: 6 días de historial, la mañana de hoy hasta las 10:30,
 * las tarjetas, los nodos, la auditoría y las alertas. Usa una semilla fija, así la
 * demo arranca siempre igual. Cuando exista el backend, este archivo desaparece.
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
  const ahora = at(10, 30);

  // ---- Horas de llegada de los 7 días (6 anteriores + hoy hasta ahora)
  const llegadas: number[] = [];
  for (let dia = -6; dia <= 0; dia++) {
    const factor = dia === DIA_ALTA_DEMANDA ? 1.6 : 0.85 + rnd.siguiente() * 0.3;
    LLEGADAS_POR_HORA.forEach((peso, i) => {
      const n = Math.max(0, Math.round(rnd.gauss(peso * PACIENTES_POR_PESO * factor, 1.5)));
      for (let k = 0; k < n; k++) llegadas.push(at(HORA_APERTURA + i, rnd.entero(0, 59), dia));
    });
  }
  llegadas.sort((a, b) => a - b);

  // ---- Tarjetas: 3 apartadas para los casos de demo de Admisión
  const tarjetas: Tarjeta[] = Array.from({ length: TOTAL_TARJETAS }, (_, i) => ({
    id: idTarjeta(i + 1), estado: 'Disponible', atencionId: null,
    ble: '0x' + (0xC0A100 + i * 37).toString(16).toUpperCase(), desde: at(7, 0),
  }));
  const tarjeta = (id: string) => tarjetas.find(t => t.id === id)!;
  const apartadas = ['T-11', 'T-14', 'T-23'];
  const libres = tarjetas.filter(t => !apartadas.includes(t.id)).map(t => t.id);

  // ---- Atenciones: cada llegada recorre su ruta hasta ahora
  const historial: Atencion[] = [];
  const hoy: Atencion[] = [];
  let n = 1;
  for (const inicio of llegadas) {
    if (inicio >= ahora) continue;
    const ruta = crearRuta(rnd);
    const av = recorrer(inicio, ruta, ahora);
    const base = { id: idAtencion(n++), pisoId: PISO_PILOTO, inicio, eventos: av.eventos };
    if (av.terminado) {
      const a: Atencion = { ...base, tarjetaId: idTarjeta(rnd.entero(1, TOTAL_TARJETAS)), fin: av.fin, estado: 'cerrada', motivo: 'Devuelta' };
      (inicio < hoy0 ? historial : hoy).push(a);
    } else {
      const tid = libres.splice(rnd.entero(0, libres.length - 1), 1)[0];
      hoy.push({ ...base, tarjetaId: tid, fin: null, estado: 'activa', motivo: null, ruta, paso: av.paso });
      Object.assign(tarjeta(tid), { estado: 'En uso', atencionId: base.id, desde: inicio });
    }
  }

  // ---- Caso de demo: una tarjeta que no se devolvió (atención cerrada por inactividad)
  const perdida = hoy.find(a => a.estado === 'cerrada' && a.inicio >= at(8, 15))!;
  perdida.tarjetaId = 'T-14';
  perdida.motivo = 'Sin lecturas';
  perdida.eventos = perdida.eventos.slice(0, 3);
  perdida.fin = perdida.eventos[2].salida = perdida.eventos[2].entrada + 18 * MIN;
  Object.assign(tarjeta('T-14'), { estado: 'Sin devolver', atencionId: perdida.id, desde: perdida.fin });
  Object.assign(tarjeta('T-11'), { estado: 'Por desinfectar', desde: at(10, 18) });
  Object.assign(tarjeta('T-23'), { estado: 'Por desinfectar', desde: at(10, 24) });

  // ---- Nodos ESP32 instalados (coinciden con los nodos de la geometría)
  const nodos: Nodo[] = [
    { codigo: 'N-01', mac: '24:6F:28:AA:01:01', pisoId: PISO_PILOTO, zonaId: ZONAS.admision, estado: 'En línea', ultimoLatido: ahora },
    { codigo: 'N-02', mac: '24:6F:28:AA:01:02', pisoId: PISO_PILOTO, zonaId: ZONAS.sala, estado: 'En línea', ultimoLatido: ahora },
    { codigo: 'N-03', mac: '24:6F:28:AA:01:03', pisoId: PISO_PILOTO, zonaId: ZONAS.modulo2, estado: 'En línea', ultimoLatido: ahora },
  ];

  // ---- Últimas operaciones de Admisión
  const operaciones: Operacion[] = [
    ...hoy.filter(a => a.estado === 'activa').map(a => ({ t: a.inicio, tipo: 'Entregada' as const, tarjetaId: a.tarjetaId, atencionId: a.id })),
    ...hoy.filter(a => a.motivo === 'Devuelta').map(a => ({ t: a.fin!, tipo: 'Recibida' as const, tarjetaId: a.tarjetaId, atencionId: a.id, totalMin: Math.round((a.fin! - a.inicio) / MIN) })),
  ].sort((a, b) => b.t - a.t).slice(0, 12);

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
  A(at(7, 30), 'Luis Rojas', 'Operador', 'Sesión', 'Inició sesión', '—', 'Navegador web');
  A(at(8, 2), 'Ana Torres', 'Administrador', 'Sesión', 'Inició sesión', '—', 'Navegador web');
  A(at(8, 5), 'Ana Torres', 'Administrador', 'Usuarios', 'Creó usuario', 'Carla Díaz', 'Rol Operador · Piso 1');
  A(perdida.fin, 'Sistema', '—', 'Admisión', 'Cerró atención por inactividad', perdida.id, 'T-14 · sin lecturas');
  operaciones.slice(0, 8).forEach(o =>
    A(o.t, 'Luis Rojas', 'Operador', 'Admisión', o.tipo === 'Entregada' ? 'Entregó tarjeta' : 'Recibió tarjeta', o.tarjetaId, o.atencionId ?? ''));
  auditoria.sort((a, b) => b.t - a.t);

  const alertas: Alerta[] = [{
    id: 1, nivel: 'alta', texto: 'Tarjeta T-14 sin devolver',
    detalle: `${perdida.id} · sin lecturas desde las ${pad2(new Date(perdida.fin).getHours())}:${pad2(new Date(perdida.fin).getMinutes())}`,
    t: perdida.fin, ruta: '/admision', leida: false,
  }];

  return {
    ahora, siguienteId: n, tarjetas, atenciones: hoy, historial, nodos, operaciones, auditoria, alertas,
  };
}
