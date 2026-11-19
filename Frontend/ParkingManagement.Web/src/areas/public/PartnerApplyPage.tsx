import { ArrowLeft, ArrowRight } from 'lucide-react';
import { useState, type FormEvent, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { AuthLayout } from '@/components/auth/AuthLayout';
import {
  BUSINESS_TYPES,
  STEPS,
  emptyForm,
  stepErrors,
  validate,
  type PartnerApplyErrors,
  type PartnerApplyField,
  type PartnerApplyForm,
} from './partnerApplyForm';
import './partnerApply.css';

const HOTLINE = '1900 6868';

/** 429 từ AdminService không có ProblemDetails → câu riêng; lỗi khác (400 / 409 / mạng) hiện nguyên văn message của server. */
const submitErrorMessage = (err: unknown) => {
  const status = (err as { status?: number })?.status;
  if (status === 429) return `Bạn đã gửi quá nhiều hồ sơ trong một giờ. Vui lòng thử lại sau hoặc gọi ${HOTLINE}.`;
  return err instanceof Error ? err.message : 'Không gửi được hồ sơ. Vui lòng thử lại.';
};

const DEFAULT_INTROS = [
  'Gửi thông tin để ParkMaster thẩm định. Chưa cần tạo tài khoản – chúng tôi gửi link kích hoạt khi hồ sơ được duyệt.',
  'Thông tin pháp lý in vào hợp đồng hợp tác của từng bãi.',
  'Chỉ để ParkMaster đánh giá sơ bộ – bạn khai báo từng bãi và nộp hồ sơ KYB sau khi có tài khoản.',
  'Kiểm tra lại thông tin trước khi gửi.',
];

interface PartnerApplyWizardProps {
  heading: string;
  /** Dữ liệu điền sẵn (bổ sung hồ sơ – T4); mặc định form trống. */
  initial?: PartnerApplyForm;
  /** Nội dung hiện trên cùng (ví dụ yêu cầu bổ sung của ParkMaster). */
  notice?: ReactNode;
  /** Câu giới thiệu theo từng bước (4 phần tử). */
  intros?: string[];
  submitLabel?: string;
  /** Gửi hồ sơ, trả mã hồ sơ để hiện màn hoàn tất. */
  onSubmit: (form: PartnerApplyForm) => Promise<string>;
  renderDone: (code: string) => ReactNode;
}

/** Wizard 4 bước dùng chung cho nộp hồ sơ mới (T1) và bổ sung hồ sơ qua link (T4). */
export function PartnerApplyWizard({ heading, initial, notice, intros = DEFAULT_INTROS, submitLabel = 'Gửi hồ sơ', onSubmit, renderDone }: PartnerApplyWizardProps) {
  const [form, setForm] = useState<PartnerApplyForm>(() => initial ?? emptyForm());
  const [step, setStep] = useState(0);
  const [shownErrors, setShownErrors] = useState<PartnerApplyErrors>({});
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [doneCode, setDoneCode] = useState<string | null>(null);

  const last = STEPS.length - 1;

  const set = <K extends PartnerApplyField>(key: K, value: PartnerApplyForm[K]) => {
    setForm((prev) => ({ ...prev, [key]: value }));
    // Sửa ô đang báo lỗi → kiểm tra lại ngay ô đó.
    if (shownErrors[key]) setShownErrors((prev) => ({ ...prev, [key]: validate({ ...form, [key]: value })[key] }));
  };

  const goTo = (target: number) => {
    setError('');
    setShownErrors({});
    setStep(target);
  };

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy) return;
    setError('');
    const errors = step === last ? validate(form) : stepErrors(form, step);
    setShownErrors(errors);
    if (Object.keys(errors).length > 0) {
      const firstStep = STEPS.findIndex((s) => s.fields.some((f) => errors[f]));
      if (firstStep !== -1 && firstStep !== step) setStep(firstStep);
      return;
    }
    if (step < last) {
      goTo(step + 1);
      return;
    }
    setBusy(true);
    try {
      setDoneCode(await onSubmit(form));
    } catch (err) {
      setError(submitErrorMessage(err));
    } finally {
      setBusy(false);
    }
  }

  const fieldError = (key: PartnerApplyField) =>
    shownErrors[key] ? (
      <span id={`partner-${key}-error`} className="auth-field-error">
        {shownErrors[key]}
      </span>
    ) : null;

  const input = (
    key: PartnerApplyField,
    label: string,
    opts: { optional?: boolean; type?: string; maxLength?: number; placeholder?: string; autoComplete?: string; inputMode?: 'numeric' | 'tel' | 'email' } = {}
  ) => (
    <label className="auth-field" htmlFor={`partner-${key}`}>
      <span>
        {label}
        {opts.optional && <span className="partner-apply-optional"> (không bắt buộc)</span>}
      </span>
      <input
        id={`partner-${key}`}
        type={opts.type ?? 'text'}
        value={String(form[key])}
        onChange={(e) => set(key, e.target.value as never)}
        maxLength={opts.maxLength}
        placeholder={opts.placeholder}
        autoComplete={opts.autoComplete}
        inputMode={opts.inputMode}
        aria-invalid={!!shownErrors[key]}
        aria-describedby={shownErrors[key] ? `partner-${key}-error` : undefined}
        disabled={busy}
      />
      {fieldError(key)}
    </label>
  );

  if (doneCode) {
    return (
      <AuthLayout>
        <div className="partner-apply-card" style={{ textAlign: 'center' }}>
          {renderDone(doneCode)}
        </div>
      </AuthLayout>
    );
  }

  const reviewGroup = (title: string, target: number, rows: Array<[string, ReactNode]>) => (
    <>
      <dt className="partner-apply-review-title">
        {title}
        <button type="button" onClick={() => goTo(target)} disabled={busy}>
          Sửa
        </button>
      </dt>
      {rows.map(([label, value]) => (
        <div key={label} style={{ display: 'contents' }}>
          <dt>{label}</dt>
          <dd>{value || '—'}</dd>
        </div>
      ))}
    </>
  );

  return (
    <AuthLayout>
      <div className="partner-apply-card">
        <ol className="auth-steps" aria-label="Các bước đăng ký đối tác">
          {STEPS.map((s, i) => (
            <li key={s.label} className={i < step ? 'is-done' : i === step ? 'is-current' : ''} aria-current={i === step ? 'step' : undefined}>
              {i + 1} {s.label}
            </li>
          ))}
        </ol>
        <h1 className="auth-heading">{heading}</h1>
        <p className="auth-description">{intros[step]}</p>
        {notice}

        <form className="auth-form" onSubmit={handleSubmit} noValidate>
          {step === 0 && (
            <>
              <div className="partner-apply-row">
                {input('fullName', 'Họ và tên người liên hệ', { maxLength: 200, autoComplete: 'name', placeholder: 'Trần Văn Minh' })}
                {input('position', 'Chức vụ', { optional: true, maxLength: 100, autoComplete: 'organization-title', placeholder: 'Giám đốc' })}
              </div>
              {input('email', 'Email', { type: 'email', maxLength: 254, autoComplete: 'email', inputMode: 'email', placeholder: 'ban@congty.vn' })}
              {input('phone', 'Số điện thoại', { type: 'tel', maxLength: 20, autoComplete: 'tel', inputMode: 'tel', placeholder: '0901 234 567' })}
            </>
          )}

          {step === 1 && (
            <>
              <div className="auth-field" role="group" aria-labelledby="partner-businessType-label">
                <span id="partner-businessType-label">Loại hình kinh doanh</span>
                <div className="partner-apply-choice" role="radiogroup" aria-labelledby="partner-businessType-label">
                  {BUSINESS_TYPES.map((t) => (
                    <button key={t.value} type="button" role="radio" aria-checked={form.businessType === t.value} onClick={() => set('businessType', t.value)} disabled={busy}>
                      {t.label}
                    </button>
                  ))}
                </div>
                {fieldError('businessType')}
              </div>
              {input('businessName', form.businessType === 'HouseholdBusiness' ? 'Tên hộ kinh doanh' : 'Tên doanh nghiệp', {
                maxLength: 300,
                autoComplete: 'organization',
              })}
              {input('taxCode', 'Mã số thuế', { maxLength: 20, inputMode: 'numeric', placeholder: '0312345678' })}
              {input('businessAddress', 'Địa chỉ đăng ký kinh doanh', { maxLength: 300, autoComplete: 'street-address' })}
              <div className="partner-apply-row">
                {input('legalRepresentativeName', 'Người đại diện theo pháp luật', { maxLength: 200 })}
                {input('legalRepresentativePosition', 'Chức vụ', { optional: true, maxLength: 100, placeholder: 'Giám đốc' })}
              </div>
            </>
          )}

          {step === 2 && (
            <>
              {input('lotCount', 'Số bãi dự kiến', { type: 'number', inputMode: 'numeric' })}
              {input('primaryLotAddress', 'Khu vực / địa chỉ bãi chính', { maxLength: 300, placeholder: 'Quận 1, TP.HCM hoặc địa chỉ cụ thể' })}
              <div className="partner-apply-row">
                {input('estimatedCarSlots', 'Tổng chỗ ô tô', { optional: true, type: 'number', inputMode: 'numeric' })}
                {input('estimatedMotorbikeSlots', 'Tổng chỗ xe máy', { optional: true, type: 'number', inputMode: 'numeric' })}
              </div>
            </>
          )}

          {step === 3 && (
            <>
              <dl className="partner-apply-review">
                {reviewGroup('Liên hệ', 0, [
                  ['Họ và tên', form.fullName.trim()],
                  ['Chức vụ', form.position.trim()],
                  ['Email', form.email.trim()],
                  ['Số điện thoại', form.phone.trim()],
                ])}
                {reviewGroup('Doanh nghiệp', 1, [
                  ['Loại hình', BUSINESS_TYPES.find((t) => t.value === form.businessType)?.label],
                  ['Tên', form.businessName.trim()],
                  ['Mã số thuế', form.taxCode.trim()],
                  ['Địa chỉ', form.businessAddress.trim()],
                  ['Người đại diện', [form.legalRepresentativeName.trim(), form.legalRepresentativePosition.trim()].filter(Boolean).join(' – ')],
                ])}
                {reviewGroup('Quy mô dự kiến', 2, [
                  ['Số bãi', form.lotCount],
                  ['Khu vực', form.primaryLotAddress.trim()],
                  ['Ô tô / xe máy', [form.estimatedCarSlots || '—', form.estimatedMotorbikeSlots || '—'].join(' / ')],
                ])}
              </dl>
              <div>
                <label className="partner-apply-consent" htmlFor="partner-consentAccepted">
                  <input
                    id="partner-consentAccepted"
                    type="checkbox"
                    checked={form.consentAccepted}
                    onChange={(e) => set('consentAccepted', e.target.checked)}
                    aria-invalid={!!shownErrors.consentAccepted}
                    aria-describedby={shownErrors.consentAccepted ? 'partner-consentAccepted-error' : undefined}
                    disabled={busy}
                  />
                  <span>
                    Tôi đồng ý để ParkMaster thu thập và xử lý thông tin trên nhằm thẩm định hồ sơ hợp tác, theo Nghị định 13/2023/NĐ-CP về
                    bảo vệ dữ liệu cá nhân.
                  </span>
                </label>
                {fieldError('consentAccepted')}
              </div>
            </>
          )}

          {error && (
            <div role="alert" className="auth-alert auth-alert-error">
              {error}
            </div>
          )}

          <div className="partner-apply-actions">
            {step > 0 && (
              <button type="button" className="partner-apply-back" onClick={() => goTo(step - 1)} disabled={busy}>
                <ArrowLeft size={16} /> Quay lại
              </button>
            )}
            <button className="auth-submit" type="submit" disabled={busy}>
              {busy ? 'Đang gửi hồ sơ…' : step === last ? submitLabel : 'Tiếp tục'}
              {!busy && <ArrowRight size={17} />}
            </button>
          </div>
        </form>
        <p className="auth-switch">
          Đã có tài khoản chủ bãi? <Link to="/login">Đăng nhập</Link>
        </p>
      </div>
    </AuthLayout>
  );
}
