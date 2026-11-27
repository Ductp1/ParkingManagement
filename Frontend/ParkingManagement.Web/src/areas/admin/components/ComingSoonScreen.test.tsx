import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { afterEach, describe, expect, it } from 'vitest';
import { ComingSoonScreen } from './ComingSoonScreen';
import { SubTabs } from './SubTabs';

const Probe = () => {
  const location = useLocation();
  return <output data-testid="location">{location.pathname}</output>;
};

afterEach(cleanup);

describe('ComingSoonScreen', () => {
  it('route của tab ẩn: thông báo + nút về tab đầu nhóm, không bảng / bộ lọc', () => {
    render(
      <MemoryRouter initialEntries={['/admin/finance/transactions']}>
        <Routes>
          <Route path="/admin/finance/transactions" element={<ComingSoonScreen />} />
          <Route path="*" element={<Probe />} />
        </Routes>
      </MemoryRouter>
    );
    expect(screen.getByRole('heading', { name: 'Transactions' })).toBeTruthy();
    expect(screen.getByText("This page isn't available yet")).toBeTruthy();
    expect(screen.queryByRole('table')).toBeNull();
    expect(screen.queryByRole('combobox')).toBeNull();
    expect(screen.queryByRole('textbox')).toBeNull();

    fireEvent.click(screen.getByRole('button', { name: 'Back to Finance' }));
    expect(screen.getByTestId('location').textContent).toBe('/admin/finance/settlements');
  });

  it('màn con: tiêu đề và đích quay lại riêng', () => {
    render(
      <MemoryRouter initialEntries={['/admin/access/security-privacy/security-events']}>
        <Routes>
          <Route
            path="/admin/access/security-privacy/security-events"
            element={<ComingSoonScreen title="Security events" backTo="/admin/access/security-privacy" backLabel="Back to Security & Privacy" />}
          />
          <Route path="*" element={<Probe />} />
        </Routes>
      </MemoryRouter>
    );
    expect(screen.getByRole('heading', { name: 'Security events' })).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Back to Security & Privacy' }));
    expect(screen.getByTestId('location').textContent).toBe('/admin/access/security-privacy');
  });
});

describe('SubTabs', () => {
  it('bỏ màn con chưa có nguồn; còn một tab thì không vẽ thanh sub-tab', () => {
    const { container } = render(
      <MemoryRouter initialEntries={['/admin/access/security-privacy']}>
        <SubTabs
          tabs={[
            { label: 'Data subject requests', to: '/admin/access/security-privacy' },
            { label: 'Security events', to: '/admin/access/security-privacy/security-events' },
          ]}
        />
      </MemoryRouter>
    );
    expect(container.querySelector('nav')).toBeNull();
  });

  it('Gate Activity có đủ Sessions, Devices, Shifts', () => {
    render(
      <MemoryRouter initialEntries={['/admin/operations/gate-activity']}>
        <SubTabs
          tabs={[
            { label: 'Sessions', to: '/admin/operations/gate-activity' },
            { label: 'Devices', to: '/admin/operations/gate-activity/devices' },
            { label: 'Shifts', to: '/admin/operations/gate-activity/shifts' },
          ]}
        />
      </MemoryRouter>
    );
    expect(screen.getAllByRole('link').map((l) => l.textContent)).toEqual(['Sessions', 'Devices', 'Shifts']);
  });
});
