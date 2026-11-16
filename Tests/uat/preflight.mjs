#!/usr/bin/env node
// T-904: kiểm tra trước buổi UAT – chạy các ca UAT có kênh API để biết ca nào sẵn sàng, ca nào sẽ bị chặn bởi lỗi đã biết.
// Tạo dữ liệu thật, nên chỉ chạy trên môi trường kiểm thử:
//   node Tests/uat/preflight.mjs                                   # mặc định http://localhost:5000
//   BASE_URL=http://localhost:5000 node Tests/uat/preflight.mjs
// Trả mã thoát 1 nếu có ca không đạt.
import { readFileSync } from 'node:fs';

const BASE_URL = (process.env.BASE_URL || 'http://localhost:5000').replace(/\/$/, '');
if (!/^https?:\/\/(localhost|127\.0\.0\.1)(:|\/|$)/.test(BASE_URL) && process.env.ALLOW_REMOTE !== 'true') {
  console.error(`BASE_URL ${BASE_URL} không phải máy local. Đặt ALLOW_REMOTE=true nếu thật sự muốn chạy.`);
  process.exit(2);
}
const demoIds = readFileSync(new URL('../../BuildingBlocks/ParkingManagement.SharedKernel/Contracts/DemoIds.cs', import.meta.url), 'utf8');
const PASSWORD = demoIds.match(/Password\s*=\s*"([^"]+)"/)?.[1];
if (!PASSWORD) { console.error('Không đọc được mật khẩu demo trong DemoIds.cs'); process.exit(2); }

const LOT = { id: 1, name: 'Bãi xe Vincom Đồng Khởi', ownerProfileId: 1 };
const USERS = { driver1: 5, driver2: 6, staff: 4 };
const CARS = { driver1: 1, driver2: 3 };

async function call(method, path, token, body) {
  const res = await fetch(`${BASE_URL}${path}`, {
    method,
    headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const text = await res.text();
  let json = null;
  try { json = text ? JSON.parse(text) : null; } catch { /* không phải JSON */ }
  return { status: res.status, body: json, text };
}

async function login(email) {
  const r = await call('POST', '/api/v1/auth/login', null, { emailOrPhone: email, password: PASSWORD });
  if (r.status !== 200) throw new Error(`Đăng nhập ${email} thất bại: HTTP ${r.status}`);
  return r.body.accessToken;
}

const n = (max, width) => String(Math.floor(Math.random() * max)).padStart(width, '0');
const plate = () => `51E-${n(1000, 3)}.${n(100, 2)}`;

function booking(token, { minutes = 120, user = 'driver1', amount = 50000, slotId = null, slotCode = null, plateNumber = plate(), hours = 2, approval = false } = {}) {
  const start = new Date(Date.now() + minutes * 60_000);
  return call('POST', '/api/v1/bookings', token, {
    userId: USERS[user], vehicleId: CARS[user], plateNumber, vehicleType: 'Sedan', parkingLotId: LOT.id, ownerProfileId: LOT.ownerProfileId,
    parkingLotName: LOT.name, zoneId: null, slotId, slotCode, allocationMode: slotId ? 'DedicatedSlot' : 'Dynamic',
    startAtUtc: start.toISOString(), endAtUtc: new Date(start.getTime() + hours * 3_600_000).toISOString(),
    totalAmount: amount, promotionCode: null, requiresOwnerApproval: approval,
  });
}

const results = [];
/** Mỗi ca tự kiểm tra và ném lỗi nếu sai; một ca lỗi không làm dừng các ca còn lại. */
async function uatCase(id, title, fn) {
  try {
    await fn();
    results.push({ id, title, ok: true });
    console.log(`ĐẠT        ${id}  ${title}`);
  } catch (e) {
    results.push({ id, title, ok: false, reason: e.message });
    console.log(`KHÔNG ĐẠT  ${id}  ${title}\n           → ${e.message}`);
  }
}
function expect(cond, message) { if (!cond) throw new Error(message); }
const statusIs = (r, want, what) => expect(r.status === want, `${what}: mong đợi HTTP ${want}, nhận ${r.status} ${r.text.slice(0, 160)}`);

const driver1 = await login('driver1@smartparking.vn');
const driver2 = await login('driver2@smartparking.vn');
const owner = await login('owner.vincom@smartparking.vn');
const staff = await login('staff.vincom@smartparking.vn');
const admin = await login('admin@smartparking.vn');

const created = async (opts, token = driver1) => {
  const r = await booking(token, opts);
  statusIs(r, 201, 'tạo booking');
  return r.body;
};
const confirmed = async (opts) => {
  const b = await created(opts);
  const r = await call('POST', `/api/v1/bookings/${b.code}/approve`, owner, { ownerProfileId: LOT.ownerProfileId });
  statusIs(r, 200, 'chủ bãi xác nhận');
  return b;
};
const checkIn = (token, b, parkingLotId = LOT.id) => call('POST', '/api/v1/gate/check-in', token,
  { parkingLotId, method: 'Qr', plate: b.plateNumber, qrToken: b.qrToken, staffUserId: USERS.staff });
const checkOut = (token, plateNumber) => call('POST', '/api/v1/gate/check-out', token,
  { parkingLotId: LOT.id, plate: plateNumber, method: 'Manual', staffUserId: USERS.staff, confirmFee: true });

await uatCase('UAT-DRV-05', 'Tạo booking giữ chỗ 15 phút', async () => {
  const b = await created({});
  expect(b.status === 'PendingPayment', `trạng thái ${b.status}`);
  const hold = (Date.parse(b.holdExpiresAtUtc) - Date.now()) / 60_000;
  expect(hold > 14 && hold <= 15.5, `giữ chỗ ${hold.toFixed(1)} phút`);
});
await uatCase('UAT-DRV-06', 'Đặt sát giờ dưới 15 phút bị từ chối', async () => {
  statusIs(await booking(driver1, { minutes: 10 }), 400, 'đặt sau 10 phút');
});
await uatCase('UAT-DRV-08', '2 người đặt cùng 1 slot cùng lúc: chỉ 1 người thành công', async () => {
  const slot = { slotId: 2, slotCode: 'B1-A02', minutes: 60 * 24 * 20 + Math.floor(Math.random() * 60 * 24 * 5) };
  const [a, b] = await Promise.all([booking(driver1, slot), booking(driver2, { ...slot, user: 'driver2' })]);
  const ok = [a, b].filter((r) => r.status === 201).length;
  expect(ok === 1, `${ok} booking thành công trên cùng slot (HTTP ${a.status}, ${b.status}), mong đợi đúng 1`);
});
await uatCase('UAT-DRV-16', 'Cùng tài xế, cùng khung giờ: booking thứ 2 bị từ chối', async () => {
  const opts = { minutes: 60 * 24 * 10 + Math.floor(Math.random() * 60 * 24 * 5) };
  await created(opts);
  const second = await booking(driver1, opts);
  expect(second.status !== 201, 'booking thứ 2 cùng khung giờ vẫn được tạo (HTTP 201)');
});
await uatCase('UAT-DRV-10', 'Hủy trước 61 phút: hoàn 100%', async () => {
  const b = await confirmed({ minutes: 61 + 1 });
  const p = await call('GET', `/api/v1/bookings/${b.code}/cancellation-preview`, driver1);
  statusIs(p, 200, 'xem trước hủy');
  expect(p.body.refundPercent === 100, `xem trước hoàn ${p.body.refundPercent}%`);
  const c = await call('POST', `/api/v1/bookings/${b.code}/cancel`, driver1, { userId: USERS.driver1, reason: 'preflight' });
  statusIs(c, 200, 'hủy');
  expect(c.body.refundPercent === 100, `hoàn ${c.body.refundPercent}%`);
});
await uatCase('UAT-DRV-11', 'Hủy trước 59 phút: hoàn 0%', async () => {
  const b = await confirmed({ minutes: 59 });
  const c = await call('POST', `/api/v1/bookings/${b.code}/cancel`, driver1, { userId: USERS.driver1, reason: 'preflight' });
  statusIs(c, 200, 'hủy');
  expect(c.body.refundPercent === 0, `hoàn ${c.body.refundPercent}%`);
});
await uatCase('UAT-OWN-01', 'Chủ bãi duyệt booking', async () => {
  const b = await created({ approval: true });
  const r = await call('POST', `/api/v1/bookings/${b.code}/approve`, owner, { ownerProfileId: LOT.ownerProfileId });
  statusIs(r, 200, 'duyệt');
  expect(r.body.status === 'Confirmed', `trạng thái ${r.body.status}`);
});
await uatCase('UAT-GAT-01', 'QR hợp lệ: vào cổng, booking sang CheckedIn', async () => {
  const b = await confirmed({ minutes: 20 });
  statusIs(await checkIn(staff, b), 200, 'check-in');
  const d = await call('GET', `/api/v1/bookings/${b.code}`, driver1);
  expect(d.body.status === 'CheckedIn', `trạng thái ${d.body.status}`);
  statusIs(await checkOut(staff, b.plateNumber), 200, 'dọn: check-out');
});
await uatCase('UAT-GAT-02', 'QR đã dùng / giả / của bãi khác bị từ chối', async () => {
  const b = await confirmed({ minutes: 20 });
  statusIs(await checkIn(staff, b), 200, 'check-in lần đầu');
  statusIs(await checkIn(staff, b), 409, 'QR đã dùng');
  statusIs(await checkIn(staff, { plateNumber: plate(), qrToken: 'forged.preflight.token' }), 400, 'QR giả');
  const other = await confirmed({ minutes: 20 });
  statusIs(await checkIn(staff, other, 2), 400, 'QR của bãi khác');
  statusIs(await checkOut(staff, b.plateNumber), 200, 'dọn: check-out');
});
await uatCase('UAT-GAT-04+05', 'Ra trong ân hạn: phí 0đ, booking Completed', async () => {
  const b = await confirmed({ minutes: 20 });
  statusIs(await checkIn(staff, b), 200, 'check-in');
  const q = await call('GET', `/api/v1/gate/exit-quote?parkingLotId=${LOT.id}&plate=${encodeURIComponent(b.plateNumber)}`, staff);
  statusIs(q, 200, 'báo phí');
  expect(q.body.isWithinBillingGrace === true && q.body.additionalDue === 0, `ân hạn=${q.body.isWithinBillingGrace}, phí=${q.body.additionalDue}`);
  statusIs(await checkOut(staff, b.plateNumber), 200, 'check-out');
  const d = await call('GET', `/api/v1/bookings/${b.code}`, driver1);
  expect(d.body.status === 'Completed', `trạng thái ${d.body.status}`);
});
await uatCase('UAT-GAT-06', 'Khách vãng lai: vào/ra, chặn trùng biển số', async () => {
  const p = plate();
  const walkIn = () => call('POST', '/api/v1/gate/check-in', staff, { parkingLotId: LOT.id, method: 'Manual', plate: p, staffUserId: USERS.staff });
  statusIs(await walkIn(), 200, 'vào lần đầu');
  statusIs(await walkIn(), 409, 'vào trùng');
  statusIs(await checkOut(staff, p), 200, 'ra');
});
await uatCase('UAT-SEC-01', 'Không token: tạo / duyệt booking, check-in đều 401', async () => {
  const b = await created({});
  const anon = await booking(null);
  const approve = await call('POST', `/api/v1/bookings/${b.code}/approve`, null, { ownerProfileId: LOT.ownerProfileId });
  const gate = await call('POST', '/api/v1/gate/check-in', null, { parkingLotId: LOT.id, method: 'Manual', plate: plate(), staffUserId: USERS.staff });
  const got = `tạo ${anon.status}, duyệt ${approve.status}, check-in ${gate.status}`;
  expect(anon.status === 401 && approve.status === 401 && gate.status === 401, got);
});
await uatCase('UAT-SEC-02', 'Tài xế gọi API Admin / chủ bãi: 403', async () => {
  statusIs(await call('GET', '/api/v1/admin/audit-logs', driver1), 403, 'audit log');
  statusIs(await call('GET', '/api/v1/complaints/disputes', driver1), 403, 'hàng chờ tranh chấp');
});
await uatCase('UAT-SEC-03', 'Tài xế khác không xem được dữ liệu của driver1', async () => {
  const b = await created({});
  const detail = await call('GET', `/api/v1/bookings/${b.code}`, driver2);
  const list = await call('GET', `/api/v1/bookings?userId=${USERS.driver1}`, driver2);
  const complaints = await call('GET', `/api/v1/complaints/my?userId=${USERS.driver1}`, driver2);
  expect([403, 404].includes(detail.status), `chi tiết booking HTTP ${detail.status}`);
  expect([403, 404].includes(list.status), `danh sách booking HTTP ${list.status}`);
  expect([403, 404].includes(complaints.status), `khiếu nại HTTP ${complaints.status}`);
});
await uatCase('UAT-SEC-04', 'Không nhận giá do client gửi', async () => {
  const b = await created({ amount: 1 });
  expect(Number(b.totalAmount) !== 1, `booking lưu totalAmount = ${b.totalAmount} đúng như client gửi`);
});
await uatCase('UAT-ADM-01', 'Audit log xem được qua API', async () => {
  const r = await call('GET', '/api/v1/admin/audit-logs?page=1&pageSize=5', admin);
  statusIs(r, 200, 'audit log');
  expect(Array.isArray(r.body.items), 'không có items');
});

const failed = results.filter((r) => !r.ok);
console.log(`\nTổng: ${results.length} ca | Đạt: ${results.length - failed.length} | Không đạt: ${failed.length}`);
process.exit(failed.length ? 1 : 0);
