/**
 * Papel de cada zona del Piso 1 en el recorrido del paciente.
 * Los números son los id de public/gemelo/geometria-piso1.json.
 */
export const ZONAS = {
  admision: 1,          // Admisión y caja
  hall: 2,              // Hall principal
  sala: 3,              // Sala de espera
  triaje: 4,            // Triaje
  topicoEnfermeria: 5,  // Tópico de enfermería
  topicoUrgencia: 6,    // Tópico urgencia
  modulo2: 7,           // Módulo 2 (consultorio)
  observacion: 8,       // Observación
} as const;

/** Zonas donde el paciente recibe su atención: al entrar termina la espera y empieza la consulta. */
export const ZONAS_CONSULTA: readonly number[] = [
  ZONAS.topicoEnfermeria, ZONAS.topicoUrgencia, ZONAS.modulo2, ZONAS.observacion,
];

/** Zonas con cupo limitado: si están llenas, el paciente espera en la sala. */
export const ZONAS_CON_CUPO: readonly number[] = [ZONAS.triaje, ...ZONAS_CONSULTA];

export const PISO_PILOTO = 1;
export const SEDE = 'Sede Principal';
export const NOMBRE_SEDE = 'Clínica CREO+ – Sede Principal';

/** Horario de atención que muestran los reportes (07:00 a 18:59). */
export const HORA_APERTURA = 7;
export const HORAS_REPORTE = 12;

/** Peso relativo de llegadas por hora, de 07:00 a 18:00: picos de 9 a 11 y de 15 a 16, calma a las 13. */
export const LLEGADAS_POR_HORA = [3, 6, 9, 9, 6, 3, 2, 5, 7, 6, 3, 1];

/** Pacientes por hora por cada punto de peso de LLEGADAS_POR_HORA (lo usan la semilla y el simulador). */
export const PACIENTES_POR_PESO = 4.5;

/** Espera a partir de la cual el feed marca la tarjeta en amarillo y en rojo. */
export const ESPERA_AVISO_MIN = 30;
export const ESPERA_CRITICA_MIN = 45;
