import type { UnavailableListSpec } from '../components/UnavailableListScreen';
import type { FormDialogSpec, FormValues } from '../components/FormDialog';
import {
  BookingStatus,
  ComplaintCategory,
  ComplaintStatus,
  DataRequestStatus,
  DataRequestType,
  DiscountType,
  GateDeviceStatus,
  NotificationChannel,
  NotificationStatus,
  ParkingLotStatus,
  PaymentMethod,
  PaymentStatus,
  PromotionSponsor,
  RefundStatus,
  RiskFlagStatus,
  RiskSeverity,
  SanctionLevel,
  SanctionStatus,
  SecurityEventType,
  SettlementStatus,
  ShiftStatus,
  VoucherStatus,
} from '@/types/adminEnums';
import { humanizeEnum } from '../utils/enumLabels';

/**
 * Cấu hình các màn danh sách mà backend CHƯA có endpoint (lấy đúng title, filter, cột, dialog từ Figma
 * "Admin Portal v1"). Số liệu trên Figma (count, subtitle có số) KHÔNG được chép vào đây.
 */
const values = <T extends Record<string, string>>(e: T, pick?: Array<T[keyof T]>) => (pick ?? Object.values(e)) as string[];
const enumOptions = (list: string[]) => list.map((v) => ({ value: v, label: humanizeEnum(v) }));

const notZero = (v: unknown) => (String(v ?? '').trim() !== '' && Number(v) === 0 ? 'Amount must not be 0. Use − for a debit.' : null);
const positive = (v: unknown) => (String(v ?? '').trim() !== '' && !(Number(v) > 0) ? 'Amount must be greater than 0.' : null);
const endAfterStart = (startKey: string) => (v: unknown, all: FormValues) => {
  const start = all[startKey];
  return v && start && new Date(String(v)) <= new Date(String(start)) ? 'End must be after start.' : null;
};

/* ===== Dialogs (Figma "… / Dialog – …"), chưa có endpoint ghi ===== */

const createAdjustment: FormDialogSpec = {
  title: 'Create adjustment',
  description: 'Adds a FinancialAdjustments row. Without a settlement it applies to the next cycle.',
  confirmLabel: 'Create adjustment',
  fields: [
    { key: 'owner', kind: 'select', label: 'Owner', required: true, options: [], placeholder: 'No owner source yet' },
    { key: 'settlement', kind: 'select', label: 'Settlement', options: [], helper: 'Optional — only Draft / Pending review settlements' },
    { key: 'amount', kind: 'number', label: 'Amount (₫)', required: true, helper: 'Positive = credit, negative = debit', validate: notZero },
    { key: 'reason', kind: 'textarea', label: 'Reason', required: true, maxLength: 500, requiredMessage: 'A reason is required.' },
  ],
};

const issueSanction: FormDialogSpec = {
  title: 'Issue sanction',
  description: 'Creates a Sanctions row. Owner-level locks go through Users › Lock owner (US-096).',
  size: 640,
  confirmLabel: 'Issue sanction',
  fields: [
    { key: 'owner', kind: 'select', label: 'Owner', required: true, options: [], placeholder: 'No owner source yet' },
    { key: 'lot', kind: 'select', label: 'Lot (optional)', options: [], helper: 'Empty = whole owner' },
    { key: 'level', kind: 'select', label: 'Level', required: true, options: enumOptions(values(SanctionLevel)) },
    { key: 'reason', kind: 'textarea', label: 'Reason', required: true, maxLength: 1000, requiredMessage: 'A reason is required.' },
    { key: 'evidence', kind: 'upload', label: 'Evidence (EvidenceJson)' },
    { key: 'starts', kind: 'datetime', label: 'Starts', required: true, half: true },
    { key: 'ends', kind: 'datetime', label: 'Ends', helper: 'Empty = no end (Permanent ban only)', half: true, validate: endAfterStart('starts') },
    { key: 'penalty', kind: 'number', label: 'Penalty amount (₫)', helper: 'Optional · deducted from the owner’s settlement' },
  ],
};

const issueVoucher: FormDialogSpec = {
  title: 'Issue compensation voucher',
  description: 'CompensationVouchers · Status = Issued. Optionally charged to an owner.',
  confirmLabel: 'Issue voucher',
  fields: [
    { key: 'driver', kind: 'select', label: 'Driver', required: true, options: [], placeholder: 'No driver source yet' },
    { key: 'amount', kind: 'number', label: 'Amount (₫)', required: true, half: true, validate: positive },
    { key: 'expires', kind: 'date', label: 'Expires', required: true, half: true },
    { key: 'reason', kind: 'textarea', label: 'Reason', required: true, maxLength: 500, requiredMessage: 'A reason is required.' },
    { key: 'booking', kind: 'select', label: 'Booking', options: [], half: true },
    { key: 'complaint', kind: 'select', label: 'Complaint', options: [], half: true },
    { key: 'chargeTo', kind: 'select', label: 'Charge to owner', options: [], helper: 'Empty = platform pays' },
  ],
};

const createPromotion: FormDialogSpec = {
  title: 'Create platform promotion',
  description: 'Promotions · Sponsor = Platform. Owner promotions are created in the owner portal.',
  size: 640,
  confirmLabel: 'Create promotion',
  fields: [
    { key: 'code', kind: 'text', label: 'Code', required: true, half: true, maxLength: 50 },
    { key: 'name', kind: 'text', label: 'Name', required: true, half: true, maxLength: 200 },
    { key: 'discountType', kind: 'select', label: 'Discount type', required: true, half: true, options: enumOptions(values(DiscountType)) },
    {
      key: 'discountValue',
      kind: 'number',
      label: 'Discount value',
      required: true,
      half: true,
      validate: (v, all) => {
        const n = Number(v);
        if (String(v ?? '') === '') return null;
        if (all.discountType === DiscountType.Percentage && (n < 1 || n > 100)) return 'Percentage must be 1–100.';
        return n > 0 ? null : 'Discount must be greater than 0.';
      },
    },
    { key: 'maxDiscount', kind: 'number', label: 'Max discount (₫)', half: true },
    { key: 'minOrder', kind: 'number', label: 'Min order (₫)', half: true },
    { key: 'starts', kind: 'datetime', label: 'Starts', required: true, half: true },
    { key: 'ends', kind: 'datetime', label: 'Ends', required: true, half: true, validate: endAfterStart('starts') },
    { key: 'usageLimit', kind: 'number', label: 'Usage limit', half: true },
    { key: 'lot', kind: 'select', label: 'Lot (optional)', half: true, options: [], placeholder: 'All lots' },
  ],
};

const addHoliday: FormDialogSpec = {
  title: 'Add holiday',
  description: 'Holidays (Date, Name, City). One row per date and city.',
  confirmLabel: 'Add holiday',
  fields: [
    { key: 'date', kind: 'date', label: 'Date', required: true },
    { key: 'name', kind: 'text', label: 'Name', required: true, maxLength: 200 },
    { key: 'city', kind: 'select', label: 'City', options: [], helper: 'Empty = nationwide' },
  ],
};

export const exportReportDialog: FormDialogSpec = {
  title: 'Export report',
  description: 'Generates a CSV of the current report and filters.',
  confirmLabel: 'Export',
  fields: [
    {
      key: 'report',
      kind: 'select',
      label: 'Report',
      required: true,
      options: ['Revenue', 'Occupancy', 'Bookings', 'Owner performance'].map((r) => ({ value: r, label: r })),
    },
    { key: 'from', kind: 'date', label: 'Period from', required: true, half: true },
    { key: 'to', kind: 'date', label: 'Period to', required: true, half: true, validate: endAfterStart('from') },
    { key: 'format', kind: 'select', label: 'Format', required: true, options: [{ value: 'csv', label: 'CSV (UTF-8)' }] },
  ],
};

export const createFaqDialog: FormDialogSpec = {
  title: 'Create FAQ article',
  description: 'FaqArticles',
  size: 640,
  confirmLabel: 'Create',
  fields: [
    { key: 'category', kind: 'text', label: 'Category', required: true, half: true, maxLength: 50 },
    {
      key: 'audience',
      kind: 'select',
      label: 'Audience',
      required: true,
      half: true,
      options: [
        { value: 'Driver', label: 'Driver' },
        { value: 'LotOwner', label: 'Lot owner' },
        { value: 'All', label: 'All' },
      ],
    },
    { key: 'question', kind: 'text', label: 'Question', required: true, maxLength: 500, helper: 'Max 500', requiredMessage: 'A question is required.' },
    { key: 'answer', kind: 'textarea', label: 'Answer', required: true, maxLength: 4000 },
    { key: 'sortOrder', kind: 'number', label: 'Sort order' },
    { key: 'published', kind: 'toggle', label: 'Published' },
  ],
};

/* ===== Danh sách ===== */

export const UNAVAILABLE_SCREENS = {
  parkingLots: {
    title: 'Parking Lots',
    subtitle: '—',
    filterGroups: [
      { param: 'status', title: 'Lot status', values: values(ParkingLotStatus) },
      { param: 'capacity', title: 'Capacity flags', allLabel: 'Any', values: ['Emergency stopped', 'Instant booking paused', 'Stale (no heartbeat)'] },
    ],
    searchPlaceholder: 'Search lot name, address, owner…',
    toolbarFilters: ['City', 'Integration'],
    columns: ['Lot', 'City / district', 'Available / total', 'Integration', 'KYB', 'Status', 'Capacity flag', ''],
    service: 'ParkingService',
    missingEndpoint: 'GET /api/v1/parking-lots (admin list)',
  },
  bookings: {
    title: 'Bookings',
    subtitle: '13 statuses (BookingStatus)',
    filterGroups: [{ param: 'status', title: 'Booking status', values: values(BookingStatus) }],
    searchPlaceholder: 'Search booking code, plate, driver…',
    toolbarFilters: ['Lot', 'Date'],
    columns: ['Booking', 'Vehicle / driver', 'Lot / slot', 'Period', 'Status', 'Total', ''],
    service: 'BookingService',
    missingEndpoint: 'GET /api/v1/bookings (admin list across users)',
    actions: [{ label: 'Monthly passes', variant: 'onNavy', to: '/admin/operations/bookings/monthly-passes' }],
  },
  monthlyPasses: {
    title: 'Monthly passes',
    subtitle: 'Phase 2 · FEATURE_MONTHLY_PASS is off — read-only preview of MonthlyPasses',
    banner: { tone: 'warning', text: 'Phase 2 (US-111). Hidden for drivers while FEATURE_MONTHLY_PASS = off. Toggle in Access & Configuration › Feature Flags.' },
    columns: ['Pass', 'Plate', 'Lot / slot', 'Valid', 'Price', 'Auto-renew', 'Status'],
    service: 'BookingService',
    missingEndpoint: 'GET /api/v1/monthly-passes',
  },
  gateShifts: {
    title: 'Gate Activity',
    subtitle: 'Shifts (ShiftStatus Open / Closed / Flagged)',
    filterGroups: [{ param: 'status', title: 'Shift status', values: values(ShiftStatus) }],
    columns: ['Shift', 'Staff', 'Lot', 'Started', 'Ended', 'In / out', 'Manual', 'Cash', 'Status'],
    service: 'GateService',
    missingEndpoint: 'GET /api/v1/shifts',
  },
  settlements: {
    title: 'Settlements',
    subtitle: '—',
    banner: { tone: 'info', text: 'One settlement per owner per cycle (SETTLEMENT_CYCLE = WEEKLY). Net payout = Gross − Commission − Refunds − Held + Adjustments.' },
    filterGroups: [{ param: 'status', title: 'Settlement status', values: values(SettlementStatus) }],
    searchPlaceholder: 'Search owner…',
    toolbarFilters: ['Period'],
    columns: ['Owner', 'Period', 'Gross', 'Commission', 'Refunds', 'Held', 'Adjust.', 'Net payout', 'Status'],
    service: 'PaymentService',
    missingEndpoint: 'GET /api/v1/settlements',
  },
  payouts: {
    title: 'Payouts',
    subtitle: 'Approved settlements → bank transfer → Paid (PayoutReference, PaidAtUtc)',
    filterGroups: [{ param: 'status', title: 'Payout', allLabel: 'Awaiting payout (Approved)', values: [SettlementStatus.Paid] }],
    columns: ['Owner / settlement', 'Bank account', 'Net payout', 'Approved', 'Payout reference', 'Status', ''],
    service: 'PaymentService',
    missingEndpoint: 'GET /api/v1/settlements?status=Approved',
  },
  commission: {
    title: 'Commission',
    subtitle: 'Read-only · rates per owner',
    banner: { tone: 'info', text: 'Default rate comes from DEFAULT_COMMISSION_RATE (System Settings). Owner-specific rates come from OwnerProfiles.CommissionRateOverride.' },
    columns: ['Owner', 'Effective rate', 'Source', 'Contract (Future)', 'Commission this cycle', 'Owner status'],
    service: 'UserService / PaymentService',
    missingEndpoint: 'GET /api/v1/admin/commission-rates',
    actions: [{ label: 'Change default rate', variant: 'onNavy', to: '/admin/access/system-settings/DEFAULT_COMMISSION_RATE' }],
  },
  adjustments: {
    title: 'Adjustments',
    subtitle: 'FinancialAdjustments · positive = credit to owner, negative = debit',
    searchPlaceholder: 'Search owner, reason…',
    toolbarFilters: ['Owner'],
    columns: ['#', 'Owner', 'Settlement', 'Amount', 'Reason', 'Created by', 'Created'],
    service: 'PaymentService',
    missingEndpoint: 'GET/POST /api/v1/financial-adjustments',
    actions: [{ label: 'Create adjustment', dialog: createAdjustment }],
  },
  transactions: {
    title: 'Transactions',
    subtitle: 'Payments · methods VNPAY / MoMo / VietQR / Cash',
    filterGroups: [
      { param: 'status', title: 'Payment status', values: values(PaymentStatus) },
      { param: 'method', title: 'Method', allLabel: 'Any', values: values(PaymentMethod) },
    ],
    searchPlaceholder: 'Search payment, booking, provider txn…',
    toolbarFilters: ['Purpose'],
    columns: ['Payment', 'Booking / session', 'Lot', 'Method', 'Purpose', 'Amount', 'Refunded', 'Status', 'Paid at'],
    service: 'PaymentService',
    missingEndpoint: 'GET /api/v1/payments (admin list; today only ?bookingId=)',
  },
  refunds: {
    title: 'Refunds',
    subtitle: 'RefundStatus',
    filterGroups: [{ param: 'status', title: 'Refund status', values: values(RefundStatus) }],
    searchPlaceholder: 'Search refund, payment, complaint…',
    toolbarFilters: ['Reason'],
    columns: ['Refund', 'Payment', 'Reason', 'Amount', 'Complaint', 'Requested by', 'Status', ''],
    service: 'PaymentService',
    missingEndpoint: 'GET /api/v1/refunds',
  },
  disputes: {
    title: 'Disputes',
    subtitle: '—',
    banner: { tone: 'info', text: 'Disputes = complaints with money at stake (categories Lot full, Overcharge, Refund request, Damage).' },
    filterGroups: [
      { param: 'status', title: 'Complaint status', values: values(ComplaintStatus) },
      {
        param: 'category',
        title: 'Category',
        allLabel: 'All disputes',
        values: values(ComplaintCategory, [ComplaintCategory.LotFull, ComplaintCategory.Overcharge, ComplaintCategory.RefundRequest, ComplaintCategory.Damage]),
      },
    ],
    searchPlaceholder: 'Search complaint code, booking, driver…',
    columns: ['Code', 'Category', 'Driver / booking', 'Lot', 'Requested', 'Owner due', 'Priority', 'Held', 'Status'],
    service: 'SupportService',
    missingEndpoint: 'GET /api/v1/complaints (admin list; today only ?ownerProfileId=)',
  },
  tickets: {
    title: 'Tickets',
    subtitle: '—',
    banner: { tone: 'info', text: 'Tickets = complaints without a money claim (Staff behavior, App error, Other).' },
    filterGroups: [{ param: 'status', title: 'Complaint status', values: values(ComplaintStatus) }],
    columns: ['Code', 'Category', 'Reporter / booking', 'Lot', 'Priority', 'Status', ''],
    service: 'SupportService',
    missingEndpoint: 'GET /api/v1/complaints?kind=ticket',
  },
  sanctions: {
    title: 'Sanctions',
    subtitle: 'SLA sanctions on owners / lots · 4 levels (SanctionLevel)',
    filterGroups: [
      { param: 'status', title: 'Sanction status', values: values(SanctionStatus) },
      { param: 'level', title: 'Level', allLabel: 'Any', values: values(SanctionLevel) },
    ],
    searchPlaceholder: 'Search owner, lot, reason…',
    columns: ['#', 'Owner / scope', 'Level', 'Reason', 'Period', 'Penalty', 'User sync', 'Status'],
    service: 'AdminService',
    missingEndpoint: 'GET/POST /api/v1/admin/sanctions (today only per owner)',
    actions: [{ label: 'Issue sanction', dialog: issueSanction }],
  },
  riskFlags: {
    title: 'Risk Flags',
    subtitle: 'Fraud / abuse signals (RiskFlags)',
    filterGroups: [
      { param: 'status', title: 'Flag status', values: values(RiskFlagStatus) },
      { param: 'severity', title: 'Severity', allLabel: 'Any', values: values(RiskSeverity) },
    ],
    columns: ['Rule', 'Subject', 'Severity', 'Description', 'Due', 'Assigned', 'Status'],
    service: 'AdminService',
    missingEndpoint: 'GET /api/v1/admin/risk-flags',
  },
  reviews: {
    title: 'Reviews',
    subtitle: 'Reviews (IsFlagged, IsHidden) · one per booking',
    filterGroups: [
      { param: 'moderation', title: 'Moderation', values: ['Flagged', 'Hidden'] },
      { param: 'rating', title: 'Rating', allLabel: 'Any', values: ['5 ★', '4 ★', '3 ★', '2 ★', '1 ★'] },
    ],
    columns: ['Reviewer / booking', 'Lot', 'Rating', 'Comment', 'Owner reply', 'Flag', 'Visibility', ''],
    service: 'SupportService',
    missingEndpoint: 'GET /api/v1/reviews (admin moderation list; today only ?parkingLotId=)',
  },
  vouchers: {
    title: 'Vouchers',
    subtitle: 'CompensationVouchers',
    filterGroups: [{ param: 'status', title: 'Voucher status', values: values(VoucherStatus) }],
    columns: ['Code', 'Driver', 'Amount', 'Reason', 'Charged to', 'Expires', 'Status', ''],
    service: 'PaymentService',
    missingEndpoint: 'GET/POST /api/v1/compensation-vouchers',
    actions: [{ label: 'Issue voucher', dialog: issueVoucher }],
  },
  templates: {
    title: 'Notification templates',
    subtitle: 'NotificationTemplates · channels In-app / Email / SMS / Push',
    filterGroups: [{ param: 'channel', title: 'Channel', values: values(NotificationChannel) }],
    columns: ['Key', 'Channel', 'Title template', 'Body template', 'Active', ''],
    service: 'NotificationService',
    missingEndpoint: 'GET/PUT /api/v1/notification-templates',
  },
  sendHistory: {
    title: 'Send history',
    subtitle: 'Notifications (Status Pending / Sent / Failed)',
    filterGroups: [{ param: 'status', title: 'Status', values: values(NotificationStatus) }],
    searchPlaceholder: 'Search user, template key…',
    columns: ['User', 'Channel', 'Template', 'Title', 'Status', 'Retry', 'Last error', 'Sent at', 'Read'],
    service: 'NotificationService',
    missingEndpoint: 'GET /api/v1/notifications (admin list across users)',
  },
  promotions: {
    title: 'Promotions',
    subtitle: 'Promotions + PromotionRedemptions · US-073',
    filterGroups: [
      { param: 'sponsor', title: 'Sponsor', values: values(PromotionSponsor, [PromotionSponsor.Platform, PromotionSponsor.LotOwner]) },
      { param: 'state', title: 'State', allLabel: 'Any', values: ['Active', 'Inactive'] },
    ],
    columns: ['Code', 'Name', 'Sponsor', 'Discount', 'Limits', 'Period', 'Used / limit', 'State', ''],
    service: 'PaymentService',
    missingEndpoint: 'GET/POST /api/v1/promotions',
    actions: [{ label: 'Create promotion', dialog: createPromotion }],
  },
  holidays: {
    title: 'Holidays',
    subtitle: '—',
    banner: { tone: 'info', text: 'Holidays (PaymentService) drive RateCards.HolidayMultiplier. City empty = nationwide.' },
    columns: ['Date', 'Name', 'City', ''],
    service: 'PaymentService',
    missingEndpoint: 'GET/POST/DELETE /api/v1/holidays',
    actions: [{ label: 'Add holiday', dialog: addHoliday }],
  },
  dataRequests: {
    title: 'Security & Privacy',
    subtitle: 'DataSubjectRequests · SecurityEvents',
    banner: { tone: 'info', text: 'Personal-data requests under Decree 13/2023 (US-006, US-007). DueAtUtc is the legal deadline.' },
    filterGroups: [
      { param: 'status', title: 'Request status', values: values(DataRequestStatus) },
      { param: 'type', title: 'Request type', allLabel: 'Any', values: values(DataRequestType) },
    ],
    columns: ['Request', 'User', 'Type', 'Status', 'Due', 'Handled by', ''],
    service: 'UserService',
    missingEndpoint: 'GET /api/v1/admin/data-requests',
  },
  securityEvents: {
    title: 'Security & Privacy',
    subtitle: 'SecurityEvents',
    filterGroups: [{ param: 'type', title: 'Event type', values: values(SecurityEventType) }],
    columns: ['At (ICT)', 'Event', 'User', 'Identifier', 'IP', 'Detail'],
    service: 'UserService',
    missingEndpoint: 'GET /api/v1/admin/security-events',
  },
} satisfies Record<string, UnavailableListSpec>;

/** Giá trị enum dùng cho cột lọc Gate Devices (màn có gọi API thật). */
export const GATE_DEVICE_STATUSES = values(GateDeviceStatus);
