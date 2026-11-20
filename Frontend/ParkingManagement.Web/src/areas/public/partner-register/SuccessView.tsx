import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { Check } from 'lucide-react';
import type { PartnerApplicationReceipt } from '@/api/partnerRegistrationApi';
import { maskEmail } from './registrationForm';
import { buttonClass } from './styles';
import { AfterSubmitTimeline, SupportText } from './SidebarCards';

/**
 * Figma 10 success. Chỉ có nút tới trang đã tồn tại ("Về trang chủ"); "Theo dõi hồ sơ" không làm vì khách vô danh
 * không có API tra cứu (D – ghi trong Redesign Notes). BE không gửi email xác nhận lúc nộp → câu chữ nói đúng điều sẽ xảy ra.
 */
export function SuccessView({ receipt, email }: { receipt: PartnerApplicationReceipt; email: string }) {
  const headingRef = useRef<HTMLHeadingElement>(null);
  const [copied, setCopied] = useState<'idle' | 'ok' | 'fail'>('idle');

  useEffect(() => {
    headingRef.current?.focus();
  }, []);

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(receipt.code);
      setCopied('ok');
    } catch {
      setCopied('fail');
    }
  };

  return (
    <section className="mx-auto w-full max-w-[720px] rounded-xl border border-(--admin-border-default) bg-(--admin-surface-card) p-6 md:p-8" aria-labelledby="pr-success-title">
      <span className="flex size-12 items-center justify-center rounded-full bg-(--admin-surface-selected) text-(--admin-brand-navy-900)" aria-hidden="true">
        <Check size={22} />
      </span>
      <h1 id="pr-success-title" ref={headingRef} tabIndex={-1} className="mt-5 text-2xl font-bold text-(--admin-text-primary) outline-none md:text-[28px]">
        Đã gửi hồ sơ thành công
      </h1>

      <div className="mt-5 flex flex-wrap items-center justify-between gap-3 rounded-lg bg-(--admin-surface-row-alt) px-4 py-3">
        <p className="text-sm font-semibold text-(--admin-text-primary)">
          Mã hồ sơ: <span className="font-mono">{receipt.code}</span>
        </p>
        <button type="button" className={`${buttonClass('ghost')} h-8 px-3 text-[13px]`} onClick={copy}>
          {copied === 'ok' ? 'Đã sao chép' : 'Sao chép'}
        </button>
      </div>
      <p className="sr-only" aria-live="polite">
        {copied === 'ok' ? `Đã sao chép mã hồ sơ ${receipt.code}` : copied === 'fail' ? 'Không sao chép được, vui lòng ghi lại mã hồ sơ.' : ''}
      </p>
      {copied === 'fail' && <p className="mt-2 text-xs text-(--admin-status-danger)">Không sao chép được, vui lòng ghi lại mã hồ sơ.</p>}

      <p className="mt-5 text-sm text-(--admin-text-secondary)">Kết quả thẩm định và tài khoản sẽ gửi về {maskEmail(email)}</p>

      <h2 className="mt-6 mb-4 text-base font-semibold text-(--admin-text-primary)">Sau khi gửi hồ sơ</h2>
      <AfterSubmitTimeline activeFirst lastNote="Bắt đầu nhận đặt chỗ trên ParkMaster" />

      <div className="mt-6 flex flex-wrap gap-3">
        <Link to="/" className={buttonClass('secondary')}>
          Về trang chủ
        </Link>
      </div>
      <SupportText className="mt-6" />
    </section>
  );
}
