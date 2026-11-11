import React, { useEffect, useId, useMemo, useState } from 'react';
import { adminSanctionsApi } from '@/api/adminSanctionsApi';
import { adminUsersApi } from '@/api/adminUsersApi';
import { adminOperationsApi } from '@/api/adminOperationsApi';
import { SanctionLevel, UserRoleType } from '@/types/adminEnums';
import type { AdminUserSummaryDto } from '@/types/adminUsers';
import type { OwnerParkingLotItemDto } from '@/types/adminOperations';
import type { IssueSanctionResultDto, SanctionSuggestionDto } from '@/types/adminSanctions';
import { AdminDialog } from '../../../components/AdminDialog';
import { AdminButton } from '../../../components/AdminButton';
import { Banner } from '../../../components/Banner';
import { DateTimeField, SelectField, TextAreaField, TextField } from '../../../components/FormFields';
import { StatusIndicator } from '../../../components/StatusIndicator';
import { useAdminToast } from '../../../hooks/useAdminToast';
import {
  EVIDENCE_MAX,
  LEVEL_OPTIONS,
  REASON_MAX,
  fieldsForLevel,
  levelLabel,
  levelTone,
  toIssueRequest,
  utcToIctInput,
  validateIssueForm,
  type IssueForm,
  type IssueFormErrors,
} from './sanctionFormat';

/** Mở dialog từ màn khác: chủ bãi (OwnerProfileId) và / hoặc cấp + bãi được chọn sẵn. */
export interface IssueSanctionPreset {
  ownerProfileId?: number;
  ownerUserId?: number;
  level?: SanctionLevel;
  lotIds?: number[];
}

/** Màn gọi chỉ render dialog khi mở; đóng = gỡ khỏi cây (AdminDialog tự phát hiệu ứng đóng). */
interface IssueSanctionDialogProps {
  preset?: IssueSanctionPreset;
  onClose: () => void;
  onIssued: (result: IssueSanctionResultDto) => void;
}

const EMPTY_FORM: IssueForm = {
  ownerProfileId: null,
  ownerUserId: null,
  level: '',
  lotIds: [],
  reason: '',
  endsAt: '',
  correctionDue: '',
  penalty: '',
  evidence: '',
  confirmOwner: '',
};

const ownerLabel = (u: AdminUserSummaryDto) => `${u.businessName || u.fullName} · U#${u.id}`;

const DAY_MS = 24 * 3600 * 1000;

/** Giá trị gợi ý khi chọn cấp (chỉ điền ô đang trống): cảnh cáo +7 ngày, hạ hạng +14 ngày, tạm ngừng +3 ngày. */
const levelDefaults = (level: string, form: IssueForm): Partial<IssueForm> => {
  const now = Date.now();
  const patch: Partial<IssueForm> = { level, confirmOwner: '' };
  if (level === SanctionLevel.Warning && !form.correctionDue) patch.correctionDue = utcToIctInput(now + 7 * DAY_MS);
  if (level === SanctionLevel.RankingPenalty && !form.endsAt) patch.endsAt = utcToIctInput(now + 14 * DAY_MS);
  if (level === SanctionLevel.TemporarySuspension && !form.endsAt) patch.endsAt = utcToIctInput(now + 3 * DAY_MS);
  return patch;
};

/** Danh sách user LotOwner (UserService qua AdminService); OwnerProfileId lấy từ chi tiết user khi chọn. */
const useOwnerSource = (open: boolean) => {
  const [owners, setOwners] = useState<AdminUserSummaryDto[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  useEffect(() => {
    if (!open || owners) return;
    let cancelled = false;
    adminUsersApi
      .list({ role: UserRoleType.LotOwner, page: 1, pageSize: 100 })
      .then((r) => !cancelled && setOwners(r.items))
      .catch((err: unknown) => !cancelled && setError(err instanceof Error ? err.message : String(err)));
    return () => {
      cancelled = true;
    };
  }, [open, owners]);
  return { owners, error };
};

/** Ô chọn nhiều bãi + "chọn tất cả bãi của chủ bãi". */
const LotPicker: React.FC<{
  lots: OwnerParkingLotItemDto[] | null;
  loading: boolean;
  selected: number[];
  onChange: (ids: number[]) => void;
  error?: string;
  emptyMeaning: string;
}> = ({ lots, loading, selected, onChange, error, emptyMeaning }) => {
  const groupId = useId();
  const all = lots !== null && lots.length > 0 && lots.every((l) => selected.includes(l.id));
  const toggle = (id: number) => onChange(selected.includes(id) ? selected.filter((x) => x !== id) : [...selected, id]);
  return (
    <fieldset className="m-0 flex flex-col gap-1 border-0 p-0" aria-describedby={`${groupId}-msg`}>
      <legend className="adm-type-body-strong mb-1 text-(--admin-text-primary)">Lots</legend>
      {loading && <span className="adm-type-body-small text-(--admin-text-secondary)">Loading lots…</span>}
      {lots !== null && lots.length === 0 && <span className="adm-type-body-small text-(--admin-text-secondary)">This owner has no lots.</span>}
      {lots !== null && lots.length > 0 && (
        <div className="flex max-h-44 flex-col gap-1.5 overflow-y-auto border border-(--admin-border-default) p-2">
          <label className="adm-type-body-strong inline-flex cursor-pointer items-center gap-2">
            <input type="checkbox" checked={all} onChange={() => onChange(all ? [] : lots.map((l) => l.id))} />
            Select all lots of this owner ({lots.length})
          </label>
          {lots.map((l) => (
            <label key={l.id} className="adm-type-body-default inline-flex cursor-pointer items-center gap-2">
              <input type="checkbox" checked={selected.includes(l.id)} onChange={() => toggle(l.id)} />
              <span>
                {l.name} <span className="text-(--admin-text-secondary)">· Lot #{l.id}</span>
              </span>
              <StatusIndicator value={l.status} />
            </label>
          ))}
        </div>
      )}
      <span id={`${groupId}-msg`} className={`adm-type-body-small ${error ? 'text-(--admin-status-danger)' : 'text-(--admin-text-secondary)'}`}>
        {error ?? (selected.length === 0 ? emptyMeaning : `${selected.length} lot(s) selected · one sanction per lot`)}
      </span>
    </fieldset>
  );
};

/**
 * Dialog – Issue sanction (US-097, Điều 7): chủ bãi → (bãi) → cấp; trường đổi theo cấp, kiểm tra client khớp server,
 * banner gợi ý cấp (sanction-suggestion), lỗi 400/409 từ server hiện ở footer. Cấp 4 phải gõ lại mã chủ bãi.
 */
export const IssueSanctionDialog: React.FC<IssueSanctionDialogProps> = ({ preset, onClose, onIssued }) => {
  const toast = useAdminToast();
  const { owners, error: ownersError } = useOwnerSource(true);
  // Màn gọi chỉ mount dialog khi mở → form khởi tạo một lần từ preset (cấp + bãi chọn sẵn kèm giá trị gợi ý).
  const [form, setForm] = useState<IssueForm>(() => {
    const base = { ...EMPTY_FORM, lotIds: preset?.lotIds ?? [] };
    return preset?.level ? { ...base, ...levelDefaults(preset.level, base) } : base;
  });
  const [nowMs, setNowMs] = useState(() => Date.now());
  const [ownerUser, setOwnerUser] = useState('');
  const [ownerLoading, setOwnerLoading] = useState(false);
  const [presetResolved, setPresetResolved] = useState(preset?.ownerProfileId === undefined);
  const [lotsFor, setLotsFor] = useState<{ ownerProfileId: number; lots: OwnerParkingLotItemDto[] } | null>(null);
  const [suggestionFor, setSuggestionFor] = useState<{ ownerProfileId: number; value: SanctionSuggestionDto } | null>(null);
  const [submitted, setSubmitted] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [apiError, setApiError] = useState<string | null>(null);

  const set = (patch: Partial<IssueForm>) => setForm((f) => ({ ...f, ...patch }));
  const show = fieldsForLevel(form.level);
  const errors: IssueFormErrors = useMemo(() => validateIssueForm(form, nowMs), [form, nowMs]);
  const visible = (key: keyof IssueFormErrors) => (submitted ? errors[key] : undefined);
  const resolving = ownerLoading || !presetResolved;
  // Bãi / gợi ý chỉ dùng khi thuộc đúng chủ bãi đang chọn (kết quả của chủ bãi cũ bị bỏ qua).
  const lots = lotsFor && lotsFor.ownerProfileId === form.ownerProfileId ? lotsFor.lots : null;
  const lotsLoading = form.ownerProfileId !== null && lots === null;
  const suggestion = suggestionFor && suggestionFor.ownerProfileId === form.ownerProfileId ? suggestionFor.value : null;

  // Chọn sẵn chủ bãi theo OwnerProfileId (từ trang bãi): tìm user LotOwner sở hữu hồ sơ đó.
  useEffect(() => {
    if (!owners || preset?.ownerProfileId === undefined) return;
    let cancelled = false;
    const resolve = async () => {
      const candidates = preset.ownerUserId ? owners.filter((o) => o.id === preset.ownerUserId) : owners;
      for (const candidate of candidates) {
        const user = await adminUsersApi.getById(candidate.id).catch(() => null);
        if (cancelled) return;
        const profile = user?.ownerProfile;
        if (user && profile && profile.id === preset.ownerProfileId) {
          setOwnerUser(String(user.id));
          set({ ownerUserId: user.id, ownerProfileId: profile.id });
          break;
        }
      }
      setPresetResolved(true);
    };
    void resolve();
    return () => {
      cancelled = true;
    };
  }, [owners, preset?.ownerProfileId, preset?.ownerUserId]);

  // Đổi chủ bãi → tải bãi + gợi ý cấp; request cũ bị hủy (AbortController) khi chọn chủ bãi khác / đóng dialog.
  useEffect(() => {
    const ownerProfileId = form.ownerProfileId;
    if (ownerProfileId === null) return;
    const controller = new AbortController();
    adminOperationsApi
      .getOwnerParkingLots(ownerProfileId)
      .then((r) => !controller.signal.aborted && setLotsFor({ ownerProfileId, lots: r }))
      .catch(() => !controller.signal.aborted && setLotsFor({ ownerProfileId, lots: [] }));
    adminSanctionsApi
      .suggestion(ownerProfileId, controller.signal)
      .then((value) => !controller.signal.aborted && setSuggestionFor({ ownerProfileId, value }))
      .catch(() => undefined);
    return () => controller.abort();
  }, [form.ownerProfileId]);

  const chooseOwner = async (userId: string) => {
    setOwnerUser(userId);
    setApiError(null);
    set({ ownerUserId: null, ownerProfileId: null, lotIds: [], confirmOwner: '' });
    if (!userId) return;
    setOwnerLoading(true);
    try {
      const user = await adminUsersApi.getById(Number(userId));
      if (user.ownerProfile) set({ ownerUserId: user.id, ownerProfileId: user.ownerProfile.id });
      else setApiError(`User U#${user.id} has no owner profile.`);
    } catch (err: unknown) {
      setApiError(err instanceof Error ? err.message : String(err));
    } finally {
      setOwnerLoading(false);
    }
  };

  const chooseLevel = (level: string) => set(levelDefaults(level, form));

  const close = () => onClose();

  const confirm = async () => {
    const now = Date.now();
    setNowMs(now);
    setSubmitted(true);
    if (Object.keys(validateIssueForm(form, now)).length > 0) return;
    setSubmitting(true);
    setApiError(null);
    try {
      const result = await adminSanctionsApi.issue(toIssueRequest(form));
      const failed = result.sanctions.filter((s) => s.parkingSyncStatus === 'Failed' || s.userServiceSyncStatus === 'Failed' || s.notificationSyncStatus === 'Failed');
      toast.show({
        type: failed.length > 0 ? 'error' : 'success',
        title: `${result.sanctions.length} sanction(s) issued`,
        message: failed.length > 0 ? 'Saved, but some services were not updated. Use “Retry sync” on the sanction.' : undefined,
      });
      onIssued(result);
      close();
    } catch (err: unknown) {
      setApiError(err instanceof Error ? err.message : String(err));
    } finally {
      setSubmitting(false);
    }
  };

  const ownerOptions = (owners ?? []).map((u) => ({ value: String(u.id), label: ownerLabel(u) }));
  const isAccountLock = form.level === SanctionLevel.TemporarySuspension && form.lotIds.length === 0;

  return (
    <AdminDialog
      open
      size={640}
      title="Issue sanction"
      description="SLA sanctions · Partner contract Art. 7. Saved first, then pushed to ParkingService / UserService and the owner is notified."
      onClose={close}
      onConfirm={confirm}
      confirmLabel={form.level === SanctionLevel.PermanentBan ? 'Terminate partner' : 'Issue sanction'}
      destructive={form.level === SanctionLevel.TemporarySuspension || form.level === SanctionLevel.PermanentBan}
      confirmDisabled={resolving}
      submitting={submitting}
      error={apiError ?? ownersError}
    >
      <SelectField
        label="Owner"
        required
        value={ownerUser}
        onChange={(v) => void chooseOwner(v)}
        options={ownerOptions}
        placeholder={owners ? 'Select an owner…' : 'Loading owners…'}
        helper={form.ownerProfileId !== null ? `OwnerProfile #${form.ownerProfileId}` : resolving ? 'Resolving owner profile…' : undefined}
        error={visible('owner')}
      />

      {suggestion && (
        <Banner
          tone="info"
          action={
            suggestion.suggestedLevel !== form.level ? (
              <AdminButton variant="ghost" onClick={() => chooseLevel(String(suggestion.suggestedLevel))}>
                Use suggestion
              </AdminButton>
            ) : undefined
          }
        >
          <span data-testid="sanction-suggestion">
            Suggested: <strong>{levelLabel(String(suggestion.suggestedLevel))}</strong> · {suggestion.basis.length} sanction(s) in the last{' '}
            {suggestion.windowDays} days. {suggestion.rationale}
          </span>
        </Banner>
      )}

      <SelectField label="Level" required value={form.level} onChange={chooseLevel} options={LEVEL_OPTIONS} error={visible('level')} />

      {form.level && show.lots && (
        <LotPicker
          lots={lots}
          loading={lotsLoading}
          selected={form.lotIds}
          onChange={(lotIds) => set({ lotIds })}
          error={visible('lots')}
          emptyMeaning={
            form.level === SanctionLevel.TemporarySuspension
              ? 'No lot selected → temporary account lock of the whole owner.'
              : 'No lot selected → applies to the whole owner.'
          }
        />
      )}
      {isAccountLock && (
        <Banner tone="warning">
          Temporary account lock: the owner cannot sign in or operate any lot until the end time (same flow as Users › Lock owner).
        </Banner>
      )}

      {show.correctionDue && (
        <DateTimeField
          label="Correction deadline (ICT)"
          required
          value={form.correctionDue}
          onChange={(correctionDue) => set({ correctionDue })}
          helper="The warning ends at this deadline."
          error={visible('correctionDue')}
        />
      )}
      {show.endsAt && (
        <DateTimeField
          label="Ends at (ICT)"
          required
          value={form.endsAt}
          onChange={(endsAt) => set({ endsAt })}
          helper={form.level === SanctionLevel.RankingPenalty ? 'At most 30 days from now.' : 'Starts now; ends automatically at this time.'}
          error={visible('endsAt')}
        />
      )}
      {form.level === SanctionLevel.PermanentBan && (
        <Banner tone="danger">Termination is permanent and applies to the whole owner: the account is locked with no end date.</Banner>
      )}

      <TextAreaField
        label="Reason"
        required
        value={form.reason}
        onChange={(reason) => set({ reason })}
        maxLength={REASON_MAX}
        placeholder="Which SLA was breached (e.g. 5 booking refusals this week)?"
        helper="Sent to the owner and kept in the audit log."
        error={visible('reason')}
      />
      <TextAreaField
        label="Evidence"
        value={form.evidence}
        onChange={(evidence) => set({ evidence })}
        maxLength={EVIDENCE_MAX}
        rows={2}
        placeholder="Booking codes, complaint IDs, links…"
      />
      <TextField
        label="Penalty amount"
        value={form.penalty}
        onChange={(penalty) => set({ penalty })}
        suffix="₫"
        placeholder="Optional"
        helper="Recorded only · deducted in the owner settlement (US-080)."
        error={visible('penalty')}
      />
      {show.confirmOwner && form.ownerProfileId !== null && (
        <TextField
          label={`Type ${form.ownerProfileId} to confirm`}
          required
          value={form.confirmOwner}
          onChange={(confirmOwner) => set({ confirmOwner })}
          error={visible('confirmOwner')}
        />
      )}
      {form.level && (
        <p className="adm-type-body-regular-12 m-0 flex items-center gap-2 text-(--admin-text-secondary)">
          <StatusIndicator label={levelLabel(form.level, isAccountLock ? null : form.lotIds[0] ?? null)} tone={levelTone(form.level)} />
          Starts immediately. Appeal window: 5 business days (ICT).
        </p>
      )}
    </AdminDialog>
  );
};
