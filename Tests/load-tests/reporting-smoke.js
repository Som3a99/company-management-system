import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
    stages: [
        { duration: '30s', target: 20 },
        { duration: '1m', target: 80 },
        { duration: '30s', target: 0 },
    ],
    thresholds: {
        http_req_failed: ['rate<0.01'],
        http_req_duration: ['p(95)<800'],
    },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';

export default function () {
    const res = http.get(`${BASE_URL}/Reporting`);
    check(res, {
        'status is 200/302/401/403/429': (r) => [200, 302, 401, 403, 429].includes(r.status),
    });
    sleep(1);
}