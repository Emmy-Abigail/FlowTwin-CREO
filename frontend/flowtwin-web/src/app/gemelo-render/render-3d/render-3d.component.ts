import {
  ChangeDetectionStrategy, Component, ElementRef, OnDestroy, ViewEncapsulation,
  afterNextRender, effect, input, output, signal, viewChild,
} from '@angular/core';
import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { CSS2DObject, CSS2DRenderer } from 'three/addons/renderers/CSS2DRenderer.js';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';
import { GeometriaPiso, NivelOcupacion, OcupacionZona, ZonaHover } from '../geometria';

/** Altura de los "muros bajos", para ver dentro de las zonas (m). */
const MURO_BAJO = 1.2;

/** Colores del semáforo (los mismos tokens del CSS). */
const COLOR_NIVEL: Record<NivelOcupacion, string> = {
  alta: '#E53935', media: '#F2A900', baja: '#2E9E6B', nd: '#B0B0B0',
};

const ICONO_PERSONA = '<svg viewBox="0 0 24 24" width="13" height="13" fill="currentColor"><circle cx="12" cy="7" r="4"/><path d="M5 21v-2a5 5 0 0 1 5-5h4a5 5 0 0 1 5 5v2z"/></svg>';

interface ZonaEnEscena {
  id: number;
  placa: THREE.Mesh<THREE.BoxGeometry, THREE.MeshStandardMaterial>;
  etiqueta: HTMLDivElement;
}

/**
 * Escena Three.js de un piso: losa, plano de fondo, muros extruidos, zonas con semáforo,
 * etiquetas y nodos. Es solo de presentación: recibe la geometría y la ocupación por
 * input() y avisa por output() qué zona se tocó. No sabe nada de pacientes ni tarjetas.
 *
 * Rendimiento: la geometría se construye una sola vez. Cuando cambia la ocupación solo
 * se actualizan los colores de las placas y el texto de las etiquetas.
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
  readonly ocupacion = input<OcupacionZona[]>([]);
  readonly planoUrl = input<string | null>(null);
  readonly zonaClic = output<number>();
  readonly zonaHover = output<ZonaHover | null>();

  /** Interruptores de vista: no cambian datos, solo cómo se ve la escena. */
  protected alturaReal = signal(false);
  protected mostrarPlano = signal(true);

  private lienzo = viewChild.required<ElementRef<HTMLDivElement>>('lienzo');

  private renderer?: THREE.WebGLRenderer;
  private etiquetas?: CSS2DRenderer;
  private escena?: THREE.Scene;
  private camara?: THREE.PerspectiveCamera;
  private controles?: OrbitControls;
  private muros?: THREE.Mesh;
  private plano?: THREE.Mesh;
  private textura?: THREE.Texture;
  private zonas: ZonaEnEscena[] = [];
  private observador?: ResizeObserver;
  private raycaster = new THREE.Raycaster();
  private puntero = new THREE.Vector2();
  private zonaBajoCursor: number | null = null;
  private inicioClic: [number, number] | null = null;
  /** true cuando la cámara se movió y hay que volver a separar las etiquetas. */
  private camaraMovida = true;

  constructor() {
    // Three.js necesita el <div> ya dibujado: se construye después del primer render.
    afterNextRender(() => this.construir());

    effect(() => this.pintarOcupacion(this.ocupacion()));
    effect(() => {
      const alto = this.alturaReal() ? this.geometria().altoMuroM : MURO_BAJO;
      if (this.muros) this.muros.scale.y = alto;
    });
    effect(() => {
      const visible = this.mostrarPlano();
      if (this.plano) this.plano.visible = visible;
    });
  }

  /**
   * Vuelve la cámara a la vista inicial: mira el centro del piso desde 40° sobre la
   * vertical, a la distancia justa para que entre todo el ancho del piso en el lienzo.
   */
  protected vistaInicial(): void {
    const camara = this.camara, controles = this.controles;
    if (!camara || !controles) return;
    const g = this.geometria();
    const mitadFov = THREE.MathUtils.degToRad(camara.fov / 2);
    const distanciaAncho = (g.anchoM * 1.08) / 2 / (Math.tan(mitadFov) * camara.aspect);
    const distanciaFondo = (g.fondoM * 1.6) / 2 / Math.tan(mitadFov);
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

    this.renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
    this.renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
    contenedor.appendChild(this.renderer.domElement);

    this.etiquetas = new CSS2DRenderer();
    this.etiquetas.domElement.className = 'r3d-capa-etiquetas';
    contenedor.appendChild(this.etiquetas.domElement);

    this.escena = new THREE.Scene();
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

    this.escena.add(new THREE.HemisphereLight(0xffffff, 0xb9b0a6, 2.4));
    const sol = new THREE.DirectionalLight(0xffffff, 1.6);
    sol.position.set(-20, 40, -10);
    this.escena.add(sol);

    this.crearLosaYPlano(g);
    this.crearMuros(g);
    this.crearZonas(g);
    this.crearNodos(g);

    this.pintarOcupacion(this.ocupacion());

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
  }

  private crearLosaYPlano(g: GeometriaPiso): void {
    const losa = new THREE.Mesh(
      new THREE.BoxGeometry(g.anchoM + 0.6, 0.25, g.fondoM + 0.6),
      new THREE.MeshStandardMaterial({ color: '#E7E0D7', roughness: 0.95 }),
    );
    losa.position.set(g.anchoM / 2, -0.13, g.fondoM / 2);
    this.escena!.add(losa);

    const url = this.planoUrl();
    if (!url) return;
    this.textura = new THREE.TextureLoader().load(url);
    this.textura.colorSpace = THREE.SRGBColorSpace;
    this.textura.anisotropy = this.renderer!.capabilities.getMaxAnisotropy();
    // La imagen cubre exactamente anchoM × fondoM.
    this.plano = new THREE.Mesh(
      new THREE.PlaneGeometry(g.anchoM, g.fondoM),
      new THREE.MeshBasicMaterial({ map: this.textura, transparent: true, opacity: 0.55, depthWrite: false }),
    );
    this.plano.rotation.x = -Math.PI / 2;
    this.plano.position.set(g.anchoM / 2, 0.005, g.fondoM / 2);
    this.plano.visible = this.mostrarPlano();
    this.escena!.add(this.plano);
  }

  /**
   * Cada muro es un polígono con agujeros. Se dibuja como Shape con la y invertida
   * (en el plano y crece hacia el fondo), se extruye 1 m y se gira para que quede de pie.
   * Todos se unen en una sola geometría: un solo objeto que dibujar, en vez de cientos.
   */
  private crearMuros(g: GeometriaPiso): void {
    const piezas = g.muros.map(m => {
      const forma = new THREE.Shape(m.o.map(([x, y]) => new THREE.Vector2(x, -y)));
      for (const agujero of m.h) forma.holes.push(new THREE.Path(agujero.map(([x, y]) => new THREE.Vector2(x, -y))));
      const pieza = new THREE.ExtrudeGeometry(forma, { depth: 1, bevelEnabled: false, curveSegments: 1 });
      pieza.rotateX(-Math.PI / 2);
      return pieza;
    });
    const unidos = mergeGeometries(piezas);
    piezas.forEach(p => p.dispose());
    this.muros = new THREE.Mesh(unidos, new THREE.MeshStandardMaterial({ color: '#FBF8F4', roughness: 0.8 }));
    // La geometría mide 1 m de alto: la escala en y es la altura real del muro.
    this.muros.scale.y = this.alturaReal() ? g.altoMuroM : MURO_BAJO;
    this.escena!.add(this.muros);
  }

  private crearZonas(g: GeometriaPiso): void {
    for (const z of g.zonas) {
      const placa = new THREE.Mesh(
        new THREE.BoxGeometry(z.ancho, 0.06, z.fondo),
        new THREE.MeshStandardMaterial({ color: COLOR_NIVEL.nd, transparent: true, opacity: 0.72, roughness: 1, depthWrite: false }),
      );
      placa.position.set(z.x + z.ancho / 2, 0.04, z.y + z.fondo / 2);
      placa.userData['zonaId'] = z.id;
      this.escena!.add(placa);

      // Etiqueta angosta (nombre arriba, ícono y cifra abajo) para que no choque con las vecinas.
      const etiqueta = document.createElement('div');
      etiqueta.className = 'r3d-etiqueta';
      etiqueta.innerHTML = `<b></b><span class="r3d-cifra"><span class="r3d-icono">${ICONO_PERSONA}</span><small></small></span>`;
      etiqueta.querySelector('b')!.textContent = z.nombre;
      const objeto = new CSS2DObject(etiqueta);
      objeto.position.set(z.x + z.ancho / 2, 0.4, z.y + z.fondo / 2);
      this.escena!.add(objeto);

      this.zonas.push({ id: z.id, placa, etiqueta });
    }
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
      const nivel = o?.nivel ?? 'nd';
      z.placa.material.color.set(COLOR_NIVEL[nivel]);
      z.etiqueta.style.setProperty('--r3d-c', COLOR_NIVEL[nivel]);
      z.etiqueta.querySelector('small')!.textContent = o ? (o.etiqueta ?? `${o.personas}/${o.aforo}`) : '—';
    }
  }

  private resaltar(zonaId: number | null): void {
    for (const z of this.zonas) {
      z.placa.material.opacity = z.id === zonaId ? 0.95 : 0.72;
      z.etiqueta.classList.toggle('r3d-activa', z.id === zonaId);
    }
  }

  // ------------------------------------------------------------------ interacción

  /** Zona bajo el puntero, con un rayo desde la cámara (Raycaster). */
  private zonaEn(e: PointerEvent): number | null {
    const r = this.renderer!.domElement.getBoundingClientRect();
    this.puntero.set(((e.clientX - r.left) / r.width) * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1);
    this.raycaster.setFromCamera(this.puntero, this.camara!);
    const golpe = this.raycaster.intersectObjects(this.zonas.map(z => z.placa), false)[0];
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
    // Sobre una zona se avisa en cada movimiento (el tooltip sigue al cursor); fuera, solo una vez.
    if (id != null) this.zonaHover.emit({ zonaId: id, clientX: e.clientX, clientY: e.clientY });
    else if (cambio) this.zonaHover.emit(null);
  };

  private alSalirPuntero = () => {
    this.zonaBajoCursor = null;
    this.resaltar(null);
    this.zonaHover.emit(null);
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
  }

  // ------------------------------------------------------------------ limpieza

  /** Libera la memoria de la GPU y los listeners al salir de la pantalla. */
  ngOnDestroy(): void {
    this.observador?.disconnect();
    this.renderer?.setAnimationLoop(null);
    const lienzo = this.renderer?.domElement;
    lienzo?.removeEventListener('pointermove', this.alMoverPuntero);
    lienzo?.removeEventListener('pointerleave', this.alSalirPuntero);
    lienzo?.removeEventListener('pointerdown', this.alPresionar);
    lienzo?.removeEventListener('pointerup', this.alSoltar);
    this.controles?.dispose();
    this.escena?.traverse(obj => {
      if (obj instanceof THREE.Mesh) {
        obj.geometry.dispose();
        (Array.isArray(obj.material) ? obj.material : [obj.material]).forEach(m => m.dispose());
      }
      if (obj instanceof CSS2DObject) obj.element.remove();
    });
    this.textura?.dispose();
    this.renderer?.dispose();
    lienzo?.remove();
    this.etiquetas?.domElement.remove();
    this.zonas = [];
  }
}
