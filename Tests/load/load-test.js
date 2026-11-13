// T-904: kiểm thử tải nhẹ bằng k6, đi qua API Gateway như client thật.
// Chạy qua scripts/run-load.ps1 (xem README.md). Biến môi trường: PROFILE, BASE_URL, DEMO_PASSWORD, RESULT_FILE.
import { check, group, sleep } from 'k6';
import http from 'k6/http';
import { Counter } from 'k6/metrics';

const PROFILE = __ENV.PROFILE || 'smoke';
const BASE_URL = (__ENV.BASE_URL || 'http://host.docker.internal:5000').replace(/\/$/, '');
const RESULT_FILE = __ENV.RESULT_FILE;

// Script tạo và hủy booking thật: chặn chạy nhầm vào môi trường dùng chung nếu không cố ý cho phép.
const host = BASE_URL.replace(/^https?:\/\//, '').split(/[:/]/)[0];
if (!['localhost', '127.0.0.1', 'host.docker.internal'].includes(host) && __ENV.ALLOW_REMOTE !== 'true') {
  throw new Error(`BASE_URL ${BASE_URL} không phải máy local. Đặt ALLOW_REMOTE=true nếu thật sự muốn chạy.`);
}
if (!__ENV.DEMO_PASSWORD) throw new Error('Thiếu DEMO_PASSWORD – hãy chạy qua scripts/run-load.ps1.');

// Dữ liệu demo khớp BuildingBlocks/.../DemoIds.cs (DB mới seed).
const LOT = { id: 1, name: 'Bãi xe Vincom Đồng Khởi', ownerProfileId: 1, lat: 10.7781, lng: 106.7019 };
const DRIVER_USER_ID = 5;
const DRIVER_CAR_ID = 1;
const STAFF_USER_ID = 4;
const ACCOUNTS = {
  driver: 'driver1@smartparking.vn',
  owner: 'owner.vincom@smartparking.vn',
  staff: 'staff.vincom@smartparking.vn',
  admin: 'admin@smartparking.vn',
};

const businessFailures = new Counter('business_flow_failures');

// Số người dùng ảo đồng thời theo từng nhóm. Tỷ lệ mô phỏng: đa số chỉ xem, ít người đặt chỗ / qua cổng / quản trị.
const LOADS = {
  light: { browse: 30, booking: 8, gate: 4, backoffice: 3, hold: '3m' },
  peak: { browse: 70, booking: 16, gate: 8, backoffice: 6, hold: '3m' },
};

function scenarios() {
  if (PROFILE === 'smoke') {
    const once = (exec, startTime) => ({ executor: 'per-vu-iterations', vus: 1, iterations: 1, maxDuration: '1m', exec, startTime });
    return { browse: once('browse', '0s'), booking: once('booking', '5s'), gate: once('gate', '10s'), backoffice: once('backoffice', '15s') };
  }
  const load = LOADS[PROFILE];
  if (!load) throw new Error(`PROFILE không hợp lệ: ${PROFILE} (smoke | light | peak)`);
  const ramp = (exec, target) => ({
    executor: 'ramping-vus',
    exec,
    startVUs: 0,
    stages: [{ duration: '1m', target }, { duration: load.hold, target }, { duration: '30s', target: 0 }],
    gracefulRampDown: '30s',
  });
  return {
    browse: ramp('browse', load.browse),
    booking: ramp('booking', load.booking),
    gate: ramp('gate', load.gate),
    backoffice: ramp('backoffice', load.backoffice),
  };
}

// Các API được đo. Khai báo threshold theo từng API để báo cáo có số liệu riêng, biết API nào chậm.
const MEASURED_APIS = [
  'GET /parking-lots/search', 'GET /parking-lots/{id}', 'GET /reviews', 'GET /faqs',
  'POST /bookings', 'GET /bookings/{code}', 'GET /bookings', 'GET /bookings/{code}/cancellation-preview', 'POST /bookings/{code}/cancel',
  'POST /bookings/{code}/approve', 'POST /gate/check-in', 'GET /gate/exit-quote', 'POST /gate/check-out',
  'GET /complaints/disputes', 'GET /complaints/payout-holds', 'GET /admin/settings', 'GET /admin/audit-logs',
];

// Chỉ tiêu theo "Kiến Trúc v3" §4.1 và "Kế Hoạch Kiểm Thử v3" §3.2. Vượt ngưỡng → k6 trả mã lỗi khác 0.
// Chỉ đo request của kịch bản (flow:app): đăng nhập ở setup chậm có chủ đích (bcrypt cost 12), không phải đối tượng đo.
// Smoke chỉ kiểm tra luồng chạy đúng: service vừa khởi động nên thời gian phản hồi của vài request đầu không có ý nghĩa.
const latencyThresholds = PROFILE === 'smoke' ? {} : {
  'http_req_duration{flow:app}': ['p(95)<500', 'p(99)<1000'],
  ...Object.fromEntries(MEASURED_APIS.map((name) => [`http_req_duration{name:${name}}`, ['p(95)<500']])),
};

export const options = {
  scenarios: scenarios(),
  thresholds: {
    ...latencyThresholds,
    'http_req_failed{flow:app}': ['rate<0.01'],
    checks: ['rate>0.99'],
    business_flow_failures: ['count==0'],
  },
  summaryTrendStats: ['avg', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
  systemTags: ['status', 'method', 'name', 'scenario', 'check', 'group'],
};

const json = (token) => ({ 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) });
const get = (url, name, token) => http.get(`${BASE_URL}${url}`, { headers: json(token), tags: { name, flow: 'app' } });
const post = (url, body, name, token) => http.post(`${BASE_URL}${url}`, JSON.stringify(body ?? {}), { headers: json(token), tags: { name, flow: 'app' } });
const think = (min = 1, max = 3) => { if (PROFILE !== 'smoke') sleep(min + Math.random() * (max - min)); };

/** Bước nghiệp vụ phải đúng; sai thì đếm vào business_flow_failures và in nội dung để điều tra. */
function expectOk(res, label, predicate = (r) => r.status >= 200 && r.status < 300) {
  const ok = check(res, { [label]: predicate });
  if (!ok) {
    businessFailures.add(1);
    console.error(`[${label}] HTTP ${res.status}: ${String(res.body).slice(0, 300)}`);
  }
  return ok;
}

/** Biển số ngẫu nhiên đúng định dạng VN để các VU không trùng lượt gửi xe đang mở. */
function randomPlate() {
  const n = (max, width) => String(Math.floor(Math.random() * max)).padStart(width, '0');
  return `51E-${n(1000, 3)}.${n(100, 2)}`;
}

function createBooking(token, startInMinutes) {
  const start = new Date(Date.now() + startInMinutes * 60_000);
  const end = new Date(start.getTime() + 2 * 3_600_000);
  return post('/api/v1/bookings', {
    userId: DRIVER_USER_ID,
    vehicleId: DRIVER_CAR_ID,
    plateNumber: randomPlate(),
    vehicleType: 'Sedan',
    parkingLotId: LOT.id,
    ownerProfileId: LOT.ownerProfileId,
    parkingLotName: LOT.name,
    zoneId: null,
    slotId: null,
    slotCode: null,
    allocationMode: 'Dynamic',
    startAtUtc: start.toISOString(),
    endAtUtc: end.toISOString(),
    totalAmount: 50000,
    promotionCode: null,
  }, 'POST /bookings', token);
}

/** Đăng nhập 1 lần cho mỗi vai trò rồi dùng chung token (API đăng nhập có rate limit, không phải đối tượng đo). */
export function setup() {
  const tokens = {};
  for (const [role, email] of Object.entries(ACCOUNTS)) {
    const res = http.post(`${BASE_URL}/api/v1/auth/login`, JSON.stringify({ emailOrPhone: email, password: __ENV.DEMO_PASSWORD }),
      { headers: json(), tags: { name: 'setup: login' } });
    if (res.status !== 200) throw new Error(`Đăng nhập ${role} thất bại: HTTP ${res.status} – đã chạy start-load-stack.ps1 chưa?`);
    tokens[role] = res.json('accessToken');
  }
  return tokens;
}

/** Tài xế xem: tìm bãi GPS → chi tiết bãi → đánh giá → FAQ. Không cần đăng nhập. */
export function browse() {
  group('browse', () => {
    const search = get(`/api/v1/parking-lots/search?lat=${LOT.lat}&lng=${LOT.lng}&radiusKm=5`, 'GET /parking-lots/search');
    expectOk(search, 'tìm bãi trả danh sách', (r) => r.status === 200 && r.json().length > 0);
    think();
    expectOk(get(`/api/v1/parking-lots/${LOT.id}`, 'GET /parking-lots/{id}'), 'chi tiết bãi');
    think();
    expectOk(get(`/api/v1/reviews?parkingLotId=${LOT.id}`, 'GET /reviews'), 'đánh giá của bãi');
    think();
    expectOk(get('/api/v1/faqs?audience=Driver', 'GET /faqs'), 'FAQ tài xế');
    think();
  });
}

/** Tài xế đặt chỗ: tạo → xem → danh sách của tôi → xem trước hủy → hủy (trả lại chỗ, dữ liệu không phình). */
export function booking(tokens) {
  group('booking', () => {
    // Đặt trước 1–7 ngày, tải trải đều nhiều khung giờ thay vì dồn vào một khung.
    const created = createBooking(tokens.driver, 60 * (24 + Math.floor(Math.random() * 144)));
    if (!expectOk(created, 'tạo booking 201, giữ chỗ', (r) => r.status === 201 && r.json('status') === 'PendingPayment')) return;
    const code = created.json('code');
    think();
    expectOk(get(`/api/v1/bookings/${code}`, 'GET /bookings/{code}', tokens.driver), 'xem booking');
    think();
    expectOk(get(`/api/v1/bookings?userId=${DRIVER_USER_ID}`, 'GET /bookings', tokens.driver), 'danh sách booking của tôi');
    think();
    expectOk(get(`/api/v1/bookings/${code}/cancellation-preview`, 'GET /bookings/{code}/cancellation-preview', tokens.driver), 'xem trước hủy');
    expectOk(post(`/api/v1/bookings/${code}/cancel`, { userId: DRIVER_USER_ID, reason: 'k6 load test' }, 'POST /bookings/{code}/cancel', tokens.driver), 'hủy booking');
    think();
  });
}

/** Vòng qua cổng: đặt → chủ bãi duyệt → QR vào → báo phí → ra; booking phải tự sang CheckedIn rồi Completed. */
export function gate(tokens) {
  group('gate', () => {
    const created = createBooking(tokens.driver, 20);
    if (!expectOk(created, 'tạo booking cho cổng', (r) => r.status === 201)) return;
    const { code, plateNumber: plate, qrToken } = created.json();
    if (!expectOk(post(`/api/v1/bookings/${code}/approve`, { ownerProfileId: LOT.ownerProfileId }, 'POST /bookings/{code}/approve', tokens.owner),
      'chủ bãi xác nhận', (r) => r.status === 200 && r.json('status') === 'Confirmed')) return;
    think();
    const checkIn = post('/api/v1/gate/check-in', { parkingLotId: LOT.id, method: 'Qr', plate, qrToken, staffUserId: STAFF_USER_ID },
      'POST /gate/check-in', tokens.staff);
    if (!expectOk(checkIn, 'check-in bằng QR')) return;
    expectOk(get(`/api/v1/bookings/${code}`, 'GET /bookings/{code}', tokens.driver), 'booking sang CheckedIn sau khi vào cổng',
      (r) => r.status === 200 && r.json('status') === 'CheckedIn');
    think();
    const encodedPlate = encodeURIComponent(plate);
    expectOk(get(`/api/v1/gate/exit-quote?parkingLotId=${LOT.id}&plate=${encodedPlate}`, 'GET /gate/exit-quote', tokens.staff), 'báo phí ở cổng ra');
    think();
    expectOk(post('/api/v1/gate/check-out', { parkingLotId: LOT.id, plate, method: 'Manual', staffUserId: STAFF_USER_ID, confirmFee: true },
      'POST /gate/check-out', tokens.staff), 'check-out');
    expectOk(get(`/api/v1/bookings/${code}`, 'GET /bookings/{code}', tokens.driver), 'booking sang Completed sau khi ra cổng',
      (r) => r.status === 200 && r.json('status') === 'Completed');
    think();
  });
}

/** Chủ bãi / Admin xem màn hình quản trị. */
export function backoffice(tokens) {
  group('backoffice', () => {
    expectOk(get('/api/v1/complaints/disputes', 'GET /complaints/disputes', tokens.owner), 'hàng chờ tranh chấp');
    think();
    expectOk(get('/api/v1/complaints/payout-holds', 'GET /complaints/payout-holds', tokens.owner), 'khoản đang giữ');
    think();
    expectOk(get('/api/v1/admin/settings', 'GET /admin/settings', tokens.admin), 'tham số hệ thống');
    think();
    expectOk(get('/api/v1/admin/audit-logs?page=1&pageSize=20', 'GET /admin/audit-logs', tokens.admin), 'audit log');
    think();
  });
}

/** Tóm tắt ngắn + ghi JSON đầy đủ để lập báo cáo. Số liệu từng API được lấy từ tag name. */
export function handleSummary(data) {
  const m = data.metrics;
  const ms = (v) => (v === undefined ? '—' : `${v.toFixed(0)}ms`);
  const d = (m['http_req_duration{flow:app}'] ?? m.http_req_duration)?.values ?? {};
  const failed = (m['http_req_failed{flow:app}'] ?? m.http_req_failed)?.values?.rate ?? 0;
  const lines = [
    `Profile: ${PROFILE} | Requests: ${m.http_reqs?.values?.count ?? 0} (${(m.http_reqs?.values?.rate ?? 0).toFixed(1)}/s) | VU tối đa: ${m.vus_max?.values?.max ?? 0}`,
    `Thời gian phản hồi: avg ${ms(d.avg)} | p95 ${ms(d['p(95)'])} | p99 ${ms(d['p(99)'])} | max ${ms(d.max)}`,
    `HTTP lỗi: ${(failed * 100).toFixed(2)}% | Check đạt: ${((m.checks?.values?.rate ?? 0) * 100).toFixed(2)}% | Lỗi nghiệp vụ: ${m.business_flow_failures?.values?.count ?? 0}`,
    ...Object.entries(m).flatMap(([name, metric]) => Object.entries(metric.thresholds ?? {})
      .map(([rule, result]) => `  ${result.ok ? 'ĐẠT ' : 'KHÔNG ĐẠT'}  ${name}: ${rule}`)),
    '',
  ];
  const out = { stdout: lines.join('\n') };
  if (RESULT_FILE) out[RESULT_FILE] = JSON.stringify(data, null, 2);
  return out;
}
