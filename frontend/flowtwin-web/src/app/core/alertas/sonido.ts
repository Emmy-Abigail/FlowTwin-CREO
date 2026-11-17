/**
 * Un aviso corto generado en el navegador (Web Audio): sin archivos de audio ni dependencias. Dos notas
 * para una alerta nueva; tres, más agudas, si es crítica. Si el navegador no deja reproducir (no hubo
 * interacción todavía, o no hay audio), simplemente no suena: nunca rompe la pantalla.
 */
let contexto: AudioContext | null = null;

export function sonarAlerta(critica: boolean): void {
  try {
    contexto ??= new AudioContext();
    if (contexto.state === 'suspended') void contexto.resume();
    const notas = critica ? [988, 1319, 1568] : [784, 1047];
    notas.forEach((frecuencia, i) => nota(contexto!, frecuencia, contexto!.currentTime + i * 0.16));
  } catch { /* sin audio: no pasa nada */ }
}

function nota(ctx: AudioContext, frecuencia: number, inicio: number): void {
  const osc = ctx.createOscillator();
  const volumen = ctx.createGain();
  osc.type = 'sine';
  osc.frequency.value = frecuencia;
  // Entrada y salida suaves: sin «clic» y sin asustar a nadie en la recepción
  volumen.gain.setValueAtTime(0.0001, inicio);
  volumen.gain.exponentialRampToValueAtTime(0.18, inicio + 0.02);
  volumen.gain.exponentialRampToValueAtTime(0.0001, inicio + 0.14);
  osc.connect(volumen).connect(ctx.destination);
  osc.start(inicio);
  osc.stop(inicio + 0.15);
}
