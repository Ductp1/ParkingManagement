import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import { adminSettlementsApi } from '@/api/adminSettlementsApi';
import type { SettlementAction, SettlementDetailDto, SettlementLineType, SettlementSummaryDto } from '@/types/adminSettlements';
import { ActionDialog } from '../../../components/ActionDialog';
import { AdminButton } from '../../../components/AdminButton';
import { AdminDrawer } from '../../../components/AdminDrawer';
import { Banner } from '../../../components/Banner';
import { CellAmount, CellLink, CellMono, CellMuted, DataTable } from '../../../components/DataTable';
import { DetailRow } from '../../../components/DetailRow';
import { Pagination } from '../../../components/Pagination';
import { QueryState } from '../../../components/PageLayout';
import { StatusIndicator } from '../../../components/StatusIndicator';
import { Timeline, type TimelineEntry } from '../../../components/Timeline';
import { ToolbarSelect } from '../../../components/Toolbar';
import { useAdminQuery } from '../../../hooks/useAdminQuery';
import { useAdminToast } from '../../../hooks/useAdminToast';
import { EMPTY, formatDateTimeIct, formatNumber, formatVnd } from '../../../utils/format';
import { saveFile } from '../../operations/lot-contracts/lotContractFormat';
import { MarkPaidDialog } from './SettlementDialogs';
import {
  actionError,
  bankLabel,
  blockReasonLabel,
  commissionLink,
  FORMULA_STEPS,
  formatPeriod,
  formatSignedVnd,
  LINE_TYPE_LABEL,
  lotLink,
  ownerLabel,
  ownerLink,
} from './settlementFormat';
import { useOwnerAccounts } from './useOwnerAccounts';

const LINES_PAGE_SIZE = 20;
const LINE_TYPES: SettlementLineType[] = ['Booking', 'WalkIn', 'Refund', 'DisputeHold'];

const Section: React.FC<{ title: string; children: React.ReactNode; aside?: React.ReactNode }> = ({ title, children, aside }) => (
  <section className="flex flex-col gap-2">
    <div className="flex items-center justify-between gap-2">
      <h3 className="adm-type-panel-title m-0 text-(--admin-text-primary)">{title}</h3>
      {aside}
    </div>
    {children}
  </section>
);

/** Công thức NetPayout từng bước – số lấy đúng từ API, không tự tính lại. */
const FormulaBreakdown: React.FC<{ detail: SettlementDetailDto }> = ({ detail }) => (
  <div className="rounded-[2px] border border-(--admin-border-default)" data-testid="formula">
    {FORMULA_STEPS.map((step) => (
      <div key={step.key} className="flex items-center gap-3 border-b border-(--admin-border-default) px-3 py-1.5" title={step.hint}>
        <span aria-hidden="true" className="adm-type-body-strong w-3 text-center text-(--admin-text-secondary)">{step.sign}</span>
        <span className="adm-type-body-regular-12 flex-1 text-(--admin-text-secondary)">{step.label}</span>
        <span className="adm-type-body-strong tabular-nums text-(--admin-text-primary)">{formatSignedVnd(detail.formula[step.key])}</span>
      </div>
    ))}
    <div className="flex items-center gap-3 bg-(--admin-surface-page) px-3 py-2">
      <span aria-hidden="true" className="adm-type-body-strong w-3 text-center">=</span>
      <span className="adm-type-body-bold flex-1 text-(--admin-text-primary)">Net payout</span>
      <span
        className={`adm-type-body-bold tabular-nums ${detail.formula.netPayout < 0 ? 'text-(--admin-status-danger)' : 'text-(--admin-text-primary)'}`}
        data-testid="net-payout"
      >
        {formatSignedVnd(detail.formula.netPayout)}
      </span>
    </div>
  </div>
);

const timelineEntries = (detail: SettlementDetailDto): TimelineEntry[] =>
  detail.timeline.map((e, i) => ({
    key: `${e.event}-${i}`,
    title: e.event === 'Generated' ? 'Generated' : e.event === 'Paid' ? `Paid · ${e.note ?? ''}` : e.event,
    meta: [formatDateTimeIct(e.atUtc), e.byUserId === null ? 'by system' : `by Admin #${e.byUserId}`, e.event === 'Rejected' ? e.note : null]
      .filter(Boolean)
      .join(' · '),
    state: e.event === 'Rejected' ? 'danger' : i === detail.timeline.length - 1 ? 'current' : 'done',
  }));

type PendingAction = Exclude<SettlementAction, 'MarkPaid'> | 'MarkPaid' | null;

/**
 * Slide-over chi tiết kỳ quyết toán (US-080): công thức NetPayout, lý do bị chặn, mốc trạng thái, dòng chi tiết (phân trang, theo loại),
 * bút toán đã gắn và các thao tác hợp lệ theo trạng thái (allowedActions do API trả). 409 → báo dữ liệu đã đổi + tải lại.
 */
export const SettlementDrawer: React.FC<{
  settlementId: number;
  onClose: () => void;
  onChanged: () => void;
}> = ({ settlementId, onClose, onChanged }) => {
  const toast = useAdminToast();
  const detail = useAdminQuery(() => adminSettlementsApi.get(settlementId), [settlementId]);
  const [lineType, setLineType] = useState<SettlementLineType | ''>('');
  const [linePage, setLinePage] = useState(1);
  const lines = useAdminQuery(
    () => adminSettlementsApi.lines(settlementId, lineType || null, linePage, LINES_PAGE_SIZE),
    [settlementId, lineType, linePage],
    (d) => d.items.length === 0
  );
  const [pending, setPending] = useState<PendingAction>(null);
  const [exporting, setExporting] = useState(false);
  const s = detail.data?.settlement ?? null;
  const { byOwner } = useOwnerAccounts(s ? [s.ownerProfileId] : []);
  const account = s ? byOwner.get(s.ownerProfileId) : undefined;

  const reloadAll = () => {
    detail.refetch();
    lines.refetch();
    onChanged();
  };

  /** Chạy thao tác; 409 → tải lại rồi ném lỗi để dialog hiển thị. */
  const run = async (label: string, call: () => Promise<unknown>) => {
    try {
      await call();
    } catch (err: unknown) {
      const e = actionError(err);
      if (e.conflict) reloadAll();
      throw new Error(e.message);
    }
    toast.show({ type: 'success', title: label });
    reloadAll();
  };

  const exportLines = async () => {
    if (!s) return;
    setExporting(true);
    try {
      saveFile(await adminSettlementsApi.exportLines(s.id), `${s.code}.csv`);
    } catch (err: unknown) {
      toast.show({ type: 'error', title: 'Export failed', message: actionError(err).message });
    } finally {
      setExporting(false);
    }
  };

  const allowed = new Set(detail.data?.allowedActions ?? []);

  return (
    <AdminDrawer
      open
      width={760}
      title={s ? s.code : 'Settlement'}
      description={s ? `${ownerLabel(s.ownerProfileId, account)} · ${formatPeriod(s.periodStart, s.periodEnd)}` : undefined}
      onClose={onClose}
      actions={
        s && (
          <AdminButton variant="secondary" icon="download" onClick={exportLines} disabled={exporting}>
            {exporting ? 'Exporting…' : 'Lines CSV'}
          </AdminButton>
        )
      }
    >
      {!detail.data || !s ? (
        <QueryState
          status={detail.status}
          error={detail.error}
          onRetry={detail.refetch}
          copy={{ emptyTitle: 'Settlement not found', emptyMessage: '', errorTitle: 'Could not load the settlement', service: 'PaymentService' }}
        />
      ) : (
        <div className="flex flex-col gap-5">
          {s.isBlocked && (
            <Banner tone="warning">
              <span className="flex flex-col gap-1">
                <strong>Cannot be submitted yet</strong>
                <ul className="m-0 pl-4" data-testid="blocked-reasons">
                  {s.blockedReasons.map((r) => (
                    <li key={r}>{blockReasonLabel(r)}</li>
                  ))}
                </ul>
              </span>
            </Banner>
          )}
          {s.status === 'Approved' && s.netPayout <= 0 && (
            <Banner tone="info">Net payout is {formatSignedVnd(s.netPayout)} – nothing to transfer; the negative amount is carried over to the next period.</Banner>
          )}

          <div className="flex flex-wrap gap-2" role="group" aria-label="Settlement actions">
            {allowed.has('Regenerate') && (
              <AdminButton variant="secondary" icon="refresh" onClick={() => setPending('Regenerate')}>Regenerate</AdminButton>
            )}
            {s.status === 'Draft' && (
              <AdminButton
                variant="primary"
                onClick={() => setPending('Submit')}
                disabled={!allowed.has('Submit')}
                title={allowed.has('Submit') ? undefined : 'Resolve the blocking reasons and regenerate first'}
              >
                Submit for review
              </AdminButton>
            )}
            {allowed.has('Approve') && <AdminButton variant="primary" onClick={() => setPending('Approve')}>Approve</AdminButton>}
            {allowed.has('Reject') && <AdminButton variant="destructive" onClick={() => setPending('Reject')}>Reject</AdminButton>}
            {allowed.has('MarkPaid') && <AdminButton variant="primary" onClick={() => setPending('MarkPaid')}>Mark as paid</AdminButton>}
          </div>

          <Section title="Summary">
            <div className="rounded-[2px] border border-(--admin-border-default)">
              <DetailRow label="Status">
                <StatusIndicator value={s.status} />
              </DetailRow>
              <DetailRow label="Owner">
                {ownerLink(account) ? <CellLink to={ownerLink(account)!}>{ownerLabel(s.ownerProfileId, account)}</CellLink> : ownerLabel(s.ownerProfileId, account)}
              </DetailRow>
              <DetailRow label="Period">
                {formatPeriod(s.periodStart, s.periodEnd)}
                {s.cycle ? ` · ${s.cycle.toLowerCase()}` : ''}
              </DetailRow>
              <DetailRow label="Payout account">{bankLabel(account) ?? EMPTY}</DetailRow>
              <DetailRow label="Payout reference">{s.payoutReference ?? EMPTY}</DetailRow>
              <DetailRow label="Commission">
                <Link to={commissionLink(s)} className="adm-type-link-small text-(--admin-brand-navy-900) hover:underline focus-visible:outline-2 focus-visible:outline-(--admin-brand-navy-900)">
                  View commission for this period
                </Link>
              </DetailRow>
            </div>
          </Section>

          <Section title="Net payout">
            <FormulaBreakdown detail={detail.data} />
          </Section>

          <Section title="Status history">
            <Timeline entries={timelineEntries(detail.data)} />
          </Section>

          <Section
            title="Lines"
            aside={
              <ToolbarSelect
                label="Type"
                value={lineType}
                width={170}
                options={[{ value: '', label: 'All' }, ...LINE_TYPES.map((t) => ({ value: t, label: LINE_TYPE_LABEL[t] }))]}
                onChange={(v) => {
                  setLineType(v as SettlementLineType | '');
                  setLinePage(1);
                }}
              />
            }
          >
            <p className="adm-type-body-small m-0 text-(--admin-text-secondary)">
              {detail.data.lineTotals.length === 0
                ? 'No lines.'
                : detail.data.lineTotals.map((t) => `${LINE_TYPE_LABEL[t.lineType]} ${formatNumber(t.count)} · ${formatVnd(t.amount)}`).join('  |  ')}
            </p>
            <DataTable
              bare
              busy={lines.isFetching && lines.data !== null}
              rows={lines.data?.items ?? []}
              rowKey={(l) => l.id}
              columns={[
                { key: 'type', header: 'Type', width: 110, render: (l) => <CellMuted>{LINE_TYPE_LABEL[l.lineType]}</CellMuted> },
                { key: 'ref', header: 'Reference', render: (l) => <CellMono>{l.referenceCode ?? EMPTY}</CellMono> },
                { key: 'lot', header: 'Lot', width: 80, render: (l) => <CellLink to={lotLink(l.parkingLotId)}>#{l.parkingLotId}</CellLink> },
                { key: 'at', header: 'Occurred (ICT)', width: 150, render: (l) => <CellMuted>{formatDateTimeIct(l.occurredAtUtc)}</CellMuted> },
                { key: 'amount', header: 'Amount', width: 110, align: 'right', render: (l) => <CellAmount>{formatSignedVnd(l.amount)}</CellAmount> },
                { key: 'commission', header: 'Commission', width: 100, align: 'right', render: (l) => <CellAmount>{formatSignedVnd(l.commissionAmount)}</CellAmount> },
                {
                  key: 'note',
                  header: '',
                  width: 120,
                  render: (l) =>
                    l.blockedReason ? (
                      <span className="adm-type-body-small text-(--admin-status-warning)" title={blockReasonLabel(l.blockedReason)}>{l.blockedReason}</span>
                    ) : l.isOwnerCollected ? (
                      <CellMuted>Owner collected</CellMuted>
                    ) : null,
                },
              ]}
              placeholder={
                <QueryState
                  status={lines.status}
                  error={lines.error}
                  onRetry={lines.refetch}
                  copy={{ emptyTitle: 'No lines', emptyMessage: 'No lines of this type.', errorTitle: 'Could not load lines', service: 'PaymentService' }}
                />
              }
              footer={
                lines.data && lines.data.totalCount > LINES_PAGE_SIZE && (
                  <Pagination page={linePage} pageSize={LINES_PAGE_SIZE} totalCount={lines.data.totalCount} onPageChange={setLinePage} />
                )
              }
            />
          </Section>

          <Section title="Adjustments attached">
            {detail.data.adjustments.length === 0 ? (
              <p className="adm-type-body-small m-0 text-(--admin-text-secondary)">No adjustments attached to this period.</p>
            ) : (
              <div className="rounded-[2px] border border-(--admin-border-default)">
                {detail.data.adjustments.map((a) => (
                  <DetailRow key={a.id} label={`#${a.id} · ${a.sourceType}`}>
                    <span className="flex flex-col">
                      <span className="tabular-nums">{formatSignedVnd(a.amount)}</span>
                      <span className="adm-type-body-small text-(--admin-text-secondary)">{a.reason}</span>
                    </span>
                  </DetailRow>
                ))}
              </div>
            )}
          </Section>
        </div>
      )}

      {s && pending === 'Regenerate' && (
        <ActionDialog
          open
          title={`Regenerate ${s.code}?`}
          description="Recalculates this Draft with the latest payments, commission, refunds, dispute holds and approved adjustments."
          confirmLabel="Regenerate"
          onClose={() => setPending(null)}
          onSubmit={() => run('Settlement regenerated', () => adminSettlementsApi.regenerate(s.id, s.rowVersion))}
        />
      )}
      {s && pending === 'Submit' && (
        <ActionDialog
          open
          title={`Submit ${s.code} for review?`}
          description={`Net payout ${formatVnd(s.netPayout)}. Once submitted the numbers are frozen until approved or rejected.`}
          confirmLabel="Submit"
          onClose={() => setPending(null)}
          onSubmit={() => run('Submitted for review', () => adminSettlementsApi.submit(s.id, s.rowVersion))}
        />
      )}
      {s && pending === 'Approve' && (
        <ActionDialog
          open
          title={`Approve ${s.code}?`}
          description="Closes the period. After approval it can only be corrected through financial adjustments in a later period."
          confirmLabel="Approve"
          onClose={() => setPending(null)}
          onSubmit={() => run('Settlement approved', () => adminSettlementsApi.approve(s.id, s.rowVersion))}
        />
      )}
      {s && pending === 'Reject' && (
        <ActionDialog
          open
          destructive
          title={`Reject ${s.code}?`}
          description="Returns the settlement to Draft so it can be fixed and regenerated."
          confirmLabel="Reject"
          field={{ label: 'Reason', required: true, requiredMessage: 'A reason is required.', maxLength: 500 }}
          onClose={() => setPending(null)}
          onSubmit={(reason) => run('Settlement rejected', () => adminSettlementsApi.reject(s.id, s.rowVersion, reason))}
        />
      )}
      {s && pending === 'MarkPaid' && (
        <MarkPaidDialog
          settlement={s}
          account={account}
          onClose={() => setPending(null)}
          onConflict={reloadAll}
          onDone={(updated: SettlementSummaryDto) => {
            setPending(null);
            toast.show({ type: 'success', title: 'Marked as paid', message: updated.payoutReference ?? undefined });
            reloadAll();
          }}
        />
      )}
    </AdminDrawer>
  );
};
