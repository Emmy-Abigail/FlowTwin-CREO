import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { MeshoptDecoder } from 'three/addons/libs/meshopt_decoder.module.js';

/**
 * La maqueta 3D de un piso: el .glb que genera tools/generador-modelo-3d desde el plano y su lista
 * de ambientes. Los archivos NO están en el repositorio (son de la clínica): se copian a
 * public/models/ en cada equipo. Ver docs/GUIA-THREEJS.md.
 *
 * Una ZONA del sistema (de donde salen ocupación y aforo) se dibuja sobre uno o más AMBIENTES de la
 * maqueta. Se enlazan por el rótulo del plano y no por el número del ambiente, porque los números
 * cambian cada vez que se regenera el modelo.
 */
export interface MaquetaPiso {
  glb: string;
  ambientes: string;
  /** zonaId → rótulos de los ambientes donde se pinta (tal como están en el plano). */
  zonas: Record<number, string[]>;
}

/** Un ambiente de ambientes-<piso>.json. El id es el nombre del objeto dentro del .glb. */
export interface AmbienteMaqueta {
  id: string;
  rotulo_en_plano: string;
  area_m2: number;
  centro: [number, number, number];
}

/**
 * Pisos que ya tienen maqueta. Provisional: cuando el Administrador configure las zonas sobre la
 * maqueta, esta tabla pasará a la base de datos y se pedirá a la API.
 */
const MAQUETAS: Record<number, MaquetaPiso> = {
  1: {
    glb: 'models/clinica-piso1-final.glb',
    ambientes: 'models/ambientes-piso1.json',
    zonas: {
      1: ['Admisión / Caja'],
      2: ['Hall principal'],
      3: ['Sala de esperas'],
      4: ['Triaje'],
      5: ['Tópico de enfermería'],
      6: ['Tópico urgencia'],
      7: ['Módulo 2', 'Módulo 2 – sillón 1', 'Módulo 2 – sillón 2'],
      8: ['Observación 01', 'Observación 02'],
    },
  },
};

/** La maqueta del piso, o null si todavía no la hay (la pantalla lo dice así). */
export function maquetaDe(pisoId: number): MaquetaPiso | null {
  return MAQUETAS[pisoId] ?? null;
}

/** Busca un ambiente por su rótulo, sin distinguir mayúsculas ni espacios de más. */
export function ambientePorRotulo(ambientes: AmbienteMaqueta[], rotulo: string): AmbienteMaqueta | undefined {
  const clave = normalizar(rotulo);
  return ambientes.find(a => normalizar(a.rotulo_en_plano) === clave);
}

/**
 * Qué objetos del .glb pinta cada zona. Los rótulos que no existen en la maqueta se devuelven en
 * «faltantes» para avisar: si el modelo se regeneró y un rótulo cambió, se nota en vez de fallar callado.
 */
export function objetosPorZona(maqueta: MaquetaPiso, ambientes: AmbienteMaqueta[]): { porZona: Map<number, string[]>; faltantes: string[] } {
  const porZona = new Map<number, string[]>();
  const faltantes: string[] = [];
  for (const [zonaId, rotulos] of Object.entries(maqueta.zonas)) {
    const ids: string[] = [];
    for (const r of rotulos) {
      const a = ambientePorRotulo(ambientes, r);
      if (a) ids.push(a.id); else faltantes.push(r);
    }
    porZona.set(Number(zonaId), ids);
  }
  return { porZona, faltantes };
}

const normalizar = (s: string) => s.normalize('NFC').trim().replace(/\s+/g, ' ').toLocaleLowerCase('es');

// ------------------------------------------------------------------ carga

/**
 * Acabado al cargar. La maqueta trae la sombra de contacto horneada (aoMap) en todos los materiales;
 * a intensidad 1, sumada a las sombras en tiempo real, las esquinas se ven «quemadas». A la mitad
 * se conserva la profundidad sin ensuciar las paredes. Los reflejos del entorno dan el brillo del piso.
 */
const ACABADO = { oclusion: 0.6, reflejos: 0.7 };

/** Descarga el .glb (geometría comprimida con meshopt) y su lista de ambientes. */
export async function cargarMaqueta(maqueta: MaquetaPiso): Promise<{ escena: THREE.Group; ambientes: AmbienteMaqueta[] }> {
  const loader = new GLTFLoader();
  loader.setMeshoptDecoder(MeshoptDecoder);
  const [gltf, lista] = await Promise.all([
    loader.loadAsync(maqueta.glb),
    fetch(maqueta.ambientes).then(r => {
      if (!r.ok) throw new Error(`No se pudo leer ${maqueta.ambientes} (${r.status})`);
      return r.json() as Promise<{ ambientes: AmbienteMaqueta[] }>;
    }),
  ]);
  // El piso, los ambientes y lo transparente reciben sombra, no la proyectan (como en la guía)
  gltf.scene.traverse(o => {
    if (!(o instanceof THREE.Mesh)) return;
    for (const m of materiales(o)) {
      if (!(m instanceof THREE.MeshStandardMaterial)) continue;
      m.envMapIntensity = ACABADO.reflejos;
      m.aoMapIntensity = ACABADO.oclusion;
    }
    o.receiveShadow = true;
    o.castShadow = !/^ambiente|^piso|^losa|vidrio|cortina|zocalo/.test(o.name);
  });
  return { escena: gltf.scene, ambientes: lista.ambientes };
}

// ------------------------------------------------------------------ color

/**
 * Tiñe un ambiente. Los ambientes comparten el material del piso: se clona UNA vez (la primera)
 * para que pintar uno no pinte todos. El color se multiplica por la textura, así que se conservan
 * el porcelanato y la sombra horneada. «mezcla» = cuánto color (0 = el original, 1 = el color puro).
 */
export function colorear(malla: THREE.Mesh, color: THREE.ColorRepresentation | null, mezcla: number, brillo: number): void {
  if (!malla.userData['materialPropio']) {
    const original = malla.material as THREE.MeshStandardMaterial;
    malla.material = original.clone();
    malla.userData['materialPropio'] = true;
    malla.userData['colorOriginal'] = original.color.clone();
  }
  const m = malla.material as THREE.MeshStandardMaterial;
  const original = malla.userData['colorOriginal'] as THREE.Color;
  if (color === null) {
    m.color.copy(original);
    m.emissive.set(0x000000);
    m.emissiveIntensity = 0;
    return;
  }
  const c = new THREE.Color(color);
  m.color.copy(original).lerp(c, mezcla);
  m.emissive.copy(c);
  m.emissiveIntensity = brillo;
}

/** Libera de la GPU todo lo que cargó la maqueta (geometrías, materiales y sus texturas). */
export function liberar(raiz: THREE.Object3D): void {
  raiz.traverse(o => {
    if (!(o instanceof THREE.Mesh)) return;
    o.geometry.dispose();
    for (const m of materiales(o)) {
      for (const valor of Object.values(m)) if (valor instanceof THREE.Texture) valor.dispose();
      m.dispose();
    }
  });
}

const materiales = (o: THREE.Mesh): THREE.Material[] => (Array.isArray(o.material) ? o.material : [o.material]);
