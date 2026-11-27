import React, { useState } from 'react';
import { adminSettlementsApi } from '@/api/adminSettlementsApi';
import type { SettlementSummaryDto } from '@/types/adminSettlements';
import { AdminButton } from '../../../components/AdminButton';
import { Banner } from '../../../components/Banner';
import { CellLink, CellMono, CellMuted, CellTwoLine, DataTable, type DataTableColumn } from '../../../components/DataTable';
import { FilterColumn, FilterPanel } from '../../../components/FilterColumn';
import { ListScreen, QueryState } from '../../../components/PageLayout';
import { Pagination } from '../../../components/Pagination';
import { StatusIndicator } from '../../../components/StatusIndicator';
import { useAdminQuery } from '../../../hooks/useAdminQuery';
import { useAdminToast } from '../../../hooks/useAdminToast';
import { DEFAULT_PAGE_SIZE, useListParams } from '../../../hooks/useListParams';
import { EMPTY, formatDate, formatDateTimeIct, formatNumber, formatVnd } from '../../../utils/format';
import { MarkPaidDialog } from './SettlementDialogs';
import { SettlementDrawer } from './SettlementDrawer';
import { Money } from './SettlementCells';
import { bankLabel, isPayable, ownerLabel, ownerLink, SETTLEMENTS_PATH } from './settlementFormat';
import { useOwnerAccounts } from './useOwnerAccounts';

type PayoutTab = 'Approved' | 'Paid';
const positiveInt = (value: string): number | null => (/^\d+$/.test(value) && Number(value) > 0 ? Number(value) : null);

/**
 * Finance / Payouts (US-080, UC-43 "xuất bảng payout"): kỳ đã duyệt chờ chuyển khoản → Mark paid (mã giao dịch ngân hàng bắt buộc),
 * và lịch sử đã chi. Tài khoản nhận tiền chỉ hiện 4 số cuối (AdminService) – số đầy đủ xem ở hồ sơ đối tác.
 */
export const PayoutsPage: React.FC = () => {
  const toast = useAdminToast();
  const params = useListParams();
  const page = params.page;
  const tab: PayoutTab = params.get('status') === 'Paid' ? 'Paid' : 'Approved';
  const openId = positiveInt(params.get('settlement'));
  const [paying, setPaying] = useState<SettlementSummaryDto | null>(null);

  const list = useAdminQuery(() => adminSettlementsApi.list({ status: tab }, page, DEFAULT_PAGE_SIZE), [tab, page], (d) => d.items.length === 0);
  const stats = useAdminQuery(() => adminSettlementsApi.stats(null), []);
  const rows = list.data?.items ?? [];
  const { byOwner, status: accountsStatus } = useOwnerAccounts(rows.map((r) => r.ownerProfileId));

  const refresh = () => {
    list.refetch();
    stats.refetch();
  };
  const open = (id: number | null) => params.set({ settlement: id, page: page <= 1 ? null : page });

  const columns: DataTableColumn<SettlementSummaryDto>[] = [
    {
      key: 'owner',
      header: 'Owner / settlement',
      render: (r) => {
        const account = byOwner.get(r.ownerProfileId);
        const link = ownerLink(account);
        return (
          <CellTwoLine
            primary={link ? <CellLink to={link}>{ownerLabel(r.ownerProfileId, account)}</CellLink> : ownerLabel(r.ownerProfileId, account)}
            secondary={r.code}
          />
        );
      },
    },
    {
      key: 'bank',
      header: 'Bank account',
      width: 230,
      render: (r) => {
        const account = byOwner.get(r.ownerProfileId);
        return account ? (
          <CellTwoLine primary={<CellMono>{bankLabel(account)}</CellMono>} secondary={account.accountName} />
        ) : (
          <CellMuted>{accountsStatus === 'loading' ? '…' : 'No payout account on file'}</CellMuted>
        );
      },
    },
    { key: 'net', header: 'Net payout', width: 130, align: 'right', render: (r) => <Money value={r.netPayout} /> },
    { key: 'approved', header: 'Approved', width: 110, render: (r) => <CellMuted>{r.approvedAtUtc ? formatDate(r.approvedAtUtc) : EMPTY}</CellMuted> },
    {
      key: 'reference',
      header: 'Payout reference',
      width: 170,
      render: (r) =>
        r.payoutReference ? <CellTwoLine primary={<CellMono>{r.payoutReference}</CellMono>} secondary={formatDateTimeIct(r.paidAtUtc)} /> : <CellMuted>{EMPTY}</CellMuted>,
    },
    { key: 'status', header: 'Status', width: 110, render: (r) => <StatusIndicator value={r.status} /> },
    {
      key: 'action',
      header: '',
      width: 140,
      render: (r) =>
        r.status !== 'Approved' ? null : isPayable(r) ? (
          <AdminButton
            variant="primary"
            onClick={(e) => {
              e.stopPropagation();
              setPaying(r);
            }}
          >
            Mark paid
          </AdminButton>
        ) : (
          <CellMuted>Carried over</CellMuted>
        ),
    },
  ];

  return (
    <ListScreen
      title="Payouts"
      subtitle="Approved settlements → bank transfer → Paid"
      top={
        <Banner
          tone="info"
          action={
            <CellLink to={`${SETTLEMENTS_PATH}?status=PendingReview`}>Settlements in review</CellLink>
          }
        >
          Awaiting transfer: <strong>{stats.data ? formatVnd(stats.data.awaitingPayoutNetPayout) : EMPTY}</strong>
          {stats.data ? ` across ${formatNumber(stats.data.approved)} approved settlements` : ''}. Accounts show the last 4 digits only; the full number is on the
          partner profile.
        </Banner>
      }
      filters={
        <FilterColumn>
          <FilterPanel
            title="Payout"
            value={tab === 'Approved' ? '' : 'Paid'}
            onChange={(v) => params.set({ status: v || null })}
            options={[
              { value: '', label: 'Awaiting payout (Approved)', count: stats.data?.approved },
              { value: 'Paid', label: 'Paid', count: stats.data?.paid },
            ]}
          />
        </FilterColumn>
      }
    >
      <DataTable
        animateRows
        busy={list.isFetching && list.data !== null}
        columns={columns}
        rows={list.status === 'ready' ? rows : []}
        rowKey={(r) => r.id}
        onRowClick={(r) => open(r.id)}
        selectedKey={openId}
        placeholder={
          <QueryState
            status={list.status}
            error={list.error}
            onRetry={list.refetch}
            copy={{
              emptyTitle: tab === 'Approved' ? 'Nothing awaiting payout' : 'No payouts yet',
              emptyMessage: tab === 'Approved' ? 'Approved settlements appear here until they are marked as paid.' : 'Paid settlements appear here.',
              errorTitle: 'Could not load payouts',
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

      {paying && (
        <MarkPaidDialog
          settlement={paying}
          account={byOwner.get(paying.ownerProfileId)}
          onClose={() => setPaying(null)}
          onConflict={refresh}
          onDone={(updated) => {
            setPaying(null);
            toast.show({ type: 'success', title: 'Marked as paid', message: `${updated.code} · ${updated.payoutReference ?? ''}` });
            refresh();
          }}
        />
      )}
      {openId !== null && <SettlementDrawer key={openId} settlementId={openId} onClose={() => open(null)} onChanged={refresh} />}
    </ListScreen>
  );
};
