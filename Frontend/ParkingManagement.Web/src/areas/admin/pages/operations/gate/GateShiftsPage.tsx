import React, { useState } from 'react';
import { adminOperationsApi } from '@/api/adminOperationsApi';
import { adminUsersApi } from '@/api/adminUsersApi';
import type { ShiftStatus, ShiftView } from '@/api/shiftApi';
import { ListScreen, QueryState } from '../../../components/PageLayout';
import { ToolbarRow, ToolbarSelect } from '../../../components/Toolbar';
import { SubTabs } from '../../../components/SubTabs';
import { CellAmount, CellMuted, CellText, DataTable, type DataTableColumn } from '../../../components/DataTable';
import { Pagination } from '../../../components/Pagination';
import { KpiStrip, KpiTile } from '../../../components/KpiTile';
import { Banner } from '../../../components/Banner';
import { AdminButton } from '../../../components/AdminButton';
import { AdminDrawer } from '../../../components/AdminDrawer';
import { DetailRow } from '../../../components/DetailRow';
import { LifecycleDialog } from '../../../components/LifecycleDialog';
import { StateBlock } from '../../../components/StateBlock';
import { useAdminQuery } from '../../../hooks/useAdminQuery';
import { useAdminToast } from '../../../hooks/useAdminToast';
import { useLotOptions } from '../../../hooks/useLotOptions';
import { DEFAULT_PAGE_SIZE, useListParams } from '../../../hooks/useListParams';
import { formatDate, formatDateTime, formatVnd } from '../../../utils/format';
import { GATE_SUB_TABS } from './gateTabs';

const STATUS_LABEL: Record<ShiftStatus, string> = {
  Scheduled: 'Scheduled',
  Open: 'Active',
  HandoverPending: 'Handover pending',
  Completed: 'Completed',
  Missed: 'Missed',
  AutoClosed: 'Auto-closed',
};
const STATUSES = Object.keys(STATUS_LABEL) as ShiftStatus[];
const timeFmt = new Intl.DateTimeFormat('en-GB', { timeZone: 'Asia/Ho_Chi_Minh', hour: '2-digit', minute: '2-digit', hour12: false });
const range = (s: ShiftView) => `${timeFmt.format(new Date(s.scheduledStartUtc))}–${timeFmt.format(new Date(s.scheduledEndUtc))}`;
/** Giá trị chưa có hiện "Pending" / "—", không hiện 0. */
const cash = (s: ShiftView) => (s.cashSystem !== null ? formatVnd(s.cashSystem) : s.endedAtUtc ? 'Pending' : '—');
const difference = (s: ShiftView) => (s.discrepancyStatus ?? (s.status === 'AutoClosed' && s.cashCounted === null ? 'Not counted' : '—'));

/** Operations / Gate Activity / Shifts (Figma Staff & Shifts / Admin 01–06) – chỉ xem; thao tác duy nhất là đình chỉ tài khoản nhân viên. */
export const GateShiftsPage: React.FC = () => {
  const params = useListParams();
  const lotParam = params.get('lot');
  const statusParam = params.get('status') as ShiftStatus;
  const status = STATUSES.includes(statusParam) ? statusParam : undefined;
  const from = params.get('from');
  const to = params.get('to');
  const page = params.page;
  const [selected, setSelected] = useState<ShiftView | null>(null);

  // Ngày theo giờ Việt Nam; "to" tính trọn ngày.
  const filter = {
    parkingLotId: Number(lotParam) || undefined,
    fromUtc: from ? new Date(`${from}T00:00:00+07:00`).toISOString() : undefined,
    toUtc: to ? new Date(Date.parse(`${to}T00:00:00+07:00`) + 86_400_000).toISOString() : undefined,
  };
  const key = [lotParam, status, from, to, page];
  const query = useAdminQuery(() => adminOperationsApi.getShifts({ ...filter, status, page, pageSize: DEFAULT_PAGE_SIZE }), key, (d) => d.items.length === 0);
  const totals = useAdminQuery(
    () => Promise.all((['Open', 'Missed'] as const).map((s) => adminOperationsApi.getShifts({ ...filter, status: s, page: 1, pageSize: 1 }).then((r) => r.total))),
    [lotParam, from, to]
  );
  const ids = [...new Set(query.data?.items.map((s) => s.staffUserId) ?? [])];
  // Tên người trực tra từ UserService; không tra được thì giữ "User #id".
  const users = useAdminQuery(
    ids.length ? () => Promise.all(ids.map((id) => adminUsersApi.getById(id).then((u) => [id, u.fullName] as const, () => [id, `User #${id}`] as const))) : null,
    [ids.join()]
  );
  const lots = useLotOptions();
  const lotName = (id: number) => lots.byId(id)?.name ?? `Lot #${id}`;
  const userName = (id: number) => users.data?.find(([key]) => key === id)?.[1] ?? `User #${id}`;

  const columns: DataTableColumn<ShiftView>[] = [
    { key: 'shift', header: 'Shift / date', render: (s) => (<span><CellText>{range(s)}</CellText><br /><CellMuted>{formatDate(s.scheduledStartUtc)}</CellMuted></span>) },
    { key: 'lot', header: 'Parking lot', render: (s) => <CellText>{lotName(s.parkingLotId)}</CellText> },
    { key: 'staff', header: 'Staff', render: (s) => <CellText>{userName(s.staffUserId)}</CellText> },
    { key: 'status', header: 'Status', render: (s) => <CellText>{STATUS_LABEL[s.status]}</CellText> },
    { key: 'cash', header: 'Cash system', render: (s) => <CellAmount>{cash(s)}</CellAmount> },
    { key: 'diff', header: 'Discrepancy', render: (s) => <CellText>{difference(s)}</CellText> },
    { key: 'flag', header: 'Flag', render: (s) => <CellText>{s.isFlagged ? 'Flagged' : '—'}</CellText> },
    { key: 'actions', header: 'Actions', render: (s) => <AdminButton variant="ghost" onClick={() => setSelected(s)}>Details</AdminButton> },
  ];

  const dateInput = (label: string, name: 'from' | 'to', value: string) => (
    <label className="adm-type-body-default flex h-8 items-center gap-2 rounded-[2px] border border-(--admin-border-default) bg-(--admin-surface-card) px-2.5 text-(--admin-text-secondary)">
      {label}
      <input type="date" value={value} onChange={(e) => params.set({ [name]: e.target.value || null })} className="bg-transparent text-(--admin-text-primary) outline-none" />
    </label>
  );

  return (
    <ListScreen
      title="Gate Activity"
      subtitle="Read-only shift monitoring · All times in ICT (UTC+7)"
      top={
        <>
          <SubTabs tabs={GATE_SUB_TABS} />
          <Banner>View and suspend only. Scheduling, gate operations and cash reconciliation remain with the owner and gate operator.</Banner>
        </>
      }
      toolbar={
        <ToolbarRow>
          <ToolbarSelect label="Lot" value={lotParam} width={260} onChange={(v) => params.set({ lot: v || null })}
            options={[{ value: '', label: 'All parking lots' }, ...lots.lots.map((l) => ({ value: String(l.parkingLotId), label: `${l.name} · ${l.city}` }))]} />
          <ToolbarSelect label="Status" value={status ?? ''} width={200} onChange={(v) => params.set({ status: v || null })}
            options={[{ value: '', label: 'All statuses' }, ...STATUSES.map((s) => ({ value: s, label: STATUS_LABEL[s] }))]} />
          {dateInput('From', 'from', from)}
          {dateInput('To', 'to', to)}
        </ToolbarRow>
      }
    >
      <KpiStrip>
        {(['Active shifts', 'Missed shifts'] as const).map((label, i) => (
          <KpiTile key={label} label={label} loading={totals.status === 'loading'} value={totals.data ? String(totals.data[i]) : '—'} muted={!totals.data} />
        ))}
      </KpiStrip>
      <DataTable
        columns={columns}
        rows={query.status === 'ready' ? query.data!.items : []}
        rowKey={(s) => s.id}
        placeholder={
          <QueryState status={query.status} error={query.error} onRetry={query.refetch}
            copy={{ emptyTitle: 'No matching shifts', emptyMessage: 'Try another lot, status or date range.', errorTitle: 'Unable to load shifts', service: 'GateService' }} />
        }
        footer={query.status === 'ready' && <Pagination page={page} pageSize={DEFAULT_PAGE_SIZE} totalCount={query.data!.total} onPageChange={params.setPage} />}
      />
      {selected && (
        <ShiftDrawer shift={selected} lotName={lotName(selected.parkingLotId)} staffName={userName(selected.staffUserId)}
          onClose={() => setSelected(null)} onSuspended={() => { setSelected(null); query.refetch(); totals.refetch(); }} />
      )}
    </ListScreen>
  );
};

const ShiftDrawer: React.FC<{ shift: ShiftView; lotName: string; staffName: string; onClose: () => void; onSuspended: () => void }> =
  ({ shift, lotName, staffName, onClose, onSuspended }) => {
    const toast = useAdminToast();
    const [suspending, setSuspending] = useState(false);
    const summary = useAdminQuery(() => adminOperationsApi.getShiftSummary(shift.id), [shift.id]);
    const user = useAdminQuery(() => adminUsersApi.getById(shift.staffUserId), [shift.staffUserId]);
    const data = summary.data;
    // Chủ bãi tự trực được khóa ở màn Users (khóa chủ bãi), không đình chỉ tại đây.
    const isStaff = !!user.data?.roles.includes('Staff') && !user.data.roles.includes('LotOwner');

    return (
      <AdminDrawer open title="Shift details" description={`${staffName} · ${lotName}`} onClose={onClose}>
        <div>
          <DetailRow label="Scheduled / actual start">{`${range(shift)} / ${shift.startedAtUtc ? formatDateTime(shift.startedAtUtc) : '—'}`}</DetailRow>
          <DetailRow label="Ended">{formatDateTime(shift.endedAtUtc)}</DetailRow>
          <DetailRow label="Status / flag">{`${STATUS_LABEL[shift.status]} / ${shift.isFlagged ? 'Flagged' : 'Not flagged'}`}</DetailRow>
          <DetailRow label="Closed reason">{shift.closedReason ?? '—'}</DetailRow>
          <DetailRow label="Cash system / counted">{`${cash(shift)} / ${shift.cashCounted === null ? 'Not submitted' : formatVnd(shift.cashCounted)}`}</DetailRow>
          <DetailRow label="Cash received">{shift.cashReceived === null ? '—' : formatVnd(shift.cashReceived)}</DetailRow>
          <DetailRow label="Discrepancy">{difference(shift)}</DetailRow>
          {data && (
            <>
              <DetailRow label="Entries / exits">{`${data.entries} / ${data.exits}`}</DetailRow>
              <DetailRow label="Vehicles in lot">{data.vehiclesInLot}</DetailRow>
              <DetailRow label="Exceptions / PMS pending">{`${data.exceptions} / ${data.pmsPending}`}</DetailRow>
              <DetailRow label="Manual opens / no-show cancels">{`${data.manualBarrierOpens} / ${data.noShowCancels}`}</DetailRow>
            </>
          )}
          <DetailRow label="Account status">{user.data?.status ?? '—'}</DetailRow>
        </div>
        {summary.status === 'loading' && <StateBlock type="loading" bare />}
        {summary.error && <Banner tone="danger">Shift totals could not be loaded: {summary.error.message}</Banner>}
        <Banner>This is a read-only view. Suspending the account closes the open shift and blocks gate actions on every assignment.</Banner>
        {isStaff && (
          <div>
            <AdminButton variant="destructive" disabled={user.data?.status !== 'Active'} onClick={() => setSuspending(true)}>Suspend staff</AdminButton>
          </div>
        )}
        <LifecycleDialog
          open={suspending}
          title="Suspend staff account?"
          description={`${staffName} · Account-level suspension`}
          confirmLabel="Suspend account"
          destructive
          reasonLabel="Reason for suspension"
          onClose={() => setSuspending(false)}
          onSubmit={async (reason) => {
            await adminUsersApi.suspendUser(shift.staffUserId, { reason, lockedUntilUtc: null, rowVersion: user.data?.rowVersion });
            toast.show({ type: 'success', title: 'Staff suspended', message: 'The open shift is closed automatically within a few seconds.' });
            onSuspended();
          }}
        >
          <Banner tone="danger">The current shift will be closed automatically. The owner will review outstanding cash totals.</Banner>
        </LifecycleDialog>
      </AdminDrawer>
    );
  };
