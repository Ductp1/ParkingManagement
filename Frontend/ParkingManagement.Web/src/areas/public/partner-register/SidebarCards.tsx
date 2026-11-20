import { Check, ChevronDown } from 'lucide-react';

/** Bốn bước sau khi gửi hồ sơ – đúng quy trình US-116 (thẩm định → ký hợp đồng → cấp tài khoản → kích hoạt bãi). */
const AFTER_SUBMIT = [
  { title: 'Admin thẩm định hồ sơ', note: '1–3 ngày làm việc' },
  { title: 'Ký hợp đồng hợp tác', note: 'Sau khi hồ sơ được duyệt' },
  { title: 'Nhận tài khoản chủ bãi qua email', note: 'Kiểm tra email đã đăng ký' },
  { title: 'Kích hoạt bãi và nhận đặt chỗ', note: 'Bắt đầu hợp tác cùng ParkMaster' },
];

const PREPARE = ['Giấy đăng ký kinh doanh', 'Giấy tờ pháp lý mặt bằng', 'CCCD người đại diện (2 mặt)'];

const SUPPORT_PHONE = '1900 6868';
const SUPPORT_EMAIL = 'partner@parkmaster.vn';

/** Timeline dọc; `activeFirst` = màn thành công (bước 1 đang diễn ra – chấm navy). */
export function AfterSubmitTimeline({ activeFirst, lastNote }: { activeFirst?: boolean; lastNote?: string }) {
  return (
    <ol className="flex flex-col">
      {AFTER_SUBMIT.map((item, i) => {
        const last = i === AFTER_SUBMIT.length - 1;
        return (
          <li key={item.title} className="relative flex gap-3 pb-4 last:pb-0" aria-current={activeFirst && i === 0 ? 'step' : undefined}>
            <span className="relative flex w-2 shrink-0 justify-center" aria-hidden="true">
              <span className={`mt-1.5 size-2 rounded-full ${activeFirst && i === 0 ? 'bg-(--admin-brand-navy-900)' : 'bg-(--admin-chart-neutral)'}`} />
              {!last && <span className="absolute top-4 bottom-[-14px] w-0.5 rounded-full bg-(--admin-chart-track)" />}
            </span>
            <span className="min-w-0">
              <span className="block text-[13px] font-semibold text-(--admin-text-primary)">
                {i + 1}&nbsp;&nbsp;{item.title}
              </span>
              <span className="block text-xs text-(--admin-text-secondary)">{last && lastNote ? lastNote : item.note}</span>
            </span>
          </li>
        );
      })}
    </ol>
  );
}

const card = 'rounded-xl border border-(--admin-border-default) bg-(--admin-surface-card) p-5';

export function AfterSubmitCard() {
  return (
    <section className={card} aria-labelledby="pr-after-title">
      <h2 id="pr-after-title" className="mb-4 text-sm font-semibold text-(--admin-text-primary)">
        Sau khi gửi hồ sơ
      </h2>
      <AfterSubmitTimeline />
    </section>
  );
}

export function PrepareCard() {
  return (
    <section className={card} aria-labelledby="pr-prepare-title">
      <h2 id="pr-prepare-title" className="mb-3 text-sm font-semibold text-(--admin-text-primary)">
        Chuẩn bị trước
      </h2>
      <ul className="flex flex-col gap-2">
        {PREPARE.map((item) => (
          <li key={item} className="flex items-center gap-2 text-xs text-(--admin-text-secondary)">
            <Check size={14} aria-hidden="true" className="shrink-0 text-(--admin-text-primary)" />
            {item}
          </li>
        ))}
      </ul>
    </section>
  );
}

/** Mobile (Figma 221:146): "Sau khi gửi hồ sơ" thu gọn thành accordion. */
export function AfterSubmitAccordion() {
  return (
    <details className="group rounded-xl border border-(--admin-border-default) bg-(--admin-surface-card)">
      <summary className="flex cursor-pointer list-none items-center justify-between rounded-xl px-5 py-4 text-sm font-semibold text-(--admin-text-primary) outline-none focus-visible:ring-2 focus-visible:ring-(--admin-brand-navy-900) [&::-webkit-details-marker]:hidden">
        Sau khi gửi hồ sơ
        <ChevronDown size={18} aria-hidden="true" className="transition-transform group-open:rotate-180" />
      </summary>
      <div className="px-5 pb-5">
        <AfterSubmitTimeline />
      </div>
    </details>
  );
}

export function SupportText({ className = '' }: { className?: string }) {
  const link = 'font-medium text-(--admin-text-primary) underline-offset-2 hover:underline focus-visible:underline outline-none';
  return (
    <p className={`text-xs text-(--admin-text-secondary) ${className}`}>
      Cần hỗ trợ? Gọi{' '}
      <a className={link} href={`tel:${SUPPORT_PHONE.replace(/\s/g, '')}`}>
        {SUPPORT_PHONE}
      </a>{' '}
      hoặc email{' '}
      <a className={link} href={`mailto:${SUPPORT_EMAIL}`}>
        {SUPPORT_EMAIL}
      </a>
    </p>
  );
}
