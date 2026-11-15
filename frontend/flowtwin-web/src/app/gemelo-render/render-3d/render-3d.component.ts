import {
  ChangeDetectionStrategy, Component, ElementRef, OnDestroy, ViewEncapsulation,
  afterNextRender, effect, input, output, signal, viewChild,
} from '@angular/core';
import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { RoomEnvironment } from 'three/addons/environments/RoomEnvironment.js';
import { CSS2DObject, CSS2DRenderer } from 'three/addons/renderers/CSS2DRenderer.js';
import { GeometriaPiso, NivelOcupacion, OcupacionZona } from '../geometria';
import { AmbienteMaqueta, MaquetaPiso, cargarMaqueta, colorear, liberar, objetosPorZona } from '../maqueta';

/** Colores del semáforo (los mismos tokens del CSS). Una zona sin aforo (ninguno) no se tiñe. */
const COLOR_NIVEL: Record<NivelOcupacion, string> = {
  alta: '#D1431F', media: '#E9A400', baja: '#2E9E6B', nd: '#B0B0B0', ninguno: '#FFFFFF',
};
/** Cuánto color lleva el piso del ambiente: deja ver el porcelanato; «sin datos» va más suave. */
const MEZCLA = { normal: 0.62, nd: 0.45 };
/** Brillo propio del color: lo hace legible sobre la textura; la zona bajo el cursor brilla más. */
const BRILLO = { normal: 0.06, nd: 0.03, activa: 0.3 };

/**
 * Luz. La guía usa ACES con exposición 0,68: ACES oscurece y apaga los colores (es para escenas de
 * cine). Usamos «Neutral» (Khronos PBR Neutral), hecho para que los materiales se vean con el color
 * con que se diseñaron: blancos limpios y madera cálida, como una maqueta de arquitectura.
 * Intensidades físicas (desde r155): la guía multiplicada por π y subida para un interior luminoso.
 */
const LUZ = { cielo: 0.9, sol: 2.1, exposicion: 0.85 };

const ICONO_PERSONA = '<svg viewBox="0 0 24 24" width="13" height="13" fill="currentColor"><circle cx="12" cy="7" r="4"/><path d="M5 21v-2a5 5 0 0 1 5-5h4a5 5 0 0 1 5 5v2z"/></svg>';

interface ZonaEnEscena {
  id: number;
  /** Los ambientes de la maqueta donde se pinta la zona (uno o varios). */
  mallas: THREE.Mesh[];
  etiqueta: HTMLDivElement;
  nivel: NivelOcupacion;
}

/** Nombre de un ambiente que no es zona (Farmacia, Rayos X…): solo para ubicarse, sin datos. */
interface NombreAmbiente { etiqueta: HTMLDivElement; area: number; }

/**
 * Escena Three.js de un piso: la maqueta 3D de la clínica, con cada zona pintada sobre sus ambientes
 * según su ocupación, su etiqueta (nombre y cifra), el nombre de los demás ambientes y los nodos. Es solo de presentación: recibe la geometría, la maqueta
 * y la ocupación por input() y avisa por output() qué zona se tocó. No sabe nada de pacientes.
 *
 * Coordenadas: el sistema mide desde la esquina del piso (0..anchoM, 0..fondoM) y la maqueta viene
 * centrada en el origen. Se mueve la maqueta (anchoM/2, fondoM/2) y todo lo demás queda igual.
 *
 * Rendimiento: la maqueta se carga una vez. Cuando cambia la ocupación solo cambian colores y textos.
 */
@Component({
  selector: 'app-render-3d',
  templateUrl: './render-3d.component.html',
  styleUrl: './render-3d.component.scss',
  // Las etiquetas las crea Three.js fuera de la plantilla, así que sus estilos no pueden
  // estar encapsulados. Todas las clases llevan el prefijo r3d- para no chocar con nada.
  encapsulation: ViewEncapsulation.None,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Render3dComponent implements OnDestroy {
  readonly geometria = input.required<GeometriaPiso>();
  readonly maqueta = input.required<MaquetaPiso>();
  readonly ocupacion = input<OcupacionZona[]>([]);
  readonly zonaClic = output<number>();

  protected estado = signal<'cargando' | 'lista' | 'error'>('cargando');

  private lienzo = viewChild.required<ElementRef<HTMLDivElement>>('lienzo');

  private renderer?: THREE.WebGLRenderer;
  private etiquetas?: CSS2DRenderer;
  private escena?: THREE.Scene;
  private entorno?: THREE.Texture;
  private modelo?: THREE.Group;
  private camara?: THREE.PerspectiveCamera;
  private controles?: OrbitControls;
  private zonas: ZonaEnEscena[] = [];
  private mallasZona: THREE.Mesh[] = [];
  /** De mayor a menor área: cuando no caben todos, se ven primero los ambientes grandes. */
  private nombres: NombreAmbiente[] = [];
  private observador?: ResizeObserver;
  private raycaster = new THREE.Raycaster();
  private puntero = new THREE.Vector2();
  private zonaBajoCursor: number | null = null;
  private inicioClic: [number, number] | null = null;
  /** true cuando la cámara se movió y hay que volver a separar las etiquetas. */
  private camaraMovida = true;
  private destruido = false;

  constructor() {
    // Three.js necesita el <div> ya dibujado: se construye después del primer render.
    afterNextRender(() => this.construir());
    effect(() => this.pintarOcupacion(this.ocupacion()));
  }

  /**
   * Vuelve la cámara a la vista inicial: mira el centro del piso desde 40° sobre la
   * vertical, a la distancia justa para que el piso LLENE el lienzo (con un margen mínimo).
   */
  protected vistaInicial(): void {
    const camara = this.camara, controles = this.controles;
    if (!camara || !controles) return;
    const g = this.geometria();
    const mitadFov = THREE.MathUtils.degToRad(camara.fov / 2);
    // En perspectiva a 40°, el fondo del piso se ve acortado: con 1.15 entra justo, sin sobrar aire
    const distanciaAncho = (g.anchoM * 1.22) / 2 / (Math.tan(mitadFov) * camara.aspect);
    const distanciaFondo = (g.fondoM * 1.15) / 2 / Math.tan(mitadFov);
    const distancia = Math.max(distanciaAncho, distanciaFondo);
    const inclinacion = THREE.MathUtils.degToRad(40);
    controles.target.set(g.anchoM / 2, 0, g.fondoM / 2);
    camara.position.set(
      g.anchoM / 2 - distancia * 0.08,
      distancia * Math.cos(inclinacion),
      g.fondoM / 2 + distancia * Math.sin(inclinacion),
    );
    controles.update();
  }

  // ------------------------------------------------------------------ construcción (una vez)

  private construir(): void {
    const g = this.geometria();
    const contenedor = this.lienzo().nativeElement;

    // Color sRGB, tono Neutral (ver LUZ) y sombras suaves. La guía pide PCFSoftShadowMap, que r186
    // ya no tiene: PCFShadowMap con radio (en crearLuces) da el mismo borde suave.
    this.renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
    this.renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
    this.renderer.outputColorSpace = THREE.SRGBColorSpace;
    this.renderer.toneMapping = THREE.NeutralToneMapping;
    this.renderer.toneMappingExposure = LUZ.exposicion;
    this.renderer.shadowMap.enabled = true;
    this.renderer.shadowMap.type = THREE.PCFShadowMap;
    contenedor.appendChild(this.renderer.domElement);

    this.etiquetas = new CSS2DRenderer();
    this.etiquetas.domElement.className = 'r3d-capa-etiquetas';
    contenedor.appendChild(this.etiquetas.domElement);

    this.escena = new THREE.Scene();
    // Luz de ambiente suave (reflejos y relleno), generada una vez
    const pmrem = new THREE.PMREMGenerator(this.renderer);
    this.entorno = pmrem.fromScene(new RoomEnvironment(), 0.04).texture;
    pmrem.dispose();
    this.escena.environment = this.entorno;

    this.camara = new THREE.PerspectiveCamera(40, 1, 0.5, 500);
    this.controles = new OrbitControls(this.camara, this.renderer.domElement);
    this.controles.enableDamping = true;
    this.controles.maxPolarAngle = Math.PI * 0.47; // nunca por debajo del piso
    this.controles.minDistance = 8;
    this.controles.maxDistance = 120;
    // El punto al que mira la cámara se queda sobre la losa: no se puede "salir" del piso.
    this.controles.addEventListener('change', () => {
      const t = this.controles!.target;
      t.set(THREE.MathUtils.clamp(t.x, 0, g.anchoM), 0, THREE.MathUtils.clamp(t.z, 0, g.fondoM));
      this.camaraMovida = true;
    });

    this.crearLuces(g);
    this.crearNodos(g);

    const lienzo = this.renderer.domElement;
    lienzo.addEventListener('pointermove', this.alMoverPuntero);
    lienzo.addEventListener('pointerleave', this.alSalirPuntero);
    lienzo.addEventListener('pointerdown', this.alPresionar);
    lienzo.addEventListener('pointerup', this.alSoltar);

    this.observador = new ResizeObserver(() => this.ajustarTamano());
    this.observador.observe(contenedor);
    this.ajustarTamano();
    this.vistaInicial(); // después de medir el lienzo: el encuadre depende de su proporción

    this.renderer.setAnimationLoop(() => {
      this.controles!.update();
      this.renderer!.render(this.escena!, this.camara!);
      this.etiquetas!.render(this.escena!, this.camara!);
      if (this.camaraMovida) this.separarEtiquetas();
    });

    this.cargar(g);
  }

  /** Cielo + sol de la guía, con sombras que cubren todo el piso. */
  private crearLuces(g: GeometriaPiso): void {
    const centro = new THREE.Vector3(g.anchoM / 2, 0, g.fondoM / 2);
    // Cielo blanco y rebote cálido del piso; sol apenas cálido, como luz de tarde
    this.escena!.add(new THREE.HemisphereLight(0xffffff, 0xd9cdbd, LUZ.cielo));
    const sol = new THREE.DirectionalLight(0xfff1de, LUZ.sol);
    sol.position.copy(centro).add(new THREE.Vector3(-14, 30, 16));
    sol.target.position.copy(centro);
    sol.castShadow = true;
    sol.shadow.mapSize.set(4096, 4096);
    Object.assign(sol.shadow.camera, { left: -28, right: 28, top: 18, bottom: -18, far: 80 });
    sol.shadow.bias = -0.0004;
    sol.shadow.normalBias = 0.03;
    sol.shadow.radius = 3;
    this.escena!.add(sol, sol.target);
  }

  /** Descarga la maqueta y pinta las zonas sobre sus ambientes. */
  private async cargar(g: GeometriaPiso): Promise<void> {
    const maqueta = this.maqueta();
    try {
      const { escena, ambientes } = await cargarMaqueta(maqueta);
      if (this.destruido) { liberar(escena); return; }   // se salió de la pantalla mientras cargaba
      escena.position.set(g.anchoM / 2, 0, g.fondoM / 2);
      escena.updateMatrixWorld(true);
      this.modelo = escena;
      this.escena!.add(escena);

      const { porZona, faltantes } = objetosPorZona(maqueta, ambientes);
      if (faltantes.length) console.warn('[gemelo] Rótulos que no están en la maqueta:', faltantes);
      this.crearZonas(g, porZona);
      this.crearNombres(ambientes, porZona);
      this.pintarOcupacion(this.ocupacion());
      this.estado.set('lista');
    } catch (e) {
      console.error('[gemelo] No se pudo cargar la maqueta 3D', e);
      if (!this.destruido) this.estado.set('error');
    }
  }

  /** Enlaza cada zona con sus ambientes y le pone su etiqueta en el centro de ellos. */
  private crearZonas(g: GeometriaPiso, porZona: Map<number, string[]>): void {
    for (const z of g.zonas) {
      const mallas = (porZona.get(z.id) ?? [])
        .map(nombre => this.modelo!.getObjectByName(nombre))
        .filter((o): o is THREE.Mesh => o instanceof THREE.Mesh);
      mallas.forEach(m => (m.userData['zonaId'] = z.id));
      this.mallasZona.push(...mallas);

      // Centro de sus ambientes; si la zona aún no está enlazada, el de su rectángulo
      const caja = new THREE.Box3();
      mallas.forEach(m => caja.expandByObject(m));
      const centro = caja.isEmpty() ? new THREE.Vector3(z.x + z.ancho / 2, 0, z.y + z.fondo / 2) : caja.getCenter(new THREE.Vector3());

      // Etiqueta angosta (nombre arriba, ícono y cifra abajo) para que no choque con las vecinas.
      const etiqueta = document.createElement('div');
      etiqueta.className = 'r3d-etiqueta';
      etiqueta.innerHTML = `<b></b><span class="r3d-cifra"><span class="r3d-icono">${ICONO_PERSONA}</span><small></small></span>`;
      etiqueta.querySelector('b')!.textContent = z.nombre;
      const objeto = new CSS2DObject(etiqueta);
      objeto.position.set(centro.x, 0.4, centro.z);
      this.escena!.add(objeto);

      this.zonas.push({ id: z.id, mallas, etiqueta, nivel: 'nd' });
    }
    this.camaraMovida = true;
  }

  /**
   * El nombre de cada ambiente que no es zona, en texto discreto: así se ubica uno en el piso
   * (Farmacia, Rayos X, Escalera N°1…) sin competir con las zonas, que son las que tienen datos.
   */
  private crearNombres(ambientes: AmbienteMaqueta[], porZona: Map<number, string[]>): void {
    const deZonas = new Set([...porZona.values()].flat());
    for (const a of ambientes) {
      const malla = deZonas.has(a.id) ? undefined : this.modelo!.getObjectByName(a.id);
      if (!malla) continue;
      const centro = new THREE.Box3().setFromObject(malla).getCenter(new THREE.Vector3());
      const etiqueta = document.createElement('div');
      etiqueta.className = 'r3d-ambiente';
      etiqueta.textContent = a.rotulo_en_plano;
      const objeto = new CSS2DObject(etiqueta);
      objeto.position.set(centro.x, 0.2, centro.z);
      this.escena!.add(objeto);
      this.nombres.push({ etiqueta, area: a.area_m2 });
    }
    this.nombres.sort((x, y) => y.area - x.area);
  }

  /** Nodos ESP32 como cilindros pequeños (su código se ve en el tooltip y en el detalle). */
  private crearNodos(g: GeometriaPiso): void {
    const cuerpo = new THREE.CylinderGeometry(0.18, 0.18, 0.5, 16);
    const material = new THREE.MeshStandardMaterial({ color: '#3A3536', emissive: '#1A1A1A' });
    for (const n of g.nodos) {
      const nodo = new THREE.Mesh(cuerpo, material);
      nodo.position.set(n.x, 0.25, n.y);
      this.escena!.add(nodo);
    }
  }

  // ------------------------------------------------------------------ actualizaciones (cada tick)

  /** Solo cambia colores y textos: no se crea ni se destruye geometría. */
  private pintarOcupacion(ocupacion: OcupacionZona[]): void {
    const porZona = new Map(ocupacion.map(o => [o.zonaId, o]));
    for (const z of this.zonas) {
      const o = porZona.get(z.id);
      z.nivel = o?.nivel ?? 'nd';
      this.pintarZona(z, z.id === this.zonaBajoCursor);
      z.etiqueta.style.setProperty('--r3d-c', z.nivel === 'ninguno' ? '#8A8183' : COLOR_NIVEL[z.nivel]);
      z.etiqueta.classList.toggle('r3d-sin-datos', z.nivel === 'nd');
      z.etiqueta.classList.toggle('r3d-solo-nombre', !!o?.soloNombre);
      z.etiqueta.querySelector('small')!.textContent = o ? (o.etiqueta ?? (o.aforo === null ? `${o.personas}` : `${o.personas}/${o.aforo}`)) : '—';
    }
  }

  /** Tiñe los ambientes de una zona con el color de su nivel; sin aforo, quedan como en la maqueta. */
  private pintarZona(z: ZonaEnEscena, activa: boolean): void {
    const nd = z.nivel === 'nd';
    for (const m of z.mallas) {
      if (z.nivel === 'ninguno') colorear(m, activa ? COLOR_NIVEL.nd : null, 0.25, activa ? BRILLO.nd : 0);
      else colorear(m, COLOR_NIVEL[z.nivel], nd ? MEZCLA.nd : MEZCLA.normal, activa ? BRILLO.activa : nd ? BRILLO.nd : BRILLO.normal);
    }
  }

  private resaltar(zonaId: number | null): void {
    for (const z of this.zonas) {
      this.pintarZona(z, z.id === zonaId);
      z.etiqueta.classList.toggle('r3d-activa', z.id === zonaId);
    }
  }

  // ------------------------------------------------------------------ interacción

  /** Zona bajo el puntero, con un rayo desde la cámara (Raycaster) contra los ambientes de las zonas. */
  private zonaEn(e: PointerEvent): number | null {
    const r = this.renderer!.domElement.getBoundingClientRect();
    this.puntero.set(((e.clientX - r.left) / r.width) * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1);
    this.raycaster.setFromCamera(this.puntero, this.camara!);
    const golpe = this.raycaster.intersectObjects(this.mallasZona, false)[0];
    return golpe ? (golpe.object.userData['zonaId'] as number) : null;
  }

  // Funciones flecha: así se pueden quitar con removeEventListener al destruir.
  private alMoverPuntero = (e: PointerEvent) => {
    const id = this.zonaEn(e);
    const cambio = id !== this.zonaBajoCursor;
    if (cambio) {
      this.zonaBajoCursor = id;
      this.resaltar(id);
      this.renderer!.domElement.style.cursor = id == null ? '' : 'pointer';
    }
  };

  private alSalirPuntero = () => {
    this.zonaBajoCursor = null;
    this.resaltar(null);
  };

  private alPresionar = (e: PointerEvent) => {
    this.inicioClic = [e.clientX, e.clientY];
  };

  /** Es clic solo si el puntero casi no se movió; si no, el usuario estaba girando la cámara. */
  private alSoltar = (e: PointerEvent) => {
    const inicio = this.inicioClic;
    this.inicioClic = null;
    if (!inicio || Math.hypot(e.clientX - inicio[0], e.clientY - inicio[1]) > 5) return;
    const id = this.zonaEn(e);
    if (id != null) this.zonaClic.emit(id);
  };

  private ajustarTamano(): void {
    const el = this.lienzo().nativeElement;
    const w = el.clientWidth, h = el.clientHeight;
    if (!w || !h) return;
    this.renderer!.setSize(w, h);
    this.etiquetas!.setSize(w, h);
    this.camara!.aspect = w / h;
    this.camara!.updateProjectionMatrix();
    this.camaraMovida = true;
  }

  /**
   * Separa las etiquetas de zonas vecinas que se pisan en pantalla: la de más arriba sube
   * y la de más abajo baja, la mitad del choque cada una. Se usa margin-top porque el
   * CSS2DRenderer ya ocupa el transform de cada etiqueta. Solo corre cuando la cámara se movió.
   */
  private separarEtiquetas(): void {
    this.camaraMovida = false;
    const etiquetas = this.zonas.map(z => z.etiqueta);
    etiquetas.forEach(e => (e.style.marginTop = '0px'));
    const cajas = etiquetas.map(e => e.getBoundingClientRect());
    const desplazamiento = etiquetas.map(() => 0);
    for (let pasada = 0; pasada < 4; pasada++) {
      for (let i = 0; i < cajas.length; i++) {
        for (let j = i + 1; j < cajas.length; j++) {
          const a = cajas[i], b = cajas[j];
          const arribaA = a.top + desplazamiento[i], arribaB = b.top + desplazamiento[j];
          const seTocanX = a.left < b.right + 4 && b.left < a.right + 4;
          const choque = Math.min(arribaA + a.height, arribaB + b.height) - Math.max(arribaA, arribaB) + 4;
          if (!seTocanX || choque <= 0) continue;
          const [sube, baja] = arribaA <= arribaB ? [i, j] : [j, i];
          desplazamiento[sube] -= choque / 2;
          desplazamiento[baja] += choque / 2;
        }
      }
    }
    etiquetas.forEach((e, k) => (e.style.marginTop = `${Math.round(desplazamiento[k])}px`));
    // Las de zona tienen prioridad; las que Three.js ocultó (fuera de cámara) miden 0 y no cuentan
    this.mostrarNombres(cajas.map((c, k) => new DOMRect(c.x, c.y + desplazamiento[k], c.width, c.height)).filter(c => c.width > 0));
  }

  /**
   * Muestra los nombres de ambiente que caben sin pisar una etiqueta de zona ni otro nombre, del más
   * grande al más chico. De lejos se ven los principales; al acercarse aparecen los demás.
   */
  private mostrarNombres(ocupadas: DOMRect[]): void {
    const marco = this.lienzo().nativeElement.getBoundingClientRect();
    const pisa = (a: DOMRect, b: DOMRect) => a.left < b.right + 3 && b.left < a.right + 3 && a.top < b.bottom + 3 && b.top < a.bottom + 3;
    const dentro = (a: DOMRect) => a.left >= marco.left && a.right <= marco.right && a.top >= marco.top && a.bottom <= marco.bottom;
    const cajas = this.nombres.map(n => n.etiqueta.getBoundingClientRect());   // primero leer todo, luego escribir
    const visibles = this.nombres.map((_, k) => {
      const c = cajas[k];
      const cabe = c.width > 0 && dentro(c) && !ocupadas.some(o => pisa(c, o));
      if (cabe) ocupadas.push(c);
      return cabe;
    });
    this.nombres.forEach((n, k) => (n.etiqueta.style.visibility = visibles[k] ? 'visible' : 'hidden'));
  }

  // ------------------------------------------------------------------ limpieza

  /** Libera la memoria de la GPU y los listeners al salir de la pantalla. */
  ngOnDestroy(): void {
    this.destruido = true;
    this.observador?.disconnect();
    this.renderer?.setAnimationLoop(null);
    const lienzo = this.renderer?.domElement;
    lienzo?.removeEventListener('pointermove', this.alMoverPuntero);
    lienzo?.removeEventListener('pointerleave', this.alSalirPuntero);
    lienzo?.removeEventListener('pointerdown', this.alPresionar);
    lienzo?.removeEventListener('pointerup', this.alSoltar);
    this.controles?.dispose();
    if (this.escena) {
      liberar(this.escena);   // maqueta (con sus texturas) y nodos
      this.escena.traverse(o => { if (o instanceof CSS2DObject) o.element.remove(); });
    }
    this.entorno?.dispose();
    this.renderer?.dispose();
    lienzo?.remove();
    this.etiquetas?.domElement.remove();
    this.zonas = [];
    this.mallasZona = [];
    this.nombres = [];
  }
}
