#!/usr/bin/env node
// T-904: tổng hợp bug hằng tuần từ bug-register.csv, không cần cài thêm gói nào.
//   node Tests/bugs/weekly-report.mjs                 # tuần hiện tại, ghi reports/<năm>-W<tuần>.md
//   node Tests/bugs/weekly-report.mjs --date 2026-11-07
//   node Tests/bugs/weekly-report.mjs --stdout        # chỉ in ra màn hình
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const HERE = dirname(fileURLToPath(import.meta.url));
export const SEVERITIES = ['P0', 'P1', 'P2', 'P3'];
export const STATUSES = ['Open', 'InProgress', 'Fixed', 'Closed', 'WontFix'];
const REQUIRED = ['id', 'title', 'severity', 'status', 'owner', 'service', 'found_on'];

/** Đọc CSV theo RFC 4180: hỗ trợ dấu phẩy, xuống dòng và "" trong ô có ngoặc kép; bỏ BOM do Excel thêm. */
export function parseCsv(text) {
  const rows = [];
  let row = [];
  let cell = '';
  let quoted = false;
  const src = text.replace(/^\uFEFF/, '');
  for (let i = 0; i < src.length; i++) {
    const ch = src[i];
    if (quoted) {
      if (ch === '"' && src[i + 1] === '"') { cell += '"'; i++; }
      else if (ch === '"') quoted = false;
      else cell += ch;
    } else if (ch === '"') quoted = true;
    else if (ch === ',') { row.push(cell); cell = ''; }
    else if (ch === '\n') { row.push(cell); rows.push(row); row = []; cell = ''; }
    else if (ch !== '\r') cell += ch;
  }
  if (cell !== '' || row.length > 0) { row.push(cell); rows.push(row); }
  const [header, ...data] = rows.filter((r) => r.some((c) => c.trim() !== ''));
  if (!header) return [];
  return data.map((r) => Object.fromEntries(header.map((h, i) => [h.trim(), (r[i] ?? '').trim()])));
}

/** Báo lỗi nhập liệu rõ ràng thay vì âm thầm cho ra số sai. Trả về danh sách lỗi, rỗng = hợp lệ. */
export function validate(bugs) {
  const errors = [];
  const seen = new Set();
  const isDate = (v) => /^\d{4}-\d{2}-\d{2}$/.test(v) && !Number.isNaN(Date.parse(v));
  bugs.forEach((b, i) => {
    const at = b.id || `dòng ${i + 2}`;
    for (const f of REQUIRED) if (!b[f]) errors.push(`${at}: thiếu ${f}`);
    if (b.id && seen.has(b.id)) errors.push(`${at}: trùng id`);
    seen.add(b.id);
    if (b.severity && !SEVERITIES.includes(b.severity)) errors.push(`${at}: severity "${b.severity}" phải là ${SEVERITIES.join('/')}`);
    if (b.status && !STATUSES.includes(b.status)) errors.push(`${at}: status "${b.status}" phải là ${STATUSES.join('/')}`);
    if (b.found_on && !isDate(b.found_on)) errors.push(`${at}: found_on phải dạng YYYY-MM-DD`);
    if (b.fixed_on && !isDate(b.fixed_on)) errors.push(`${at}: fixed_on phải dạng YYYY-MM-DD`);
    if (['Fixed', 'Closed'].includes(b.status) && !b.fixed_on) errors.push(`${at}: status ${b.status} cần fixed_on`);
    if (b.fixed_on && b.found_on && b.fixed_on < b.found_on) errors.push(`${at}: fixed_on trước found_on`);
  });
  return errors;
}

const utc = (iso) => new Date(`${iso}T00:00:00Z`);
const isoDay = (d) => d.toISOString().slice(0, 10);

/** Tuần theo ISO 8601: thứ Hai → Chủ nhật; tuần 1 là tuần chứa thứ Năm đầu tiên của năm. */
export function isoWeek(date) {
  const monday = new Date(date);
  monday.setUTCDate(date.getUTCDate() - ((date.getUTCDay() + 6) % 7));
  const thursday = new Date(monday);
  thursday.setUTCDate(monday.getUTCDate() + 3);
  const yearStart = Date.UTC(thursday.getUTCFullYear(), 0, 1);
  const week = Math.ceil(((thursday - yearStart) / 86_400_000 + 1) / 7);
  const sunday = new Date(monday);
  sunday.setUTCDate(monday.getUTCDate() + 6);
  return { label: `${thursday.getUTCFullYear()}-W${String(week).padStart(2, '0')}`, from: isoDay(monday), to: isoDay(sunday) };
}

const isOpen = (b) => ['Open', 'InProgress'].includes(b.status);
const bySeverity = (a, b) => SEVERITIES.indexOf(a.severity) - SEVERITIES.indexOf(b.severity) || a.found_on.localeCompare(b.found_on);
const ageDays = (b, asOf) => Math.round((utc(asOf) - utc(b.found_on)) / 86_400_000);
const cellText = (v) => String(v ?? '').replace(/\|/g, '\\|').replace(/\s*\n\s*/g, ' ');

function bugTable(bugs, asOf) {
  if (bugs.length === 0) return '_Không có._\n';
  const rows = bugs.map((b) => `| ${b.id} | ${b.severity} | ${cellText(b.title)} | ${cellText(b.owner)} | ${cellText(b.service)} | ${b.status} | ${ageDays(b, asOf)} |`);
  return ['| Mã | Mức | Lỗi | Người xử lý | Service | Trạng thái | Tuổi (ngày) |', '|---|---|---|---|---|---|---|', ...rows, ''].join('\n');
}

/** Dựng báo cáo tuần chứa asOf. Bug có found_on sau asOf bị bỏ qua để báo cáo tuần cũ không đổi khi chạy lại. */
export function buildReport(allBugs, asOf) {
  const week = isoWeek(utc(asOf));
  const bugs = allBugs.filter((b) => b.found_on <= week.to);
  const statusAtWeekEnd = (b) => (b.fixed_on && b.fixed_on > week.to && ['Fixed', 'Closed'].includes(b.status) ? 'Open' : b.status);
  const known = bugs.map((b) => ({ ...b, status: statusAtWeekEnd(b) }));
  const open = known.filter(isOpen).sort(bySeverity);
  const blockers = open.filter((b) => ['P0', 'P1'].includes(b.severity));
  const opened = known.filter((b) => b.found_on >= week.from && b.found_on <= week.to).sort(bySeverity);
  const fixed = known.filter((b) => b.fixed_on && b.fixed_on >= week.from && b.fixed_on <= week.to).sort(bySeverity);
  const perOwner = new Map();
  for (const b of open) perOwner.set(b.owner, [...(perOwner.get(b.owner) ?? []), b]);
  const owners = [...perOwner.entries()].sort((x, y) => y[1].length - x[1].length || x[0].localeCompare(y[0]));

  return [
    `# Báo cáo bug tuần ${week.label} (${week.from} → ${week.to})`,
    '',
    `Sinh tự động từ \`bug-register.csv\`, số liệu tính đến ${asOf}.`,
    '',
    '## Tóm tắt',
    '',
    blockers.length > 0
      ? `**Chưa đủ điều kiện release:** còn ${blockers.length} lỗi P0/P1 chưa xử lý (Kế Hoạch Kiểm Thử v3 §3.2 yêu cầu 0).`
      : '**Không còn lỗi P0/P1 đang mở** (tiêu chí Zero Blocker Defect, Kế Hoạch Kiểm Thử v3 §3.2).',
    '',
    '| Chỉ số | Số lượng |',
    '|---|---|',
    `| Đang mở | ${open.length} |`,
    `| Mới phát hiện trong tuần | ${opened.length} |`,
    `| Đã sửa trong tuần | ${fixed.length} |`,
    `| Đã ghi nhận từ đầu | ${known.length} |`,
    '',
    '## Lỗi đang mở theo mức độ',
    '',
    `| ${SEVERITIES.join(' | ')} |`,
    `|${SEVERITIES.map(() => '---').join('|')}|`,
    `| ${SEVERITIES.map((s) => open.filter((b) => b.severity === s).length).join(' | ')} |`,
    '',
    '## Lỗi chặn release (P0/P1)',
    '',
    bugTable(blockers, asOf),
    '## Mới phát hiện trong tuần',
    '',
    bugTable(opened, asOf),
    '## Đã sửa trong tuần',
    '',
    bugTable(fixed, asOf),
    '## Việc còn lại theo người xử lý',
    '',
    owners.length === 0 ? '_Không có._\n' : [
      '| Người xử lý | Đang mở | Mã lỗi |',
      '|---|---|---|',
      ...owners.map(([owner, list]) => `| ${cellText(owner)} | ${list.length} | ${list.map((b) => `${b.id} (${b.severity})`).join(', ')} |`),
      '',
    ].join('\n'),
    '## Tất cả lỗi đang mở',
    '',
    bugTable(open, asOf),
  ].join('\n');
}

function main() {
  const args = process.argv.slice(2);
  const dateAt = args.indexOf('--date');
  const asOf = dateAt >= 0 ? args[dateAt + 1] : isoDay(new Date());
  if (!/^\d{4}-\d{2}-\d{2}$/.test(asOf ?? '')) { console.error('--date phải dạng YYYY-MM-DD'); process.exit(2); }

  const bugs = parseCsv(readFileSync(join(HERE, 'bug-register.csv'), 'utf8'));
  const errors = validate(bugs);
  if (errors.length > 0) {
    console.error(`bug-register.csv có ${errors.length} lỗi nhập liệu:\n- ${errors.join('\n- ')}`);
    process.exit(1);
  }
  const report = buildReport(bugs, asOf);
  if (args.includes('--stdout')) { console.log(report); return; }
  const dir = join(HERE, 'reports');
  mkdirSync(dir, { recursive: true });
  const file = join(dir, `${isoWeek(utc(asOf)).label}.md`);
  writeFileSync(file, report);
  console.log(`Đã ghi ${file}`);
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) main();
