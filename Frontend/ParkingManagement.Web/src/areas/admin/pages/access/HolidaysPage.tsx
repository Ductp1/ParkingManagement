import React, { useState } from 'react';
import { adminHolidaysApi } from '@/api/adminHolidaysApi';
import type { HolidayDto } from '@/types/adminHolidays';
import { ListScreen } from '../../components/PageLayout';
import { ActionDialog } from '../../components/ActionDialog';
import { AdminButton } from '../../components/AdminButton';
import { CellMuted, CellText, CellTwoLine, DataTable, type DataTableColumn } from '../../components/DataTable';
import { FormDialog, type FormDialogSpec, type FormValues } from '../../components/FormDialog';
import { StateBlock } from '../../components/StateBlock';
import { ToolbarRow, ToolbarSelect } from '../../components/Toolbar';
import { useAdminQuery } from '../../hooks/useAdminQuery';
import { useAdminToast } from '../../hooks/useAdminToast';
import { useListParams } from '../../hooks/useListParams';
import { formatNumber } from '../../utils/format';
import { MAX_HOLIDAY_YEAR, MIN_HOLIDAY_YEAR, currentIctYear, formatHolidayDate, holidayWeekday, holidayYearOptions, parseHolidayYear } from './holidayFormat';

/** Tên ngày lễ tối đa 100 ký tự (cột Holidays.Name của PaymentDb). */
const NAME_MAX_LENGTH = 100;

const yearOf = (date: string): number => Number(date.slice(0, 4));

const ADD_HOLIDAY: FormDialogSpec = {
  title: 'Add holiday',
  description: 'Holiday rates apply at every parking lot on this date.',
  confirmLabel: 'Add holiday',
  fields: [
    {
      key: 'date',
      kind: 'date',
      label: 'Date',
      required: true,
      validate: (value) => {
        if (typeof value !== 'string' || value === '') return null;
        const year = yearOf(value);
        return year >= MIN_HOLIDAY_YEAR && year <= MAX_HOLIDAY_YEAR ? null : `Pick a date between ${MIN_HOLIDAY_YEAR} and ${MAX_HOLIDAY_YEAR}.`;
      },
    },
    { key: 'name', kind: 'text', label: 'Name', required: true, maxLength: NAME_MAX_LENGTH, placeholder: 'e.g. Reunification Day' },
  ],
};

/**
 * Access & Configuration / Holidays (US-071): ngày lễ toàn quốc dùng để áp giá ngày lễ khi báo giá.
 * Lọc theo năm (giữ trên URL ?year=), thêm ngày lễ, xóa có xác nhận. Không có số liệu mẫu: chỉ hiển thị dữ liệu API.
 */
export const HolidaysPage: React.FC = () => {
  const params = useListParams();
  const toast = useAdminToast();
  // Năm hiện tại theo ICT – chốt một lần khi mở màn.
  const [currentYear] = useState(currentIctYear);
  const year = parseHolidayYear(params.get('year'), currentYear);

  const query = useAdminQuery(() => adminHolidaysApi.list(year), [year], (rows) => rows.length === 0);
  const rows = query.status === 'ready' ? (query.data ?? []) : [];

  const [addKey, setAddKey] = useState<number | null>(null);
  const [toDelete, setToDelete] = useState<HolidayDto | null>(null);

  const add = async (values: FormValues) => {
    const date = String(values.date);
    const created = await adminHolidaysApi.create({ date, name: String(values.name).trim() });
    toast.show({ type: 'success', title: 'Holiday added', message: `${created.name} · ${formatHolidayDate(created.date)}` });
    if (yearOf(created.date) !== year) params.set({ year: yearOf(created.date) });
    else query.refetch();
  };

  const remove = async () => {
    if (!toDelete) return;
    await adminHolidaysApi.remove(toDelete.id);
    toast.show({ type: 'success', title: 'Holiday deleted', message: `${toDelete.name} · ${formatHolidayDate(toDelete.date)}` });
    query.refetch();
  };

  // Date / City theo % để cột Name luôn còn chỗ khi bảng hẹp (768px: bảng ~514px).
  const columns: DataTableColumn<HolidayDto>[] = [
    {
      key: 'date',
      header: 'Date',
      width: '20%',
      render: (r) => <CellTwoLine primary={formatHolidayDate(r.date)} secondary={holidayWeekday(r.date)} />,
    },
    { key: 'name', header: 'Name', render: (r) => <CellText>{r.name}</CellText> },
    { key: 'city', header: 'City', width: '18%', render: (r) => (r.city ? <CellText>{r.city}</CellText> : <CellMuted>Nationwide</CellMuted>) },
    {
      key: 'actions',
      header: '',
      width: 96,
      align: 'right',
      render: (r) => (
        <AdminButton variant="ghost" icon="trash" onClick={() => setToDelete(r)} aria-label={`Delete ${r.name} (${formatHolidayDate(r.date)})`}>
          Delete
        </AdminButton>
      ),
    },
  ];

  const placeholder = (() => {
    switch (query.status) {
      case 'loading':
        return <StateBlock type="loading" bare />;
      case 'empty':
        return <StateBlock type="empty" title={`No holidays in ${year}`} message="Add the public holidays for this year so holiday rates apply on those dates." bare />;
      case 'forbidden':
        return <StateBlock type="noPermission" message="Only administrators can manage holidays." bare />;
      case 'error':
      case 'unavailable':
        return (
          <StateBlock
            type="error"
            title="Could not load holidays"
            message={query.error?.problem ? query.error.message : 'The holiday list could not be loaded. Try again.'}
            onRetry={query.refetch}
            bare
          />
        );
      default:
        return null;
    }
  })();

  return (
    <ListScreen
      title="Holidays"
      subtitle={rows.length > 0 ? `${formatNumber(rows.length)} ${rows.length === 1 ? 'holiday' : 'holidays'} in ${year} · holiday rates apply at every lot` : undefined}
      actions={
        <AdminButton variant="onNavy" icon="plus" onClick={() => setAddKey(Date.now())}>
          Add holiday
        </AdminButton>
      }
      toolbar={
        <ToolbarRow>
          <ToolbarSelect
            label="Year"
            value={String(year)}
            width={120}
            options={holidayYearOptions(currentYear, year).map((y) => ({ value: String(y), label: String(y) }))}
            onChange={(value) => params.set({ year: Number(value) === currentYear ? null : value })}
          />
        </ToolbarRow>
      }
    >
      <DataTable columns={columns} rows={rows} rowKey={(r) => r.id} busy={query.isFetching && rows.length > 0} placeholder={placeholder} />

      {addKey !== null && <FormDialog key={addKey} spec={ADD_HOLIDAY} open onClose={() => setAddKey(null)} onSubmit={add} />}

      <ActionDialog
        open={toDelete !== null}
        title="Delete holiday"
        description={
          toDelete
            ? `${toDelete.name} (${formatHolidayDate(toDelete.date)}) will be removed. Quotes made after this will not apply holiday rates on that date.`
            : undefined
        }
        confirmLabel="Delete holiday"
        destructive
        onClose={() => setToDelete(null)}
        onSubmit={remove}
      />
    </ListScreen>
  );
};
