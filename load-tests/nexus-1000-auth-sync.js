import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend } from 'k6/metrics';

const baseUrl = __ENV.NEXUS_BASE_URL || 'http://localhost:8080';
const tokenFile = __ENV.NEXUS_TEST_TOKENS_FILE || '';
const tokens = tokenFile ? JSON.parse(open(tokenFile)) : (__ENV.NEXUS_TEST_TOKENS || '').split(',').map(t => t.trim()).filter(Boolean);
const failures = new Rate('nexus_auth_sync_failures');
const syncDuration = new Trend('nexus_sync_duration_ms');

export const options = {
  scenarios: {
    concurrent_sync_users: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '1m', target: 100 },
        { duration: '2m', target: 500 },
        { duration: '3m', target: 1000 },
        { duration: '5m', target: 1000 },
        { duration: '1m', target: 0 },
      ],
      gracefulRampDown: '30s',
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<750'],
    nexus_auth_sync_failures: ['rate<0.01'],
    nexus_sync_duration_ms: ['p(95)<750'],
  },
};

export function setup() {
  if (tokens.length < 1000) {
    throw new Error('Provide a JSON array of 1,000 distinct test session tokens using NEXUS_TEST_TOKENS_FILE. Do not use production tokens.');
  }
  return { tokens };
}

export default function (data) {
  const token = data.tokens[__VU - 1];
  const headers = { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' };
  const key = `load-test-user-${__VU}`;
  const getResponse = http.get(`${baseUrl}/api/sync`, { headers, tags: { name: 'sync_list' } });
  const getOk = check(getResponse, { 'sync list returns 200': r => r.status === 200 });
  failures.add(!getOk);

  const existing = getResponse.status === 200 ? (() => { try { return getResponse.json().find(x => x.key === key); } catch { return null; } })() : null;
  const expectedRevision = existing ? Number(existing.revision) : 0;
  const payload = JSON.stringify({ test: true, vu: __VU, iteration: __ITER, timestamp: new Date().toISOString() });
  const start = Date.now();
  const putResponse = http.put(`${baseUrl}/api/sync/${key}`, JSON.stringify({ payload, expectedRevision }), { headers, tags: { name: 'sync_write' } });
  syncDuration.add(Date.now() - start);
  const putOk = check(putResponse, { 'sync write accepted': r => r.status === 200 });
  failures.add(!putOk);
  sleep(0.5 + Math.random());
}
