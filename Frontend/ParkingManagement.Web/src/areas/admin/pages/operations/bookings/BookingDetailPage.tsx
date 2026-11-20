import React from 'react';
import { useParams } from 'react-router-dom';
import { adminOperationsApi } from '@/api/adminOperationsApi';
import { BookingStatus } from '@/types/adminEnums';
import type { AdminBookingDetailDto } from '@/types/adminOperations';
import { AdminTitleBar } from '../../../layout/AdminTitleBar';
import { DetailColumns, QueryState } from '../../../components/PageLayout';
import { Panel } from '../../../components/Panel';
import { DetailList, DetailRow } from '../../../components/DetailRow';
import { Timeline, type TimelineEntry } from '../../../components/Timeline';
import { CellAmount, CellMono, CellMuted, CellStatus, CellText, DataTable } from '../../../components/DataTable';
import { useAdminQuery } from '../../../hooks/useAdminQuery';
import { useAdminBreadcrumb } from '../../../hooks/useAdminBreadcrumb';
import { humanizeEnum } from '../../../utils/enumLabels';
import { formatDateTimeIct, formatShortDateTime, formatVnd, orEmpty } from '../../../utils/format';

const timeline = (b: AdminBookingDetailDto): TimelineEntry[] =>
  b.history.map((h, i) => ({
    key: `${i}-${h.toStatus}`,
    title: humanizeEnum(h.toStatus),
    meta: [formatDateTimeIct(h.atUtc), h.changedByUserId ? `User #${h.changedByUserId}` : null, h.reason].filter(Boolean).join(' · '),
    state:
      i < b.history.length - 1
        ? 'done'
        : ([BookingStatus.Cancelled, BookingStatus.CancelledNoRefund, BookingStatus.NoShow, BookingStatus.Rejected, BookingStatus.Expired] as string[]).includes(h.toStatus)
          ? 'danger'
          : 'current',
  }));

/** Operations / Bookings / Detail (GET /api/v1/bookings/{code}). */
export const BookingDetailPage: React.FC = () => {
  const { code = '' } = useParams();
  const query = useAdminQuery(code ? () => adminOperationsApi.getBooking(code) : null, [code]);
  const booking = query.data;
  const bookingId = booking?.id ?? null;
  const payments = useAdminQuery(bookingId ? () => adminOperationsApi.getBookingPayments(bookingId) : null, [bookingId], (d) => d.length === 0);
  useAdminBreadcrumb(code || null);

  if (!booking) {
    return (
      <>
        <AdminTitleBar title="Booking" />
        <QueryState
          status={query.status}
          error={query.error}
          onRetry={query.refetch}
          bare={false}
          copy={{ emptyTitle: 'Booking not found', emptyMessage: 'Check the booking code.', errorTitle: 'Could not load the booking', service: 'BookingService' }}
        />
      </>
    );
  }

  const snap = booking.priceSnapshot;
  return (
    <>
      <AdminTitleBar
        title={booking.code}
        subtitle={`Status: ${humanizeEnum(booking.status)} · ${booking.parkingLotName} · ${booking.plateNumber}`}
      />
      <DetailColumns
        main={
          <>
            <Panel title="Booking">
              <DetailList>
                <DetailRow label="Code">{booking.code}</DetailRow>
                <DetailRow label="Driver">User #{booking.userId}</DetailRow>
                <DetailRow label="Vehicle">
                  {booking.plateNumber} · {humanizeEnum(booking.vehicleType)}
                </DetailRow>
                <DetailRow label="Lot">
                  {[booking.parkingLotName, booking.zoneId ? `Zone #${booking.zoneId}` : null, booking.slotCode ? `Slot ${booking.slotCode}` : null].filter(Boolean).join(' · ')}
                </DetailRow>
                <DetailRow label="Period">
                  {formatDateTimeIct(booking.startAtUtc)} → {formatDateTimeIct(booking.endAtUtc)}
                </DetailRow>
                <DetailRow label="Hold expires">{formatDateTimeIct(booking.holdExpiresAtUtc)}</DetailRow>
                <DetailRow label="QR token">{booking.qrToken ? '•••• (issued)' : '—'}</DetailRow>
              </DetailList>
            </Panel>
            <Panel title="Amounts & price lock" description="Price locked when the booking was made">
              <DetailList>
                <DetailRow label="Base amount">{formatVnd(snap?.baseAmount)}</DetailRow>
                <DetailRow label="Surcharge">{formatVnd(snap?.surchargeAmount)}</DetailRow>
                <DetailRow label="Discount">
                  {formatVnd(booking.discountAmount)} · promotion {orEmpty(booking.promotionCode)}
                </DetailRow>
                <DetailRow label="Final / total">{formatVnd(booking.totalAmount)}</DetailRow>
                <DetailRow label="Paid">{formatVnd(booking.paidAmount)}</DetailRow>
                <DetailRow label="Billing grace period">{snap ? `${snap.billingGracePeriodMinutes} min` : '—'}</DetailRow>
                <DetailRow label="Rate card">{snap ? `#${snap.rateCardId}` : '—'}</DetailRow>
              </DetailList>
            </Panel>
            <Panel title="Payments" padded={false}>
              {payments.status === 'ready' && payments.data ? (
                <DataTable
                  bare
                  rows={payments.data}
                  rowKey={(r) => r.id}
                  columns={[
                    { key: 'code', header: 'Payment', render: (r) => <CellMono>{r.code}</CellMono> },
                    { key: 'method', header: 'Method', render: (r) => <CellText>{humanizeEnum(r.method)}</CellText> },
                    { key: 'amount', header: 'Amount', render: (r) => <CellAmount>{formatVnd(r.amount)}</CellAmount> },
                    { key: 'refunded', header: 'Refunded', render: (r) => <CellAmount>{formatVnd(r.refundedAmount)}</CellAmount> },
                    { key: 'status', header: 'Status', render: (r) => <CellStatus value={r.status} /> },
                    { key: 'paid', header: 'Paid at', render: (r) => <CellMuted>{formatShortDateTime(r.paidAtUtc)}</CellMuted> },
                  ]}
                />
              ) : (
                <QueryState
                  status={payments.status}
                  error={payments.error}
                  onRetry={payments.refetch}
                  copy={{ emptyTitle: 'No payments', emptyMessage: 'No payment attempts for this booking.', errorTitle: 'Could not load payments', service: 'PaymentService' }}
                />
              )}
            </Panel>
          </>
        }
        side={
          <Panel title="Status timeline">
            {booking.history.length > 0 ? <Timeline entries={timeline(booking)} /> : <p className="adm-type-body-regular-12 m-0">—</p>}
          </Panel>
        }
      />
    </>
  );
};
