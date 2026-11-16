/** Descarga de archivos que llegan de la API (CSV), comunes a Historial y Auditoría. */

/** Guarda un Blob con su nombre, como si fuera un enlace de descarga. */
export function descargar(blob: Blob, archivo: string): void {
  const url = URL.createObjectURL(blob);
  Object.assign(document.createElement('a'), { href: url, download: archivo }).click();
  setTimeout(() => URL.revokeObjectURL(url), 1_000);
}

/** «attachment; filename=a.csv; filename*=UTF-8''a.csv» → «a.csv». */
export function nombreDe(cabecera: string | null): string | null {
  const m = /filename\*=UTF-8''([^;]+)/i.exec(cabecera ?? '') ?? /filename="?([^";]+)"?/i.exec(cabecera ?? '');
  return m ? decodeURIComponent(m[1]) : null;
}
