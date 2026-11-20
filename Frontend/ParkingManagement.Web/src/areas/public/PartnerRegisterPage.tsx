import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { Loader2 } from 'lucide-react';
import '@/areas/admin/theme/admin.css';
import { logoutSession } from '@/api/authApi';
import {
  partnerRegistrationApi,
  type PartnerApplicationReceipt,
  type PartnerDocumentFiles,
  type PartnerDocumentKind,
} from '@/api/partnerRegistrationApi';
import { useAppDispatch, useAppSelector } from '@/store';
import { logout } from '@/store/slices/authSlice';
import {
  checkFile,
  createEmptyForm,
  DOCUMENTS,
  EMPTY_FILES,
  FIELD_STEP,
  fieldLabel,
  mapServerError,
  maskEmail,
  STEPS,
  toSubmitFields,
  validateAll,
  validateStep,
  type FieldErrors,
  type FieldKey,
  type RegistrationForm,
  type StepId,
} from './partner-register/registrationForm';
import { clearDraft, formatClock, loadDraft, saveDraft } from './partner-register/registrationDraft';
import { CompactStepper, RegisterStepper, type StepStatus } from './partner-register/RegisterStepper';
import { AfterSubmitAccordion, AfterSubmitCard, PrepareCard, SupportText } from './partner-register/SidebarCards';
import { ApplicantStep, BusinessStep, DocumentsStep, LotStep, ReviewStep } from './partner-register/Steps';
import { SuccessView } from './partner-register/SuccessView';
import { Notice } from './partner-register/ui';
import { buttonClass } from './partner-register/styles';

type SubmitError = { kind: 'network' | 'conflict' | 'rate' | 'server'; message: string } | null;

/** Sửa trường nào thì xoá lỗi của ô tương ứng (một số trường gộp vào một ô lỗi). */
const ERROR_KEYS: Partial<Record<keyof RegistrationForm, FieldKey[]>> = {
  openTime: ['operatingHours'],
  closeTime: ['operatingHours'],
  open24h: ['operatingHours'],
  representativeIsMe: ['legalRepresentativeName', 'legalRepresentativePosition'],
  fullName: ['fullName', 'legalRepresentativeName'],
  position: ['position', 'legalRepresentativePosition'],
};

const AUTOSAVE_MS = 800;

/**
 * US-053 / US-116: trang public "Đăng ký trở thành đối tác" – KHÔNG cần đăng nhập (đang đăng nhập thì hồ sơ gắn với tài khoản đó).
 * Wizard 5 bước theo Figma "Partner Registration" (221:139…221:161). Gửi một lần multipart tới
 * POST /api/v1/admin/partner-registrations (tên field giữ nguyên). Bản nháp chỉ lưu chữ / số trong localStorage 7 ngày.
 */
export function PartnerRegisterPage() {
  const dispatch = useAppDispatch();
  const user = useAppSelector((s) => s.auth.user);
  const signedInEmail = user?.email?.trim() || undefined;

  // Khôi phục bản nháp (Figma 13) – đọc một lần khi mở trang.
  const [draft] = useState(loadDraft);
  const [form, setForm] = useState<RegistrationForm>(() => draft?.form ?? createEmptyForm());
  const [files, setFiles] = useState<PartnerDocumentFiles>(EMPTY_FILES);
  const [step, setStep] = useState<StepId>(() => draft?.step ?? 1);
  const [completed, setCompleted] = useState<Set<StepId>>(() => new Set(STEPS.filter((s) => s.id < (draft?.step ?? 1)).map((s) => s.id)));
  const [errors, setErrors] = useState<FieldErrors>({});
  /** Bước đã bị chặn vì lỗi – stepper hiện "Cần sửa" cho tới khi hết lỗi. */
  const [flagged, setFlagged] = useState<Set<StepId>>(() => new Set());
  const [showSummary, setShowSummary] = useState(false);
  /** Tăng mỗi lần chặn vì lỗi → focus vào summary (không focus lại khi người dùng đang gõ sửa). */
  const [summaryTick, setSummaryTick] = useState(0);
  const [submitting, setSubmitting] = useState(false);
  const [progress, setProgress] = useState(0);
  const [submitError, setSubmitError] = useState<SubmitError>(null);
  const [receipt, setReceipt] = useState<PartnerApplicationReceipt | null>(null);
  const [savedAt, setSavedAt] = useState<number | null>(() => draft?.savedAt ?? null);
  const [restoredAt, setRestoredAt] = useState<number | null>(() => draft?.savedAt ?? null);
  const [signingOut, setSigningOut] = useState(false);

  const dirty = useRef(false);
  const headingRef = useRef<HTMLHeadingElement>(null);
  const summaryRef = useRef<HTMLDivElement>(null);
  const moved = useRef(false);

  // Đang đăng nhập (Figma 2): email lấy từ tài khoản và khoá lại; họ tên / SĐT chỉ điền khi còn trống.
  // Điều chỉnh state ngay lúc render khi tài khoản đổi (user nạp bất đồng bộ sau khi trang mở).
  const [prefilledFor, setPrefilledFor] = useState<number | null>(null);
  if (user && prefilledFor !== user.id) {
    setPrefilledFor(user.id);
    setForm((prev) => ({
      ...prev,
      email: user.email?.trim() || prev.email,
      fullName: prev.fullName || user.fullName || '',
      phone: prev.phone || user.phone || '',
    }));
  }

  // Tự lưu nháp sau khi người dùng sửa (không lưu file).
  useEffect(() => {
    if (!dirty.current || receipt) return undefined;
    const timer = window.setTimeout(() => {
      const at = saveDraft(form, step);
      if (at) setSavedAt(at);
    }, AUTOSAVE_MS);
    return () => window.clearTimeout(timer);
  }, [form, step, receipt]);

  // Đổi bước → đưa tiêu đề bước vào tầm nhìn và focus (bỏ qua lần render đầu).
  useEffect(() => {
    if (!moved.current) return;
    headingRef.current?.focus({ preventScroll: true });
    headingRef.current?.scrollIntoView?.({ block: 'start', behavior: 'smooth' });
  }, [step]);

  useEffect(() => {
    if (summaryTick > 0) summaryRef.current?.focus();
  }, [summaryTick]);

  const goTo = useCallback((next: StepId) => {
    moved.current = true;
    setShowSummary(false);
    setStep(next);
  }, []);

  const update = (patch: Partial<RegistrationForm>) => {
    dirty.current = true;
    setForm((prev) => ({ ...prev, ...patch }));
    setSubmitError(null);
    setErrors((prev) => {
      const next = { ...prev };
      (Object.keys(patch) as Array<keyof RegistrationForm>).forEach((key) => {
        (ERROR_KEYS[key] ?? [key as FieldKey]).forEach((k) => delete next[k]);
      });
      return next;
    });
  };

  const stepErrors = (s: StepId, source: FieldErrors = errors) => (Object.keys(source) as FieldKey[]).filter((k) => FIELD_STEP[k] === s);

  const statusOf = (s: StepId): StepStatus => {
    if (flagged.has(s) && stepErrors(s).length > 0) return 'error';
    if (s === step) return 'current';
    return completed.has(s) ? 'done' : 'todo';
  };

  const showErrors = (found: FieldErrors) => {
    const first = (Object.keys(found) as FieldKey[])[0];
    const target = FIELD_STEP[first];
    setErrors((prev) => ({ ...prev, ...found }));
    setFlagged((prev) => new Set(prev).add(target));
    if (target !== step) {
      moved.current = true;
      setStep(target);
    }
    setShowSummary(true);
    setSummaryTick((t) => t + 1);
  };

  const onPick = (kind: PartnerDocumentKind, picked: File[], replacing?: File) => {
    const spec = DOCUMENTS.find((d) => d.kind === kind)!;
    const next: PartnerDocumentFiles = { ...files, [kind]: [...files[kind]] };
    let problem: string | null = null;
    let target = replacing;
    picked.forEach((file) => {
      const err = checkFile(spec, file, next, target);
      if (err) {
        problem ??= picked.length > 1 ? `${file.name}: ${err}` : err;
        return;
      }
      if (spec.imageOnly) next[kind] = [file];
      else if (target) {
        const replaced = target;
        next[kind] = next[kind].map((f) => (f === replaced ? file : f));
        target = undefined;
      } else next[kind] = [...next[kind], file];
    });
    setFiles(next);
    setSubmitError(null);
    setErrors((prev) => {
      const rest = { ...prev };
      delete rest[`doc-${kind}`];
      delete rest.documents;
      return problem ? { ...rest, [`doc-${kind}`]: problem } : rest;
    });
  };

  const onRemove = (kind: PartnerDocumentKind, file: File) => {
    setFiles((prev) => ({ ...prev, [kind]: prev[kind].filter((f) => f !== file) }));
    setErrors((prev) => {
      const rest = { ...prev };
      delete rest[`doc-${kind}`];
      delete rest.documents;
      return rest;
    });
  };

  const saveNow = () => {
    const at = saveDraft(form, step);
    if (at) setSavedAt(at);
  };

  const startOver = () => {
    clearDraft();
    dirty.current = false;
    setForm({ ...createEmptyForm(), email: signedInEmail ?? '', fullName: user?.fullName ?? '', phone: user?.phone ?? '' });
    setFiles(EMPTY_FILES);
    setCompleted(new Set());
    setFlagged(new Set());
    setErrors({});
    setSubmitError(null);
    setSavedAt(null);
    setRestoredAt(null);
    goTo(1);
  };

  const signOut = async () => {
    setSigningOut(true);
    try {
      await logoutSession();
    } catch {
      /* Phiên phía server có thể đã hết hạn – vẫn đăng xuất ở trình duyệt. */
    } finally {
      dispatch(logout());
      setPrefilledFor(null);
      setForm((prev) => ({ ...prev, email: '' }));
      setSigningOut(false);
    }
  };

  const submit = async () => {
    const all = validateAll(form, files);
    if (Object.keys(all).length > 0) return showErrors(all);
    setSubmitting(true);
    setSubmitError(null);
    setProgress(0);
    try {
      const result = await partnerRegistrationApi.submit(toSubmitFields(form), files, { onUploadProgress: setProgress });
      clearDraft();
      dirty.current = false;
      setReceipt(result);
      window.scrollTo?.({ top: 0 });
    } catch (err) {
      const message = err instanceof Error ? err.message : '';
      const status = (err as { status?: number }).status;
      if (status === 400) {
        const field = mapServerError(message);
        if (field) return showErrors({ [field]: message });
        return setSubmitError({ kind: 'server', message });
      }
      if (status === 409) return setSubmitError({ kind: 'conflict', message });
      if (status === 429) return setSubmitError({ kind: 'rate', message: message || 'Bạn đã gửi hồ sơ quá nhiều lần. Vui lòng thử lại sau 10 phút.' });
      if (status === undefined || status >= 500) return setSubmitError({ kind: 'network', message });
      setSubmitError({ kind: 'server', message });
    } finally {
      setSubmitting(false);
    }
  };

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault();
    if (submitting) return;
    if (step === 5) {
      void submit();
      return;
    }
    const found = validateStep(step, form, files);
    if (Object.keys(found).length > 0) {
      setErrors((prev) => {
        const rest = { ...prev };
        stepErrors(step, prev).forEach((k) => delete rest[k]);
        return { ...rest, ...found };
      });
      setFlagged((prev) => new Set(prev).add(step));
      setShowSummary(true);
      setSummaryTick((t) => t + 1);
      return;
    }
    setCompleted((prev) => new Set(prev).add(step));
    setFlagged((prev) => {
      const next = new Set(prev);
      next.delete(step);
      return next;
    });
    goTo((step + 1) as StepId);
  };

  const focusField = (key: FieldKey) => {
    const el = document.getElementById(`pr-${key}`);
    el?.focus();
    el?.scrollIntoView?.({ block: 'center', behavior: 'smooth' });
  };

  const current = STEPS[step - 1];
  const summaryKeys = showSummary ? stepErrors(step) : [];
  const stepProps = { form, update, errors, disabled: submitting };
  const savedText = savedAt ? `Đã lưu nháp lúc ${formatClock(savedAt)}` : '';
  const primaryLabel = step < 5 ? 'Tiếp tục' : submitError?.kind === 'network' ? 'Thử lại' : 'Gửi hồ sơ đăng ký';

  return (
    <div className="admin-scope min-h-screen bg-(--admin-surface-page)">
      <header className="border-b border-(--admin-border-default) bg-(--admin-surface-card)">
        <div className="mx-auto flex h-[72px] max-w-[1120px] items-center justify-between gap-4 px-4 md:px-8">
          <Link to="/" className="rounded text-lg font-bold text-(--admin-brand-navy-900) outline-none focus-visible:ring-2 focus-visible:ring-(--admin-brand-navy-900)">
            ParkMaster
          </Link>
          <p className="flex items-center gap-3 text-[13px]">
            <span className="text-(--admin-text-secondary)">Đã là đối tác?</span>
            <Link to="/login" className="rounded font-semibold text-(--admin-brand-navy-900) outline-none hover:underline focus-visible:ring-2 focus-visible:ring-(--admin-brand-navy-900)">
              Đăng nhập
            </Link>
          </p>
        </div>
      </header>

      {receipt ? (
        <main className="mx-auto max-w-[1120px] px-4 py-10 md:px-8">
          <SuccessView receipt={receipt} email={signedInEmail ?? form.email} />
        </main>
      ) : (
        <main className="mx-auto max-w-[1120px] px-4 pt-8 pb-48 md:px-8 md:pb-12 lg:pt-12">
          <h1 className="text-2xl font-bold tracking-tight text-(--admin-text-primary) md:text-[32px] md:leading-10">Đăng ký trở thành đối tác</h1>
          <p className="mt-2 text-sm text-(--admin-text-secondary)">Đưa bãi đỗ của Bạn lên ParkMaster. Hoàn tất hồ sơ theo 5 bước dưới đây.</p>

          <div className="mt-6 lg:hidden">
            <CompactStepper current={step} statusOf={statusOf} />
          </div>

          <div className="mt-6 grid gap-8 lg:mt-8 lg:grid-cols-[260px_minmax(0,1fr)] xl:grid-cols-[320px_minmax(0,720px)] xl:justify-between">
            <aside className="hidden flex-col gap-6 self-start lg:sticky lg:top-6 lg:flex">
              <RegisterStepper current={step} statusOf={statusOf} onSelect={goTo} disabled={submitting} />
              <AfterSubmitCard />
              <PrepareCard />
              <SupportText />
            </aside>

            <div className="min-w-0">
              {restoredAt && (
                <div className="mb-4">
                  <Notice tone="success" role="status" title={`Đã khôi phục bản nháp lúc ${formatClock(restoredAt)}`} onClose={() => setRestoredAt(null)}>
                    <button
                      type="button"
                      onClick={startOver}
                      className="rounded font-semibold text-(--admin-brand-navy-900) underline-offset-2 outline-none hover:underline focus-visible:ring-2 focus-visible:ring-(--admin-brand-navy-900)"
                    >
                      Bắt đầu lại
                    </button>
                  </Notice>
                </div>
              )}

              <form onSubmit={handleSubmit} noValidate aria-labelledby="pr-step-title">
                <section className="scroll-mt-6 rounded-xl border border-(--admin-border-default) bg-(--admin-surface-card) p-5 md:p-6">
                  <h2 id="pr-step-title" ref={headingRef} tabIndex={-1} className="scroll-mt-6 text-lg font-semibold text-(--admin-text-primary) outline-none">
                    Bước {step}/{STEPS.length} · {current.title}
                  </h2>
                  <p className="mt-1 text-sm text-(--admin-text-secondary)">{current.description}</p>

                  <div className="mt-5 flex flex-col gap-5">
                    {summaryKeys.length > 0 && (
                      <div ref={summaryRef} tabIndex={-1} role="alert" className="flex flex-col gap-2 outline-none">
                        <Notice tone="danger" title={`Còn ${summaryKeys.length} mục cần sửa`}>
                          Vui lòng kiểm tra các thông tin dưới đây để tiếp tục.
                        </Notice>
                        <ul className="flex flex-wrap gap-1">
                          {summaryKeys.map((key) => (
                            <li key={key}>
                              <a
                                href={`#pr-${key}`}
                                onClick={(e) => {
                                  e.preventDefault();
                                  focusField(key);
                                }}
                                className={`${buttonClass('ghost')} h-8 px-3 text-[13px]`}
                              >
                                {fieldLabel(key, form.businessType)}
                              </a>
                            </li>
                          ))}
                        </ul>
                      </div>
                    )}

                    {step === 5 && submitting && (
                      <Notice tone="info" role="status" title="Đang gửi hồ sơ của Bạn...">
                        Vui lòng giữ trang này mở cho đến khi hoàn tất.{progress > 0 && progress < 100 ? ` Đã tải lên ${progress}%.` : ''}
                      </Notice>
                    )}
                    {step === 5 && submitError && (
                      <div role="alert">
                        {submitError.kind === 'network' ? (
                          <Notice tone="danger" title="Không thể gửi hồ sơ lúc này">
                            Kết nối bị gián đoạn. Thông tin và giấy tờ của Bạn vẫn được giữ lại.
                          </Notice>
                        ) : (
                          <Notice tone={submitError.kind === 'rate' ? 'warning' : 'danger'} title={submitError.message} />
                        )}
                      </div>
                    )}

                    {step === 1 && (
                      <ApplicantStep {...stepProps} signedInEmail={signedInEmail} onSignOut={() => void signOut()} signingOut={signingOut} />
                    )}
                    {step === 2 && <BusinessStep {...stepProps} />}
                    {step === 3 && <LotStep {...stepProps} />}
                    {step === 4 && <DocumentsStep files={files} errors={errors} disabled={submitting} onPick={onPick} onRemove={onRemove} />}
                    {step === 5 && <ReviewStep {...stepProps} files={files} onEdit={goTo} emailShown={signedInEmail ? maskEmail(signedInEmail) : form.email} />}
                  </div>
                </section>

                {/* Thanh thao tác: mobile cố định đáy màn hình, từ md dính đáy cột form (Figma sticky action bar). */}
                <div className="fixed inset-x-0 bottom-0 z-20 border-t border-(--admin-border-default) bg-(--admin-surface-card) px-4 pt-3 pb-[max(12px,env(safe-area-inset-bottom))] shadow-[0_-4px_12px_rgb(0_0_0/0.06)] md:sticky md:bottom-4 md:mt-4 md:rounded-xl md:border md:px-5 md:py-3">
                  <div className="flex flex-col gap-2 md:flex-row md:items-center md:gap-3">
                    <button
                      type="submit"
                      className={`${buttonClass('primary')} order-1 w-full md:order-2 md:ml-auto md:w-auto`}
                      disabled={submitting || (step === 5 && !form.consentAccepted)}
                      aria-busy={submitting || undefined}
                    >
                      {submitting ? (
                        <>
                          <Loader2 size={16} className="animate-spin motion-reduce:animate-none" aria-hidden="true" />
                          Đang gửi…
                        </>
                      ) : (
                        primaryLabel
                      )}
                    </button>
                    <div className={`order-2 grid gap-2 md:order-1 md:flex md:items-center md:gap-3 ${step > 1 ? 'grid-cols-2' : 'grid-cols-1'}`}>
                      {step > 1 && (
                        <button type="button" className={buttonClass('secondary')} onClick={() => goTo((step - 1) as StepId)} disabled={submitting}>
                          Quay lại
                        </button>
                      )}
                      <button type="button" className={buttonClass('ghost')} onClick={saveNow} disabled={submitting}>
                        Lưu nháp
                      </button>
                      {savedText && <span className="hidden text-xs text-(--admin-text-secondary) md:inline">{savedText}</span>}
                    </div>
                    {savedText && (
                      <span className="order-3 text-center text-xs text-(--admin-text-secondary) md:hidden" aria-live="polite">
                        {savedText}
                      </span>
                    )}
                  </div>
                </div>
              </form>

              <div className="mt-6 flex flex-col gap-4 lg:hidden">
                <AfterSubmitAccordion />
                <SupportText />
              </div>
            </div>
          </div>
        </main>
      )}
    </div>
  );
}
