import React, { useId } from 'react';
import type { CapacityFieldName, CapacityTextValues } from '@/features/capacity/capacityRules';
import { AdminButton } from '../../../components/AdminButton';
import { Banner } from '../../../components/Banner';
import { Panel } from '../../../components/Panel';
import { formatNumber } from '../../../utils/format';
import { FIELD_LABELS } from './capacityCopy';

const FIELDS: CapacityFieldName[] = ['capacity', 'transientBuffer', 'subscriberQuota'];

interface NumberFieldProps {
  name: CapacityFieldName;
  value: string;
  error: string | null;
  onChange: (name: CapacityFieldName, value: string) => void;
  onBlur: (name: CapacityFieldName) => void;
}

/** Ô số nguyên: nhãn gắn input, đơn vị "spots", helper / lỗi nối bằng aria-describedby, viền focus-visible rõ. */
const NumberField: React.FC<NumberFieldProps> = ({ name, value, error, onChange, onBlur }) => {
  const id = useId();
  const msgId = `${id}-msg`;
  const { label, helper } = FIELD_LABELS[name];
  return (
    <div className="flex flex-col gap-1">
      <label htmlFor={id} className="adm-type-body-strong text-(--admin-text-primary)">
        {label}
      </label>
      <div className="relative flex max-w-[240px]">
        <input
          id={id}
          name={name}
          type="text"
          inputMode="numeric"
          autoComplete="off"
          value={value}
          aria-invalid={error ? true : undefined}
          aria-describedby={msgId}
          onChange={(e) => onChange(name, e.target.value)}
          onBlur={() => onBlur(name)}
          className={`adm-type-body-default h-8 w-full rounded-[2px] border px-2.5 pr-14 text-(--admin-text-primary) outline-none focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-(--admin-brand-navy-900) ${
            error ? 'border-(--admin-status-danger) bg-(--admin-surface-danger)' : 'border-(--admin-border-default) bg-(--admin-surface-card)'
          }`}
        />
        <span aria-hidden="true" className="adm-type-body-default absolute top-1.5 right-2.5 text-(--admin-text-secondary)">
          spots
        </span>
      </div>
      <span id={msgId} className={`adm-type-body-small ${error ? 'text-(--admin-status-danger)' : 'text-(--admin-text-secondary)'}`}>
        {error ?? helper}
      </span>
    </div>
  );
};

export interface CapacityFormProps {
  values: CapacityTextValues;
  errorFor: (name: CapacityFieldName) => string | null;
  summary: string | null;
  /** Chỗ dùng chung khi đủ dữ liệu hợp lệ; null → ẩn. */
  shared: number | null;
  warnings: string[];
  canSave: boolean;
  dirty: boolean;
  saving: boolean;
  configured: boolean;
  lastUpdated: string | null;
  /** 409 / lỗi lưu khác hiển thị trên nút. */
  alert: React.ReactNode;
  onChange: (name: CapacityFieldName, value: string) => void;
  onBlur: (name: CapacityFieldName) => void;
  onSubmit: () => void;
  onReset: () => void;
}

/** Cột "Configuration": 3 ô số + chỗ dùng chung tính trực tiếp + cảnh báo + Save / Reset. */
export const CapacityForm: React.FC<CapacityFormProps> = (p) => (
  <Panel title="Configuration">
    <form
      noValidate
      className="flex flex-col gap-4"
      onSubmit={(e) => {
        e.preventDefault();
        if (p.canSave) p.onSubmit();
      }}
    >
      {!p.configured && (
        <p className="adm-type-body-regular-12 m-0 text-(--admin-text-secondary)">This lot has no capacity yet. Fill in all three numbers to create it.</p>
      )}
      {FIELDS.map((name) => (
        <NumberField key={name} name={name} value={p.values[name]} error={p.errorFor(name)} onChange={p.onChange} onBlur={p.onBlur} />
      ))}

      {p.shared !== null && (
        <div className="flex items-center justify-between border-t border-(--admin-border-default) pt-3" data-testid="shared-spots">
          <span className="adm-type-body-regular-12 text-(--admin-text-secondary)">Shared spots (first come, first served)</span>
          <span className="adm-type-body-strong tabular-nums text-(--admin-text-primary)">{`${formatNumber(p.shared)} ${p.shared === 1 ? 'spot' : 'spots'}`}</span>
        </div>
      )}

      {p.summary && (
        <p role="alert" className="adm-type-body-small m-0 border-l-[3px] border-(--admin-status-danger) bg-(--admin-surface-danger) px-3 py-2 text-(--admin-status-danger)">
          {p.summary}
        </p>
      )}
      {p.warnings.map((w) => (
        <Banner key={w} tone="warning">
          {w}
        </Banner>
      ))}
      {p.alert}

      <div className="flex flex-wrap items-center gap-2">
        <AdminButton type="submit" variant="primary" disabled={!p.canSave} loading={p.saving} loadingLabel="Saving…">
          Save
        </AdminButton>
        <AdminButton type="button" variant="secondary" disabled={!p.dirty || p.saving} onClick={p.onReset}>
          Reset
        </AdminButton>
        {p.lastUpdated && <span className="adm-type-body-small ml-auto text-(--admin-text-secondary)">{`Last updated ${p.lastUpdated}`}</span>}
      </div>
    </form>
  </Panel>
);
