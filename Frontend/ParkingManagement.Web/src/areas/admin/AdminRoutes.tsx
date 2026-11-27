import React from 'react';
import { Navigate, Route, Routes } from 'react-router-dom';
import { AdminLayout } from './layout/AdminLayout';
import { SignedOutPage } from './layout/SignedOutPage';
import { ComingSoonScreen } from './components/ComingSoonScreen';
import { DashboardPage } from './pages/overview/DashboardPage';
import { PartnerListPage } from './pages/operations/partner/PartnerListPage';
import { PartnerDetailPage } from './pages/operations/partner/PartnerDetailPage';
import { ParkingLotDetailPage } from './pages/operations/lots/ParkingLotDetailPage';
import { ParkingLotsListPage } from './pages/operations/lots/ParkingLotsListPage';
import { CapacityConfigPage } from './pages/operations/lots/CapacityConfigPage';
import { KybListPage } from './pages/operations/kyb/KybListPage';
import { KybDetailPage } from './pages/operations/kyb/KybDetailPage';
import { ContractTemplateEditorPage } from './pages/operations/contract-templates/ContractTemplateEditorPage';
import { ContractTemplateListPage } from './pages/operations/contract-templates/ContractTemplateListPage';
import { LotContractListPage } from './pages/operations/lot-contracts/LotContractListPage';
import { UsersListPage } from './pages/operations/users/UsersListPage';
import { UserDetailPage } from './pages/operations/users/UserDetailPage';
import { BookingDetailPage } from './pages/operations/bookings/BookingDetailPage';
import { GateDevicesPage, GateSessionsPage } from './pages/operations/gate/GateActivityPages';
import { GateShiftsPage } from './pages/operations/gate/GateShiftsPage';
import { FaqListPage } from './pages/communications/FaqListPage';
import { SendNotificationPage } from './pages/communications/SendNotificationPage';
import { CommissionPage } from './pages/finance/commission/CommissionPage';
import { AdjustmentsPage } from './pages/finance/settlements/AdjustmentsPage';
import { PayoutsPage } from './pages/finance/settlements/PayoutsPage';
import { SettlementsPage } from './pages/finance/settlements/SettlementsPage';
import { RevenueReportPage } from './pages/reports/RevenueReportPage';
import { SystemSettingsListPage } from './pages/access/SystemSettingsListPage';
import { SystemSettingDetailPage } from './pages/access/SystemSettingDetailPage';
import { FeatureFlagsPage } from './pages/access/FeatureFlagsPage';
import { RolesPermissionsPage } from './pages/access/RolesPermissionsPage';
import { AuditLogListPage } from './pages/access/AuditLogListPage';
import { AuditLogDetailPage } from './pages/access/AuditLogDetailPage';
import { DataRequestsPage } from './pages/access/DataRequestsPage';
import { HolidaysPage } from './pages/access/HolidaysPage';
import { IdentityReviewPage } from './pages/operations/identity/IdentityReviewPage';
import { SanctionsListPage } from './pages/support/sanctions/SanctionsListPage';
import { ReviewsModerationPage } from './pages/support/reviews/ReviewsModerationPage';

const SECURITY_TABS = [
  { label: 'Data subject requests', to: '/admin/access/security-privacy', end: true },
  { label: 'Security events', to: '/admin/access/security-privacy/security-events' },
];

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
        <Route path="operations/parking-lots" element={<ParkingLotsListPage />} />
        <Route path="operations/parking-lots/:id" element={<ParkingLotDetailPage />} />
        <Route path="operations/parking-lots/:id/:tab" element={<ParkingLotDetailPage />} />
        <Route path="operations/kyb-review" element={<KybListPage />} />
        <Route path="operations/kyb-review/:id" element={<KybDetailPage />} />
        {/* US-005: duyệt CCCD/GPLX của người dùng (UserService). */}
        <Route path="operations/identity-review" element={<IdentityReviewPage />} />
        <Route path="operations/lot-contracts" element={<LotContractListPage />} />
        <Route path="operations/contract-templates" element={<ContractTemplateListPage />} />
        <Route path="operations/contract-templates/:id" element={<ContractTemplateEditorPage />} />
        <Route path="operations/users" element={<UsersListPage />} />
        <Route path="operations/users/:id" element={<UserDetailPage />} />
        <Route path="operations/bookings" element={<ComingSoonScreen />} />
        <Route path="operations/bookings/monthly-passes" element={<ComingSoonScreen title="Monthly passes" />} />
        <Route path="operations/bookings/:code" element={<BookingDetailPage />} />
        {/* US-066: cấu hình sức chứa & vùng đệm vãng lai (mặc định bãi #1). */}
        <Route path="operations/capacity-config" element={<CapacityConfigPage />} />
        <Route path="operations/gate-activity" element={<GateSessionsPage />} />
        <Route path="operations/gate-activity/devices" element={<GateDevicesPage />} />
        <Route path="operations/gate-activity/shifts" element={<GateShiftsPage />} />

        {/* Finance */}
        {/* US-080: quyết toán định kỳ – kỳ (chi tiết ?settlement=), chi trả, bút toán điều chỉnh. */}
        <Route path="finance/settlements" element={<SettlementsPage />} />
        <Route path="finance/payouts" element={<PayoutsPage />} />
        {/* US-079: hoa hồng nền tảng – theo bãi / chủ bãi / nguồn tỷ lệ (bộ lọc trên URL, đổi tab giữ nguyên). */}
        <Route path="finance/commission" element={<CommissionPage tab="lots" />} />
        <Route path="finance/commission/owners" element={<CommissionPage tab="owners" />} />
        <Route path="finance/commission/sources" element={<CommissionPage tab="sources" />} />
        <Route path="finance/adjustments" element={<AdjustmentsPage />} />
        <Route path="finance/transactions" element={<ComingSoonScreen />} />
        <Route path="finance/refunds" element={<ComingSoonScreen />} />

        {/* Support & Sanctions */}
        <Route path="support/disputes" element={<ComingSoonScreen />} />
        <Route path="support/tickets" element={<ComingSoonScreen />} />
        <Route path="support/sanctions" element={<SanctionsListPage />} />
        <Route path="support/risk-flags" element={<ComingSoonScreen />} />
        <Route path="support/reviews" element={<ReviewsModerationPage />} />
        <Route path="support/vouchers" element={<ComingSoonScreen />} />

        {/* Communications */}
        <Route path="communications/send-notification" element={<SendNotificationPage />} />
        <Route path="communications/templates" element={<ComingSoonScreen />} />
        <Route path="communications/send-history" element={<ComingSoonScreen />} />
        <Route path="communications/faq" element={<FaqListPage />} />
        <Route path="communications/promotions" element={<ComingSoonScreen />} />

        {/* Reports */}
        <Route path="reports/revenue" element={<RevenueReportPage />} />
        <Route path="reports/occupancy" element={<ComingSoonScreen />} />
        <Route path="reports/bookings" element={<ComingSoonScreen />} />
        <Route path="reports/owner-performance" element={<ComingSoonScreen />} />
        <Route path="reports/exports" element={<ComingSoonScreen />} />

        {/* Access & Configuration */}
        <Route path="access/system-settings" element={<SystemSettingsListPage />} />
        <Route path="access/system-settings/:key" element={<SystemSettingDetailPage />} />
        <Route path="access/feature-flags" element={<FeatureFlagsPage />} />
        <Route path="access/roles-permissions" element={<RolesPermissionsPage />} />
        <Route path="access/holidays" element={<HolidaysPage />} />
        <Route path="access/audit-log" element={<AuditLogListPage />} />
        <Route path="access/audit-log/:id" element={<AuditLogDetailPage />} />
        {/* US-007: yêu cầu xem/xuất/sửa/xóa dữ liệu cá nhân (UserService). */}
        <Route path="access/security-privacy" element={<DataRequestsPage subTabs={SECURITY_TABS} />} />
        <Route path="access/security-privacy/security-events" element={<ComingSoonScreen title="Security events" backTo="/admin/access/security-privacy" backLabel="Back to Security & Privacy" />} />

        <Route path="*" element={null} />
      </Route>
    </Routes>
  );
};
