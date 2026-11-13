import React, { useState } from 'react';
import { AdminApiError } from '@/api/adminClientApi';
import { adminReviewsApi } from '@/api/adminReviewsApi';
import { REPORTED_QUEUE_SIZE, REVIEW_REASON_MAX, REVIEW_REASON_MIN, type ModeratedReviewDto } from '@/types/adminReviews';
import { ListScreen, QueryState } from '../../../components/PageLayout';
import { Banner } from '../../../components/Banner';
import { AdminButton } from '../../../components/AdminButton';
import { AdminDrawer } from '../../../components/AdminDrawer';
import { ActionDialog } from '../../../components/ActionDialog';
import { DialogSummary } from '../../../components/AdminDialog';
import { AuditTrailLink } from '../../../components/AuditTrailLink';
import { DetailList, DetailRow } from '../../../components/DetailRow';
import { FilterColumn, FilterPanel } from '../../../components/FilterColumn';
import { SearchBox, ToolbarRow, ToolbarSelect } from '../../../components/Toolbar';
import { CellLink, CellMuted, CellTwoLine, DataTable, type DataTableColumn } from '../../../components/DataTable';
import { useAdminQuery } from '../../../hooks/useAdminQuery';
import { useAdminToast } from '../../../hooks/useAdminToast';
import { useListParams } from '../../../hooks/useListParams';
import { useLotOptions } from '../../../hooks/useLotOptions';
import { formatDateTimeIct, formatNumber } from '../../../utils/format';
import { auditLogListLink } from '../../access/auditLogFormat';
import {
  RATINGS,
  filterBase,
  filterReviews,
  lotLabel,
  lotsInQueue,
  parseId,
  parseRating,
  ratingCounts,
  ratingText,
  reviewCode,
} from './reviewFormat';

const AUDIT_SERVICE = 'SupportService';
const AUDIT_ENTITY = 'Review';
const lotPath = (id: number) => `/admin/operations/parking-lots/${id}`;

const Rating: React.FC<{ value: number }> = ({ value }) => (
  <span className="adm-type-body-strong whitespace-nowrap text-(--admin-text-primary)" aria-label={`${value} out of 5 stars`}>
    {ratingText(value)}
  </span>
);

/** Đoạn văn dài trong ô bảng: tối đa 2 dòng, đầy đủ ở drawer. */
const Clamp: React.FC<{ text: string | null; empty?: string }> = ({ text, empty = '—' }) => (
  <span className="adm-type-body-default line-clamp-2 break-words text-(--admin-text-primary)" title={text ?? undefined}>
    {text ?? <span className="text-(--admin-text-secondary)">{empty}</span>}
  </span>
);

const Paragraph: React.FC<{ text: string | null; empty: string }> = ({ text, empty }) =>
  text ? <p className="m-0 whitespace-pre-wrap break-words">{text}</p> : <span className="text-(--admin-text-secondary)">{empty}</span>;

type ModerationDialog = 'hide' | 'dismiss' | null;

const reasonField = (label: string, placeholder: string) => ({
  label,
  required: true,
  requiredMessage: 'A reason is required.',
  minLength: REVIEW_REASON_MIN,
  maxLength: REVIEW_REASON_MAX,
  placeholder,
  helper: `${REVIEW_REASON_MIN}–${REVIEW_REASON_MAX} characters. Recorded in the audit log.`,
});

const ReviewDrawer: React.FC<{
  review: ModeratedReviewDto;
  lotName: string | undefined;
  onClose: () => void;
  onDone: () => void;
  onStale: () => void;
}> = ({ review, lotName, onClose, onDone, onStale }) => {
  const toast = useAdminToast();
  const [dialog, setDialog] = useState<ModerationDialog>(null);
  const lot = lotLabel(review.parkingLotId, lotName);
  const summary = (
    <DialogSummary title={`${reviewCode(review.id)} · ${ratingText(review.rating)}`} subtitle={`${review.reviewerName} · ${lot}`} />
  );

  /** 404 / 409 = đánh giá đã được xử lý ở nơi khác → tải lại hàng chờ, giữ thông báo của server trong dialog. */
  const run = async (action: () => Promise<unknown>, title: string, message: string) => {
    try {
      await action();
    } catch (err) {
      if (err instanceof AdminApiError && (err.status === 404 || err.status === 409)) onStale();
      throw err;
    }
    toast.show({ type: 'success', title, message });
    onDone();
  };

  return (
    <>
      <AdminDrawer open width={560} title={reviewCode(review.id)} description={`Reported ${formatDateTimeIct(review.reportedAtUtc)}`} onClose={onClose}>
        <div className="adm-motion-fade flex flex-col gap-4">
          <section className="border border-(--admin-border-default) p-3">
            <DetailList>
              <DetailRow label="Reviewer">{review.reviewerName}</DetailRow>
              <DetailRow label="Lot">
                <CellLink to={lotPath(review.parkingLotId)}>{lot}</CellLink>
              </DetailRow>
              <DetailRow label="Rating">
                <Rating value={review.rating} />
              </DetailRow>
              <DetailRow label="Posted">{formatDateTimeIct(review.createdAtUtc)}</DetailRow>
              <DetailRow label="Comment">
                <Paragraph text={review.comment} empty="No comment, rating only." />
              </DetailRow>
              <DetailRow label="Owner reply">
                <Paragraph text={review.ownerReply} empty="The owner has not replied." />
              </DetailRow>
              {review.ownerRepliedAtUtc && <DetailRow label="Replied">{formatDateTimeIct(review.ownerRepliedAtUtc)}</DetailRow>}
              <DetailRow label="Report reason">
                <Paragraph text={review.reportReason} empty="—" />
              </DetailRow>
              <DetailRow label="Reported">{formatDateTimeIct(review.reportedAtUtc)}</DetailRow>
              <DetailRow label="History">
                <CellLink to={auditLogListLink({ service: AUDIT_SERVICE, entity: AUDIT_ENTITY, entityId: String(review.id) })}>View in audit log</CellLink>
              </DetailRow>
            </DetailList>
          </section>

          <Banner tone="info">
            Hide removes the review from the lot page and from the lot rating. Dismiss keeps the review and closes the owner&apos;s report.
          </Banner>

          <div className="flex flex-wrap justify-end gap-2 border-t border-(--admin-border-default) pt-4">
            <AdminButton variant="secondary" icon="check" onClick={() => setDialog('dismiss')}>
              Dismiss report
            </AdminButton>
            <AdminButton variant="destructive" icon="eyeOff" onClick={() => setDialog('hide')}>
              Hide review
            </AdminButton>
          </div>
        </div>
      </AdminDrawer>

      <ActionDialog
        open={dialog === 'hide'}
        title="Hide review"
        description="The review is removed from the lot page. Drivers and the owner no longer see it."
        confirmLabel="Hide review"
        destructive
        field={reasonField('Reason for hiding', 'For example: abusive language that breaks the review policy.')}
        onClose={() => setDialog(null)}
        onSubmit={(reason) =>
          run(() => adminReviewsApi.hide(review.id, reason), 'Review hidden', `${reviewCode(review.id)} is no longer shown on ${lot}.`)
        }
      >
        {summary}
      </ActionDialog>

      <ActionDialog
        open={dialog === 'dismiss'}
        title="Dismiss report"
        description="The review stays visible and leaves the queue."
        confirmLabel="Dismiss report"
        field={reasonField('Reason for keeping the review', 'For example: the review describes a real visit.')}
        onClose={() => setDialog(null)}
        onSubmit={(reason) =>
          run(() => adminReviewsApi.dismissReport(review.id, reason), 'Report dismissed', `${reviewCode(review.id)} stays on ${lot}.`)
        }
      >
        {summary}
      </ActionDialog>
    </>
  );
};

/**
 * Support & Sanctions / Reviews (US-095, US-094 AC2): hàng chờ đánh giá bị chủ bãi khiếu nại – gỡ hoặc bác khiếu nại, có lý do,
 * ghi audit. API trả tối đa 100 dòng, không lọc → tìm / lọc sao / lọc bãi phía client.
 */
export const ReviewsModerationPage: React.FC = () => {
  const params = useListParams();
  const filters = { search: params.get('q'), rating: parseRating(params.get('rating')), lotId: parseId(params.get('lot')) };
  const openId = parseId(params.get('review'));
  const lots = useLotOptions();
  const lotName = (id: number) => lots.byId(id)?.name;

  const query = useAdminQuery(() => adminReviewsApi.reported(), [], (d) => d.length === 0);
  const all = query.status === 'ready' ? query.data ?? [] : [];
  const base = filterBase(all, filters, lotName);
  const rows = filterReviews(all, filters, lotName);
  const counts = ratingCounts(base);
  const open = all.find((r) => r.id === openId) ?? null;
  const full = all.length >= REPORTED_QUEUE_SIZE;
  const filtered = filters.search !== '' || filters.rating !== null || filters.lotId !== null;

  const columns: DataTableColumn<ModeratedReviewDto>[] = [
    {
      key: 'reviewer',
      header: 'Reviewer',
      width: 180,
      render: (r) => <CellTwoLine primary={r.reviewerName} secondary={`${reviewCode(r.id)} · ${formatDateTimeIct(r.createdAtUtc)}`} />,
    },
    {
      key: 'lot',
      header: 'Lot',
      width: 170,
      render: (r) => <CellLink to={lotPath(r.parkingLotId)}>{lotLabel(r.parkingLotId, lotName(r.parkingLotId))}</CellLink>,
    },
    { key: 'rating', header: 'Rating', width: 80, render: (r) => <Rating value={r.rating} /> },
    { key: 'comment', header: 'Comment', render: (r) => <Clamp text={r.comment} empty="Rating only" /> },
    { key: 'reason', header: 'Report reason', render: (r) => <Clamp text={r.reportReason} /> },
    { key: 'reported', header: 'Reported', width: 150, render: (r) => <CellMuted>{formatDateTimeIct(r.reportedAtUtc)}</CellMuted> },
    {
      key: 'actions',
      header: '',
      width: 90,
      render: (r) => (
        <AdminButton variant="ghost" onClick={(e) => { e.stopPropagation(); params.set({ review: r.id }); }}>
          Review
        </AdminButton>
      ),
    },
  ];

  const subtitle =
    query.status === 'ready'
      ? filtered
        ? `${formatNumber(rows.length)} of ${formatNumber(all.length)} reported · oldest report first`
        : `${formatNumber(all.length)} reported · oldest report first`
      : query.status === 'empty'
        ? 'No reported reviews'
        : '—';

  const queueLots = lotsInQueue(all, lotName);

  return (
    <ListScreen
      title="Reviews"
      subtitle={subtitle}
      actions={
        <>
          <AuditTrailLink to={auditLogListLink({ service: AUDIT_SERVICE, entity: AUDIT_ENTITY })} />
          <AdminButton variant="onNavy" icon="refresh" onClick={query.refetch}>
            Refresh
          </AdminButton>
        </>
      }
      top={
        <>
          <Banner tone="info">Reviews that lot owners reported (US-094). Hide the review or dismiss the report; each decision needs a reason.</Banner>
          {full && (
            <Banner tone="warning">
              {`Showing the ${formatNumber(REPORTED_QUEUE_SIZE)} oldest reports. Newer ones appear as these are handled.`}
            </Banner>
          )}
        </>
      }
      filters={
        <FilterColumn>
          <FilterPanel
            title="Rating"
            value={filters.rating === null ? '' : String(filters.rating)}
            onChange={(v) => params.set({ rating: v || null })}
            options={[
              { value: '', label: 'Any', count: query.status === 'ready' ? base.length : null },
              ...RATINGS.map((n) => ({ value: String(n), label: ratingText(n), count: query.status === 'ready' ? counts.get(n) ?? 0 : null })),
            ]}
          />
        </FilterColumn>
      }
      toolbar={
        <ToolbarRow>
          <SearchBox placeholder="Search reviewer, comment, reason or lot…" value={filters.search} onSearch={(q) => params.set({ q: q || null })} />
          <ToolbarSelect
            label="Lot"
            width={220}
            value={filters.lotId === null ? '' : String(filters.lotId)}
            onChange={(v) => params.set({ lot: v || null })}
            options={[{ value: '', label: 'All' }, ...queueLots.map((l) => ({ value: String(l.id), label: l.label }))]}
          />
        </ToolbarRow>
      }
    >
      <DataTable
        columns={columns}
        rows={rows}
        rowKey={(r) => r.id}
        selectedKey={openId}
        onRowClick={(r) => params.set({ review: r.id })}
        busy={query.isFetching && query.data !== null}
        placeholder={
          query.status === 'ready' ? (
            <QueryState
              status="empty"
              error={null}
              onRetry={query.refetch}
              copy={{
                emptyTitle: 'No reviews match',
                emptyMessage: 'Change the search, rating or lot filter.',
                errorTitle: '',
                service: 'SupportService',
              }}
            />
          ) : (
            <QueryState
              status={query.status}
              error={query.error}
              onRetry={query.refetch}
              copy={{
                emptyTitle: 'No reported reviews',
                emptyMessage: 'Reviews appear here when a lot owner reports one from the owner portal.',
                errorTitle: 'Could not load reported reviews',
                service: 'SupportService',
              }}
            />
          )
        }
      />

      {open && (
        <ReviewDrawer
          key={open.id}
          review={open}
          lotName={lotName(open.parkingLotId)}
          onClose={() => params.set({ review: null })}
          onStale={query.refetch}
          onDone={() => {
            params.set({ review: null });
            query.refetch();
          }}
        />
      )}
    </ListScreen>
  );
};
