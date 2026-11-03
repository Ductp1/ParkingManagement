// Xóa các database pm_e2e_* để lần chạy E2E sau bắt đầu từ dữ liệu seed sạch (CP-0001 chưa phản hồi, ID khớp DemoIds).
// Chỉ đụng database có tiền tố pm_e2e_ – không bao giờ xóa DB dev pm_*. Phải dừng E2E stack trước khi chạy.
import { readFileSync, existsSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import pg from 'pg';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../../..');
const envFile = resolve(root, '.env');
if (existsSync(envFile)) {
  for (const line of readFileSync(envFile, 'utf8').split(/\r?\n/)) {
    const m = line.match(/^\s*([^#=\s]+)\s*=\s*(.*)\s*$/);
    if (m && !(m[1] in process.env)) process.env[m[1]] = m[2];
  }
}

const client = new pg.Client({
  host: process.env.DB_HOST ?? 'localhost',
  port: Number(process.env.DB_PORT ?? 5432),
  user: process.env.DB_USER ?? 'postgres',
  password: process.env.DB_PASSWORD ?? 'postgres',
  database: 'postgres',
});
await client.connect();
try {
  const { rows } = await client.query("SELECT datname FROM pg_database WHERE datname LIKE 'pm\\_e2e\\_%'");
  for (const { datname } of rows) {
    await client.query(`DROP DATABASE "${datname}" WITH (FORCE)`);
    console.log(`Đã xóa ${datname}`);
  }
  if (rows.length === 0) console.log('Không có database pm_e2e_* nào.');
} finally {
  await client.end();
}
