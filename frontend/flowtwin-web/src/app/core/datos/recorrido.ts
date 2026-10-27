/**
 * Recorrido típico de un paciente por el Piso 1, usado por los datos semilla y por el
 * simulador. En el sistema real no existe: el recorrido sale de las lecturas BLE.
 */
import { Aleatorio } from '../utils/aleatorio';
import { MIN } from '../utils/tiempo';
import { ZONAS } from './clinica.config';
import { EventoZona, PasoRuta } from './modelos';

/** Admisión → Hall → Sala → Triaje → Sala → zona de atención → Hall (sale y devuelve la tarjeta). */
export function crearRuta(rnd: Aleatorio): PasoRuta[] {
  const destino = rnd.ponderado([
    [ZONAS.modulo2, 5],
    [ZONAS.topicoEnfermeria, 2],
    [ZONAS.topicoUrgencia, 1.5],
    [ZONAS.observacion, 1.5],
  ] as const);
  const enConsulta = destino === ZONAS.observacion ? rnd.entero(18, 32) : rnd.entero(8, 19);
  return [
    { zonaId: ZONAS.admision, minutos: rnd.entero(2, 4) },
    { zonaId: ZONAS.hall, minutos: 1 },
    { zonaId: ZONAS.sala, minutos: rnd.entero(4, 12) },
    { zonaId: ZONAS.triaje, minutos: rnd.entero(3, 5) },
    { zonaId: ZONAS.sala, minutos: rnd.entero(4, 14) },
    { zonaId: destino, minutos: enConsulta },
    { zonaId: ZONAS.hall, minutos: rnd.entero(2, 4) },
  ];
}

export interface Avance {
  eventos: EventoZona[];
  /** Índice del paso en el que está en el instante `ahora`. */
  paso: number;
  terminado: boolean;
  /** Hora en que termina el recorrido completo. */
  fin: number;
}

/** Recorre la ruta desde `inicio` hasta `ahora` y devuelve los eventos que habría generado. */
export function recorrer(inicio: number, ruta: PasoRuta[], ahora = Infinity): Avance {
  const eventos: EventoZona[] = [];
  let t = inicio;
  for (let i = 0; i < ruta.length; i++) {
    const salida = t + ruta[i].minutos * MIN;
    if (salida > ahora) {
      eventos.push({ zonaId: ruta[i].zonaId, entrada: t, salida: null });
      return { eventos, paso: i, terminado: false, fin: salida };
    }
    eventos.push({ zonaId: ruta[i].zonaId, entrada: t, salida });
    t = salida;
  }
  return { eventos, paso: ruta.length - 1, terminado: true, fin: t };
}
