import React, { useState } from 'react';
import { adminKycApi } from '@/api/adminIdentityApi';
import { IDENTITY_PAGE_SIZE, type AdminKycItemDto } from '@/types/adminIdentity';
import { ListScreen, QueryState } from '../../../components/PageLayout';
import { Banner } from '../../../components/Banner';
import { AdminButton } from '../../../components/AdminButton';
import { AdminDrawer } from '../../../components/AdminDrawer';
import { ActionDialog } from '../../../components/ActionDialog';
import { DialogSummary } from '../../../components/AdminDialog';
import { DetailList, DetailRow } from '../../../components/DetailRow';
import { CellLink, CellMono, CellMuted, CellText, CellTwoLine, DataTable, type DataTableColumn } from '../../../components/DataTable';
import { SimplePager } from '../../../components/SimplePager';
import { useAdminQuery } from '../../../hooks/useAdminQuery';
import { useAdminToast } from '../../../hooks/useAdminToast';
import { useListParams } from '../../../hooks/useListParams';
import { formatDateTimeIct } from '../../../utils/format';

export const IDENTITY_BASE = '/admin/operations/identity-review';

const DOCUMENT_LABEL: Record<string, string> = { CitizenId: 'Citizen ID (CCCD)', DriverLicense: 'Driver licence' };
const documentLabel = (type: string) => DOCUMENT_LABEL[type] ?? type;

/** Thời gian chờ kể từ lúc gửi: "35 min", "5 h", "2 d". */
const waitingFor = (submittedAtUtc: string | null): string => {
  if (!submittedAtUtc) return '—';
  const minutes = Math.max(0, Math.floor((Date.now() - new Date(submittedAtUtc).getTime()) / 60000));
  if (minutes < 60) return `${minutes} min`;
  const hours = Math.floor(minutes / 60);
  return hours < 48 ? `${hours} h` : `${Math.floor(hours / 24)} d`;
};

type ReviewDialog = 'approve' | 'reject' | null;

/** Slide-over duyệt một hồ sơ: thông tin số CCCD đã che, nút Approve / Reject. */
const KycReviewDrawer: React.FC<{ item: AdminKycItemDto; onClose: () => void; onDone: () => void }> = ({ item, onClose, onDone }) => {
  const toast = useAdminToast();
  const [dialog, setDialog] = useState<ReviewDialog>(null);
  const summary = <DialogSummary title={item.fullName} subtitle={`${documentLabel(item.documentType)} · ${item.maskedNumber}`} />;

  const finish = (title: string, message: string) => {
    toast.show({ type: 'success', title, message });
    onDone();
  };

  return (
    <>
      <AdminDrawer
        open
        width={640}
        title={`Identity check · User #${item.userId}`}
        description={`Submitted ${formatDateTimeIct(item.submittedAtUtc)} · waiting ${waitingFor(item.submittedAtUtc)}`}
        onClose={onClose}
      >
        <div className="adm-motion-fade flex flex-col gap-4">
          <section className="border border-(--admin-border-default) p-3">
            <DetailList>
              <DetailRow label="Full name">{item.fullName}</DetailRow>
              <DetailRow label="Document">{documentLabel(item.documentType)}</DetailRow>
              <DetailRow label="Number">
                <span className="adm-type-body-mono">{item.maskedNumber}</span>
              </DetailRow>
              <DetailRow label="Submitted">{formatDateTimeIct(item.submittedAtUtc)}</DetailRow>
            </DetailList>
          </section>

          <Banner tone="info">
            Users verify with their 12-digit Citizen ID number through the VNeID link simulation. Check that the number belongs to the
            account holder before approving. The full number stays encrypted; only the last digits are shown.
          </Banner>

          <div className="flex flex-wrap justify-end gap-2 border-t border-(--admin-border-default) pt-4">
            <AdminButton variant="destructive" icon="x" onClick={() => setDialog('reject')}>
              Reject
            </AdminButton>
            <AdminButton icon="check" onClick={() => setDialog('approve')}>
              Approve
            </AdminButton>
          </div>
        </div>
      </AdminDrawer>

      <ActionDialog
        open={dialog === 'approve'}
        title="Approve identity"
        description="KycStatus Pending → Verified. The user sees the account as verified."
        confirmLabel="Approve"
        onClose={() => setDialog(null)}
        onSubmit={async () => {
          await adminKycApi.approve(item.userId);
          finish('Identity approved', `${item.fullName} is now verified.`);
        }}
      >
        {summary}
      </ActionDialog>

      <ActionDialog
        open={dialog === 'reject'}
        title="Reject identity"
        description="KycStatus → Rejected. The document number is deleted; the user can submit again."
        confirmLabel="Reject"
        destructive
        field={{
          label: 'Reason shown to the user',
          required: true,
          requiredMessage: 'A reason is required so the user knows what to fix.',
          placeholder: 'For example: the back side is blurred, please retake it in good light.',
          maxLength: 500,
        }}
        onClose={() => setDialog(null)}
        onSubmit={async (reason) => {
          await adminKycApi.reject(item.userId, reason);
          finish('Identity rejected', `${item.fullName} was asked to resubmit.`);
        }}
      >
        {summary}
      </ActionDialog>
    </>
  );
};

/** Operations / Identity Review (US-005): hàng chờ duyệt CCCD/GPLX của người dùng, sớm nhất lên trước. */
export const IdentityReviewPage: React.FC = () => {
  const params = useListParams();
  const page = params.page;
  const openUserId = Number(params.get('user')) || null;

  const query = useAdminQuery(() => adminKycApi.listPending(page), [page], (d) => d.length === 0);
  const rows = query.status === 'ready' ? query.data ?? [] : [];
  const open = rows.find((r) => r.userId === openUserId) ?? null;

  const columns: DataTableColumn<AdminKycItemDto>[] = [
    {
      key: 'user',
      header: 'User',
      render: (r) => <CellTwoLine primary={r.fullName} secondary={<CellLink to={`/admin/operations/users/${r.userId}`}>{`User #${r.userId}`}</CellLink>} />,
    },
    { key: 'doc', header: 'Document', width: 170, render: (r) => <CellText>{documentLabel(r.documentType)}</CellText> },
    { key: 'number', header: 'Number', width: 160, render: (r) => <CellMono>{r.maskedNumber}</CellMono> },
    { key: 'submitted', header: 'Submitted', width: 150, render: (r) => <CellMuted>{formatDateTimeIct(r.submittedAtUtc)}</CellMuted> },
    { key: 'waiting', header: 'Waiting', width: 90, render: (r) => <CellText>{waitingFor(r.submittedAtUtc)}</CellText> },
    {
      key: 'actions',
      header: '',
      width: 90,
      render: (r) => (
        <AdminButton variant="ghost" onClick={(e) => { e.stopPropagation(); params.set({ user: r.userId }); }}>
          Review
        </AdminButton>
      ),
    },
  ];

  const subtitle =
    query.status === 'ready' ? `${rows.length} waiting on page ${page}` : query.status === 'empty' ? 'Queue is clear' : '—';

  return (
    <ListScreen
      title="Identity Review"
      subtitle={subtitle}
      actions={
        <AdminButton variant="onNavy" icon="refresh" onClick={query.refetch}>
          Refresh
        </AdminButton>
      }
      top={
        <Banner tone="info">
          Citizen ID verifications submitted through the VNeID link simulation (US-005). Numbers are stored encrypted and shown masked.
        </Banner>
      }
    >
      <DataTable
        columns={columns}
        rows={rows}
        rowKey={(r) => r.userId}
        selectedKey={openUserId}
        onRowClick={(r) => params.set({ user: r.userId })}
        busy={query.isFetching && query.data !== null}
        placeholder={
          <QueryState
            status={query.status}
            error={query.error}
            onRetry={query.refetch}
            copy={{
              emptyTitle: 'No identity checks waiting',
              emptyMessage: 'Submissions appear here when a user sends their Citizen ID or driver licence from Account → Identity.',
              errorTitle: 'Could not load identity checks',
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
        <KycReviewDrawer
          key={open.userId}
          item={open}
          onClose={() => params.set({ user: null })}
          onDone={() => {
            params.set({ user: null });
            query.refetch();
          }}
        />
      )}
    </ListScreen>
  );
};
