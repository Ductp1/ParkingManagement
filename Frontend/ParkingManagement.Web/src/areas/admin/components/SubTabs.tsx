import React from 'react';
import { NavLink } from 'react-router-dom';

export interface SubTab {
  label: string;
  to: string;
  /** Tab bị khóa (ví dụ "3D view · Phase 2" khi FEATURE_3D_MAP tắt). */
  disabled?: boolean;
  end?: boolean;
}

/** Sub-tabs trong màn (Figma "Sub-tabs"): cao 40, viền dưới, gap 16; active = Bold 13 navy-900 + gạch chân 2px. */
export const SubTabs: React.FC<{ tabs: SubTab[] }> = ({ tabs }) => (
  <nav className="flex h-10 items-start gap-4 overflow-x-auto border-b border-(--admin-border-default)">
    {tabs.map((tab) =>
      tab.disabled ? (
        <span key={tab.label} aria-disabled="true" className="adm-type-tab-default flex h-[39px] shrink-0 cursor-not-allowed items-center whitespace-nowrap text-(--admin-text-placeholder)">
          {tab.label}
        </span>
      ) : (
        <NavLink
          key={tab.label}
          to={tab.to}
          end={tab.end ?? true}
          replace
          className={({ isActive }) =>
            `flex h-[39px] shrink-0 items-center whitespace-nowrap ${
              isActive
                ? 'adm-type-tab-active border-b-2 border-(--admin-brand-navy-900) text-(--admin-brand-navy-900)'
                : 'adm-type-tab-default text-(--admin-text-nav) hover:text-(--admin-brand-navy-900)'
            }`
          }
        >
          {tab.label}
        </NavLink>
      )
    )}
  </nav>
);
