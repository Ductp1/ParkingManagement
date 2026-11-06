import React, { useCallback, useEffect, useState } from 'react';
import { capacityApi, type LotCapacityConfigDto } from '@/api/capacityApi';
import { useParkingHub, type CapacityChangedEvent } from '@/realtime/useParkingHub';
import {
  validateCapacityConfig,
  type CapacityFieldError,
  type CapacityFieldName,
  type CapacityTextValues,
} from '@/features/capacity/capacityRules';

/**
 * US-066 – Trang admin cấu hình sức chứa:
 * - Nhãn tiếng Việt không dùng ký hiệu C/B/Q + gợi ý xám dưới mỗi ô.
 * - Lỗi hiện sau khi blur từng ô hoặc bấm Lưu (hiện lỗi mọi ô); ô đang đỏ mà gõ lại thì lỗi tự mất.
 * - "Chỗ dùng chung" chỉ tính khi đủ 3 ô hợp lệ; thiếu/âm → "—"; B + Q > C → cảnh báo đỏ.
 * - Server 400: hiển thị lỗi từ server đúng chỗ (cùng nội dung client); 409: tải lại + báo.
 * - Nút Lưu khóa khi chưa có thay đổi, đang lưu, hoặc còn lỗi từ server chưa được sửa.
 */
export const CapacityConfigPage: React.FC<{ lotId?: number }> = ({ lotId = 1 }) => {
  const EMPTY: CapacityTextValues = { capacity: '', transientBuffer: '', subscriberQuota: '' };
  const [dto, setDto] = useState<LotCapacityConfigDto | null>(null);
  const [values, setValues] = useState<CapacityTextValues>(EMPTY);
  const [initial, setInitial] = useState<CapacityTextValues>(EMPTY);
  const [touched, setTouched] = useState<Record<CapacityFieldName, boolean>>({
    capacity: false, transientBuffer: false, subscriberQuota: false,
  });
  const [serverFieldErrors, setServerFieldErrors] = useState<Partial<Record<CapacityFieldName, CapacityFieldError>>>({});
  const [serverSummary, setServerSummary] = useState<CapacityFieldError | null>(null);
  const [warnings, setWarnings] = useState<string[]>([]);
  const [conflict, setConflict] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [saving, setSaving] = useState(false);
  const [runtime, setRuntime] = useState<LotCapacityConfigDto | null>(null);

  const load = useCallback(async () => {
    const data = await capacityApi.get(lotId);
    setDto(data);
    setRuntime(data);
    const loaded: CapacityTextValues = data.configured
      ? { capacity: String(data.capacity), transientBuffer: String(data.transientBuffer), subscriberQuota: String(data.subscriberQuota) }
      : EMPTY;
    setValues(loaded);
    setInitial(loaded);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [lotId]);

  useEffect(() => { void load(); }, [load]);

  const onCapacityChanged = useCallback((event: CapacityChangedEvent) => {
    setRuntime((prev) => (prev
      ? {
          ...prev,
          capacity: event.capacity,
          transientBuffer: event.transientBuffer,
          subscriberQuota: event.subscriberQuota,
          availableForTransient: event.availableForTransient,
          availableForSubscriber: event.availableForSubscriber,
          version: event.version,
        }
      : prev));
  }, []);
  useParkingHub(lotId, onCapacityChanged);

  const outcome = validateCapacityConfig(values);
  const isDirty = values.capacity !== initial.capacity
    || values.transientBuffer !== initial.transientBuffer
    || values.subscriberQuota !== initial.subscriberQuota;
  const hasServerError = Object.keys(serverFieldErrors).length > 0 || serverSummary !== null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setTouched({ capacity: true, transientBuffer: true, subscriberQuota: true });
    setSaved(false);
    if (!outcome.isValid) return; // hiện lỗi mọi ô + khối tổng hợp, không gọi API

    setSaving(true);
    try {
      const result = await capacityApi.put(lotId, { ...outcome.parsed!, version: dto?.version ?? 0 });
      setWarnings(result.warnings);
      setServerFieldErrors({});
      setServerSummary(null);
      setSaved(true);
      setRuntime(result.snapshot);
      setDto(result.snapshot);
      setInitial(values);
    } catch (err: unknown) {
      const axiosErr = err as { response?: { status: number; data?: {
        errors?: Record<string, CapacityFieldError | CapacityFieldError[]>;
        summary?: CapacityFieldError;
        detail?: string;
      } } };
      if (axiosErr.response?.status === 409) {
        setConflict('Có người vừa sửa cấu hình — đã tải lại dữ liệu mới nhất, vui lòng kiểm tra và lưu lại.');
        await load();
      } else if (axiosErr.response?.status === 400) {
        const raw = axiosErr.response.data?.errors ?? {};
        const mapped: Partial<Record<CapacityFieldName, CapacityFieldError>> = {};
        for (const [field, value] of Object.entries(raw)) {
          const first = Array.isArray(value) ? value[0] : value;
          if (first) mapped[field as CapacityFieldName] = first;
        }
        setServerFieldErrors(mapped);
        setServerSummary(axiosErr.response.data?.summary ?? null);
      } else {
        setConflict(axiosErr.response?.data?.detail ?? 'Không lưu được cấu hình. Vui lòng thử lại.');
      }
    } finally {
      setSaving(false);
    }
  };

  const handleChange = (name: CapacityFieldName, text: string) => {
    setValues((prev) => ({ ...prev, [name]: text }));
    // Sửa ô đang đỏ → xóa lỗi server của ô đó; client validate tự tính lại tức thì.
    setServerFieldErrors((prev) => {
      if (!(name in prev)) return prev;
      const next = { ...prev };
      delete next[name];
      return next;
    });
    setServerSummary(null);
    setSaved(false);
  };

  const handleBlur = (name: CapacityFieldName) => setTouched((prev) => ({ ...prev, [name]: true }));

  const visibleError = (name: CapacityFieldName): CapacityFieldError | null =>
    serverFieldErrors[name] ?? (touched[name] ? outcome.fieldErrors[name] ?? null : null);

  const view = runtime ?? dto;
  return renderPage({
    lotId, view, values, touched, outcome, visibleError,
    hasServerError, serverSummary, isDirty, saving, warnings, conflict, saved,
    handleChange, handleBlur, handleSubmit,
  });
};

type PageProps = {
  lotId: number;
  view: LotCapacityConfigDto | null;
  values: CapacityTextValues;
  touched: Record<CapacityFieldName, boolean>;
  outcome: ReturnType<typeof validateCapacityConfig>;
  visibleError: (name: CapacityFieldName) => CapacityFieldError | null;
  hasServerError: boolean;
  serverSummary: CapacityFieldError | null;
  isDirty: boolean;
  saving: boolean;
  warnings: string[];
  conflict: string | null;
  saved: boolean;
  handleChange: (name: CapacityFieldName, text: string) => void;
  handleBlur: (name: CapacityFieldName) => void;
  handleSubmit: (e: React.FormEvent) => void;
};

const FIELDS: Array<{ name: CapacityFieldName; label: string; hint: string }> = [
  { name: 'capacity', label: 'Tổng số chỗ của bãi', hint: 'Ví dụ: 100' },
  { name: 'transientBuffer', label: 'Chỗ tối đa cho xe vãng lai', hint: 'Xe vào ra lẻ, không đăng ký trước' },
  { name: 'subscriberQuota', label: 'Chỗ tối đa cho khách đăng ký', hint: 'Khách thuê tháng hoặc đặt trước' },
];

const renderPage = (p: PageProps): React.ReactElement => {
  return (
    <div className="p-6 max-w-3xl mx-auto space-y-6" data-testid="capacity-config-page">
      <h1 className="text-2xl font-bold text-slate-800">Cấu hình sức chứa — Bãi #{p.lotId}</h1>

      <div className="grid grid-cols-3 gap-3" data-testid="runtime-panel">
        <div className="rounded-xl border border-slate-200 p-3">
          <div className="text-xs text-slate-500">Đang đỗ (T)</div>
          <div className="text-xl font-bold" data-testid="runtime-total">{p.view?.totalParked ?? 0}</div>
        </div>
        <div className="rounded-xl border border-slate-200 p-3">
          <div className="text-xs text-slate-500">Vãng lai (V)</div>
          <div className="text-xl font-bold" data-testid="runtime-transient">{p.view?.transientParked ?? 0}</div>
        </div>
        <div className="rounded-xl border border-slate-200 p-3">
          <div className="text-xs text-slate-500">Thuê bao hiệu lực (A)</div>
          <div className="text-xl font-bold" data-testid="runtime-subscribers">{p.view?.activeSubscribers ?? 0}</div>
        </div>
        <div className="rounded-xl bg-emerald-50 border border-emerald-200 p-3">
          <div className="text-xs text-emerald-700">Còn cho vãng lai</div>
          <div className="text-xl font-bold text-emerald-700" data-testid="runtime-avail-transient">{p.view?.availableForTransient ?? 0}</div>
        </div>
        <div className="rounded-xl bg-blue-50 border border-blue-200 p-3">
          <div className="text-xs text-blue-700">Còn cho thuê bao</div>
          <div className="text-xl font-bold text-blue-700" data-testid="runtime-avail-subscriber">{p.view?.availableForSubscriber ?? 0}</div>
        </div>
        <div className="rounded-xl border border-slate-200 p-3">
          <div className="text-xs text-slate-500">Version</div>
          <div className="text-xl font-bold" data-testid="runtime-version">{p.view?.version ?? 0}</div>
        </div>
      </div>

      {p.warnings.length > 0 && (
        <div className="rounded-xl bg-amber-50 border border-amber-300 p-4 text-amber-800 text-sm space-y-1" data-testid="server-warnings">
          {p.warnings.map((w) => <p key={w}>{w}</p>)}
        </div>
      )}
      {p.conflict && (
        <div className="rounded-xl bg-red-50 border border-red-300 p-4 text-red-700 text-sm" data-testid="conflict-banner">{p.conflict}</div>
      )}
      {p.saved && p.warnings.length === 0 && !p.conflict && (
        <div className="rounded-xl bg-emerald-50 border border-emerald-300 p-4 text-emerald-700 text-sm" data-testid="saved-banner">Đã lưu cấu hình thành công.</div>
      )}

      <form onSubmit={p.handleSubmit} className="rounded-2xl border border-slate-200 p-5 space-y-4" noValidate>
        {FIELDS.map((field) => {
          const error = p.visibleError(field.name);
          return (
            <div key={field.name}>
              <label className="block text-sm font-medium text-slate-700 mb-1" htmlFor={`field-${field.name}`}>{field.label}</label>
              <input
                id={`field-${field.name}`}
                data-testid={`input-${field.name}`}
                type="number"
                className={`w-full rounded-lg border px-3 py-2 ${error ? 'border-red-400' : 'border-slate-300'}`}
                value={p.values[field.name]}
                onChange={(e) => p.handleChange(field.name, e.target.value)}
                onBlur={() => p.handleBlur(field.name)}
              />
              {error
                ? <p className="text-xs text-red-600 mt-1" data-testid={`error-${field.name}`}>{error.message}</p>
                : <p className="text-xs text-slate-400 mt-1">{field.hint}</p>}
            </div>
          );
        })}

        <div
          className={`rounded-lg p-3 text-sm ${
            p.outcome.shared.state === 'overflow' ? 'bg-red-50 text-red-700 border border-red-300' : 'bg-slate-50 text-slate-700'
          }`}
          data-testid="shared-zone"
        >
          <div className="font-medium">
            Chỗ dùng chung <span className="text-xs text-slate-400 font-normal">(Ai đến trước được trước)</span>
          </div>
          <div data-testid="shared-zone-value" className="text-lg font-bold">
            {p.outcome.shared.state === 'ok' && p.outcome.shared.value}
            {p.outcome.shared.state === 'overflow' && `Đang thiếu ${p.outcome.shared.deficit} chỗ, hãy giảm bớt`}
            {(p.outcome.shared.state === 'incomplete' || p.outcome.shared.state === 'invalid') && '—'}
          </div>
          {(p.outcome.shared.state === 'incomplete' || p.outcome.shared.state === 'invalid') && (
            <div className="text-xs text-slate-400" data-testid="shared-zone-hint">Nhập đủ 3 ô để xem</div>
          )}
        </div>

        {(p.serverSummary ?? p.outcome.summary) && (
          <div className="rounded-xl bg-red-50 border border-red-300 p-4 text-red-700 text-sm" data-testid="summary-error">
            {(p.serverSummary ?? p.outcome.summary)?.message}
          </div>
        )}

        <button
          type="submit"
          data-testid="submit-button"
          disabled={p.saving || !p.isDirty || p.hasServerError}
          title={!p.isDirty ? 'Chưa có thay đổi nào để lưu' : p.hasServerError ? 'Sửa các ô lỗi trước khi lưu' : undefined}
          className="w-full rounded-lg bg-blue-600 text-white font-semibold py-2.5 disabled:opacity-50 disabled:cursor-not-allowed"
        >
          {p.saving ? 'Đang lưu…' : 'Lưu cấu hình'}
        </button>
      </form>
    </div>
  );
};