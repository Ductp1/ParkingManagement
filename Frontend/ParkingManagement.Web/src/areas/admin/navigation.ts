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
}

export interface AdminSection {
  key: string;
  label: string;
  path: string;
  icon: AdminIconAsset;
  tabs: AdminTab[];
}

const tab = (sectionPath: string, key: string, label: string): AdminTab => ({
  key,
  label,
  path: `${sectionPath}/${key}`,
});

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
      tab('/admin/operations', 'kyb-review', 'KYB Review'),
      tab('/admin/operations', 'users', 'Users'),
      tab('/admin/operations', 'bookings', 'Bookings'),
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
      tab('/admin/finance', 'transactions', 'Transactions'),
      tab('/admin/finance', 'refunds', 'Refunds'),
    ],
  },
  {
    key: 'support',
    label: 'Support & Sanctions',
    path: '/admin/support',
    icon: { src: navSupport, width: 21.5, height: 14.25 },
    tabs: [
      tab('/admin/support', 'disputes', 'Disputes'),
      tab('/admin/support', 'tickets', 'Tickets'),
      tab('/admin/support', 'sanctions', 'Sanctions'),
      tab('/admin/support', 'risk-flags', 'Risk Flags'),
      tab('/admin/support', 'reviews', 'Reviews'),
      tab('/admin/support', 'vouchers', 'Vouchers'),
    ],
  },
  {
    key: 'communications',
    label: 'Communications',
    path: '/admin/communications',
    icon: { src: navCommunications, width: 23, height: 15 },
    tabs: [
      tab('/admin/communications', 'send-notification', 'Send notification'),
      tab('/admin/communications', 'templates', 'Templates'),
      tab('/admin/communications', 'send-history', 'Send history'),
      tab('/admin/communications', 'faq', 'FAQ'),
      tab('/admin/communications', 'promotions', 'Promotions'),
    ],
  },
  {
    key: 'reports',
    label: 'Reports',
    path: '/admin/reports',
    icon: { src: navReports, width: 21.5, height: 13.5 },
    tabs: [
      tab('/admin/reports', 'revenue', 'Revenue'),
      tab('/admin/reports', 'occupancy', 'Occupancy'),
      tab('/admin/reports', 'bookings', 'Bookings'),
      tab('/admin/reports', 'owner-performance', 'Owner performance'),
      tab('/admin/reports', 'exports', 'Exports'),
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

export const ADMIN_HOME_PATH = ADMIN_SECTIONS[0].tabs[0].path;

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
