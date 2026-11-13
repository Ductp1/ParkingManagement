// Chạy: node --test Tests/bugs/weekly-report.test.mjs
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import { buildReport, isoWeek, parseCsv, validate } from './weekly-report.mjs';

const bug = (over = {}) => ({
  id: 'BUG-1', title: 'Lỗi', severity: 'P2', status: 'Open', owner: 'TV3', service: 'BookingService', found_on: '2026-11-03', fixed_on: '', ...over,
});
const week = (iso) => isoWeek(new Date(`${iso}T00:00:00Z`));

test('CSV: dấu phẩy, xuống dòng, ngoặc kép trong ô và BOM của Excel', () => {
  const rows = parseCsv('﻿id,title\r\nA,"x, ""y""\nz"\r\nB,\r\n');
  assert.deepEqual(rows, [{ id: 'A', title: 'x, "y"\nz' }, { id: 'B', title: '' }]);
});

test('CSV: bỏ dòng trống, file rỗng không lỗi', () => {
  assert.deepEqual(parseCsv('id\nA\n\n'), [{ id: 'A' }]);
  assert.deepEqual(parseCsv(''), []);
});

test('Tuần ISO: thứ Hai đầu tuần, biên năm', () => {
  assert.deepEqual(week('2026-11-07'), { label: '2026-W45', from: '2026-11-02', to: '2026-11-08' });
  assert.equal(week('2026-11-08').label, '2026-W45');      // Chủ nhật vẫn thuộc tuần đó
  assert.equal(week('2026-11-09').label, '2026-W46');
  assert.equal(week('2021-01-03').label, '2020-W53');      // đầu năm thuộc tuần cuối năm trước
  assert.equal(week('2024-12-30').label, '2025-W01');      // cuối năm thuộc tuần đầu năm sau
});

test('Kiểm tra dữ liệu: bản ghi hợp lệ không có lỗi', () => assert.deepEqual(validate([bug()]), []));

test('Kiểm tra dữ liệu: báo đúng từng loại lỗi nhập', () => {
  const errors = validate([
    bug({ severity: 'Cao' }),
    bug({ id: 'BUG-2', status: 'Done' }),
    bug({ id: 'BUG-3', status: 'Fixed' }),
    bug({ id: 'BUG-4', found_on: '03/11/2026' }),
    bug({ id: 'BUG-5', status: 'Fixed', fixed_on: '2026-11-01' }),
    bug({ id: 'BUG-1' }),
    bug({ id: '', title: '' }),
  ]).join('\n');
  for (const part of ['BUG-1: severity', 'BUG-2: status', 'BUG-3: status Fixed cần fixed_on', 'BUG-4: found_on', 'BUG-5: fixed_on trước', 'BUG-1: trùng id', 'dòng 8: thiếu id']) {
    assert.ok(errors.includes(part), `thiếu: ${part}\n${errors}`);
  }
});

test('Báo cáo: có P0/P1 đang mở thì ghi chưa đủ điều kiện release', () => {
  const r = buildReport([bug({ severity: 'P1' })], '2026-11-07');
  assert.match(r, /Chưa đủ điều kiện release:\*\* còn 1 lỗi/);
});

test('Báo cáo: P0/P1 đã sửa và P2 đang mở thì không chặn release', () => {
  const r = buildReport([bug({ id: 'A', severity: 'P0', status: 'Fixed', fixed_on: '2026-11-04' }), bug({ id: 'B' })], '2026-11-07');
  assert.match(r, /Không còn lỗi P0\/P1 đang mở/);
  assert.match(r, /\| Đang mở \| 1 \|/);
  assert.match(r, /\| Đã sửa trong tuần \| 1 \|/);
});

test('Báo cáo: phân biệt mới trong tuần và lỗi cũ, tính tuổi lỗi', () => {
  const r = buildReport([bug({ id: 'OLD', found_on: '2026-10-20' }), bug({ id: 'NEW', found_on: '2026-11-05' })], '2026-11-07');
  assert.match(r, /\| Mới phát hiện trong tuần \| 1 \|/);
  assert.match(r, /\| Đã ghi nhận từ đầu \| 2 \|/);
  assert.match(r, /\| OLD \| P2 .*\| 18 \|/);
});

test('Báo cáo tuần cũ không đổi khi lỗi được sửa hoặc phát hiện về sau', () => {
  const r = buildReport([
    bug({ id: 'LATER-FIX', severity: 'P0', status: 'Closed', fixed_on: '2026-11-20' }),
    bug({ id: 'LATER-FOUND', found_on: '2026-11-15' }),
  ], '2026-11-07');
  assert.match(r, /còn 1 lỗi P0\/P1/);          // cuối tuần W45 lỗi này vẫn đang mở
  assert.doesNotMatch(r, /LATER-FOUND/);
  assert.match(r, /\| Đã sửa trong tuần \| 0 \|/);
});

test('Báo cáo: sắp theo mức độ, gom theo người, escape ký tự | trong tiêu đề', () => {
  const r = buildReport([
    bug({ id: 'LOW', severity: 'P3', owner: 'TV7' }),
    bug({ id: 'HIGH', severity: 'P0', owner: 'TV7', title: 'a | b' }),
  ], '2026-11-07');
  assert.ok(r.lastIndexOf('HIGH') > 0 && r.indexOf('| HIGH |') < r.indexOf('| LOW |'));
  assert.match(r, /\| TV7 \| 2 \| HIGH \(P0\), LOW \(P3\) \|/);
  assert.match(r, /a \\\| b/);
});

test('Sổ bug thật của nhóm hợp lệ', () => {
  const rows = parseCsv(readFileSync(new URL('./bug-register.csv', import.meta.url), 'utf8'));
  assert.deepEqual(validate(rows), []);
  assert.ok(rows.length > 0);
});
