import * as THREE from 'three';
import { AmbienteMaqueta, MaquetaPiso, ambientePorRotulo, colorear, maquetaDe, objetosPorZona } from './maqueta';

const amb = (id: string, rotulo: string): AmbienteMaqueta => ({ id, rotulo_en_plano: rotulo, area_m2: 10, centro: [0, 0, 0] });
const AMBIENTES = [amb('ambiente_51', 'Sala de esperas'), amb('ambiente_50', 'Observación 01'), amb('ambiente_57', 'Observación 02')];

describe('maqueta 3D', () => {
  it('solo los pisos con maqueta la tienen; el resto muestra «en producción»', () => {
    expect(maquetaDe(1)?.glb).toBe('models/clinica-piso1-final.glb');
    expect(maquetaDe(2)).toBeNull();
  });

  it('busca un ambiente por su rótulo, sin importar mayúsculas ni espacios de más', () => {
    expect(ambientePorRotulo(AMBIENTES, 'Sala de esperas')?.id).toBe('ambiente_51');
    expect(ambientePorRotulo(AMBIENTES, '  sala  de ESPERAS ')?.id).toBe('ambiente_51');
    expect(ambientePorRotulo(AMBIENTES, 'Sala de espera')).toBeUndefined();
  });

  it('una zona puede pintarse sobre varios ambientes; los rótulos que no existen se avisan', () => {
    const maqueta: MaquetaPiso = { glb: 'x.glb', ambientes: 'x.json', zonas: { 3: ['Sala de esperas'], 8: ['Observación 01', 'Observación 02'], 9: ['Rayos Z'] } };
    const { porZona, faltantes } = objetosPorZona(maqueta, AMBIENTES);
    expect(porZona.get(3)).toEqual(['ambiente_51']);
    expect(porZona.get(8)).toEqual(['ambiente_50', 'ambiente_57']);
    expect(porZona.get(9)).toEqual([]);
    expect(faltantes).toEqual(['Rayos Z']);
  });

  describe('colorear', () => {
    // Dos ambientes que comparten el material del piso, como en el .glb
    const piso = () => new THREE.MeshStandardMaterial({ color: '#ffffff' });

    it('clona el material compartido una sola vez: pintar un ambiente no pinta los demás', () => {
      const comun = piso();
      const a = new THREE.Mesh(new THREE.BufferGeometry(), comun), b = new THREE.Mesh(new THREE.BufferGeometry(), comun);
      colorear(a, '#ff0000', 1, 0.2);
      const propio = a.material;
      colorear(a, '#00ff00', 1, 0.2);
      expect(a.material).toBe(propio);
      expect(b.material).toBe(comun);
      expect(comun.color.getHexString()).toBe('ffffff');
    });

    it('mezcla el color con el original y, con null, vuelve a como estaba', () => {
      const a = new THREE.Mesh(new THREE.BufferGeometry(), piso());
      colorear(a, '#000000', 0.5, 0.3);
      const m = a.material as THREE.MeshStandardMaterial;
      expect(m.color.r).toBeCloseTo(0.5);
      expect(m.emissiveIntensity).toBe(0.3);
      colorear(a, null, 0, 0);
      expect(m.color.getHexString()).toBe('ffffff');
      expect(m.emissiveIntensity).toBe(0);
    });
  });
});
