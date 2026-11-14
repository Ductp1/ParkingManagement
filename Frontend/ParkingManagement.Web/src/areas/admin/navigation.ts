import navOverview from './assets/icons/nav-overview.svg';
import navOperations from './assets/icons/nav-operations.svg';
import navFinance from './assets/icons/nav-finance.svg';
import navSupport from './assets/icons/nav-support.svg';
import navCommunications from './assets/icons/nav-communications.svg';
import navReports from './assets/icons/nav-reports.svg';
import navAccess from './assets/icons/nav-access.svg';

/** Asset icon Figma kèm kích thước gốc (đã gồm margin phải 8px tới nhãn). */
export interface AdminIconAsset {
  src: string;
  width: number;
  height: number;
}

export interface AdminTab {
  key: string;
  label: string;
  path: string;
  /** false = chưa có nguồn dữ liệu: ẩn khỏi menu, route hiện màn "This page isn't available yet". */
  available: boolean;
}

export interface AdminSection {
  key: string;
  label: string;
  path: string;
  icon: AdminIconAsset;
  tabs: AdminTab[];
}

const tab = (sectionPath: string, key: string, label: string, available = true): AdminTab => ({
  key,
  label,
  path: `${sectionPath}/${key}`,
  available,
});

/** Tab chưa có nguồn dữ liệu (P0-10): ẩn khỏi menu thay vì hiện bảng/bộ lọc giả. */
const NOT_YET = false;

/**
 * IA Admin Portal theo Figma "Admin Portal v1" (page 65:2): 7 mục sidebar → tab trên header.
 * URL: /admin/{section}/{tab}[/...detail].
 */
export const ADMIN_SECTIONS: AdminSection[] = [
  {
    key: 'overview',
    label: 'Overview',
    path: '/admin/overview',
    icon: { src: navOverview, width: 21.5, height: 13.5 },
    tabs: [tab('/admin/overview', 'dashboard', 'Dashboard')],
  },
  {
    key: 'operations',
    label: 'Operations',
    path: '/admin/operations',
    icon: { src: navOperations, width: 17.75, height: 13.5 },
    tabs: [
      tab('/admin/operations', 'partner-onboarding', 'Partner Onboarding'),
      tab('/admin/operations', 'parking-lots', 'Parking Lots'),
      tab('/admin/operations', 'capacity-config', 'Capacity Config'),
      tab('/admin/operations', 'kyb-review', 'KYB Review'),
      tab('/admin/operations', 'identity-review', 'Identity Review'),
      tab('/admin/operations', 'lot-contracts', 'Lot Contracts'),
      tab('/admin/operations', 'contract-templates', 'Contract Templates'),
      tab('/admin/operations', 'users', 'Users'),
      tab('/admin/operations', 'bookings', 'Bookings', NOT_YET),
      tab('/admin/operations', 'gate-activity', 'Gate Activity'),
    ],
  },
  {
    key: 'finance',
    label: 'Finance',
    path: '/admin/finance',
    icon: { src: navFinance, width: 21.5, height: 15 },
    tabs: [
      tab('/admin/finance', 'settlements', 'Settlements'),
      tab('/admin/finance', 'payouts', 'Payouts'),
      tab('/admin/finance', 'commission', 'Commission'),
      tab('/admin/finance', 'adjustments', 'Adjustments'),
      tab('/admin/finance', 'transactions', 'Transactions', NOT_YET),
      tab('/admin/finance', 'refunds', 'Refunds', NOT_YET),
    ],
  },
  {
    key: 'support',
    label: 'Support & Sanctions',
    path: '/admin/support',
    icon: { src: navSupport, width: 21.5, height: 14.25 },
    tabs: [
      tab('/admin/support', 'disputes', 'Disputes', NOT_YET),
      tab('/admin/support', 'tickets', 'Tickets', NOT_YET),
      tab('/admin/support', 'sanctions', 'Sanctions'),
      tab('/admin/support', 'risk-flags', 'Risk Flags', NOT_YET),
      tab('/admin/support', 'reviews', 'Reviews'),
      tab('/admin/support', 'vouchers', 'Vouchers', NOT_YET),
    ],
  },
  {
    key: 'communications',
    label: 'Communications',
    path: '/admin/communications',
    icon: { src: navCommunications, width: 23, height: 15 },
    tabs: [
      tab('/admin/communications', 'send-notification', 'Send notification'),
      tab('/admin/communications', 'templates', 'Templates', NOT_YET),
      tab('/admin/communications', 'send-history', 'Send history', NOT_YET),
      tab('/admin/communications', 'faq', 'FAQ'),
      tab('/admin/communications', 'promotions', 'Promotions', NOT_YET),
    ],
  },
  {
    key: 'reports',
    label: 'Reports',
    path: '/admin/reports',
    icon: { src: navReports, width: 21.5, height: 13.5 },
    tabs: [
      tab('/admin/reports', 'revenue', 'Revenue'),
      tab('/admin/reports', 'occupancy', 'Occupancy', NOT_YET),
      tab('/admin/reports', 'bookings', 'Bookings', NOT_YET),
      tab('/admin/reports', 'owner-performance', 'Owner performance', NOT_YET),
      tab('/admin/reports', 'exports', 'Exports', NOT_YET),
    ],
  },
  {
    key: 'access',
    label: 'Access & Configuration',
    path: '/admin/access',
    icon: { src: navAccess, width: 23.075, height: 15 },
    tabs: [
      tab('/admin/access', 'system-settings', 'System Settings'),
      tab('/admin/access', 'feature-flags', 'Feature Flags'),
      tab('/admin/access', 'roles-permissions', 'Roles & Permissions'),
      tab('/admin/access', 'holidays', 'Holidays'),
      tab('/admin/access', 'audit-log', 'Audit Log'),
      tab('/admin/access', 'security-privacy', 'Security & Privacy'),
    ],
  },
];

/**
 * Màn con (không phải tab) chưa có nguồn dữ liệu – route vẫn tồn tại nhưng hiện màn "not available yet".
 */
export const UNAVAILABLE_SUB_PATHS: readonly string[] = [
  '/admin/operations/bookings/monthly-passes',
  '/admin/operations/gate-activity/shifts',
  '/admin/access/security-privacy/security-events',
];

/** Cờ dev: localStorage "admin.showUnavailable" = "1" → vẫn hiện tab chưa có nguồn trong menu (chỉ khi chạy dev). */
export const SHOW_UNAVAILABLE_KEY = 'admin.showUnavailable';

export const showUnavailableTabs = (): boolean => {
  if (!import.meta.env.DEV) return false;
  try {
    return window.localStorage.getItem(SHOW_UNAVAILABLE_KEY) === '1';
  } catch {
    return false;
  }
};

/** Tab hiện trên menu của một nhóm. */
export const visibleTabs = (section: AdminSection): AdminTab[] =>
  showUnavailableTabs() ? section.tabs : section.tabs.filter((t) => t.available);

/** Tab đầu tiên dùng được của nhóm (đích của mục sidebar và nút "Back to <Group>"). */
export const firstTabPath = (section: AdminSection): string => (section.tabs.find((t) => t.available) ?? section.tabs[0]).path;

/** false khi đường dẫn trỏ vào tab hoặc màn con chưa có nguồn dữ liệu – dùng để không tạo link tới đó. */
export const isAdminPathAvailable = (path: string): boolean => {
  const pathname = path.split(/[?#]/)[0];
  if (UNAVAILABLE_SUB_PATHS.some((p) => pathname === p || pathname.startsWith(`${p}/`))) return false;
  const [, , sectionKey, tabKey, ...rest] = pathname.split('/');
  const section = ADMIN_SECTIONS.find((s) => s.key === sectionKey);
  const t = section?.tabs.find((x) => x.key === tabKey);
  // Trang chi tiết (vd. /operations/bookings/:code) có nguồn riêng, chỉ danh sách là chưa có.
  if (!t || rest.length > 0) return true;
  return t.available;
};

export const ADMIN_HOME_PATH = firstTabPath(ADMIN_SECTIONS[0]);

export interface AdminLocation {
  section: AdminSection;
  tab: AdminTab | null;
}

/** Xác định mục sidebar + tab đang mở từ pathname (/admin/{section}/{tab}/...). */
export const resolveAdminLocation = (pathname: string): AdminLocation | null => {
  const [, , sectionKey, tabKey] = pathname.split('/');
  const section = ADMIN_SECTIONS.find((s) => s.key === sectionKey);
  if (!section) return null;
  return { section, tab: section.tabs.find((t) => t.key === tabKey) ?? null };
};
