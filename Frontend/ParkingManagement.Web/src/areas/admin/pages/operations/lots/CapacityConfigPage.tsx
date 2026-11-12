import React, { useCallback, useState } from 'react';
import { capacityApi, type LotCapacityConfigDto } from '@/api/capacityApi';
import { AdminApiError } from '@/api/adminClientApi';
import { useParkingHub, type CapacityChangedEvent } from '@/realtime/useParkingHub';
import type { CapacityFieldError, CapacityFieldName, CapacityTextValues } from '@/features/capacity/capacityRules';
import { AdminTitleBar } from '../../../layout/AdminTitleBar';
import { ActionDialog } from '../../../components/ActionDialog';
import { AdminButton } from '../../../components/AdminButton';
import { Banner } from '../../../components/Banner';
import { LotPicker } from '../../../components/LotPicker';
import { Panel } from '../../../components/Panel';
import { StateBlock } from '../../../components/StateBlock';
import { useAdminQuery } from '../../../hooks/useAdminQuery';
import { useAdminToast } from '../../../hooks/useAdminToast';
import { useListParams } from '../../../hooks/useListParams';
import { useLotOptions } from '../../../hooks/useLotOptions';
import { formatDateTimeIct, formatNumber } from '../../../utils/format';
import { CapacityForm } from './CapacityForm';
import { CapacityUsagePanel } from './CapacityUsagePanel';
import { capacityWarnings, checkForm, fieldErrorText, toCapacityFailure, valuesFrom } from './capacityCopy';

const NO_TOUCH: Record<CapacityFieldName, boolean> = { capacity: false, transientBuffer: false, subscriberQuota: false };

/** capacityApi (GateService, axios) → AdminApiError để dùng chung trạng thái của useAdminQuery (403 / lỗi + Retry). */
const loadCapacity = async (lotId: number): Promise<LotCapacityConfigDto> => {
  try {
    return await capacityApi.get(lotId);
  } catch (err: unknown) {
    const f = toCapacityFailure(err);
    throw new AdminApiError(f.status, f.message, f.problem);
  }
};

const sameValues = (a: CapacityTextValues, b: CapacityTextValues) =>
  a.capacity === b.capacity && a.transientBuffer === b.transientBuffer && a.subscriberQuota === b.subscriberQuota;

const TwoColumns: React.FC<{ children: React.ReactNode }> = ({ children }) => (
  // ≥1280px: hai cột (Live usage | Configuration); hẹp hơn: xếp dọc.
  <div className="grid items-start gap-4 xl:grid-cols-2">{children}</div>
);

/**
 * Sức chứa của MỘT bãi. Gắn key = lotId ở trang cha → đổi bãi thì state + truy vấn làm lại từ đầu
 * (không hiện số của bãi cũ trong lúc tải bãi mới).
 */
const LotCapacityEditor: React.FC<{ lotId: number; lotName: string }> = ({ lotId, lotName }) => {
  const toast = useAdminToast();
  const query = useAdminQuery(() => loadCapacity(lotId), [lotId]);

  const [live, setLive] = useState<LotCapacityConfigDto | null>(null);
  /**
   * Version mà form đang dựa vào (gửi kèm PUT để chống ghi đè). Tách khỏi live.version: sự kiện realtime khi form đang sửa
   * KHÔNG được nâng version này, nếu không lần lưu sẽ ghi đè thay đổi của người khác thay vì nhận 409.
   */
  const [baseVersion, setBaseVersion] = useState(0);
  const [values, setValues] = useState<CapacityTextValues>(valuesFrom(null));
  const [initial, setInitial] = useState<CapacityTextValues>(valuesFrom(null));
  const [touched, setTouched] = useState(NO_TOUCH);
  const [serverErrors, setServerErrors] = useState<Partial<Record<CapacityFieldName, CapacityFieldError>>>({});
  const [serverSummary, setServerSummary] = useState<CapacityFieldError | null>(null);
  const [conflict, setConflict] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [confirmOpen, setConfirmOpen] = useState(false);

  /** Nạp snapshot mới vào màn hình (tải xong, lưu xong, Reload sau 409). */
  const applySnapshot = (snapshot: LotCapacityConfigDto) => {
    setLive(snapshot);
    setBaseVersion(snapshot.version);
    setValues(valuesFrom(snapshot));
    setInitial(valuesFrom(snapshot));
    setTouched(NO_TOUCH);
    setServerErrors({});
    setServerSummary(null);
    setConflict(false);
    setSaveError(null);
  };

  // Dữ liệu tải mới (Retry, Reload sau 409) → nạp vào form ngay trong render (không dùng effect).
  const [synced, setSynced] = useState<LotCapacityConfigDto | null>(null);
  if (query.data && query.data !== synced) {
    setSynced(query.data);
    applySnapshot(query.data);
  }

  const dirty = !sameValues(values, initial);

  // Realtime: số chỗ trống + cấu hình mới (người khác lưu). Form chưa sửa thì cập nhật luôn giá trị trong ô.
  const onCapacityChanged = useCallback(
    (e: CapacityChangedEvent) => {
      setLive((prev) =>
        prev
          ? {
              ...prev,
              capacity: e.capacity,
              transientBuffer: e.transientBuffer,
              subscriberQuota: e.subscriberQuota,
              availableForTransient: e.availableForTransient,
              availableForSubscriber: e.availableForSubscriber,
              version: e.version,
            }
          : prev
      );
      if (!dirty) {
        const next = { capacity: String(e.capacity), transientBuffer: String(e.transientBuffer), subscriberQuota: String(e.subscriberQuota) };
        setValues(next);
        setInitial(next);
        setBaseVersion(e.version);
      }
    },
    [dirty]
  );
  useParkingHub(lotId, onCapacityChanged);

  const parked = live?.configured ? live.totalParked : 0;
  const check = checkForm(values, parked);
  const hasServerError = Object.keys(serverErrors).length > 0 || serverSummary !== null;
  const canSave = dirty && check.isValid && !hasServerError && !saving && !conflict;

  const errorFor = (name: CapacityFieldName): string | null => {
    const error = serverErrors[name] ?? (touched[name] ? check.fieldErrors[name] : undefined);
    return error ? fieldErrorText(error, values, parked) : null;
  };
  const anyTouched = Object.values(touched).some(Boolean);
  const summaryError = serverSummary ?? (anyTouched ? check.summary : null);

  const change = (name: CapacityFieldName, text: string) => {
    setValues((prev) => ({ ...prev, [name]: text }));
    setTouched((prev) => ({ ...prev, [name]: true }));
    setServerErrors((prev) => {
      if (!(name in prev)) return prev;
      const next = { ...prev };
      delete next[name];
      return next;
    });
    setServerSummary(null);
    setSaveError(null);
  };

  const save = async () => {
    if (!check.parsed || !live) return;
    setSaving(true);
    setSaveError(null);
    try {
      const result = await capacityApi.put(lotId, { ...check.parsed, version: baseVersion });
      applySnapshot(result.snapshot);
      toast.show({ type: 'success', title: 'Capacity saved', message: `${lotName} · ${formatNumber(result.snapshot.capacity)} spots` });
    } catch (err: unknown) {
      const f = toCapacityFailure(err);
      if (f.status === 409) {
        setConflict(true);
      } else if (f.status === 400 && (Object.keys(f.fieldErrors).length > 0 || f.summary)) {
        setServerErrors(f.fieldErrors);
        setServerSummary(f.summary);
      } else {
        setSaveError(f.message);
      }
    } finally {
      setSaving(false);
    }
  };

  /** Giảm tổng số chỗ so với cấu hình đang áp dụng → hỏi lại trước khi lưu. */
  const submit = () => {
    const before = live?.configured ? live.capacity : null;
    if (before !== null && check.parsed && check.parsed.capacity < before) setConfirmOpen(true);
    else void save();
  };

  const reset = () => {
    setValues(initial);
    setTouched(NO_TOUCH);
    setServerErrors({});
    setServerSummary(null);
    setSaveError(null);
  };

  if (query.status === 'forbidden') return <StateBlock type="noPermission" message="Only administrators can change lot capacity." />;
  if (query.status === 'error' || query.status === 'unavailable') {
    return (
      <StateBlock
        type="error"
        title="Could not load capacity"
        message={query.error?.problem ? query.error.message : 'Capacity for this lot could not be loaded. Try again.'}
        onRetry={query.refetch}
      />
    );
  }
  if (!live) {
    return (
      <TwoColumns>
        <Panel title="Live usage">
          <StateBlock type="loading" bare />
        </Panel>
        <Panel title="Configuration">
          <StateBlock type="loading" bare />
        </Panel>
      </TwoColumns>
    );
  }

  const alert = conflict ? (
    <Banner
      tone="danger"
      action={
        <AdminButton variant="secondary" icon="refresh" onClick={query.refetch}>
          Reload
        </AdminButton>
      }
    >
      This configuration was changed by someone else. Reload to see the latest values.
    </Banner>
  ) : saveError ? (
    <Banner tone="danger">{saveError}</Banner>
  ) : null;

  return (
    <>
      <TwoColumns>
        <CapacityUsagePanel live={live} />
        <CapacityForm
          values={values}
          errorFor={errorFor}
          summary={summaryError ? fieldErrorText(summaryError, values, parked) : null}
          shared={check.shared}
          warnings={capacityWarnings(check.parsed, live.configured ? live : null)}
          canSave={canSave}
          dirty={dirty}
          saving={saving}
          configured={live.configured}
          lastUpdated={live.updatedAtUtc ? formatDateTimeIct(live.updatedAtUtc) : null}
          alert={alert}
          onChange={change}
          onBlur={(name) => setTouched((prev) => ({ ...prev, [name]: true }))}
          onSubmit={submit}
          onReset={reset}
        />
      </TwoColumns>
      <ActionDialog
        open={confirmOpen}
        title="Reduce total spots?"
        description={
          check.parsed
            ? `Total spots for ${lotName} go from ${formatNumber(live.capacity)} to ${formatNumber(check.parsed.capacity)}. ` +
              'Fewer vehicles will be admitted at the gate once this is saved.'
            : undefined
        }
        confirmLabel="Save changes"
        onClose={() => setConfirmOpen(false)}
        onSubmit={save}
      />
    </>
  );
};

/**
 * Operations / Capacity (P1-10, US-066): sức chứa tại cổng theo bãi – GateService GET/PUT /api/v1/lots/{lotId}/capacity-config.
 * Chọn bãi bằng LotPicker (?lotId= trên URL); trái Live usage (realtime qua useParkingHub), phải Configuration.
 * Version chỉ dùng ngầm để chống ghi đè (409 → Reload). Câu chữ tiếng Anh theo `code` (capacityCopy).
 */
export const CapacityConfigPage: React.FC = () => {
  const params = useListParams();
  const lotParam = params.get('lotId');
  const lotId = Number(lotParam);
  const hasLot = Number.isInteger(lotId) && lotId > 0;
  const lots = useLotOptions();
  const lotsFailed = ['error', 'forbidden', 'unavailable'].includes(lots.query.status);

  return (
    <>
      <AdminTitleBar
        title="Capacity"
        subtitle="How many vehicles each lot takes at the gate, split between walk-ins and monthly passes."
        actions={
          <LotPicker
            value={lotParam}
            lots={lots.lots}
            loading={lots.query.status === 'loading'}
            onChange={(v) => params.set({ lotId: v || null })}
          />
        }
      />
      {hasLot ? (
        <LotCapacityEditor key={lotId} lotId={lotId} lotName={lots.byId(lotId)?.name ?? `Lot #${lotId}`} />
      ) : lotsFailed ? (
        <StateBlock
          type="error"
          title="Could not load parking lots"
          message={lots.query.error?.problem ? lots.query.error.message : 'The lot list could not be loaded. Try again.'}
          onRetry={lots.query.refetch}
        />
      ) : (
        <StateBlock type="empty" title="Choose a lot" message="Pick a parking lot above to see and change its capacity." />
      )}
    </>
  );
};
