import React, { useState } from 'react';
import { useParams } from 'react-router-dom';
import { adminOperationsApi } from '@/api/adminOperationsApi';
import { BookingStatus, RefundReason } from '@/types/adminEnums';
import type { AdminBookingDetailDto } from '@/types/adminOperations';
import { AdminTitleBar } from '../../../layout/AdminTitleBar';
import { AdminButton } from '../../../components/AdminButton';
import { DetailColumns, QueryState } from '../../../components/PageLayout';
import { Panel } from '../../../components/Panel';
import { DetailList, DetailRow } from '../../../components/DetailRow';
import { Timeline, type TimelineEntry } from '../../../components/Timeline';
import { CellAmount, CellMono, CellMuted, CellStatus, CellText, DataTable } from '../../../components/DataTable';
import { StateBlock } from '../../../components/StateBlock';
import { FormDialog, type FormDialogSpec } from '../../../components/FormDialog';
import { useAdminQuery } from '../../../hooks/useAdminQuery';
import { useAdminBreadcrumb } from '../../../hooks/useAdminBreadcrumb';
import { humanizeEnum } from '../../../utils/enumLabels';
import { formatDateTimeIct, formatShortDateTime, formatVnd, orEmpty } from '../../../utils/format';

const TERMINAL: string[] = [BookingStatus.Completed, BookingStatus.Cancelled, BookingStatus.CancelledNoRefund, BookingStatus.NoShow, BookingStatus.Expired, BookingStatus.Rejected];

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

const cancelSpec = (b: AdminBookingDetailDto): FormDialogSpec => ({
  title: 'Cancel booking',
  description: 'Status → Cancelled (refund) or Cancelled no refund. Driver notified (BOOKING_CANCELLED).',
  confirmLabel: 'Cancel booking',
  destructive: true,
  fields: [
    { key: 'reason', kind: 'textarea', label: 'Cancel reason', required: true, maxLength: 500, helper: 'Saved to Bookings.CancelReason', requiredMessage: 'A reason is required.' },
    {
      key: 'refundReason',
      kind: 'select',
      label: 'Refund reason',
      required: true,
      half: true,
      options: Object.values(RefundReason).map((r) => ({ value: r, label: humanizeEnum(r) })),
    },
    {
      key: 'refundAmount',
      kind: 'number',
      label: 'Refund amount (₫)',
      required: true,
      half: true,
      helper: '≤ PaidAmount',
      validate: (v) => (String(v ?? '') !== '' && (Number(v) < 0 || Number(v) > b.paidAmount) ? `Must be between 0 and ${formatVnd(b.paidAmount)}.` : null),
    },
  ],
});

/** Operations / Bookings / Detail (GET /api/v1/bookings/{code}). */
export const BookingDetailPage: React.FC = () => {
  const { code = '' } = useParams();
  const [cancelOpen, setCancelOpen] = useState(false);
  const query = useAdminQuery(code ? () => adminOperationsApi.getBooking(code) : null, [code]);
  const booking = query.data;
  const bookingId = booking?.id ?? null;
  const payments = useAdminQuery(bookingId ? () => adminOperationsApi.getBookingPayments(bookingId) : null, [bookingId], (d) => d.length === 0);
  useAdminBreadcrumb(code || null);

  if (!booking) {
    return (
      <>
        <AdminTitleBar title="Bookings" subtitle="—" />
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
        actions={
          !TERMINAL.includes(booking.status) && (
            <AdminButton variant="destructive" onClick={() => setCancelOpen(true)}>
              Cancel booking
            </AdminButton>
          )
        }
      />
      <DetailColumns
        main={
          <>
            <Panel title="Booking" description="Bookings">
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
            <Panel title="Amounts & price lock" description="Bookings amounts · PriceSnapshots (RateCardJson)">
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
            <Panel title="Payments" description="PaymentService · Payments" padded={false}>
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
            <Panel title="Modifications" description="BookingModifications (ChangeTime, ChangeVehicle, Extend, ChangeSlot)">
              <StateBlock type="unavailable" bare className="min-h-0 py-4" message="BookingDetailDto does not include modifications yet." />
            </Panel>
          </>
        }
        side={
          <Panel title="Status timeline" description="BookingStatusLogs (FromStatus → ToStatus, Reason)">
            {booking.history.length > 0 ? <Timeline entries={timeline(booking)} /> : <p className="adm-type-body-regular-12 m-0">—</p>}
          </Panel>
        }
      />
      {cancelOpen && <FormDialog spec={cancelSpec(booking)} open onClose={() => setCancelOpen(false)} unavailableNote="No admin cancel-with-refund endpoint in BookingService yet." />}
    </>
  );
};
