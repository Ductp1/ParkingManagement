export interface ReportSpec {
  title: string;
  source: string;
  kpis: string[];
  panels: Array<{ title: string; description?: string }>;
  service: string;
}

/** Spec từ Figma "Reports / … / Report – Default": nguồn dữ liệu, tên KPI, panel biểu đồ (không chép số liệu). */
export const REPORTS: Record<string, ReportSpec> = {
  revenue: {
    title: 'Revenue',
    source: 'Derived · Payments (Status = Succeeded) grouped by PaidAtUtc, Method; Refunds; Settlements.CommissionAmount',
    kpis: ['Gross revenue', 'Platform commission', 'Refunds', 'Succeeded payments'],
    panels: [
      { title: 'Gross revenue by day', description: 'Payments.Amount where Status = Succeeded, by PaidAtUtc (ICT)' },
      { title: 'Revenue by payment method', description: 'Payments.Method' },
    ],
    service: 'PaymentService',
  },
  occupancy: {
    title: 'Occupancy',
    source: 'Derived · ParkingLots.AvailableSlots/TotalSlots snapshots, SlotStateLogs, ParkingSessions (EntryAtUtc / ExitAtUtc)',
    kpis: ['Average occupancy', 'Peak hour', 'Average stay', 'Stale lots'],
    panels: [
      { title: 'Check-ins by hour (all lots)', description: 'GateEvents.EventType = CheckIn by hour' },
      { title: 'By lot', description: 'ParkingLots · ParkingSessions · Slots.State = Maintenance' },
    ],
    service: 'ParkingService / GateService',
  },
  bookings: {
    title: 'Bookings',
    source: 'Derived · Bookings by Status, StartAtUtc; BookingStatusLogs; BookingModifications',
    kpis: ['Bookings', 'Completed', 'Cancelled', 'No-show'],
    panels: [
      { title: 'Bookings by day', description: 'Bookings.CreatedAtUtc' },
      { title: 'Status mix (BookingStatus)', description: 'Completed · Cancelled · No-show · Expired · Rejected' },
    ],
    service: 'BookingService',
  },
  ownerPerformance: {
    title: 'Owner performance',
    source: 'Derived · Settlements, Complaints (by OwnerProfileId), Reviews.Rating, Sanctions, OwnerResponseDueAtUtc vs OwnerRespondedAtUtc',
    kpis: [],
    panels: [
      { title: 'Owners', description: 'Joined in reporting layer by OwnerProfileId (no cross-DB FK)' },
      { title: 'Disputes per 1,000 bookings', description: 'Complaints (dispute categories) / Bookings' },
    ],
    service: 'Reporting layer',
  },
};
