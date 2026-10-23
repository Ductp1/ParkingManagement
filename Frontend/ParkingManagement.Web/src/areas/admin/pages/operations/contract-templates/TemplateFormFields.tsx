import React, { useId, useRef } from 'react';
import type { ContractPlaceholderDto } from '@/types/adminContractTemplates';
import { AdminButton } from '../../../components/AdminButton';
import { move } from './contractTemplateFormat';

/**
 * Ô nhập chữ của mẫu hợp đồng dạng form (US-116 v11): textarea / input + menu "Insert field" chèn {{Key}} tại con trỏ.
 * Admin không gõ cú pháp markup – mỗi ô là một phần của hợp đồng; giá trị thật được điền khi lập hợp đồng bãi.
 */

const boxClass =
  'w-full rounded-[2px] border border-(--admin-border-default) bg-(--admin-surface-card) px-2.5 py-1.5 adm-type-body-default ' +
  'text-(--admin-text-primary) outline-none focus:border-(--admin-brand-navy-900) read-only:bg-(--admin-surface-page) ' +
  'placeholder:text-(--admin-text-placeholder)';

interface InsertFieldMenuProps {
  catalog: ContractPlaceholderDto[];
  onPick: (key: string) => void;
  disabled?: boolean;
}

/** Menu chèn trường (nhóm theo Contract / Party A / Party B / Parking lot / Terms / Payout account). */
export const InsertFieldMenu: React.FC<InsertFieldMenuProps> = ({ catalog, onPick, disabled }) => {
  const groups = [...new Set(catalog.filter((p) => !p.block).map((p) => p.group))];
  return (
    <select
      aria-label="Insert field"
      value=""
      disabled={disabled || catalog.length === 0}
      onChange={(e) => e.target.value && onPick(e.target.value)}
      className="h-7 max-w-40 shrink-0 rounded-[2px] border border-(--admin-border-default) bg-(--admin-surface-card) px-1.5 text-xs text-(--admin-brand-navy-900) disabled:opacity-40"
    >
      <option value="">+ Insert field</option>
      {groups.map((g) => (
        <optgroup key={g} label={g}>
          {catalog
            .filter((p) => p.group === g && !p.block)
            .map((p) => (
              <option key={p.key} value={p.key} title={`${p.description} – e.g. ${p.sample}`}>
                {p.description}
              </option>
            ))}
        </optgroup>
      ))}
    </select>
  );
};

interface TemplateTextProps {
  label?: string;
  value: string;
  onChange: (value: string) => void;
  catalog: ContractPlaceholderDto[];
  readOnly?: boolean;
  multiline?: boolean;
  rows?: number;
  placeholder?: string;
  required?: boolean;
  helper?: string;
  /** Không hiện menu chèn trường (VD tiêu đề phụ lục). */
  noFields?: boolean;
}

/** Ô chữ (một dòng hoặc nhiều dòng) có menu chèn trường tại con trỏ. */
export const TemplateText: React.FC<TemplateTextProps> = ({
  label,
  value,
  onChange,
  catalog,
  readOnly,
  multiline,
  rows = 2,
  placeholder,
  required,
  helper,
  noFields,
}) => {
  const id = useId();
  const ref = useRef<HTMLTextAreaElement & HTMLInputElement>(null);

  const insert = (key: string) => {
    const el = ref.current;
    const start = el?.selectionStart ?? value.length;
    const end = el?.selectionEnd ?? value.length;
    const token = `{{${key}}}`;
    onChange(value.slice(0, start) + token + value.slice(end));
    requestAnimationFrame(() => {
      el?.focus();
      el?.setSelectionRange(start + token.length, start + token.length);
    });
  };

  return (
    <div className="flex min-w-0 flex-1 flex-col gap-1">
      {(label || !noFields) && (
        <div className="flex items-center gap-1">
          {label && (
            <label htmlFor={id} className="adm-type-body-strong text-(--admin-text-primary)">
              {label}
            </label>
          )}
          {label && required && <span className="adm-type-body-strong text-(--admin-status-danger)">*</span>}
          {!noFields && !readOnly && (
            <span className="ml-auto">
              <InsertFieldMenu catalog={catalog} onPick={insert} />
            </span>
          )}
        </div>
      )}
      {multiline ? (
        <textarea
          id={id}
          ref={ref}
          rows={rows}
          value={value}
          readOnly={readOnly}
          placeholder={placeholder}
          onChange={(e) => onChange(e.target.value)}
          className={`${boxClass} resize-y`}
        />
      ) : (
        <input
          id={id}
          ref={ref}
          value={value}
          readOnly={readOnly}
          placeholder={placeholder}
          onChange={(e) => onChange(e.target.value)}
          className={`${boxClass} h-8`}
        />
      )}
      {helper && <span className="adm-type-body-small text-(--admin-text-secondary)">{helper}</span>}
    </div>
  );
};

interface RowActionsProps {
  index: number;
  count: number;
  onMove: (from: number, to: number) => void;
  onRemove: (index: number) => void;
  readOnly?: boolean;
  removeLabel?: string;
}

/** Nút lên / xuống / xóa của một mục trong danh sách. */
export const RowActions: React.FC<RowActionsProps> = ({ index, count, onMove, onRemove, readOnly, removeLabel = 'Remove' }) =>
  readOnly ? null : (
    <div className="flex shrink-0 items-center gap-1">
      <AdminButton variant="ghost" aria-label="Move up" title="Move up" disabled={index === 0} onClick={() => onMove(index, index - 1)}>
        ↑
      </AdminButton>
      <AdminButton variant="ghost" aria-label="Move down" title="Move down" disabled={index === count - 1} onClick={() => onMove(index, index + 1)}>
        ↓
      </AdminButton>
      <AdminButton variant="ghost" aria-label={removeLabel} title={removeLabel} onClick={() => onRemove(index)}>
        ✕
      </AdminButton>
    </div>
  );

interface ListEditorProps {
  label: string;
  items: string[];
  onChange: (items: string[]) => void;
  catalog: ContractPlaceholderDto[];
  readOnly?: boolean;
  addLabel: string;
  placeholder?: string;
  /** Tiền tố hiển thị trước mỗi mục (VD "4.1."). */
  prefix?: (index: number) => string;
  rows?: number;
  helper?: string;
}

/** Danh sách đoạn chữ (căn cứ, khoản, ghi chú): thêm / sửa / sắp xếp / xóa. */
export const ListEditor: React.FC<ListEditorProps> = ({ label, items, onChange, catalog, readOnly, addLabel, placeholder, prefix, rows = 2, helper }) => (
  <div className="flex flex-col gap-2">
    <span className="adm-type-body-strong text-(--admin-text-primary)">{label}</span>
    {helper && <span className="adm-type-body-small -mt-1 text-(--admin-text-secondary)">{helper}</span>}
    {items.length === 0 && <span className="adm-type-body-small text-(--admin-text-secondary)">None.</span>}
    {items.map((item, i) => (
      <div key={i} className="flex items-start gap-2">
        {prefix && <span className="adm-type-body-strong w-10 shrink-0 pt-8 text-(--admin-text-secondary)">{prefix(i)}</span>}
        <TemplateText
          value={item}
          onChange={(v) => onChange(items.map((x, j) => (j === i ? v : x)))}
          catalog={catalog}
          readOnly={readOnly}
          multiline
          rows={rows}
          placeholder={placeholder}
        />
        <div className="pt-7">
          <RowActions index={i} count={items.length} readOnly={readOnly} onMove={(f, t) => onChange(move(items, f, t))} onRemove={(j) => onChange(items.filter((_, k) => k !== j))} />
        </div>
      </div>
    ))}
    {!readOnly && (
      <div>
        <AdminButton variant="secondary" onClick={() => onChange([...items, ''])}>
          {addLabel}
        </AdminButton>
      </div>
    )}
  </div>
);
