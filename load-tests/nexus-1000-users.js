import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend, Rate } from 'k6/metrics';

const baseUrl = __ENV.NEXUS_BASE_URL || 'http://localhost:8080';
const durationTrend = new Trend('nexus_ready_duration_ms');
const failures = new Rate('nexus_request_failures');

export const options = {
  scenarios: {
    ramp_to_1000: {
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
    http_req_duration: ['p(95)<500'],
    nexus_request_failures: ['rate<0.01'],
  },
};

export default function () {
  const response = http.get(`${baseUrl}/health/ready`, { tags: { name: 'readiness' } });
  durationTrend.add(response.timings.duration);
  const ok = check(response, {
    'readiness status is 200': (r) => r.status === 200,
    'database reports connected': (r) => r.body && r.body.includes('connected'),
  });
  failures.add(!ok);
  sleep(0.5 + Math.random());
}
