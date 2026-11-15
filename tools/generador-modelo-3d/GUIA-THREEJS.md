# Cargar el modelo final en Angular + Three.js

El archivo `clinica-piso1-final.glb` trae materiales con textura y oclusión
ambiental horneada (sombras de contacto). La geometría está comprimida con
meshopt, así que el `GLTFLoader` necesita el decodificador (viene con Three.js;
no hay archivos extra que copiar).

## Carga

```ts
import * as THREE from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { MeshoptDecoder } from 'three/examples/jsm/libs/meshopt_decoder.module.js';
import { RoomEnvironment } from 'three/examples/jsm/environments/RoomEnvironment.js';

// Renderizador
renderer.outputColorSpace = THREE.SRGBColorSpace;
renderer.toneMapping = THREE.ACESFilmicToneMapping;
renderer.toneMappingExposure = 0.68;
renderer.shadowMap.enabled = true;
renderer.shadowMap.type = THREE.PCFSoftShadowMap;

// Luz de ambiente suave (reflejos y relleno)
const pmrem = new THREE.PMREMGenerator(renderer);
scene.environment = pmrem.fromScene(new RoomEnvironment(), 0.04).texture;

// Luces. Desde Three.js r155 las intensidades son físicas: si tu versión es
// r155 o posterior, multiplica estas intensidades por Math.PI.
scene.add(new THREE.HemisphereLight(0xffffff, 0x9a9286, 0.25));
const sol = new THREE.DirectionalLight(0xfff3e2, 0.9);
sol.position.set(-14, 30, 16);
sol.castShadow = true;
sol.shadow.mapSize.set(4096, 4096);
Object.assign(sol.shadow.camera, { left: -28, right: 28, top: 18, bottom: -18, far: 80 });
sol.shadow.bias = -0.0004;
sol.shadow.normalBias = 0.03;
scene.add(sol);

const loader = new GLTFLoader();
loader.setMeshoptDecoder(MeshoptDecoder);
loader.load('models/clinica-piso1-final.glb', (gltf) => {
  gltf.scene.traverse((o) => {
    if (!(o instanceof THREE.Mesh)) return;
    const m = o.material as THREE.MeshStandardMaterial;
    m.envMapIntensity = 0.55;
    o.receiveShadow = true;
    o.castShadow = !/^ambiente|^piso|^losa|vidrio|cortina|zocalo/.test(o.name);
  });
  scene.add(gltf.scene);
});
```

## Colorear un ambiente

Los ambientes comparten el material del piso; clónalo antes de cambiar el color.
El color se multiplica por la textura y la sombra horneada se conserva.

```ts
const zona = gltf.scene.getObjectByName('ambiente_31') as THREE.Mesh;
zona.material = (zona.material as THREE.MeshStandardMaterial).clone();
(zona.material as THREE.MeshStandardMaterial).color.set('#c0392b');
```

## Cámara sugerida

Posición `(0, 50, 47)` mirando a `(0, 0, 1)`; el modelo está en metros, centrado
en el origen, con `y = 0` en el N.P.T. +0.75.
