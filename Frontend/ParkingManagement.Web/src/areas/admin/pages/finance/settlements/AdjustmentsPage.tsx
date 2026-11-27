import React, { useState } from 'react';
import { adminSettlementsApi } from '@/api/adminSettlementsApi';
import type { FinancialAdjustmentDto, FinancialAdjustmentStatus } from '@/types/adminSettlements';
import { ActionDialog } from '../../../components/ActionDialog';
import { AdminButton } from '../../../components/AdminButton';
import { AdminDialog } from '../../../components/AdminDialog';
import { Banner } from '../../../components/Banner';
import { CellLink, CellMuted, CellTwoLine, DataTable, type DataTableColumn } from '../../../components/DataTable';
import { FilterColumn, FilterPanel } from '../../../components/FilterColumn';
import { TextAreaField, TextField } from '../../../components/FormFields';
import { ListScreen, QueryState } from '../../../components/PageLayout';
import { Pagination } from '../../../components/Pagination';
import { StatusIndicator } from '../../../components/StatusIndicator';
import { SearchBox, ToolbarRow, ToolbarSelect } from '../../../components/Toolbar';
import { useAdminQuery } from '../../../hooks/useAdminQuery';
import { useAdminToast } from '../../../hooks/useAdminToast';
import { DEFAULT_PAGE_SIZE, useListParams } from '../../../hooks/useListParams';
import { EMPTY, formatDateTimeIct, formatNumber, formatVnd } from '../../../utils/format';
import { Money } from './SettlementCells';
import { actionError, formatSignedVnd, ownerLabel, ownerLink, SETTLEMENTS_PATH } from './settlementFormat';
import { useOwnerAccounts } from './useOwnerAccounts';

const STATUSES: FinancialAdjustmentStatus[] = ['PendingApproval', 'Approved', 'Rejected'];
const isStatus = (value: string): value is FinancialAdjustmentStatus => (STATUSES as string[]).includes(value);
const positiveInt = (value: string): number | null => (/^\d+$/.test(value.trim()) && Number(value) > 0 ? Number(value) : null);

const SOURCE_LABEL: Record<string, string> = { Manual: 'Manual', CompensationVoucher: 'Compensation voucher', Sanction: 'SLA sanction' };

/** Giá trị nhập ban đầu khi đảo một bút toán đã duyệt. */
interface CreatePrefill {
  ownerProfileId: number;
  amount: number;
  reverses: FinancialAdjustmentDto;
}

/**
 * Tạo bút toán: chủ bãi (Id + tên tra từ AdminService), số tiền ± (≠ 0, số nguyên VND), lý do bắt buộc, chế tài tùy chọn.
 * Chỉ thêm – không sửa / xóa được; tạo xong chờ một Admin khác duyệt.
 */
export const CreateAdjustmentDialog: React.FC<{
  prefill: CreatePrefill | null;
  onClose: () => void;
  onDone: (created: FinancialAdjustmentDto) => void;
}> = ({ prefill, onClose, onDone }) => {
  const [owner, setOwner] = useState(prefill ? String(prefill.ownerProfileId) : '');
  const [amount, setAmount] = useState(prefill ? String(prefill.amount) : '');
  const [reason, setReason] = useState(prefill ? `Reverse adjustment #${prefill.reverses.id}: ` : '');
  const [sanction, setSanction] = useState('');
  const [submitted, setSubmitted] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [apiError, setApiError] = useState<string | null>(null);

  const ownerId = positiveInt(owner);
  const lookup = useOwnerAccounts(ownerId ? [ownerId] : []);
  const account = ownerId ? lookup.byOwner.get(ownerId) : undefined;
  const amountValue = Number(amount);
  const errors = {
    owner: ownerId === null ? 'Enter the owner profile ID.' : null,
    amount:
      amount.trim() === '' || !Number.isFinite(amountValue)
        ? 'Enter an amount.'
        : amountValue === 0
          ? 'Amount must not be 0.'
          : !Number.isInteger(amountValue)
            ? 'Amount must be whole VND.'
            : Math.abs(amountValue) > 10_000_000_000
              ? 'Amount exceeds 10 billion VND.'
              : null,
    reason: reason.trim() === '' ? 'A reason is required.' : reason.trim().length > 500 ? 'Max 500 characters.' : null,
    sanction: sanction.trim() !== '' && positiveInt(sanction) === null ? 'Sanction ID must be a positive whole number.' : null,
  };
  const invalid = Object.values(errors).some((e) => e !== null);

  const confirm = async () => {
    setSubmitted(true);
    if (invalid || ownerId === null) return;
    setSubmitting(true);
    setApiError(null);
    try {
      onDone(
        await adminSettlementsApi.createAdjustment({
          ownerProfileId: ownerId,
          amount: amountValue,
          reason: reason.trim(),
          sanctionId: positiveInt(sanction),
          reversesAdjustmentId: prefill?.reverses.id ?? null,
        })
      );
    } catch (err: unknown) {
      setApiError(actionError(err).message);
      setSubmitting(false);
    }
  };

  const ownerHelper =
    ownerId === null ? undefined : lookup.status === 'loading' ? 'Looking up…' : account ? account.ownerName : 'No partner profile found for this owner ID.';

  return (
    <AdminDialog
      open
      title={prefill ? `Reverse adjustment #${prefill.reverses.id}` : 'Create adjustment'}
      description="Adds a financial adjustment. Positive = credit to the owner, negative = debit. Applied to the owner's next Draft settlement after approval."
      onClose={onClose}
      onConfirm={confirm}
      confirmLabel="Create adjustment"
      submitting={submitting}
      error={apiError}
      footerNote="Needs approval by a different admin."
    >
      <Banner tone="warning">Adjustments cannot be edited or deleted. To correct one, create an opposite (reversing) adjustment.</Banner>
      <TextField
        label="Owner profile ID"
        type="number"
        required
        value={owner}
        onChange={setOwner}
        readOnly={prefill !== null}
        helper={ownerHelper}
        error={submitted ? errors.owner : null}
      />
      <TextField
        label="Amount (₫)"
        type="number"
        required
        value={amount}
        onChange={setAmount}
        readOnly={prefill !== null}
        helper={Number.isFinite(amountValue) && amountValue !== 0 ? `${amountValue > 0 ? 'Credit' : 'Debit'} ${formatVnd(Math.abs(amountValue))}` : 'Positive = credit, negative = debit'}
        error={submitted ? errors.amount : null}
      />
      <TextAreaField label="Reason" required value={reason} onChange={setReason} maxLength={500} error={submitted ? errors.reason : null} />
      {!prefill && (
        <TextField
          label="Sanction ID (optional)"
          type="number"
          value={sanction}
          onChange={setSanction}
          helper="Link a penalty to an SLA sanction (US-097)."
          error={submitted ? errors.sanction : null}
        />
      )}
    </AdminDialog>
  );
};

type Pending = { kind: 'approve' | 'reject'; adjustment: FinancialAdjustmentDto } | null;

/**
 * Finance / Adjustments (US-080 AC3): bút toán điều chỉnh có lý do và người duyệt – chỉ thêm, không sửa / xóa.
 * Bộ lọc (trạng thái, đã gắn kỳ, chủ bãi, tìm kiếm, trang) nằm trên URL.
 */
export const AdjustmentsPage: React.FC = () => {
  const toast = useAdminToast();
  const params = useListParams();
  const page = params.page;
  const statusParam = params.get('status');
  const attachedParam = params.get('attached');
  const query = {
    status: isStatus(statusParam) ? statusParam : null,
    attached: attachedParam === 'true' ? true : attachedParam === 'false' ? false : null,
    ownerProfileId: positiveInt(params.get('owner')),
    search: params.get('q') || null,
  };
  const queryKey = JSON.stringify(query);
  const [creating, setCreating] = useState<CreatePrefill | null | false>(false);
  const [pending, setPending] = useState<Pending>(null);

  const list = useAdminQuery(() => adminSettlementsApi.adjustments(query, page, DEFAULT_PAGE_SIZE), [queryKey, page], (d) => d.items.length === 0);
  const rows = list.data?.items ?? [];
  const { byOwner } = useOwnerAccounts(rows.map((r) => r.ownerProfileId));
  const reversed = new Set(rows.map((r) => r.reversesAdjustmentId).filter((id): id is number => id !== null));

  /** 409 → tải lại rồi ném lỗi để dialog hiển thị. */
  const run = async (label: string, call: () => Promise<unknown>) => {
    try {
      await call();
    } catch (err: unknown) {
      const e = actionError(err);
      if (e.conflict) list.refetch();
      throw new Error(e.message);
    }
    toast.show({ type: 'success', title: label });
    list.refetch();
  };

  const columns: DataTableColumn<FinancialAdjustmentDto>[] = [
    { key: 'id', header: '#', width: 70, render: (r) => <CellMuted>#{r.id}</CellMuted> },
    {
      key: 'owner',
      header: 'Owner',
      render: (r) => {
        const account = byOwner.get(r.ownerProfileId);
        const link = ownerLink(account);
        return (
          <CellTwoLine
            primary={link ? <CellLink to={link}>{ownerLabel(r.ownerProfileId, account)}</CellLink> : ownerLabel(r.ownerProfileId, account)}
            secondary={`OwnerProfile #${r.ownerProfileId}`}
          />
        );
      },
    },
    {
      key: 'settlement',
      header: 'Settlement',
      width: 170,
      render: (r) =>
        r.settlementId ? (
          <CellLink to={`${SETTLEMENTS_PATH}?settlement=${r.settlementId}`}>{r.settlementCode ?? `#${r.settlementId}`}</CellLink>
        ) : (
          <CellMuted>{r.status === 'Approved' ? 'Next draft' : EMPTY}</CellMuted>
        ),
    },
    { key: 'amount', header: 'Amount', width: 120, align: 'right', render: (r) => <Money value={r.amount} /> },
    {
      key: 'reason',
      header: 'Reason',
      render: (r) => (
        <CellTwoLine
          primary={r.reason}
          secondary={[SOURCE_LABEL[r.sourceType] ?? r.sourceType, r.sourceReference, r.reversesAdjustmentId ? `reverses #${r.reversesAdjustmentId}` : null]
            .filter(Boolean)
            .join(' · ')}
        />
      ),
    },
    {
      key: 'status',
      header: 'Status',
      width: 150,
      render: (r) => (
        <CellTwoLine
          primary={<StatusIndicator value={r.status} />}
          secondary={r.approvedByUserId ? `by Admin #${r.approvedByUserId}` : r.rejectReason ?? undefined}
        />
      ),
    },
    {
      key: 'created',
      header: 'Created by',
      width: 170,
      render: (r) => <CellTwoLine primary={`Admin #${r.createdByUserId}`} secondary={formatDateTimeIct(r.createdAtUtc)} />,
    },
    {
      key: 'actions',
      header: '',
      width: 180,
      render: (r) =>
        r.status === 'PendingApproval' ? (
          <span className="flex gap-1">
            <AdminButton variant="secondary" onClick={() => setPending({ kind: 'approve', adjustment: r })}>Approve</AdminButton>
            <AdminButton variant="ghost" onClick={() => setPending({ kind: 'reject', adjustment: r })}>Reject</AdminButton>
          </span>
        ) : r.status === 'Approved' && r.reversesAdjustmentId === null && !reversed.has(r.id) ? (
          <AdminButton variant="ghost" onClick={() => setCreating({ ownerProfileId: r.ownerProfileId, amount: -r.amount, reverses: r })}>
            Reverse
          </AdminButton>
        ) : null,
    },
  ];

  const hasFilter = Object.values(query).some((v) => v !== null);

  return (
    <ListScreen
      title="Adjustments"
      subtitle={list.data ? `${formatNumber(list.data.totalCount)} adjustments · positive = credit to owner, negative = debit` : 'FinancialAdjustments'}
      actions={
        <AdminButton variant="onNavy" icon="plus" onClick={() => setCreating(null)}>
          Create adjustment
        </AdminButton>
      }
      top={
        <Banner tone="info">
          Append-only: adjustments are never edited or deleted. Each needs a reason and approval by a different admin; approved adjustments are attached to the
          owner&apos;s next Draft settlement when it is generated or regenerated.
        </Banner>
      }
      filters={
        <FilterColumn>
          <FilterPanel
            title="Approval"
            value={query.status ?? ''}
            onChange={(v) => params.set({ status: v || null })}
            options={[
              { value: '', label: 'All' },
              { value: 'PendingApproval', label: 'Pending approval' },
              { value: 'Approved', label: 'Approved' },
              { value: 'Rejected', label: 'Rejected' },
            ]}
          />
        </FilterColumn>
      }
      toolbar={
        <ToolbarRow>
          <SearchBox placeholder="Search reason or source reference…" width={280} value={params.get('q')} onSearch={(q) => params.set({ q: q || null })} />
          <SearchBox placeholder="Owner profile ID" width={170} value={params.get('owner')} onSearch={(v) => params.set({ owner: v.trim() || null })} />
          <ToolbarSelect
            label="Settlement"
            width={170}
            value={attachedParam}
            options={[{ value: '', label: 'Any' }, { value: 'true', label: 'Attached' }, { value: 'false', label: 'Not attached' }]}
            onChange={(v) => params.set({ attached: v || null })}
          />
          {hasFilter && (
            <AdminButton variant="ghost" className="adm-motion-fade" onClick={() => params.set({ status: null, attached: null, owner: null, q: null })}>
              Clear filters
            </AdminButton>
          )}
        </ToolbarRow>
      }
    >
      <DataTable
        animateRows
        busy={list.isFetching && list.data !== null}
        columns={columns}
        rows={list.status === 'ready' ? rows : []}
        rowKey={(r) => r.id}
        placeholder={
          <QueryState
            status={list.status}
            error={list.error}
            onRetry={list.refetch}
            copy={{
              emptyTitle: hasFilter ? 'No adjustments match' : 'No adjustments yet',
              emptyMessage: hasFilter ? 'Try other filters.' : 'Create an adjustment to correct a closed settlement.',
              errorTitle: 'Could not load adjustments',
              service: 'PaymentService',
            }}
          />
        }
        footer={
          list.data && list.data.totalCount > DEFAULT_PAGE_SIZE && (
            <Pagination page={page} pageSize={DEFAULT_PAGE_SIZE} totalCount={list.data.totalCount} onPageChange={params.setPage} />
          )
        }
      />

      {creating !== false && (
        <CreateAdjustmentDialog
          prefill={creating}
          onClose={() => setCreating(false)}
          onDone={(created) => {
            setCreating(false);
            toast.show({ type: 'success', title: `Adjustment #${created.id} created`, message: 'Waiting for approval by another admin.' });
            list.refetch();
          }}
        />
      )}
      {pending?.kind === 'approve' && (
        <ActionDialog
          open
          title={`Approve adjustment #${pending.adjustment.id}?`}
          description={`${formatSignedVnd(pending.adjustment.amount)} for ${ownerLabel(pending.adjustment.ownerProfileId, byOwner.get(pending.adjustment.ownerProfileId))}: ${pending.adjustment.reason}`}
          confirmLabel="Approve"
          onClose={() => setPending(null)}
          onSubmit={() => run('Adjustment approved', () => adminSettlementsApi.approveAdjustment(pending.adjustment.id, pending.adjustment.rowVersion))}
        />
      )}
      {pending?.kind === 'reject' && (
        <ActionDialog
          open
          destructive
          title={`Reject adjustment #${pending.adjustment.id}?`}
          description="A rejected adjustment is never applied to a settlement."
          confirmLabel="Reject"
          field={{ label: 'Reason', required: true, requiredMessage: 'A reason is required.', maxLength: 500 }}
          onClose={() => setPending(null)}
          onSubmit={(reason) =>
            run('Adjustment rejected', () => adminSettlementsApi.rejectAdjustment(pending.adjustment.id, pending.adjustment.rowVersion, reason))
          }
        />
      )}
    </ListScreen>
  );
};
