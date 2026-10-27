/**
 * Generador pseudoaleatorio con semilla. Con la misma semilla produce siempre los mismos
 * números, así la demo arranca igual cada vez que se recarga la página.
 */
export class Aleatorio {
  constructor(private semilla: number) {}

  /** Número entre 0 y 1. */
  siguiente(): number {
    this.semilla = (this.semilla * 16807) % 2147483647;
    return (this.semilla - 1) / 2147483646;
  }

  /** Entero entre a y b, ambos incluidos. */
  entero(a: number, b: number): number {
    return Math.floor(a + this.siguiente() * (b - a + 1));
  }

  /** Distribución normal (campana) con media m y desviación s. */
  gauss(m: number, s: number): number {
    let u = 0, v = 0;
    while (!u) u = this.siguiente();
    while (!v) v = this.siguiente();
    return m + s * Math.sqrt(-2 * Math.log(u)) * Math.cos(2 * Math.PI * v);
  }

  elegir<T>(items: readonly T[]): T {
    return items[this.entero(0, items.length - 1)];
  }

  /** Elige una opción según su peso: [['a', 3], ['b', 1]] sale 'a' el 75 % de las veces. */
  ponderado<T>(opciones: readonly (readonly [T, number])[]): T {
    let r = this.siguiente() * opciones.reduce((s, [, p]) => s + p, 0);
    for (const [valor, peso] of opciones) {
      if ((r -= peso) <= 0) return valor;
    }
    return opciones[opciones.length - 1][0];
  }
}

export const limitar = (v: number, min: number, max: number) => Math.max(min, Math.min(max, v));
