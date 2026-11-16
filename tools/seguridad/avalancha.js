// Uso: docker run --rm --network host -v "$PWD/tools/seguridad":/scripts:ro -e BASE=http://127.0.0.1:5090 grafana/k6 run /scripts/avalancha.js
// Contra una API con sus límites POR DEFECTO (son los que se prueban) y los usuarios de demo (solo desarrollo).
// Prueba de carga: ¿un abusador puede tumbar la API? Corre contra la API con sus límites por defecto.
import http from 'k6/http';
import { check } from 'k6';
import { Counter, Trend } from 'k6/metrics';

const BASE = __ENV.BASE || 'http://127.0.0.1:5090';
const rechazos = new Counter('abusador_429');
const aceptadas = new Counter('abusador_200');
const latenciaNormal = new Trend('usuario_normal_ms', true);

export const options = {
  scenarios: {
    abusador: { executor: 'constant-vus', vus: 50, duration: '30s', exec: 'abusar' },
    normal: { executor: 'constant-arrival-rate', rate: 1, timeUnit: '1s', duration: '30s', preAllocatedVUs: 2, exec: 'trabajar' },
    sinSesion: { executor: 'constant-vus', vus: 10, duration: '30s', exec: 'adivinar' },
  },
  thresholds: {
    'checks{escenario:normal}': ['rate==1'],            // el usuario normal NUNCA es rechazado
    'usuario_normal_ms': ['p(95)<500'],                 // y sigue rápido
  },
};

function token(correo) {
  const r = http.post(`${BASE}/api/v1/auth/login`, JSON.stringify({ correo, clave: 'demo123' }), { headers: { 'Content-Type': 'application/json' } });
  return r.json('accessToken');
}

export function setup() { return { abusador: token('gerencia@creo.pe'), normal: token('operador@creo.pe') }; }

export function abusar(t) {
  const r = http.get(`${BASE}/api/v1/sedes`, { headers: { Authorization: `Bearer ${t.abusador}` }, tags: { escenario: 'abusador' } });
  if (r.status === 429) rechazos.add(1); else if (r.status === 200) aceptadas.add(1);
}

export function trabajar(t) {
  const r = http.get(`${BASE}/api/v1/sedes`, { headers: { Authorization: `Bearer ${t.normal}` }, tags: { escenario: 'normal' } });
  latenciaNormal.add(r.timings.duration);
  check(r, { 'usuario normal atendido (200)': x => x.status === 200 }, { escenario: 'normal' });
}

// Alguien sin cuenta probando contraseñas a toda velocidad
export function adivinar() {
  http.post(`${BASE}/api/v1/auth/login`, JSON.stringify({ correo: 'nadie@creo.pe', clave: 'adivinando' }),
    { headers: { 'Content-Type': 'application/json' }, tags: { escenario: 'sinSesion' } });
}
