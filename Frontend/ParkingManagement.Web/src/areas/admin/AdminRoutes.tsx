import React from 'react';
import { Navigate, Route, Routes } from 'react-router-dom';
import { AdminLayout } from './layout/AdminLayout';
import { SignedOutPage } from './layout/SignedOutPage';
import { UnavailableListScreen, type UnavailableListSpec } from './components/UnavailableListScreen';
import { UNAVAILABLE_SCREENS as U } from './pages/unavailableScreens';
import { DashboardPage } from './pages/overview/DashboardPage';
import { PartnerListPage } from './pages/operations/partner/PartnerListPage';
import { PartnerDetailPage } from './pages/operations/partner/PartnerDetailPage';
import { ParkingLotDetailPage } from './pages/operations/lots/ParkingLotDetailPage';
import { KybListPage } from './pages/operations/kyb/KybListPage';
import { KybDetailPage } from './pages/operations/kyb/KybDetailPage';
import { ContractTemplateEditorPage } from './pages/operations/contract-templates/ContractTemplateEditorPage';
import { ContractTemplateListPage } from './pages/operations/contract-templates/ContractTemplateListPage';
import { LotContractListPage } from './pages/operations/lot-contracts/LotContractListPage';
import { UsersListPage } from './pages/operations/users/UsersListPage';
import { UserDetailPage } from './pages/operations/users/UserDetailPage';
import { BookingDetailPage } from './pages/operations/bookings/BookingDetailPage';
import { GateDevicesPage, GateSessionsPage } from './pages/operations/gate/GateActivityPages';
import { GATE_SUB_TABS } from './pages/operations/gate/gateTabs';
import { FaqListPage } from './pages/communications/FaqListPage';
import { SendNotificationPage } from './pages/communications/SendNotificationPage';
import { ExportsPage, ReportPage } from './pages/reports/ReportPages';
import { REPORTS } from './pages/reports/reportSpecs';
import { SystemSettingsListPage } from './pages/access/SystemSettingsListPage';
import { SystemSettingDetailPage } from './pages/access/SystemSettingDetailPage';
import { FeatureFlagsPage } from './pages/access/FeatureFlagsPage';
import { RolesPermissionsPage } from './pages/access/RolesPermissionsPage';
import { AuditLogListPage } from './pages/access/AuditLogListPage';
import { AuditLogDetailPage } from './pages/access/AuditLogDetailPage';

const SECURITY_TABS = [
  { label: 'Data subject requests', to: '/admin/access/security-privacy' },
  { label: 'Security events', to: '/admin/access/security-privacy/security-events' },
];

/** key = remount khi đổi màn để state lọc/dialog không lẫn giữa các tab dùng chung component. */
const unavailable = (key: keyof typeof U, extra?: Partial<UnavailableListSpec>) => (
  <UnavailableListScreen key={key} spec={{ ...U[key], ...extra }} />
);

/** Đường dẫn admin cũ (menu MainNavbar) → tab mới. */
const LEGACY_PATHS: Array<[string, string]> = [
  ['users', 'operations/users'],
  ['kyb-approvals', 'operations/kyb-review'],
  ['parameters', 'access/system-settings'],
  ['toggles', 'access/feature-flags'],
  ['settlements', 'finance/settlements'],
];

/**
 * Cây route /admin/* (gắn trong src/routes/AppRoutes.tsx, bảo vệ role Admin bằng ProtectedRoute).
 * IA theo Figma "Admin Portal v1": /admin/{section}/{tab}[/:id]. AdminLayout chuyển /admin, /admin/{section}
 * và đường dẫn lạ về tab đầu tiên tương ứng.
 */
export const AdminRoutes: React.FC = () => {
  return (
    <Routes>
      <Route path="signed-out" element={<SignedOutPage />} />
      {/* Đường dẫn cũ của menu MainNavbar (/admin/users, /admin/kyb-approvals…) → tab tương ứng của cổng admin. */}
      {LEGACY_PATHS.map(([from, to]) => (
        <Route key={from} path={from} element={<Navigate to={`/admin/${to}`} replace />} />
      ))}
      <Route element={<AdminLayout />}>
        {/* Overview */}
        <Route path="overview/dashboard" element={<DashboardPage />} />

        {/* Operations */}
        <Route path="operations/partner-onboarding" element={<PartnerListPage />} />
        <Route path="operations/partner-onboarding/:id" element={<PartnerDetailPage />} />
        <Route path="operations/parking-lots" element={unavailable('parkingLots')} />
        <Route path="operations/parking-lots/:id" element={<ParkingLotDetailPage />} />
        <Route path="operations/parking-lots/:id/:tab" element={<ParkingLotDetailPage />} />
        <Route path="operations/kyb-review" element={<KybListPage />} />
        <Route path="operations/kyb-review/:id" element={<KybDetailPage />} />
        <Route path="operations/lot-contracts" element={<LotContractListPage />} />
        <Route path="operations/contract-templates" element={<ContractTemplateListPage />} />
        <Route path="operations/contract-templates/:id" element={<ContractTemplateEditorPage />} />
        <Route path="operations/users" element={<UsersListPage />} />
        <Route path="operations/users/:id" element={<UserDetailPage />} />
        <Route path="operations/bookings" element={unavailable('bookings')} />
        <Route path="operations/bookings/monthly-passes" element={unavailable('monthlyPasses')} />
        <Route path="operations/bookings/:code" element={<BookingDetailPage />} />
        <Route path="operations/gate-activity" element={<GateSessionsPage />} />
        <Route path="operations/gate-activity/devices" element={<GateDevicesPage />} />
        <Route path="operations/gate-activity/shifts" element={unavailable('gateShifts', { subTabs: GATE_SUB_TABS })} />

        {/* Finance */}
        <Route path="finance/settlements" element={unavailable('settlements')} />
        <Route path="finance/payouts" element={unavailable('payouts')} />
        <Route path="finance/commission" element={unavailable('commission')} />
        <Route path="finance/adjustments" element={unavailable('adjustments')} />
        <Route path="finance/transactions" element={unavailable('transactions')} />
        <Route path="finance/refunds" element={unavailable('refunds')} />

        {/* Support & Sanctions */}
        <Route path="support/disputes" element={unavailable('disputes')} />
        <Route path="support/tickets" element={unavailable('tickets')} />
        <Route path="support/sanctions" element={unavailable('sanctions')} />
        <Route path="support/risk-flags" element={unavailable('riskFlags')} />
        <Route path="support/reviews" element={unavailable('reviews')} />
        <Route path="support/vouchers" element={unavailable('vouchers')} />

        {/* Communications */}
        <Route path="communications/send-notification" element={<SendNotificationPage />} />
        <Route path="communications/templates" element={unavailable('templates')} />
        <Route path="communications/send-history" element={unavailable('sendHistory')} />
        <Route path="communications/faq" element={<FaqListPage />} />
        <Route path="communications/promotions" element={unavailable('promotions')} />

        {/* Reports */}
        <Route path="reports/revenue" element={<ReportPage key="revenue" spec={REPORTS.revenue} />} />
        <Route path="reports/occupancy" element={<ReportPage key="occupancy" spec={REPORTS.occupancy} />} />
        <Route path="reports/bookings" element={<ReportPage key="bookings" spec={REPORTS.bookings} />} />
        <Route path="reports/owner-performance" element={<ReportPage key="owner" spec={REPORTS.ownerPerformance} />} />
        <Route path="reports/exports" element={<ExportsPage />} />

        {/* Access & Configuration */}
        <Route path="access/system-settings" element={<SystemSettingsListPage />} />
        <Route path="access/system-settings/:key" element={<SystemSettingDetailPage />} />
        <Route path="access/feature-flags" element={<FeatureFlagsPage />} />
        <Route path="access/roles-permissions" element={<RolesPermissionsPage />} />
        <Route path="access/holidays" element={unavailable('holidays')} />
        <Route path="access/audit-log" element={<AuditLogListPage />} />
        <Route path="access/audit-log/:id" element={<AuditLogDetailPage />} />
        <Route path="access/security-privacy" element={unavailable('dataRequests', { subTabs: SECURITY_TABS })} />
        <Route path="access/security-privacy/security-events" element={unavailable('securityEvents', { subTabs: SECURITY_TABS })} />

        <Route path="*" element={null} />
      </Route>
    </Routes>
  );
};
