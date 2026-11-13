import { useState, type ChangeEvent, type FormEvent, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { ArrowLeft, CheckCircle2, FileText, Lock, Upload, X } from 'lucide-react';
import {
  PARTNER_DOCUMENT_LIMITS as LIMITS,
  partnerRegistrationApi,
  type PartnerApplicationReceipt,
  type PartnerDocumentFiles,
  type PartnerDocumentKind,
  type PartnerRegistrationFields,
} from '@/api/partnerRegistrationApi';

const EMPTY_FIELDS: PartnerRegistrationFields = {
  fullName: '',
  position: '',
  email: '',
  phone: '',
  businessType: 'Company',
  businessName: '',
  taxCode: '',
  businessAddress: '',
  legalRepresentativeName: '',
  legalRepresentativePosition: '',
  lotCount: '1',
  primaryLotAddress: '',
  estimatedCarSlots: '',
  estimatedMotorbikeSlots: '',
  operatingHours: '',
  consentAccepted: false,
};

const EMPTY_FILES: PartnerDocumentFiles = { businessLicense: [], representativeId: [], lotLegalProof: [], other: [] };

const DOCUMENTS: Array<{ kind: PartnerDocumentKind; label: string; hint: string; required: boolean }> = [
  { kind: 'businessLicense', label: 'Giấy đăng ký kinh doanh', hint: 'Giấy chứng nhận đăng ký doanh nghiệp hoặc hộ kinh doanh', required: true },
  { kind: 'representativeId', label: 'Giấy tờ tùy thân người đại diện', hint: 'CCCD (2 mặt) hoặc hộ chiếu', required: true },
  { kind: 'lotLegalProof', label: 'Giấy tờ mặt bằng bãi', hint: 'Sổ đỏ, hợp đồng thuê mặt bằng…', required: false },
  { kind: 'other', label: 'Giấy tờ khác', hint: 'Chứng nhận PCCC, giấy phép liên quan…', required: false },
];

const inputClass =
  'mt-1 w-full rounded-xl border border-slate-300 bg-white px-3.5 py-2.5 text-sm text-slate-900 outline-none transition focus:border-blue-500 focus:ring-2 focus:ring-blue-100 disabled:bg-slate-100';

const formatSize = (bytes: number) => (bytes >= 1024 * 1024 ? `${(bytes / 1024 / 1024).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`);

const isAllowedFile = (file: File) => /\.(pdf|jpe?g|png)$/i.test(file.name);

function Field({ label, required, children }: { label: string; required?: boolean; children: ReactNode }) {
  return (
    <label className="block text-sm font-semibold text-slate-700">
      {label}
      {required && <span className="text-red-500"> *</span>}
      {children}
    </label>
  );
}

function Section({ step, title, description, children }: { step: number; title: string; description?: string; children: ReactNode }) {
  return (
    <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm sm:p-6">
      <h2 className="flex items-center gap-2.5 text-base font-bold text-slate-900">
        <span className="flex h-7 w-7 items-center justify-center rounded-full bg-blue-600 text-xs text-white">{step}</span>
        {title}
      </h2>
      {description && <p className="mt-1 text-sm text-slate-500">{description}</p>}
      <div className="mt-4 grid gap-4 sm:grid-cols-2">{children}</div>
    </section>
  );
}

/**
 * US-053: trang public "Trở thành đối tác chủ bãi". Người đăng ký điền thông tin pháp nhân + bãi dự kiến và tải giấy tờ pháp lý.
 * Hồ sơ vào hàng chờ Partner Onboarding; Admin thẩm định (xem giấy tờ) rồi mới cấp tài khoản chủ bãi.
 */
export function PartnerRegisterPage() {
  const [fields, setFields] = useState<PartnerRegistrationFields>(EMPTY_FIELDS);
  const [files, setFiles] = useState<PartnerDocumentFiles>(EMPTY_FILES);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [receipt, setReceipt] = useState<PartnerApplicationReceipt | null>(null);

  const allFiles = Object.values(files).flat();
  const totalBytes = allFiles.reduce((sum, f) => sum + f.size, 0);

  const set = <K extends keyof PartnerRegistrationFields>(key: K) => (e: ChangeEvent<HTMLInputElement | HTMLSelectElement>) => {
    const value = e.target instanceof HTMLInputElement && e.target.type === 'checkbox' ? e.target.checked : e.target.value;
    setFields((prev) => ({ ...prev, [key]: value }));
  };

  const addFiles = (kind: PartnerDocumentKind) => (e: ChangeEvent<HTMLInputElement>) => {
    const picked = Array.from(e.target.files ?? []);
    e.target.value = '';
    setError('');
    const bad = picked.find((f) => !isAllowedFile(f));
    if (bad) return setError(`"${bad.name}": chỉ nhận file PDF, JPG hoặc PNG.`);
    const big = picked.find((f) => f.size > LIMITS.maxFileBytes);
    if (big) return setError(`"${big.name}" lớn hơn 10 MB.`);
    if (allFiles.length + picked.length > LIMITS.maxFiles) return setError(`Tối đa ${LIMITS.maxFiles} file giấy tờ cho mỗi hồ sơ.`);
    if (totalBytes + picked.reduce((s, f) => s + f.size, 0) > LIMITS.maxTotalBytes) return setError('Tổng dung lượng giấy tờ tối đa 25 MB.');
    setFiles((prev) => ({ ...prev, [kind]: [...prev[kind], ...picked] }));
  };

  const removeFile = (kind: PartnerDocumentKind, index: number) =>
    setFiles((prev) => ({ ...prev, [kind]: prev[kind].filter((_, i) => i !== index) }));

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (busy) return;
    setError('');
    const missing = DOCUMENTS.find((d) => d.required && files[d.kind].length === 0);
    if (missing) return setError(`Vui lòng tải lên ${missing.label.toLowerCase()}.`);
    if (!fields.consentAccepted) return setError('Vui lòng đồng ý với điều khoản xử lý dữ liệu.');

    setBusy(true);
    try {
      setReceipt(await partnerRegistrationApi.submit(fields, files));
      window.scrollTo({ top: 0, behavior: 'smooth' });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Không gửi được hồ sơ, vui lòng thử lại.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="min-h-screen bg-slate-50">
      <header className="border-b border-slate-200 bg-white">
        <div className="mx-auto flex max-w-4xl items-center justify-between px-4 py-4">
          <Link to="/" className="flex items-center gap-2 text-lg font-extrabold text-slate-900">
            <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-blue-600 text-white">P</span>
            ParkMaster
          </Link>
          <Link to="/login" className="text-sm font-semibold text-blue-600 hover:underline">
            Đã là đối tác? Đăng nhập
          </Link>
        </div>
      </header>

      <main className="mx-auto max-w-4xl px-4 py-8">
        {receipt ? (
          <div className="rounded-2xl border border-emerald-200 bg-white p-8 text-center shadow-sm">
            <CheckCircle2 className="mx-auto h-14 w-14 text-emerald-500" />
            <h1 className="mt-4 text-2xl font-extrabold text-slate-900">Đã gửi hồ sơ đăng ký</h1>
            <p className="mt-2 text-slate-600">
              Mã hồ sơ của bạn là <strong className="text-slate-900">{receipt.code}</strong>. Vui lòng lưu lại để tra cứu khi liên hệ hỗ trợ.
            </p>
            <ol className="mx-auto mt-6 max-w-md space-y-2 text-left text-sm text-slate-600">
              <li>1. Admin ParkMaster thẩm định thông tin và giấy tờ pháp lý của bạn.</li>
              <li>2. Nếu cần bổ sung, chúng tôi sẽ liên hệ qua email hoặc số điện thoại đã đăng ký.</li>
              <li>3. Khi hồ sơ được duyệt, bạn nhận email kích hoạt tài khoản chủ bãi.</li>
            </ol>
            <Link to="/" className="mt-8 inline-flex items-center gap-2 rounded-xl bg-blue-600 px-5 py-2.5 text-sm font-bold text-white hover:bg-blue-700">
              <ArrowLeft size={16} /> Về trang chủ
            </Link>
          </div>
        ) : (
          <>
            <h1 className="text-2xl font-extrabold text-slate-900 sm:text-3xl">Đăng ký trở thành đối tác chủ bãi</h1>
            <p className="mt-2 text-slate-600">
              Điền thông tin doanh nghiệp và tải giấy tờ pháp lý. Hồ sơ sẽ được Admin thẩm định trước khi cấp tài khoản chủ bãi.
            </p>

            <form onSubmit={handleSubmit} className="mt-6 space-y-5" noValidate={false}>
              <Section step={1} title="Người đăng ký">
                <Field label="Họ và tên" required>
                  <input className={inputClass} required maxLength={200} autoComplete="name" value={fields.fullName} onChange={set('fullName')} disabled={busy} />
                </Field>
                <Field label="Chức vụ">
                  <input className={inputClass} maxLength={100} value={fields.position} onChange={set('position')} disabled={busy} placeholder="Giám đốc, quản lý…" />
                </Field>
                <Field label="Email" required>
                  <input className={inputClass} required type="email" maxLength={254} autoComplete="email" value={fields.email} onChange={set('email')} disabled={busy} />
                </Field>
                <Field label="Số điện thoại" required>
                  <input className={inputClass} required type="tel" maxLength={20} autoComplete="tel" value={fields.phone} onChange={set('phone')} disabled={busy} placeholder="0901234567" />
                </Field>
              </Section>

              <Section step={2} title="Doanh nghiệp / hộ kinh doanh">
                <Field label="Loại hình" required>
                  <select className={inputClass} value={fields.businessType} onChange={set('businessType')} disabled={busy}>
                    <option value="Company">Doanh nghiệp</option>
                    <option value="HouseholdBusiness">Hộ kinh doanh</option>
                  </select>
                </Field>
                <Field label="Mã số thuế" required>
                  <input className={inputClass} required maxLength={14} inputMode="numeric" pattern="\d{10}(-\d{3})?" title="10 số, hoặc 10 số kèm -3 số chi nhánh" value={fields.taxCode} onChange={set('taxCode')} disabled={busy} placeholder="0312345678" />
                </Field>
                <div className="sm:col-span-2">
                  <Field label="Tên doanh nghiệp / hộ kinh doanh" required>
                    <input className={inputClass} required maxLength={300} value={fields.businessName} onChange={set('businessName')} disabled={busy} />
                  </Field>
                </div>
                <div className="sm:col-span-2">
                  <Field label="Địa chỉ trụ sở" required>
                    <input className={inputClass} required maxLength={500} value={fields.businessAddress} onChange={set('businessAddress')} disabled={busy} />
                  </Field>
                </div>
                <Field label="Người đại diện pháp luật" required>
                  <input className={inputClass} required maxLength={200} value={fields.legalRepresentativeName} onChange={set('legalRepresentativeName')} disabled={busy} />
                </Field>
                <Field label="Chức vụ người đại diện">
                  <input className={inputClass} maxLength={100} value={fields.legalRepresentativePosition} onChange={set('legalRepresentativePosition')} disabled={busy} />
                </Field>
              </Section>

              <Section step={3} title="Bãi đỗ dự kiến">
                <Field label="Số bãi" required>
                  <input className={inputClass} required type="number" min={1} max={100} value={fields.lotCount} onChange={set('lotCount')} disabled={busy} />
                </Field>
                <Field label="Giờ hoạt động">
                  <input className={inputClass} maxLength={100} value={fields.operatingHours} onChange={set('operatingHours')} disabled={busy} placeholder="06:00–22:00" />
                </Field>
                <div className="sm:col-span-2">
                  <Field label="Địa chỉ bãi chính" required>
                    <input className={inputClass} required maxLength={500} value={fields.primaryLotAddress} onChange={set('primaryLotAddress')} disabled={busy} />
                  </Field>
                </div>
                <Field label="Số chỗ ô tô (ước tính)">
                  <input className={inputClass} type="number" min={0} max={10000} value={fields.estimatedCarSlots} onChange={set('estimatedCarSlots')} disabled={busy} />
                </Field>
                <Field label="Số chỗ xe máy (ước tính)">
                  <input className={inputClass} type="number" min={0} max={10000} value={fields.estimatedMotorbikeSlots} onChange={set('estimatedMotorbikeSlots')} disabled={busy} />
                </Field>
              </Section>

              <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm sm:p-6">
                <h2 className="flex items-center gap-2.5 text-base font-bold text-slate-900">
                  <span className="flex h-7 w-7 items-center justify-center rounded-full bg-blue-600 text-xs text-white">4</span>
                  Giấy tờ pháp lý
                </h2>
                <p className="mt-1 flex items-start gap-1.5 text-sm text-slate-500">
                  <Lock size={15} className="mt-0.5 shrink-0 text-emerald-600" />
                  Giấy tờ chỉ Admin ParkMaster xem được để thẩm định hồ sơ. PDF, JPG hoặc PNG, mỗi file tối đa 10 MB, tối đa {LIMITS.maxFiles} file (tổng 25 MB).
                </p>
                <div className="mt-4 grid gap-3 sm:grid-cols-2">
                  {DOCUMENTS.map((doc) => (
                    <div key={doc.kind} className="rounded-xl border border-dashed border-slate-300 p-4">
                      <div className="flex items-start justify-between gap-3">
                        <div>
                          <p className="text-sm font-semibold text-slate-800">
                            {doc.label}
                            {doc.required && <span className="text-red-500"> *</span>}
                          </p>
                          <p className="text-xs text-slate-500">{doc.hint}</p>
                        </div>
                        <label className={`inline-flex shrink-0 cursor-pointer items-center gap-1.5 rounded-lg bg-blue-50 px-3 py-1.5 text-xs font-bold text-blue-700 hover:bg-blue-100 ${busy ? 'pointer-events-none opacity-60' : ''}`}>
                          <Upload size={14} /> Chọn file
                          <input type="file" className="sr-only" multiple accept={LIMITS.accept} onChange={addFiles(doc.kind)} disabled={busy} aria-label={`Tải lên ${doc.label}`} />
                        </label>
                      </div>
                      {files[doc.kind].length > 0 && (
                        <ul className="mt-3 space-y-1.5">
                          {files[doc.kind].map((file, i) => (
                            <li key={`${file.name}-${i}`} className="flex items-center gap-2 rounded-lg bg-slate-50 px-2.5 py-1.5 text-xs text-slate-700">
                              <FileText size={14} className="shrink-0 text-slate-400" />
                              <span className="min-w-0 flex-1 truncate">{file.name}</span>
                              <span className="text-slate-400">{formatSize(file.size)}</span>
                              <button type="button" onClick={() => removeFile(doc.kind, i)} disabled={busy} className="text-slate-400 hover:text-red-500" aria-label={`Bỏ ${file.name}`}>
                                <X size={14} />
                              </button>
                            </li>
                          ))}
                        </ul>
                      )}
                    </div>
                  ))}
                </div>
              </section>

              <label className="flex items-start gap-3 rounded-2xl border border-slate-200 bg-white p-4 text-sm text-slate-700">
                <input type="checkbox" className="mt-0.5 h-4 w-4 accent-blue-600" checked={fields.consentAccepted} onChange={set('consentAccepted')} disabled={busy} />
                <span>
                  Tôi xác nhận thông tin và giấy tờ là chính xác, đồng ý để ParkMaster lưu trữ và xử lý dữ liệu này nhằm thẩm định hồ sơ đối tác.
                </span>
              </label>

              {error && (
                <div role="alert" className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm font-medium text-red-700">
                  {error}
                </div>
              )}

              <div className="flex flex-col-reverse items-stretch gap-3 sm:flex-row sm:items-center sm:justify-between">
                <Link to="/" className="text-center text-sm font-semibold text-slate-500 hover:text-slate-700">
                  Hủy
                </Link>
                <button type="submit" disabled={busy} className="rounded-xl bg-blue-600 px-6 py-3 text-sm font-bold text-white shadow hover:bg-blue-700 disabled:opacity-60">
                  {busy ? 'Đang gửi hồ sơ…' : 'Gửi hồ sơ đăng ký'}
                </button>
              </div>
            </form>
          </>
        )}
      </main>
    </div>
  );
}
