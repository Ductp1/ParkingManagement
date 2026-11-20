import type { ReactNode } from 'react';
import { Lock } from 'lucide-react';
import { PARTNER_DOCUMENT_LIMITS as LIMITS, type PartnerDocumentFiles, type PartnerDocumentKind } from '@/api/partnerRegistrationApi';
import {
  allFiles,
  businessLabels,
  composeAddress,
  DOCUMENTS,
  maskEmail,
  representativeOf,
  usedMegabytes,
  type AddressParts,
  type FieldErrors,
  type RegistrationForm,
  type StepId,
} from './registrationForm';
import { Checkbox, Field, FieldMessage, Notice, NumberStepper, TextInput, Toggle } from './ui';
import { buttonClass, inputClass, messageId } from './styles';
import { UploadRow } from './UploadRow';

export interface StepProps {
  form: RegistrationForm;
  update: (patch: Partial<RegistrationForm>) => void;
  errors: FieldErrors;
  disabled?: boolean;
}

const REQUIRED_NOTE = <p className="text-xs text-(--admin-text-secondary)">Các mục có dấu * là bắt buộc.</p>;

// ---------- Bước 1 ----------

export function ApplicantStep({
  form,
  update,
  errors,
  disabled,
  signedInEmail,
  onSignOut,
  signingOut,
}: StepProps & { signedInEmail?: string; onSignOut?: () => void; signingOut?: boolean }) {
  return (
    <div className="flex flex-col gap-5">
      {signedInEmail && (
        <div className="flex flex-col items-start gap-2">
          <Notice tone="info" title={`Bạn đang đăng ký bằng tài khoản ${maskEmail(signedInEmail)}`}>
            Kết quả thẩm định và tài khoản sẽ gửi về email này
          </Notice>
          <button type="button" className={buttonClass('ghost')} onClick={onSignOut} disabled={disabled || signingOut}>
            {signingOut ? 'Đang đăng xuất…' : 'Không phải bạn? Đăng xuất'}
          </button>
        </div>
      )}
      <div className="grid gap-x-4 gap-y-5 sm:grid-cols-2">
        <TextInput
          id="pr-fullName"
          label="Họ và tên"
          required
          autoComplete="name"
          maxLength={200}
          placeholder="Nhập họ và tên"
          value={form.fullName}
          onChange={(e) => update({ fullName: e.target.value })}
          error={errors.fullName}
          disabled={disabled}
        />
        <TextInput
          id="pr-position"
          label="Chức vụ"
          autoComplete="organization-title"
          maxLength={100}
          placeholder="Giám đốc, quản lý…"
          value={form.position}
          onChange={(e) => update({ position: e.target.value })}
          error={errors.position}
          disabled={disabled}
        />
        <TextInput
          id="pr-email"
          label="Email"
          required
          type="email"
          autoComplete="email"
          maxLength={254}
          placeholder="Nhập email của Bạn"
          helper="Kết quả thẩm định và tài khoản sẽ gửi về email này"
          value={signedInEmail ? maskEmail(signedInEmail) : form.email}
          onChange={(e) => update({ email: e.target.value })}
          error={errors.email}
          disabled={disabled}
          readOnly={Boolean(signedInEmail)}
          locked={Boolean(signedInEmail)}
        />
        <TextInput
          id="pr-phone"
          label="Số điện thoại"
          required
          type="tel"
          autoComplete="tel"
          maxLength={20}
          placeholder="0901 234 567"
          value={form.phone}
          onChange={(e) => update({ phone: e.target.value })}
          error={errors.phone}
          disabled={disabled}
        />
      </div>
      {REQUIRED_NOTE}
    </div>
  );
}

// ---------- Nhóm địa chỉ (B: ghép thành một chuỗi khi gửi) ----------

function AddressFields({
  id,
  label,
  value,
  onChange,
  error,
  disabled,
}: {
  id: 'pr-businessAddress' | 'pr-lotAddress';
  label: string;
  value: AddressParts;
  onChange: (next: AddressParts) => void;
  error?: string;
  disabled?: boolean;
}) {
  const describedBy = error ? messageId(id) : undefined;
  const invalid = (part: keyof AddressParts) => Boolean(error) && (!value[part].trim() || composeAddress(value).length > 500);
  const input = (part: keyof AddressParts, inputId: string, placeholder: string, autoComplete: string) => (
    <input
      id={inputId}
      className={inputClass(invalid(part) ? error : null)}
      value={value[part]}
      placeholder={placeholder}
      autoComplete={autoComplete}
      maxLength={200}
      disabled={disabled}
      aria-required
      aria-invalid={invalid(part) || undefined}
      aria-describedby={describedBy}
      onChange={(e) => onChange({ ...value, [part]: e.target.value })}
    />
  );
  return (
    <fieldset className="flex min-w-0 flex-col gap-3">
      <legend className="mb-3 text-[13px] font-semibold text-(--admin-text-primary)">
        {label}
        <span className="text-(--admin-status-danger)" aria-hidden="true">
          {' '}
          *
        </span>
      </legend>
      <div className="grid gap-x-4 gap-y-3 sm:grid-cols-2">
        <Field id={id} label="Tỉnh/Thành phố" compact>
          {input('province', id, 'Nhập Tỉnh/Thành phố', 'address-level1')}
        </Field>
        <Field id={`${id}-ward`} label="Phường/Xã" compact>
          {input('ward', `${id}-ward`, 'Nhập Phường/Xã', 'address-level3')}
        </Field>
      </div>
      <Field id={`${id}-street`} label="Số nhà, tên đường" compact>
        {input('street', `${id}-street`, 'Nhập số nhà, tên đường', 'street-address')}
      </Field>
      <FieldMessage id={id} error={error} />
    </fieldset>
  );
}

// ---------- Bước 2 ----------

export function BusinessStep({ form, update, errors, disabled }: StepProps) {
  const labels = businessLabels(form.businessType);
  const rep = representativeOf(form);
  const segment = (value: RegistrationForm['businessType'], text: string) => (
    <label
      className={`flex h-9 cursor-pointer items-center justify-center rounded-md text-[13px] transition-colors has-focus-visible:ring-2 has-focus-visible:ring-(--admin-brand-navy-900) ${
        form.businessType === value
          ? 'border border-(--admin-border-default) bg-(--admin-surface-card) font-semibold text-(--admin-text-primary) shadow-sm'
          : 'text-(--admin-text-secondary) hover:text-(--admin-text-primary)'
      }`}
    >
      <input
        type="radio"
        name="pr-businessType"
        value={value}
        className="sr-only"
        checked={form.businessType === value}
        disabled={disabled}
        onChange={() => update({ businessType: value })}
      />
      {text}
    </label>
  );
  return (
    <div className="flex flex-col gap-5">
      <fieldset id="pr-businessType" tabIndex={-1} className="outline-none">
        <legend className="mb-1.5 text-[13px] font-semibold text-(--admin-text-primary)">
          Loại hình
          <span className="text-(--admin-status-danger)" aria-hidden="true">
            {' '}
            *
          </span>
        </legend>
        <div className="grid grid-cols-2 gap-1 rounded-lg border border-(--admin-border-default) bg-(--admin-surface-row-alt) p-1">
          {segment('Company', 'Doanh nghiệp')}
          {segment('HouseholdBusiness', 'Hộ kinh doanh')}
        </div>
        <FieldMessage id="pr-businessType" error={errors.businessType} />
      </fieldset>

      <TextInput
        id="pr-taxCode"
        label="Mã số thuế"
        required
        inputMode="numeric"
        maxLength={14}
        placeholder="Nhập mã số thuế"
        helper="10 hoặc 13 chữ số"
        value={form.taxCode}
        onChange={(e) => update({ taxCode: e.target.value.replace(/[^\d-]/g, '') })}
        error={errors.taxCode}
        disabled={disabled}
      />
      <TextInput
        id="pr-businessName"
        label={labels.name}
        required
        autoComplete="organization"
        maxLength={200}
        placeholder={`Nhập ${labels.name.toLowerCase()}`}
        value={form.businessName}
        onChange={(e) => update({ businessName: e.target.value })}
        error={errors.businessName}
        disabled={disabled}
      />
      <AddressFields
        id="pr-businessAddress"
        label="Địa chỉ trụ sở"
        value={form.businessAddress}
        onChange={(businessAddress) => update({ businessAddress })}
        error={errors.businessAddress}
        disabled={disabled}
      />

      <Checkbox id="pr-representativeIsMe" checked={form.representativeIsMe} onChange={(representativeIsMe) => update({ representativeIsMe })} disabled={disabled}>
        Người đại diện là tôi
      </Checkbox>

      <div className="grid gap-x-4 gap-y-5 sm:grid-cols-2">
        <TextInput
          id="pr-legalRepresentativeName"
          label={labels.representative}
          required
          maxLength={200}
          placeholder="Nhập họ và tên"
          value={rep.name}
          onChange={(e) => update({ legalRepresentativeName: e.target.value })}
          readOnly={form.representativeIsMe}
          error={errors.legalRepresentativeName}
          disabled={disabled}
        />
        <TextInput
          id="pr-legalRepresentativePosition"
          label="Chức vụ người đại diện"
          maxLength={100}
          placeholder="Giám đốc, chủ hộ…"
          value={rep.position}
          onChange={(e) => update({ legalRepresentativePosition: e.target.value })}
          readOnly={form.representativeIsMe}
          error={errors.legalRepresentativePosition}
          disabled={disabled}
        />
      </div>
      {REQUIRED_NOTE}
    </div>
  );
}

// ---------- Bước 3 ----------

export function LotStep({ form, update, errors, disabled }: StepProps) {
  const hoursDescribedBy = errors.operatingHours ? messageId('pr-operatingHours') : undefined;
  const timeInput = (id: string, label: string, key: 'openTime' | 'closeTime') => (
    <Field id={id} label={label} compact>
      <input
        id={id}
        type="time"
        className={inputClass(errors.operatingHours && !form[key] ? errors.operatingHours : null)}
        value={form.open24h ? '' : form[key]}
        disabled={disabled || form.open24h}
        aria-describedby={hoursDescribedBy}
        aria-invalid={(Boolean(errors.operatingHours) && !form[key]) || undefined}
        onChange={(e) => update({ [key]: e.target.value })}
      />
    </Field>
  );
  return (
    <div className="flex flex-col gap-5">
      <NumberStepper
        id="pr-lotCount"
        label="Số bãi"
        required
        min={1}
        max={100}
        value={form.lotCount}
        onChange={(lotCount) => update({ lotCount })}
        error={errors.lotCount}
        disabled={disabled}
      />
      <AddressFields
        id="pr-lotAddress"
        label="Địa chỉ bãi chính"
        value={form.lotAddress}
        onChange={(lotAddress) => update({ lotAddress })}
        error={errors.lotAddress}
        disabled={disabled}
      />

      <fieldset id="pr-operatingHours" tabIndex={-1} className="flex flex-col gap-3 outline-none">
        <legend className="mb-3 text-[13px] font-semibold text-(--admin-text-primary)">Giờ hoạt động</legend>
        <div className="grid gap-x-4 gap-y-3 sm:grid-cols-2">
          {timeInput('pr-openTime', 'Mở cửa', 'openTime')}
          {timeInput('pr-closeTime', 'Đóng cửa', 'closeTime')}
        </div>
        <Toggle id="pr-open24h" label="Mở cửa 24/7" checked={form.open24h} onChange={(open24h) => update({ open24h })} disabled={disabled} />
        <FieldMessage id="pr-operatingHours" error={errors.operatingHours} />
      </fieldset>

      <fieldset className="flex flex-col gap-3">
        <legend className="mb-3 text-[13px] font-semibold text-(--admin-text-primary)">Sức chứa ước tính</legend>
        <div className="grid gap-x-4 gap-y-3 sm:grid-cols-2">
          <TextInput
            id="pr-estimatedCarSlots"
            label="Ô tô"
            compact
            inputMode="numeric"
            maxLength={5}
            placeholder="0"
            suffix="chỗ"
            value={form.estimatedCarSlots}
            onChange={(e) => update({ estimatedCarSlots: e.target.value.replace(/\D/g, '') })}
            error={errors.estimatedCarSlots}
            disabled={disabled}
          />
          <TextInput
            id="pr-estimatedMotorbikeSlots"
            label="Xe máy"
            compact
            inputMode="numeric"
            maxLength={5}
            placeholder="0"
            suffix="chỗ"
            value={form.estimatedMotorbikeSlots}
            onChange={(e) => update({ estimatedMotorbikeSlots: e.target.value.replace(/\D/g, '') })}
            error={errors.estimatedMotorbikeSlots}
            disabled={disabled}
          />
        </div>
      </fieldset>

      <Notice tone="info" title="Bạn có thể thêm các bãi khác sau khi được cấp tài khoản">
        Chỉ cần khai báo bãi chính ở bước này.
      </Notice>
    </div>
  );
}

// ---------- Bước 4 ----------

export function DocumentsStep({
  files,
  errors,
  disabled,
  onPick,
  onRemove,
}: {
  files: PartnerDocumentFiles;
  errors: FieldErrors;
  disabled?: boolean;
  onPick: (kind: PartnerDocumentKind, picked: File[], replacing?: File) => void;
  onRemove: (kind: PartnerDocumentKind, file: File) => void;
}) {
  const list = allFiles(files);
  const bytes = list.reduce((s, f) => s + f.size, 0);
  const percent = Math.min(100, Math.round((bytes / LIMITS.maxTotalBytes) * 100));
  return (
    <div className="flex flex-col gap-4">
      <Notice tone="lock" title="Chỉ Admin ParkMaster xem được giấy tờ để thẩm định.">
        PDF, JPG hoặc PNG · tối đa 10 MB/file · tối đa {LIMITS.maxFiles} file (tổng 25 MB)
      </Notice>

      <div id="pr-documents" tabIndex={-1} className="outline-none">
        <p className="text-xs text-(--admin-text-secondary)" id="pr-usage-label">
          Đã dùng {usedMegabytes(bytes)} / 25 MB · {list.length}/{LIMITS.maxFiles} file
        </p>
        <div
          role="progressbar"
          aria-labelledby="pr-usage-label"
          aria-valuemin={0}
          aria-valuemax={100}
          aria-valuenow={percent}
          className="mt-1.5 h-1.5 overflow-hidden rounded-full bg-(--admin-chart-track)"
        >
          <div className="h-full rounded-full bg-(--admin-brand-navy-900) transition-[width]" style={{ width: `${percent}%` }} />
        </div>
        <FieldMessage id="pr-documents" error={errors.documents} />
      </div>

      {DOCUMENTS.map((spec) => (
        <UploadRow
          key={spec.kind}
          spec={spec}
          files={files[spec.kind]}
          error={errors[`doc-${spec.kind}`]}
          disabled={disabled}
          onPick={(picked, replacing) => onPick(spec.kind, picked, replacing)}
          onRemove={(file) => onRemove(spec.kind, file)}
        />
      ))}
    </div>
  );
}

// ---------- Bước 5 ----------

function ReviewSection({ title, step, onEdit, locked, children }: { title: string; step: StepId; onEdit: (s: StepId) => void; locked?: boolean; children: ReactNode }) {
  return (
    <section className="border-b border-(--admin-border-default) py-4 first:pt-0 last:border-b-0" aria-labelledby={`pr-review-${step}`}>
      <div className="mb-2 flex items-center justify-between gap-3">
        <h3 id={`pr-review-${step}`} className="text-sm font-semibold text-(--admin-text-primary)">
          {step}. {title}
        </h3>
        {locked ? (
          <Lock size={16} className="text-(--admin-text-secondary)" aria-label="Đang khoá trong lúc gửi" />
        ) : (
          <button type="button" className={`${buttonClass('ghost')} h-8 px-3 text-[13px]`} onClick={() => onEdit(step)} aria-label={`Sửa ${title}`}>
            Sửa
          </button>
        )}
      </div>
      <dl className="flex flex-col gap-0.5 text-[13px] text-(--admin-text-secondary)">{children}</dl>
    </section>
  );
}

function Row({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div className="flex flex-wrap gap-x-1">
      <dt>{label}:</dt>
      <dd className="min-w-0 break-words text-(--admin-text-primary)">{value || 'Chưa cung cấp'}</dd>
    </div>
  );
}

export function ReviewStep({
  form,
  files,
  update,
  errors,
  disabled,
  onEdit,
  emailShown,
}: StepProps & { files: PartnerDocumentFiles; onEdit: (s: StepId) => void; emailShown: string }) {
  const labels = businessLabels(form.businessType);
  const rep = representativeOf(form);
  const hours = form.open24h ? 'Mở cửa 24/7' : form.openTime && form.closeTime ? `${form.openTime}–${form.closeTime} · Không mở cửa 24/7` : '';
  const capacity = [
    form.estimatedCarSlots ? `${form.estimatedCarSlots} chỗ ô tô` : '',
    form.estimatedMotorbikeSlots ? `${form.estimatedMotorbikeSlots} chỗ xe máy` : '',
  ]
    .filter(Boolean)
    .join(' · ');
  return (
    <div className="flex flex-col gap-4">
      <div>
        <ReviewSection title="Người đăng ký" step={1} onEdit={onEdit} locked={disabled}>
          <Row label="Họ và tên" value={form.fullName} />
          <Row label="Chức vụ" value={form.position} />
          <Row label="Email" value={emailShown} />
          <Row label="Số điện thoại" value={form.phone} />
        </ReviewSection>
        <ReviewSection title="Doanh nghiệp / hộ kinh doanh" step={2} onEdit={onEdit} locked={disabled}>
          <Row label="Loại hình" value={labels.typeName} />
          <Row label="Mã số thuế" value={form.taxCode} />
          <Row label={labels.name} value={form.businessName} />
          <Row label="Địa chỉ trụ sở" value={composeAddress(form.businessAddress)} />
          <Row label={labels.representative} value={rep.name} />
          <Row label="Chức vụ người đại diện" value={rep.position} />
        </ReviewSection>
        <ReviewSection title="Bãi đỗ dự kiến" step={3} onEdit={onEdit} locked={disabled}>
          <Row label="Số bãi" value={String(form.lotCount)} />
          <Row label="Địa chỉ bãi chính" value={composeAddress(form.lotAddress)} />
          <Row label="Giờ hoạt động" value={hours} />
          <Row label="Sức chứa ước tính" value={capacity} />
        </ReviewSection>
        <ReviewSection title="Giấy tờ pháp lý" step={4} onEdit={onEdit} locked={disabled}>
          {DOCUMENTS.map((d) => (
            <Row key={d.kind} label={d.label} value={files[d.kind].map((f) => f.name).join(', ')} />
          ))}
        </ReviewSection>
      </div>

      <div className="border-t border-(--admin-border-default) pt-4">
        <Checkbox
          id="pr-consentAccepted"
          checked={form.consentAccepted}
          onChange={(consentAccepted) => update({ consentAccepted })}
          disabled={disabled}
          describedBy={errors.consentAccepted ? messageId('pr-consentAccepted') : undefined}
          invalid={Boolean(errors.consentAccepted)}
        >
          Tôi xác nhận thông tin và giấy tờ là chính xác, đồng ý để ParkMaster lưu trữ và xử lý dữ liệu này nhằm thẩm định hồ sơ đối tác.
        </Checkbox>
        <div className="mt-1.5 pl-6">
          <FieldMessage id="pr-consentAccepted" error={errors.consentAccepted} />
        </div>
      </div>
    </div>
  );
}
