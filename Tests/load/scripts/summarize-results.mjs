#!/usr/bin/env node
// T-904: chuyển file JSON do k6 ghi thành bảng Markdown để đưa vào báo cáo.
//   node Tests/load/scripts/summarize-results.mjs Tests/load/results/light-20261107-120000.json
import { readFileSync } from 'node:fs';

const file = process.argv[2];
if (!file) { console.error('Cách dùng: summarize-results.mjs <file json của k6>'); process.exit(2); }
const { metrics: m, state } = JSON.parse(readFileSync(file, 'utf8'));

const ms = (v) => (v === undefined ? '—' : `${Math.round(v)} ms`);
const verdict = (metric) => (Object.values(metric?.thresholds ?? {}).every((t) => t.ok) ? 'Đạt' : '**Không đạt**');
const all = m['http_req_duration{flow:app}'] ?? m.http_req_duration;
const failed = (m['http_req_failed{flow:app}'] ?? m.http_req_failed)?.values?.rate ?? 0;

const apis = Object.entries(m)
  .map(([name, metric]) => [name.match(/^http_req_duration\{name:(.+)\}$/)?.[1], metric])
  .filter(([name]) => name)
  .sort((a, b) => b[1].values['p(95)'] - a[1].values['p(95)']);

console.log(`| Chỉ số | Kết quả | Chỉ tiêu |
|---|---|---|
| Thời lượng | ${Math.round((state?.testRunDurationMs ?? 0) / 1000)} giây | |
| VU tối đa | ${m.vus_max?.values?.max ?? 0} | |
| Tổng request / thông lượng | ${m.http_reqs?.values?.count ?? 0} / ${(m.http_reqs?.values?.rate ?? 0).toFixed(1)} req/s | |
| p95 | ${ms(all?.values?.['p(95)'])} | < 500 ms |
| p99 | ${ms(all?.values?.['p(99)'])} | < 1000 ms |
| Tỷ lệ HTTP lỗi | ${(failed * 100).toFixed(2)}% | < 1% |
| Check đạt | ${((m.checks?.values?.rate ?? 0) * 100).toFixed(2)}% | > 99% |
| Lỗi nghiệp vụ | ${m.business_flow_failures?.values?.count ?? 0} | 0 |

| API (chậm nhất lên đầu) | Trung bình | p95 | p99 | Max | p95 < 500 ms |
|---|---|---|---|---|---|`);
for (const [name, metric] of apis) {
  const v = metric.values;
  console.log(`| \`${name}\` | ${ms(v.avg)} | ${ms(v['p(95)'])} | ${ms(v['p(99)'])} | ${ms(v.max)} | ${verdict(metric)} |`);
}
