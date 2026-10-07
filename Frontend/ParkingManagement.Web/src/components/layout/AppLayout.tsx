import React from 'react';
import { Outlet } from 'react-router-dom';
import { MainNavbar } from './MainNavbar';
import { MainFooter } from './MainFooter';

export const AppLayout: React.FC = () => {
  return (
    <div className="min-h-screen flex flex-col bg-slate-50 text-slate-900 font-sans">
      <MainNavbar />
      <main className="flex-1 w-full">
        <Outlet />
      </main>
      <MainFooter />
    </div>
  );
};
