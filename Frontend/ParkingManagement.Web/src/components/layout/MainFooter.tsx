import React from 'react';
import { NavLink } from 'react-router-dom';

export const MainFooter: React.FC = () => {
  return (
    <footer className="bg-white border-t border-slate-200 py-6 text-slate-500 text-xs mt-auto">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 flex flex-col md:flex-row items-center justify-between gap-4">
        {/* Brand & Copyright */}
        <div className="flex items-center gap-2.5">
          <div className="w-6 h-6 rounded-lg bg-blue-600 text-white flex items-center justify-center font-black text-xs shadow-xs">
            P
          </div>
          <span className="font-bold text-slate-900 text-sm">ParkMaster</span>
          <span className="text-slate-300">•</span>
          <span>Bản quyền © 2026 ParkMaster Vietnam. All rights reserved.</span>
        </div>

        {/* Essential Navigation Links */}
        <div className="flex flex-wrap items-center gap-5 font-medium text-slate-600 text-xs">
          <NavLink to="/driver/find-lot" className="hover:text-blue-600 transition-colors">
            Tìm bãi đỗ
          </NavLink>
          <NavLink to="/driver/vehicles" className="hover:text-blue-600 transition-colors">
            Xe của tôi
          </NavLink>
          <NavLink to="/driver/history" className="hover:text-blue-600 transition-colors">
            Lịch sử đặt
          </NavLink>
          <NavLink to="/driver/support" className="hover:text-blue-600 transition-colors">
            Hỗ trợ CSKH
          </NavLink>
        </div>

        {/* Live Status & Hotline */}
        <div className="flex items-center gap-3">
          <div className="flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-emerald-50 text-emerald-700 font-semibold text-[11px] border border-emerald-200">
            <span className="w-1.5 h-1.5 rounded-full bg-emerald-500 animate-pulse"></span>
            <span>Hệ thống trực tuyến</span>
          </div>
          <a
            href="tel:19006868"
            className="flex items-center gap-1.5 px-3 py-1 rounded-lg bg-slate-100 hover:bg-blue-50 hover:text-blue-700 text-slate-700 font-bold text-xs transition-colors"
          >
            <span className="material-symbols-outlined text-[15px] text-blue-600">call</span>
            <span>Hotline: 1900 6868</span>
          </a>
        </div>
      </div>
    </footer>
  );
};
