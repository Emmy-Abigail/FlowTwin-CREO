/**
 * Dispositivos en la Simulación (ADR-31): los nodos y las tarjetas de la clínica simulada, con el mismo formato
 * que la API (inventario paginado con filtros y conteos). Registrar, cambiar de zona, regenerar la clave y dar
 * de baja funcionan en memoria; la clave que se muestra es ficticia y no sirve para ningún equipo.
 */
import { ClinicaStore } from '../../core/datos/clinica.store';
import { HORA_APERTURA, PISO_PILOTO } from '../../core/datos/clinica.config';
import { Actor, EstadoTarjeta, Nodo, Tarjeta } from '../../core/datos/modelos';
import { zonaActual } from '../../core/datos/calculos';
import { DIA, inicioDelDia, pad2 } from '../../core/utils/tiempo';
import { CuentaMqtt, NodoConClave, NodoInventario, PaginaInventario, TarjetaInventario } from './modelo-dispositivos';

/** Lo mismo que la API (InventarioServicio.Tamano). */
export const TAMANO_PAGINA = 8;

const CODIGO_ESTADO: Record<EstadoTarjeta, string> = {
  'Disponible': 'DISPONIBLE', 'En uso': 'EN_USO', 'Por desinfectar': 'POR_DESINFECTAR',
  'Sin devolver': 'SIN_DEVOLVER', 'Extraviada': 'EXTRAVIADA', 'Baja': 'BAJA',
};
const NOMBRE_ESTADO: Record<string, string> = {
  DISPONIBLE: 'Disponible', EN_USO: 'En uso', POR_DESINFECTAR: 'Por desinfectar',
  SIN_DEVOLVER: 'Sin devolver', EXTRAVIADA: 'Extraviada', BAJA: 'De baja',
};

const numero = (codigo: string) => Number(codigo.replace(/\D/g, ''));
const macSinSeparadores = (mac: string) => mac.toLowerCase().replace(/[^0-9a-f]/g, '');

function paginar<T>(todas: T[], pagina: number, conteos: Record<string, number>): PaginaInventario<T> {
  const p = Math.max(1, pagina);
  return { filas: todas.slice((p - 1) * TAMANO_PAGINA, p * TAMANO_PAGINA), total: todas.length, pagina: p, tamano: TAMANO_PAGINA, conteos };
}

const coincide = (codigo: string, buscar: string) => !buscar || codigo.toLowerCase().includes(buscar.toLowerCase());

export class InventarioSimulado {
  constructor(private clinica: ClinicaStore, private nombreZona: (id: number) => string, private quien: () => Actor) {}

  // ---------------------------------------------------------------- Nodos

  nodos(filtro: string, buscar: string, pagina: number): PaginaInventario<NodoInventario> {
    const todos = this.clinica.nodos().filter(n => n.estado !== 'Baja').map(n => this.nodoInventario(n));
    const conteos = {
      ATENCION: todos.filter(n => n.requiereAtencion).length,
      EN_LINEA: todos.filter(n => n.estado === 'EN_LINEA').length,
      TODAS: todos.length,
    };
    const filtrados = todos
      .filter(n => filtro === 'ATENCION' ? n.requiereAtencion : filtro === 'EN_LINEA' ? n.estado === 'EN_LINEA' : true)
      .filter(n => coincide(n.codigo, buscar));
    return paginar(filtrados, pagina, conteos);
  }

  registrarNodo(codigo: string, mac: string, zonaId: number): NodoConClave {
    const c = codigo.trim().toUpperCase();
    if (!/^N-\d{2,4}$/.test(c)) throw new Error('El código debe tener la forma N-04.');
    if (macSinSeparadores(mac).length !== 12) throw new Error('La MAC debe tener 12 dígitos hexadecimales, por ejemplo A4:CF:12:9B:3E:04.');
    if (this.clinica.nodos().some(n => n.codigo === c && n.estado !== 'Baja')) throw new Error(`Ya existe el nodo ${c}.`);
    this.clinica.nodos.update(l => [...l, { codigo: c, mac: mac.trim().toUpperCase(), pisoId: PISO_PILOTO, zonaId, estado: 'Esperando primera señal', ultimoLatido: 0 }]);
    this.clinica.auditar('Dispositivos', 'Registró nodo', c, this.nombreZona(zonaId), this.quien());
    return this.conClave(c, mac);
  }

  cambiarZona(codigo: string, zonaId: number): void {
    this.clinica.nodos.update(l => l.map(n => (n.codigo === codigo ? { ...n, zonaId } : n)));
    this.clinica.auditar('Dispositivos', 'Cambió de zona', codigo, this.nombreZona(zonaId), this.quien());
  }

  regenerarClave(codigo: string): NodoConClave {
    const n = this.clinica.nodos().find(x => x.codigo === codigo)!;
    this.clinica.auditar('Dispositivos', 'Regeneró la clave', codigo, 'La anterior dejó de funcionar', this.quien());
    return this.conClave(codigo, n.mac);
  }

  darDeBajaNodo(codigo: string): void {
    this.clinica.nodos.update(l => l.map(n => (n.codigo === codigo ? { ...n, estado: 'Baja' } : n)));
    this.clinica.auditar('Dispositivos', 'Dio de baja', codigo, 'Nodo retirado', this.quien());
  }

  // ---------------------------------------------------------------- Tarjetas

  tarjetas(filtro: string, buscar: string, pagina: number): PaginaInventario<TarjetaInventario> {
    const todas = this.clinica.tarjetas().map(t => this.tarjetaInventario(t));
    const conteos: Record<string, number> = { TODAS: todas.length, ATENCION: todas.filter(requiereAtencion).length };
    for (const t of todas) conteos[t.estado] = (conteos[t.estado] ?? 0) + 1;
    const filtradas = todas
      .filter(t => filtro === 'ATENCION' ? requiereAtencion(t) : filtro === 'TODAS' ? true : t.estado === filtro)
      .filter(t => coincide(t.codigo, buscar));
    return paginar(filtradas, pagina, conteos);
  }

  /** Por rango, todas o ninguna (como la API): si alguna ya existe, no crea nada. */
  registrarTarjetas(desde: number, hasta: number): { codigo: string; codigoQr: string }[] {
    if (!desde || !hasta || desde > hasta) throw new Error('El rango no es válido: «Desde» debe ser menor o igual que «Hasta».');
    const codigos = Array.from({ length: hasta - desde + 1 }, (_, i) => 'T-' + pad2(desde + i));
    const repetidas = codigos.filter(c => this.clinica.tarjeta(c));
    if (repetidas.length) throw new Error(`Ya existen: ${repetidas.slice(0, 5).join(', ')}${repetidas.length > 5 ? '…' : ''}. No se registró ninguna.`);
    const ahora = this.clinica.ahora();
    this.clinica.tarjetas.update(l => [...l, ...codigos.map((id): Tarjeta => ({ id, estado: 'Disponible', atencionId: null, ble: '', desde: ahora }))]);
    this.clinica.auditar('Dispositivos', 'Registró tarjetas', `${codigos[0]} a ${codigos.at(-1)}`, `${codigos.length} tarjetas`, this.quien());
    return codigos.map(c => ({ codigo: c, codigoQr: 'CREO-' + c.replace('-', '') }));
  }

  darDeBajaTarjeta(codigo: string): void {
    this.clinica.tarjetas.update(l => l.map(t => (t.id === codigo ? { ...t, estado: 'Baja', desde: this.clinica.ahora() } : t)));
    this.clinica.auditar('Dispositivos', 'Dio de baja', codigo, 'Tarjeta retirada', this.quien());
  }

  // ---------------------------------------------------------------- Conversión

  private nodoInventario(n: Nodo): NodoInventario {
    const enLinea = n.estado === 'En línea';
    return {
      id: numero(n.codigo), codigo: n.codigo, mac: n.mac, zonaId: n.zonaId, zona: this.nombreZona(n.zonaId),
      pisoId: n.pisoId, piso: 'Piso ' + n.pisoId, estado: enLinea ? 'EN_LINEA' : 'SIN_SENAL',
      ultimoLatidoEn: n.ultimoLatido ? new Date(n.ultimoLatido).toISOString() : null,
      requiereAtencion: !enLinea, posX: null, posY: null, ajusteRssiDb: 0,
    };
  }

  /**
   * Dónde se vio por última vez: la zona actual de su paciente, o la de su última atención. Las tarjetas
   * del lote que nunca se usaron aparecen como «nunca detectada», igual que en el sistema real.
   */
  private tarjetaInventario(t: Tarjeta): TarjetaInventario {
    const estado = CODIGO_ESTADO[t.estado];
    const atencion = this.ultimaAtencion(t.id);
    const ultimo = atencion?.eventos.at(-1);
    const visto = ultimo ? (ultimo.salida ?? (atencion!.estado === 'activa' ? this.clinica.ahora() : atencion!.fin)) : null;
    const zona = atencion ? zonaActual(atencion) ?? ultimo?.zonaId ?? null : null;
    const problema: TarjetaInventario['problema'] = estado === 'EXTRAVIADA' ? 'EXTRAVIADA'
      : visto === null && estado === 'DISPONIBLE' ? 'NUNCA_DETECTADA'
      : visto !== null && estado === 'DISPONIBLE' && this.clinica.ahora() - visto > 3 * DIA ? 'SIN_DETECCION' : null;
    return {
      id: numero(t.id), codigo: t.id, estado, estadoNombre: NOMBRE_ESTADO[estado], codigoQr: 'CREO-' + t.id.replace('-', ''),
      problema, ultimaDeteccionEn: visto === null ? null : new Date(visto).toISOString(),
      zonaUltimaDeteccion: zona === null ? null : this.nombreZona(zona),
    };
  }

  private ultimaAtencion(tarjetaId: string) {
    const deHoy = this.clinica.atenciones().filter(a => a.tarjetaId === tarjetaId).at(-1);
    return deHoy ?? this.clinica.historial().filter(a => a.tarjetaId === tarjetaId && a.inicio >= inicioDelDia(this.clinica.ahora()) - 6 * DIA).at(-1);
  }

  private conClave(codigo: string, mac: string): NodoConClave {
    const id = macSinSeparadores(mac);
    const raiz = `flowtwin/v1/nodos/${id}`;
    const mqtt: CuentaMqtt = { usuario: `flowtwin-${id}`, temaLecturas: `${raiz}/lecturas`, temaLatidos: `${raiz}/latidos`, temaRespuestas: `${raiz}/respuestas` };
    // Ficticia a propósito: en la simulación ningún equipo se conecta con ella
    return { nodo: { id: numero(codigo), codigo }, clave: `nk_SIMULACION_${codigo.replace('-', '')}_${HORA_APERTURA}${pad2(numero(codigo))}`, mqtt };
  }
}

const requiereAtencion = (t: TarjetaInventario) => t.problema !== null || t.estado === 'SIN_DEVOLVER';
