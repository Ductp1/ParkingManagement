import { useEffect, useRef, useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { Check, Loader2 } from 'lucide-react';
import '@/areas/admin/theme/admin.css';
import { getPartnerApplicationForEdit, resubmitPartnerApplication, type PartnerApplicationEditView } from '@/api/partnerApplicationsApi';
import type { PartnerBusinessType, PartnerDocumentFiles, PartnerRegistrationFields } from '@/api/partnerRegistrationApi';
import { takeHashParams } from './partnerApplyForm';
import { PublicTopBar } from './partner-register/PublicTopBar';
import { Checkbox, Notice, NumberStepper, TextInput } from './partner-register/ui';
import { UploadRow } from './partner-register/UploadRow';
import { buttonClass } from './partner-register/styles';
import { SupportText } from './partner-register/SidebarCards';
import { applyPick, DOCUMENTS, EMPTY_FILES, formatBytes } from './partner-register/registrationForm';

const dueText = (iso: string | null | undefined) =>
  iso ? new Date(iso).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit', year: 'numeric' }) : null;

type Problem = { kind: 'invalid' | 'expired'; message: string };
type LoadState = { status: 'loading' } | { status: 'problem'; problem: Problem } | { status: 'ready'; view: PartnerApplicationEditView };

const problemOf = (err: unknown): Problem => ({
  kind: (err as { code?: string } | null)?.code === 'edit_link_expired' ? 'expired' : 'invalid',
  message: err instanceof Error ? err.message : 'Không mở được hồ sơ.',
});

const text = (value: string | number | null | undefined) => (value === null || value === undefined ? '' : String(value));

/** Trường chữ của hồ sơ → khóa mục Admin yêu cầu (khớp PartnerInfoItems). */
type FieldKey =
  | 'fullName' | 'position' | 'phone' | 'businessName' | 'taxCode' | 'businessAddress' | 'legalRepresentativeName'
  | 'legalRepresentativePosition' | 'lotCount' | 'primaryLotAddress' | 'operatingHours' | 'estimatedSlots';

/**
 * /partner/apply/edit#code=PA-…&token=… – đối tác bổ sung hồ sơ theo yêu cầu của ParkMaster (US-116 T4, Figma 221:157). Chỉ các mục Admin
 * yêu cầu được sửa (mục khác chỉ đọc, có icon khóa); giấy tờ được yêu cầu phải tải lại. Link một lần, có hạn; gửi xong hồ sơ quay lại
 * "đang thẩm định".
 */
export function PartnerApplyEditPage() {
  const [link] = useState(() => {
    const params = takeHashParams();
    return { code: params.get('code') ?? '', token: params.get('token') ?? '' };
  });
  const [state, setState] = useState<LoadState>(() =>
    link.code && link.token
      ? { status: 'loading' }
      : { status: 'problem', problem: { kind: 'invalid', message: 'Link bổ sung hồ sơ không đầy đủ. Mở lại đúng link ParkMaster gửi cho bạn.' } }
  );

  useEffect(() => {
    if (!link.code || !link.token) return undefined;
    let cancelled = false;
    getPartnerApplicationForEdit(link.code, link.token)
      .then((view) => {
        if (!cancelled) setState({ status: 'ready', view });
      })
      .catch((err: unknown) => {
        if (!cancelled) setState({ status: 'problem', problem: problemOf(err) });
      });
    return () => {
      cancelled = true;
    };
  }, [link]);

  return (
    <div className="admin-scope min-h-screen bg-(--admin-surface-page)">
      <PublicTopBar />
      <main className="mx-auto w-full max-w-[720px] px-4 py-8 md:px-8 md:py-12">
        {state.status === 'loading' && (
          <p role="status" className="flex items-center gap-2 text-sm text-(--admin-text-secondary)">
            <Loader2 size={16} className="animate-spin motion-reduce:animate-none" aria-hidden="true" />
            Đang mở hồ sơ…
          </p>
        )}
        {state.status === 'problem' && (
          <section className="flex flex-col gap-4 rounded-xl border border-(--admin-border-default) bg-(--admin-surface-card) p-6" aria-labelledby="edit-problem">
            <h1 id="edit-problem" className="text-xl font-bold text-(--admin-text-primary)">
              {state.problem.kind === 'expired' ? 'Link bổ sung đã hết hạn' : 'Link bổ sung không hợp lệ'}
            </h1>
            <div role="alert">
              <Notice tone="danger" title={state.problem.message} />
            </div>
            <p className="text-sm text-(--admin-text-secondary)">ParkMaster sẽ gửi link mới sau khi xác minh. Liên hệ để nhận link bổ sung mới.</p>
            <SupportText />
          </section>
        )}
        {state.status === 'ready' && <EditForm view={state.view} code={link.code} token={link.token} />}
      </main>
    </div>
  );
}

function EditForm({ view, code, token }: { view: PartnerApplicationEditView; code: string; token: string }) {
  const requested = view.infoRequestItems ?? [];
  const all = requested.length === 0;
  const can = (key: FieldKey) => all || requested.includes(key);
  const docSpecs = DOCUMENTS.filter((d) => all || requested.includes(`doc:${d.kind}`));

  const [values, setValues] = useState({
    fullName: text(view.fullName),
    position: text(view.position),
    phone: text(view.phone),
    businessName: text(view.businessName),
    taxCode: text(view.taxCode),
    businessAddress: text(view.businessAddress),
    legalRepresentativeName: text(view.legalRepresentativeName),
    legalRepresentativePosition: text(view.legalRepresentativePosition),
    lotCount: view.lotCount,
    primaryLotAddress: text(view.primaryLotAddress),
    operatingHours: text(view.operatingHours),
    car: text(view.estimatedCarSlots),
    moto: text(view.estimatedMotorbikeSlots),
  });
  const [files, setFiles] = useState<PartnerDocumentFiles>(EMPTY_FILES);
  const [docErrors, setDocErrors] = useState<Record<string, string>>({});
  const [consent, setConsent] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<{ kind: 'edit' | 'link'; message: string } | null>(null);
  const [done, setDone] = useState<string | null>(null);
  const errorRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (error) errorRef.current?.focus();
  }, [error]);

  const set = <K extends keyof typeof values>(key: K, value: (typeof values)[K]) => setValues((prev) => ({ ...prev, [key]: value }));

  const text_ = (id: string, label: string, key: Exclude<keyof typeof values, 'lotCount'>, item: FieldKey, extra: { required?: boolean; maxLength?: number; inputMode?: 'numeric'; suffix?: string } = {}) => (
    <TextInput
      id={id}
      label={label}
      value={values[key]}
      onChange={(e) => set(key, e.target.value)}
      readOnly={!can(item)}
      locked={!can(item)}
      disabled={busy}
      required={extra.required}
      maxLength={extra.maxLength}
      inputMode={extra.inputMode}
      suffix={can(item) ? extra.suffix : undefined}
    />
  );

  const onPick = (kind: (typeof DOCUMENTS)[number]['kind'], picked: File[], replacing?: File) => {
    const spec = DOCUMENTS.find((d) => d.kind === kind)!;
    const result = applyPick(files, spec, picked, replacing);
    setFiles(result.files);
    setDocErrors((prev) => {
      const { [`doc-${kind}`]: _removed, ...rest } = prev;
      void _removed;
      return result.problem ? { ...rest, [`doc-${kind}`]: result.problem } : rest;
    });
  };

  const missingDoc = docSpecs.find((d) => (all ? false : files[d.kind].length === 0));

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (busy) return;
    if (!consent) return setError({ kind: 'edit', message: 'Vui lòng xác nhận thông tin để gửi bổ sung.' });
    if (missingDoc) return setDocErrors((prev) => ({ ...prev, [`doc-${missingDoc.kind}`]: `Vui lòng tải lên ${missingDoc.label.charAt(0).toLowerCase()}${missingDoc.label.slice(1)}` }));
    setBusy(true);
    setError(null);
    const fields: PartnerRegistrationFields = {
      fullName: values.fullName,
      position: values.position,
      email: view.email,
      phone: values.phone,
      businessType: view.businessType as PartnerBusinessType,
      businessName: values.businessName,
      taxCode: values.taxCode,
      businessAddress: values.businessAddress,
      legalRepresentativeName: values.legalRepresentativeName,
      legalRepresentativePosition: values.legalRepresentativePosition,
      lotCount: String(values.lotCount),
      primaryLotAddress: values.primaryLotAddress,
      estimatedCarSlots: values.car,
      estimatedMotorbikeSlots: values.moto,
      operatingHours: values.operatingHours,
      consentAccepted: true,
    };
    try {
      const receipt = await resubmitPartnerApplication(code, token, fields, files);
      setDone(receipt.code);
    } catch (err) {
      const c = (err as { code?: string }).code;
      setError({ kind: c === 'edit_link_expired' || c === 'edit_link_invalid' ? 'link' : 'edit', message: err instanceof Error ? err.message : 'Không gửi được bổ sung, vui lòng thử lại.' });
    } finally {
      setBusy(false);
    }
  }

  if (done)
    return (
      <section className="flex flex-col gap-4 rounded-xl border border-(--admin-border-default) bg-(--admin-surface-card) p-6 md:p-8" aria-labelledby="edit-done">
        <span className="flex size-12 items-center justify-center rounded-full bg-(--admin-surface-selected) text-(--admin-brand-navy-900)" aria-hidden="true">
          <Check size={22} />
        </span>
        <h1 id="edit-done" className="text-2xl font-bold text-(--admin-text-primary)">
          Đã gửi bổ sung
        </h1>
        <p className="text-sm text-(--admin-text-secondary)">
          Hồ sơ <span className="font-mono font-semibold text-(--admin-text-primary)">{done}</span> đã chuyển lại cho ParkMaster thẩm định tiếp. Link bổ sung đã hết hiệu lực.
        </p>
        <Link to="/" className={buttonClass('secondary')}>
          Về trang chủ
        </Link>
        <SupportText />
      </section>
    );

  const due = dueText(view.infoRequestDueAtUtc);
  const lockedNote = !all && (
    <p className="text-xs text-(--admin-text-secondary)">Các mục có biểu tượng khóa không nằm trong yêu cầu bổ sung nên không chỉnh sửa được.</p>
  );
  return (
    <form onSubmit={submit} noValidate className="flex flex-col gap-5" aria-labelledby="edit-title">
      <div>
        <h1 id="edit-title" className="text-2xl font-bold text-(--admin-text-primary) md:text-[28px]">
          Bổ sung hồ sơ đối tác
        </h1>
        <p className="mt-1 text-sm text-(--admin-text-secondary)">
          Mã hồ sơ <span className="font-mono font-semibold text-(--admin-text-primary)">{view.code}</span>
        </p>
      </div>

      <div role="note">
        <Notice tone="warning" title="ParkMaster cần bạn bổ sung">
          {view.infoRequestNote && <span className="block whitespace-pre-line">“{view.infoRequestNote}”</span>}
          {due && <span className="mt-1 block">Hạn bổ sung: {due}</span>}
        </Notice>
      </div>

      <section className="flex flex-col gap-5 rounded-xl border border-(--admin-border-default) bg-(--admin-surface-card) p-5 md:p-6" aria-labelledby="edit-info">
        <h2 id="edit-info" className="text-lg font-semibold text-(--admin-text-primary)">
          Thông tin hồ sơ
        </h2>
        <TextInput id="edit-email" label="Email" value={view.email} readOnly locked />
        <div className="grid gap-x-4 gap-y-5 sm:grid-cols-2">
          {text_('edit-fullName', 'Họ và tên', 'fullName', 'fullName', { required: true, maxLength: 200 })}
          {text_('edit-position', 'Chức vụ', 'position', 'position', { maxLength: 100 })}
          {text_('edit-phone', 'Số điện thoại', 'phone', 'phone', { required: true, maxLength: 20 })}
          {text_('edit-taxCode', 'Mã số thuế', 'taxCode', 'taxCode', { required: true, maxLength: 14, inputMode: 'numeric' })}
        </div>
        {text_('edit-businessName', 'Tên doanh nghiệp / hộ kinh doanh', 'businessName', 'businessName', { required: true, maxLength: 200 })}
        {text_('edit-businessAddress', 'Địa chỉ trụ sở', 'businessAddress', 'businessAddress', { required: true, maxLength: 500 })}
        <div className="grid gap-x-4 gap-y-5 sm:grid-cols-2">
          {text_('edit-rep', 'Người đại diện pháp luật', 'legalRepresentativeName', 'legalRepresentativeName', { required: true, maxLength: 200 })}
          {text_('edit-repPosition', 'Chức vụ người đại diện', 'legalRepresentativePosition', 'legalRepresentativePosition', { maxLength: 100 })}
        </div>
        {can('lotCount') ? (
          <NumberStepper id="edit-lotCount" label="Số bãi" required min={1} max={100} value={values.lotCount} onChange={(v) => set('lotCount', v)} disabled={busy} />
        ) : (
          <TextInput id="edit-lotCount" label="Số bãi" value={String(values.lotCount)} readOnly locked />
        )}
        {text_('edit-lotAddress', 'Địa chỉ bãi chính', 'primaryLotAddress', 'primaryLotAddress', { required: true, maxLength: 500 })}
        {text_('edit-hours', 'Giờ hoạt động', 'operatingHours', 'operatingHours', { maxLength: 100 })}
        <div className="grid gap-x-4 gap-y-5 sm:grid-cols-2">
          {text_('edit-car', 'Sức chứa ô tô', 'car', 'estimatedSlots', { inputMode: 'numeric', maxLength: 5, suffix: 'chỗ' })}
          {text_('edit-moto', 'Sức chứa xe máy', 'moto', 'estimatedSlots', { inputMode: 'numeric', maxLength: 5, suffix: 'chỗ' })}
        </div>
        {lockedNote}
      </section>

      {docSpecs.length > 0 && (
        <section className="flex flex-col gap-4 rounded-xl border border-(--admin-border-default) bg-(--admin-surface-card) p-5 md:p-6" aria-labelledby="edit-docs">
          <div>
            <h2 id="edit-docs" className="text-lg font-semibold text-(--admin-text-primary)">
              Giấy tờ cần bổ sung
            </h2>
            <p className="mt-1 text-xs text-(--admin-text-secondary)">
              PDF, JPG hoặc PNG · tối đa 10 MB/file. Giấy tờ mới thay thế giấy tờ cũ cùng loại
              {files.businessLicense.length + files.lotLegalProof.length + files.idCardFront.length + files.idCardBack.length + files.other.length > 0
                ? ` · đã chọn ${formatBytes([...Object.values(files)].flat().reduce((s, f) => s + f.size, 0))}`
                : ''}
              .
            </p>
          </div>
          {docSpecs.map((spec) => (
            <UploadRow
              key={spec.kind}
              spec={spec}
              files={files[spec.kind]}
              error={docErrors[`doc-${spec.kind}`]}
              disabled={busy}
              onPick={(picked, replacing) => onPick(spec.kind, picked, replacing)}
              onRemove={(file) => setFiles((prev) => ({ ...prev, [spec.kind]: prev[spec.kind].filter((f) => f !== file) }))}
            />
          ))}
        </section>
      )}

      <Checkbox id="edit-consent" checked={consent} onChange={setConsent} disabled={busy}>
        Tôi xác nhận thông tin và giấy tờ là chính xác, đồng ý để ParkMaster lưu trữ và xử lý dữ liệu này nhằm thẩm định hồ sơ đối tác.
      </Checkbox>

      {error && (
        <div ref={errorRef} tabIndex={-1} role="alert" className="outline-none">
          <Notice tone="danger" title={error.message}>
            {error.kind === 'link' ? 'Liên hệ ParkMaster để nhận link bổ sung mới.' : undefined}
          </Notice>
        </div>
      )}

      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <SupportText />
        <button type="submit" disabled={busy || !consent} aria-busy={busy || undefined} className={`${buttonClass('primary')} h-11 w-full sm:w-auto`}>
          {busy ? (
            <>
              <Loader2 size={16} className="animate-spin motion-reduce:animate-none" aria-hidden="true" />
              Đang gửi…
            </>
          ) : (
            'Gửi bổ sung'
          )}
        </button>
      </div>
    </form>
  );
}
