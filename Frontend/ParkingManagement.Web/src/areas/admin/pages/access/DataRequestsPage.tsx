import React, { useState } from 'react';
import { adminDataRequestsApi } from '@/api/adminIdentityApi';
import { DataRequestStatus, DataRequestType } from '@/types/adminEnums';
import { IDENTITY_PAGE_SIZE, type AdminDataRequestDto } from '@/types/adminIdentity';
import { ListScreen, QueryState } from '../../components/PageLayout';
import { Banner } from '../../components/Banner';
import { AdminButton } from '../../components/AdminButton';
import { AdminDrawer } from '../../components/AdminDrawer';
import { ActionDialog } from '../../components/ActionDialog';
import { DialogSummary } from '../../components/AdminDialog';
import { DetailList, DetailRow } from '../../components/DetailRow';
import { FilterColumn, FilterPanel } from '../../components/FilterColumn';
import { StatusIndicator } from '../../components/StatusIndicator';
import { SubTabs, type SubTab } from '../../components/SubTabs';
import { CellLink, CellMono, CellMuted, CellStatus, CellText, DataTable, type DataTableColumn } from '../../components/DataTable';
import { SimplePager } from '../../components/SimplePager';
import { useAdminQuery } from '../../hooks/useAdminQuery';
import { useAdminToast } from '../../hooks/useAdminToast';
import { useListParams } from '../../hooks/useListParams';
import { humanizeEnum } from '../../utils/enumLabels';
import { formatDateTimeIct, orEmpty } from '../../utils/format';

const STATUSES = Object.values(DataRequestStatus);
const TYPES = Object.values(DataRequestType);
const isStatus = (v: string): v is DataRequestStatus => (STATUSES as string[]).includes(v);
const isType = (v: string): v is DataRequestType => (TYPES as string[]).includes(v);

const requestCode = (id: number) => `DSR-${String(id).padStart(5, '0')}`;
const isOpen = (r: AdminDataRequestDto) => r.status === DataRequestStatus.Submitted || r.status === DataRequestStatus.InProgress;
const isOverdue = (r: AdminDataRequestDto) => isOpen(r) && new Date(r.dueAtUtc).getTime() < Date.now();

/** Mô tả ngắn từng loại yêu cầu để người duyệt biết cần làm gì. */
const TYPE_HELP: Record<string, string> = {
  Export: 'The user downloads the export themselves after re-entering their password; the request completes on download.',
  Access: 'The user views their data from Account → My data; no admin action is needed.',
  Correction: 'Fix the data the user described, then mark the request as corrected with a note of what changed.',
  Deletion: 'Approving anonymises the account: name, email, phone, avatar and identity documents are erased and every session is signed out.',
  WithdrawConsent: 'Record the withdrawal and reject with a note if it cannot be honoured.',
};

type HandleDialog = 'approveDeletion' | 'complete' | 'reject' | null;

const DataRequestDrawer: React.FC<{ request: AdminDataRequestDto; onClose: () => void; onDone: () => void }> = ({ request, onClose, onDone }) => {
  const toast = useAdminToast();
  const [dialog, setDialog] = useState<HandleDialog>(null);
  const open = isOpen(request);
  const code = requestCode(request.id);
  const summary = <DialogSummary title={`${code} · ${humanizeEnum(request.type)}`} subtitle={`User #${request.userId} · due ${formatDateTimeIct(request.dueAtUtc)}`} />;

  const finish = (title: string, message: string) => {
    toast.show({ type: 'success', title, message });
    onDone();
  };

  return (
    <>
      <AdminDrawer
        open
        width={560}
        title={code}
        description={`${humanizeEnum(request.type)} request · created ${formatDateTimeIct(request.createdAtUtc)}`}
        onClose={onClose}
      >
        <div className="adm-motion-fade flex flex-col gap-4">
          {isOverdue(request) && <Banner tone="danger">Past the legal deadline ({formatDateTimeIct(request.dueAtUtc)}). Handle this request first.</Banner>}

          <section className="border border-(--admin-border-default) p-3">
            <DetailList>
              <DetailRow label="Status">
                <StatusIndicator value={request.status} />
              </DetailRow>
              <DetailRow label="User">
                <CellLink to={`/admin/operations/users/${request.userId}`}>{`User #${request.userId}`}</CellLink>
              </DetailRow>
              <DetailRow label="Type">{humanizeEnum(request.type)}</DetailRow>
              <DetailRow label="Created">{formatDateTimeIct(request.createdAtUtc)}</DetailRow>
              <DetailRow label="Due">{formatDateTimeIct(request.dueAtUtc)}</DetailRow>
              <DetailRow label="Completed">{formatDateTimeIct(request.completedAtUtc)}</DetailRow>
              <DetailRow label="Handled by">{request.handledByUserId ? `Admin #${request.handledByUserId}` : '—'}</DetailRow>
              <DetailRow label="Note">{orEmpty(request.resultNote)}</DetailRow>
            </DetailList>
          </section>

          {TYPE_HELP[request.type] && <Banner tone="info">{TYPE_HELP[request.type]}</Banner>}

          {open && (
            <div className="flex flex-wrap justify-end gap-2 border-t border-(--admin-border-default) pt-4">
              <AdminButton variant="secondary" icon="x" onClick={() => setDialog('reject')}>
                Reject
              </AdminButton>
              {request.type === DataRequestType.Correction && (
                <AdminButton icon="check" onClick={() => setDialog('complete')}>
                  Mark corrected
                </AdminButton>
              )}
              {request.type === DataRequestType.Deletion && (
                <AdminButton variant="destructive" icon="trash" onClick={() => setDialog('approveDeletion')}>
                  Approve deletion
                </AdminButton>
              )}
            </div>
          )}
        </div>
      </AdminDrawer>

      <ActionDialog
        open={dialog === 'approveDeletion'}
        title="Approve deletion"
        description="The account is anonymised and locked, every session and pending OTP is revoked, staff assignments are removed. This cannot be undone."
        confirmLabel="Anonymise account"
        destructive
        field={{ label: 'Internal note', placeholder: 'Optional', maxLength: 1000 }}
        onClose={() => setDialog(null)}
        onSubmit={async (note) => {
          await adminDataRequestsApi.approveDeletion(request.id, note);
          finish('Account anonymised', `${code} completed; user #${request.userId} can no longer sign in.`);
        }}
      >
        {summary}
        <Banner tone="warning">Admin accounts and lot owners who have not finished leaving the platform cannot be anonymised.</Banner>
      </ActionDialog>

      <ActionDialog
        open={dialog === 'complete'}
        title="Mark as corrected"
        description="Status → Completed. The user sees your note on their request."
        confirmLabel="Mark corrected"
        field={{
          label: 'What was changed',
          required: true,
          requiredMessage: 'Describe what you corrected.',
          placeholder: 'For example: date of birth changed from 01/02 to 02/01/1998.',
          maxLength: 1000,
        }}
        onClose={() => setDialog(null)}
        onSubmit={async (note) => {
          await adminDataRequestsApi.completeCorrection(request.id, note);
          finish('Request completed', `${code} marked as corrected.`);
        }}
      >
        {summary}
      </ActionDialog>

      <ActionDialog
        open={dialog === 'reject'}
        title="Reject request"
        description="Status → Rejected. The user sees the reason on their request."
        confirmLabel="Reject"
        destructive
        field={{
          label: 'Reason shown to the user',
          required: true,
          requiredMessage: 'A reason is required.',
          maxLength: 1000,
        }}
        onClose={() => setDialog(null)}
        onSubmit={async (note) => {
          await adminDataRequestsApi.reject(request.id, note);
          finish('Request rejected', `${code} was rejected.`);
        }}
      >
        {summary}
      </ActionDialog>
    </>
  );
};

/**
 * Access & Configuration / Security & Privacy / Data subject requests (US-007, Nghị định 13/2023):
 * lọc theo trạng thái + loại, hạn xử lý theo luật, duyệt xóa / hoàn tất sửa / từ chối.
 */
export const DataRequestsPage: React.FC<{ subTabs: SubTab[] }> = ({ subTabs }) => {
  const params = useListParams();
  const page = params.page;
  const statusParam = params.get('status');
  const typeParam = params.get('type');
  const status = isStatus(statusParam) ? statusParam : undefined;
  const type = isType(typeParam) ? typeParam : undefined;
  const openId = Number(params.get('request')) || null;

  const query = useAdminQuery(() => adminDataRequestsApi.list({ status, type, page }), [status, type, page], (d) => d.length === 0);
  const rows = query.status === 'ready' ? query.data ?? [] : [];
  const open = rows.find((r) => r.id === openId) ?? null;
  const overdue = rows.filter(isOverdue).length;

  const columns: DataTableColumn<AdminDataRequestDto>[] = [
    { key: 'id', header: 'Request', width: 110, render: (r) => <CellMono>{requestCode(r.id)}</CellMono> },
    { key: 'user', header: 'User', width: 110, render: (r) => <CellLink to={`/admin/operations/users/${r.userId}`}>{`User #${r.userId}`}</CellLink> },
    { key: 'type', header: 'Type', width: 140, render: (r) => <CellText>{humanizeEnum(r.type)}</CellText> },
    { key: 'status', header: 'Status', width: 120, render: (r) => <CellStatus value={r.status} /> },
    { key: 'created', header: 'Created', width: 150, render: (r) => <CellMuted>{formatDateTimeIct(r.createdAtUtc)}</CellMuted> },
    {
      key: 'due',
      header: 'Due',
      width: 160,
      render: (r) =>
        isOverdue(r) ? (
          <StatusIndicator value="Overdue" tone="danger" label={`Overdue · ${formatDateTimeIct(r.dueAtUtc)}`} />
        ) : (
          <CellMuted>{formatDateTimeIct(r.dueAtUtc)}</CellMuted>
        ),
    },
    { key: 'handled', header: 'Handled by', width: 110, render: (r) => <CellText>{r.handledByUserId ? `Admin #${r.handledByUserId}` : '—'}</CellText> },
    {
      key: 'actions',
      header: '',
      width: 90,
      render: (r) => (
        <AdminButton variant="ghost" onClick={(e) => { e.stopPropagation(); params.set({ request: r.id }); }}>
          {isOpen(r) ? 'Handle' : 'View'}
        </AdminButton>
      ),
    },
  ];

  const subtitle =
    query.status === 'ready'
      ? `${rows.length} on page ${page}${overdue > 0 ? ` · ${overdue} overdue` : ''}`
      : query.status === 'empty'
        ? 'No requests match'
        : '—';

  return (
    <ListScreen
      title="Security & Privacy"
      subtitle={subtitle}
      actions={
        <AdminButton variant="onNavy" icon="refresh" onClick={query.refetch}>
          Refresh
        </AdminButton>
      }
      top={
        <>
          <SubTabs tabs={subTabs} />
          <Banner tone="info">Personal-data requests under Decree 13/2023 (US-007). Due is the legal deadline; oldest deadlines are listed first.</Banner>
        </>
      }
      filters={
        <FilterColumn>
          <FilterPanel
            title="Request status"
            value={status ?? ''}
            onChange={(v) => params.set({ status: v || null })}
            options={[{ value: '', label: 'All' }, ...STATUSES.map((s) => ({ value: s, label: humanizeEnum(s) }))]}
          />
          <FilterPanel
            title="Request type"
            value={type ?? ''}
            onChange={(v) => params.set({ type: v || null })}
            options={[{ value: '', label: 'Any' }, ...TYPES.map((t) => ({ value: t, label: humanizeEnum(t) }))]}
          />
        </FilterColumn>
      }
    >
      <DataTable
        columns={columns}
        rows={rows}
        rowKey={(r) => r.id}
        selectedKey={openId}
        onRowClick={(r) => params.set({ request: r.id })}
        busy={query.isFetching && query.data !== null}
        placeholder={
          <QueryState
            status={query.status}
            error={query.error}
            onRetry={query.refetch}
            copy={{
              emptyTitle: 'No data requests',
              emptyMessage: 'Requests appear when a user asks to export, correct or delete their data from Account → My data.',
              errorTitle: 'Could not load data requests',
              service: 'UserService',
            }}
          />
        }
        footer={
          (query.status === 'ready' || (query.status === 'empty' && page > 1)) && (
            <SimplePager page={page} pageSize={IDENTITY_PAGE_SIZE} rowCount={rows.length} onPageChange={params.setPage} />
          )
        }
      />

      {open && (
        <DataRequestDrawer
          key={open.id}
          request={open}
          onClose={() => params.set({ request: null })}
          onDone={() => {
            params.set({ request: null });
            query.refetch();
          }}
        />
      )}
    </ListScreen>
  );
};
