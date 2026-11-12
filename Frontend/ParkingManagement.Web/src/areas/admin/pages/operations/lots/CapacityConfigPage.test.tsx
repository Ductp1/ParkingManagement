import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { CapacityConfigPage } from './CapacityConfigPage';
import type { LotCapacityConfigDto } from '@/api/capacityApi';

// ---- Mocks ----
const h = vi.hoisted(() => ({
  get: vi.fn(),
  put: vi.fn(),
  capacityChanged: vi.fn(),
}));

vi.mock('@/api/capacityApi', () => ({
  capacityApi: { get: h.get, put: h.put },
}));

vi.mock('@/realtime/useParkingHub', () => ({
  useParkingHub: (_lotId: number, onCapacityChanged?: (e: unknown) => void) => {
    h.capacityChanged = onCapacityChanged as any;
  },
}));

// Dòng 1–22 giữ nguyên bản develop (nhánh US-069 sửa khối mock phía trên) – import bổ sung đặt ở đây để giảm xung đột merge.
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { within } from '@testing-library/react';
import { AdminToastProvider } from '../../../components/AdminToastProvider';

const lots = vi.hoisted(() => ({ lotsSummary: vi.fn() }));
vi.mock('@/api/adminLotsApi', () => ({ adminLotsApi: { lotsSummary: lots.lotsSummary } }));

const snapshot: LotCapacityConfigDto = {
  configured: true,
  capacity: 50,
  transientBuffer: 10,
  subscriberQuota: 10,
  totalParked: 30,
  transientParked: 8,
  subscriberParked: 22,
  activeSubscribers: 25,
  availableForTransient: 2,
  availableForSubscriber: 20,
  version: 4,
  updatedAtUtc: '2026-01-01T00:00:00Z',
};

/** Snapshot khớp: không có cảnh báo (V ≤ B, A ≤ Q). */
const calm: LotCapacityConfigDto = { ...snapshot, activeSubscribers: 5 };

const notConfigured: LotCapacityConfigDto = {
  configured: false, capacity: 0, transientBuffer: 0, subscriberQuota: 0, totalParked: 0, transientParked: 0, subscriberParked: 0,
  activeSubscribers: 0, availableForTransient: 0, availableForSubscriber: 0, version: 0, updatedAtUtc: null,
};

const lot = (parkingLotId: number, name: string) => ({
  parkingLotId, name, city: 'HCMC', district: null, status: 'Active', integrationTier: 'Manual', totalSlots: 50,
  isEmergencyStopped: false, instantBookingPaused: false,
});

const PATH = '/admin/operations/capacity-config';

beforeEach(() => {
  vi.clearAllMocks();
  h.get.mockResolvedValue(calm);
  h.put.mockResolvedValue({ saved: true, warnings: [], version: 5, snapshot: { ...calm, capacity: 60, version: 5, updatedAtUtc: '2026-11-13T03:15:00Z' } });
  lots.lotsSummary.mockResolvedValue({ asOfUtc: '2026-11-13T03:00:00Z', lots: [lot(3, 'Riverside Lot'), lot(7, 'Airport P2')] });
});

// RTL auto-cleanup chỉ hoạt động với test globals → chủ động dọn DOM giữa các test.
afterEach(() => cleanup());

const Probe = () => {
  const location = useLocation();
  return <output data-testid="location">{location.pathname + location.search}</output>;
};

const renderPage = (url = `${PATH}?lotId=3`) =>
  render(
    <AdminToastProvider>
      <MemoryRouter initialEntries={[url]}>
        <Routes>
          <Route path={PATH} element={<><CapacityConfigPage /><Probe /></>} />
        </Routes>
      </MemoryRouter>
    </AdminToastProvider>
  );

const field = (label: 'Total spots' | 'Max walk-in spots' | 'Max monthly-pass spots') => screen.getByLabelText(label) as HTMLInputElement;
const saveButton = () => screen.getByRole('button', { name: 'Save' }) as HTMLButtonElement;
const loaded = () => waitFor(() => expect(field('Total spots').value).toBe('50'));
const location = () => screen.getByTestId('location').textContent ?? '';
const stat = (label: string) => screen.getByText(label).parentElement as HTMLElement;

const setValue = async (label: Parameters<typeof field>[0], text: string) => {
  const user = userEvent.setup();
  await user.clear(field(label));
  if (text !== '') await user.type(field(label), text);
};

describe('CapacityConfigPage (P1-10, US-066) – shell and lot picker', () => {
  it('asks for a lot first and loads nothing', async () => {
    renderPage(PATH);

    expect(await screen.findByText('Choose a lot')).toBeTruthy();
    expect(h.get).not.toHaveBeenCalled();
    expect(screen.getByRole('heading', { level: 1, name: 'Capacity' })).toBeTruthy();
  });

  it('choosing a lot puts it on the URL and loads its capacity; switching lots reloads', async () => {
    renderPage(PATH);
    await waitFor(() => expect((screen.getByLabelText('Lot') as HTMLSelectElement).disabled).toBe(false));

    fireEvent.change(screen.getByLabelText('Lot'), { target: { value: '3' } });
    await waitFor(() => expect(location()).toBe(`${PATH}?lotId=3`));
    await waitFor(() => expect(h.get).toHaveBeenCalledWith(3));
    await loaded();

    fireEvent.change(screen.getByLabelText('Lot'), { target: { value: '7' } });
    await waitFor(() => expect(location()).toBe(`${PATH}?lotId=7`));
    await waitFor(() => expect(h.get).toHaveBeenLastCalledWith(7));
  });

  it('shows live usage with clear labels and units, and no internal symbols, version or Vietnamese text', async () => {
    renderPage();
    await loaded();

    expect(within(stat('Occupied now')).getByText('30 vehicles')).toBeTruthy();
    expect(within(stat('Occupied now')).getByText('of 50 spots')).toBeTruthy();
    expect(within(stat('Walk-in vehicles')).getByText('8 vehicles')).toBeTruthy();
    expect(within(stat('Active monthly passes')).getByText('5')).toBeTruthy();
    expect(within(stat('Walk-in spots left')).getByText('2 spots')).toBeTruthy();
    expect(within(stat('Free spots for monthly passes')).getByText('20 spots')).toBeTruthy();

    const table = screen.getByRole('table', { name: 'Capacity split' });
    expect(within(table).getAllByRole('row').slice(1).map((r) => r.textContent)).toEqual([
      'Walk-in1020%',
      'Monthly pass1020%',
      'Shared (first come, first served)3060%',
    ]);

    const text = document.body.textContent ?? '';
    expect(text).not.toMatch(/[ăâđêôơưàảãáạằẳẵắặầẩẫấậèẻẽéẹềểễếệìỉĩíịòỏõóọồổỗốộờởỡớợùủũúụừửữứựỳỷỹýỵ]/i);
    expect(text).not.toMatch(/\((T|V|A)\)/);
    expect(text).not.toMatch(/Version/);
  });
});

describe('CapacityConfigPage – first view', () => {
  it('prefills current values, shows no errors, computes shared spots and keeps Save off until something changes', async () => {
    renderPage();
    await loaded();

    expect(field('Max walk-in spots').value).toBe('10');
    expect(field('Max monthly-pass spots').value).toBe('10');
    expect(screen.queryAllByRole('alert')).toHaveLength(0);
    expect(screen.getByText('Every spot at this lot, including shared ones. At least 1.')).toBeTruthy();
    expect(screen.getByTestId('shared-spots').textContent).toBe('Shared spots (first come, first served)30 spots');
    expect(saveButton().disabled).toBe(true);
    expect(screen.getByText('Last updated 01/01/2026 07:00 ICT')).toBeTruthy();
  });
});

describe('CapacityConfigPage – field errors (blur / retype)', () => {
  it('blurring an empty field shows its required error only', async () => {
    renderPage();
    await loaded();

    await setValue('Total spots', '');
    fireEvent.blur(field('Total spots'));

    expect(screen.getByText('Enter the total number of spots.')).toBeTruthy();
    expect(field('Total spots').getAttribute('aria-invalid')).toBe('true');
    expect(field('Max walk-in spots').getAttribute('aria-invalid')).toBeNull();
  });

  it('typing a valid value clears the error right away', async () => {
    renderPage();
    await loaded();

    await setValue('Total spots', '');
    expect(screen.getByText('Enter the total number of spots.')).toBeTruthy();
    await userEvent.setup().type(field('Total spots'), '60');
    expect(screen.queryByText('Enter the total number of spots.')).toBeNull();
  });

  it('clearing every field shows each error and keeps Save off, so nothing is sent', async () => {
    renderPage();
    await loaded();

    await setValue('Total spots', '');
    await setValue('Max walk-in spots', '');
    await setValue('Max monthly-pass spots', '');

    expect(screen.getByText('Enter the total number of spots.')).toBeTruthy();
    expect(screen.getByText('Enter the walk-in limit. Enter 0 to stop walk-ins.')).toBeTruthy();
    expect(screen.getByText('Enter the monthly-pass limit. Enter 0 if the lot sells none.')).toBeTruthy();
    expect(saveButton().disabled).toBe(true);
    fireEvent.submit(field('Total spots').closest('form')!);
    expect(h.put).not.toHaveBeenCalled();
  });

  it('"more than total" is attached to the right field: walk-in 60 with total 50', async () => {
    renderPage();
    await loaded();

    await setValue('Max walk-in spots', '60');

    expect(screen.getByText("Walk-in spots (60) can't be more than total spots (50).")).toBeTruthy();
    expect(field('Total spots').getAttribute('aria-invalid')).toBeNull();
    // Helper / lỗi nối vào ô bằng aria-describedby.
    const describedBy = field('Max walk-in spots').getAttribute('aria-describedby')!;
    expect(document.getElementById(describedBy)?.textContent).toBe("Walk-in spots (60) can't be more than total spots (50).");
  });

  it('total spots below the vehicles parked now is blocked, like the server', async () => {
    renderPage();
    await loaded();

    await setValue('Total spots', '20');

    expect(screen.getByText("30 vehicles are parked right now. Total spots can't go below 30.")).toBeTruthy();
    expect(saveButton().disabled).toBe(true);
  });
});

describe('CapacityConfigPage – shared spots', () => {
  it('is hidden (no "—") while a field is empty', async () => {
    renderPage();
    await loaded();

    await setValue('Total spots', '');
    expect(screen.queryByTestId('shared-spots')).toBeNull();
    expect(screen.queryByText('—')).toBeNull();
  });

  it('a negative walk-in value never produces a positive shared number', async () => {
    renderPage();
    await loaded();

    fireEvent.change(field('Max walk-in spots'), { target: { value: '-2' } });

    expect(screen.queryByTestId('shared-spots')).toBeNull();
    expect(screen.getByText('Use a whole number of 0 or more.')).toBeTruthy();
    expect(document.body.textContent).not.toContain('52 spots');
  });

  it('walk-in + monthly pass above total shows one summary error and keeps Save off', async () => {
    renderPage();
    await loaded();

    await setValue('Max walk-in spots', '30');
    await setValue('Max monthly-pass spots', '30');

    expect(screen.getByRole('alert').textContent).toBe(
      'Walk-in (30) plus monthly-pass (30) spots come to 60, more than the 50 total spots. Remove at least 10.'
    );
    expect(screen.queryByTestId('shared-spots')).toBeNull();
    expect(saveButton().disabled).toBe(true);
  });
});

describe('CapacityConfigPage – warnings (saved anyway)', () => {
  it('warns when the walk-in limit goes below walk-in vehicles parked now, but still allows saving', async () => {
    renderPage();
    await loaded();

    await setValue('Max walk-in spots', '5');

    expect(screen.getByText(/8 walk-in vehicles are parked now, more than the new limit of 5/)).toBeTruthy();
    expect(saveButton().disabled).toBe(false);
  });

  it('warns about active monthly passes above the limit', async () => {
    h.get.mockResolvedValue(snapshot); // 25 passes, limit 10
    renderPage();
    await loaded();

    expect(screen.getByText(/25 monthly passes are active, more than the new limit of 10/)).toBeTruthy();
  });
});

describe('CapacityConfigPage – saving', () => {
  it('Save turns on only when the form changed and is valid; Reset restores the saved values', async () => {
    renderPage();
    await loaded();

    await setValue('Total spots', '60');
    expect(saveButton().disabled).toBe(false);

    fireEvent.click(screen.getByRole('button', { name: 'Reset' }));
    expect(field('Total spots').value).toBe('50');
    expect(saveButton().disabled).toBe(true);
  });

  it('sends the values with the hidden version, shows a toast and the new last-updated time', async () => {
    const user = userEvent.setup();
    renderPage();
    await loaded();

    await setValue('Total spots', '60');
    await user.click(saveButton());

    await waitFor(() => expect(h.put).toHaveBeenCalledWith(3, { capacity: 60, transientBuffer: 10, subscriberQuota: 10, version: 4 }));
    expect(await screen.findByText('Capacity saved')).toBeTruthy();
    expect(screen.getByText('Riverside Lot · 60 spots')).toBeTruthy();
    expect(screen.getByText('Last updated 13/11/2026 10:15 ICT')).toBeTruthy();
    expect(saveButton().disabled).toBe(true);
  });

  it('asks for confirmation before reducing total spots; cancelling sends nothing', async () => {
    const user = userEvent.setup();
    renderPage();
    await loaded();

    await setValue('Total spots', '40');
    await user.click(saveButton());

    const dialog = screen.getByRole('dialog', { name: 'Reduce total spots?' });
    expect(within(dialog).getByText(/go from 50 to 40/)).toBeTruthy();
    fireEvent.click(within(dialog).getByRole('button', { name: 'Cancel' }));
    expect(h.put).not.toHaveBeenCalled();

    await user.click(saveButton());
    fireEvent.click(within(screen.getByRole('dialog', { name: 'Reduce total spots?' })).getByRole('button', { name: 'Save changes' }));
    await waitFor(() => expect(h.put).toHaveBeenCalledWith(3, { capacity: 40, transientBuffer: 10, subscriberQuota: 10, version: 4 }));
  });

  it('server 400: errors appear on the right field in English (by code) and Save stays off until fixed', async () => {
    h.put.mockRejectedValue({
      response: {
        status: 400,
        data: {
          title: 'Cấu hình sức chứa không hợp lệ',
          errors: { transientBuffer: [{ code: 'TRANSIENT_EXCEEDS_CAPACITY', message: 'Chỗ cho xe vãng lai (60) nhiều hơn tổng số chỗ (50).' }] },
        },
      },
    });
    const user = userEvent.setup();
    renderPage();
    await loaded();

    await setValue('Total spots', '60');
    await user.click(saveButton());

    await waitFor(() => expect(field('Max walk-in spots').getAttribute('aria-invalid')).toBe('true'));
    expect(screen.getByText("Walk-in spots (10) can't be more than total spots (60).")).toBeTruthy();
    expect(saveButton().disabled).toBe(true);

    await setValue('Max walk-in spots', '5'); // sửa → mở khóa
    expect(saveButton().disabled).toBe(false);
  });

  it('409: explains that someone else changed it and offers Reload', async () => {
    h.put.mockRejectedValue({ response: { status: 409, data: { title: 'Version cấu hình đã cũ' } } });
    const user = userEvent.setup();
    renderPage();
    await loaded();

    await setValue('Total spots', '60');
    await user.click(saveButton());

    expect(await screen.findByText('This configuration was changed by someone else. Reload to see the latest values.')).toBeTruthy();
    expect(saveButton().disabled).toBe(true);

    h.get.mockResolvedValue({ ...calm, capacity: 55, version: 6 });
    fireEvent.click(screen.getByRole('button', { name: 'Reload' }));
    await waitFor(() => expect(field('Total spots').value).toBe('55'));
    expect(screen.queryByText(/changed by someone else/)).toBeNull();
    expect(h.get).toHaveBeenCalledTimes(2);
  });

  it('other save errors show the server message as is', async () => {
    h.put.mockRejectedValue({ response: { status: 500, data: { title: 'Lỗi hệ thống', detail: 'Lỗi hệ thống' } } });
    const user = userEvent.setup();
    renderPage();
    await loaded();

    await setValue('Total spots', '60');
    await user.click(saveButton());

    expect(await screen.findByText('Lỗi hệ thống')).toBeTruthy();
  });
});

describe('CapacityConfigPage – realtime and other states', () => {
  it('a CapacityChanged event updates live numbers and the next save uses the new version', async () => {
    const user = userEvent.setup();
    renderPage();
    await loaded();

    act(() => {
      h.capacityChanged({
        lotId: 3, capacity: 60, transientBuffer: 12, subscriberQuota: 10,
        availableForTransient: 3, availableForSubscriber: 25, version: 9, timestampUtc: '2026-01-01T01:00:00Z',
      });
    });

    expect(within(stat('Walk-in spots left')).getByText('3 spots')).toBeTruthy();
    expect(within(stat('Free spots for monthly passes')).getByText('25 spots')).toBeTruthy();
    expect(field('Total spots').value).toBe('60'); // form chưa sửa → cập nhật theo

    await setValue('Total spots', '70');
    await user.click(saveButton());
    await waitFor(() => expect(h.put).toHaveBeenCalledWith(3, { capacity: 70, transientBuffer: 12, subscriberQuota: 10, version: 9 }));
  });

  it('a change by someone else while the form is being edited keeps the old version, so saving gets 409 instead of overwriting', async () => {
    h.put.mockRejectedValue({ response: { status: 409, data: { title: 'Version cấu hình đã cũ' } } });
    const user = userEvent.setup();
    renderPage();
    await loaded();

    await setValue('Total spots', '60'); // form đang sửa
    act(() => {
      h.capacityChanged({
        lotId: 3, capacity: 55, transientBuffer: 10, subscriberQuota: 10,
        availableForTransient: 2, availableForSubscriber: 25, version: 9, timestampUtc: '2026-01-01T01:00:00Z',
      });
    });
    expect(field('Total spots').value).toBe('60'); // không ghi đè chữ đang gõ

    await user.click(saveButton());
    await waitFor(() => expect(h.put).toHaveBeenCalledWith(3, { capacity: 60, transientBuffer: 10, subscriberQuota: 10, version: 4 }));
    expect(await screen.findByText('This configuration was changed by someone else. Reload to see the latest values.')).toBeTruthy();
  });

  it('a lot without capacity shows "not configured yet" and an empty form that creates it', async () => {
    h.get.mockResolvedValue(notConfigured);
    const user = userEvent.setup();
    renderPage();

    expect(await screen.findByText('Capacity not configured yet')).toBeTruthy();
    expect(field('Total spots').value).toBe('');
    expect(screen.queryByText(/vehicles are parked right now/)).toBeNull();

    await setValue('Total spots', '40');
    await setValue('Max walk-in spots', '10');
    await setValue('Max monthly-pass spots', '0');
    await user.click(saveButton());
    await waitFor(() => expect(h.put).toHaveBeenCalledWith(3, { capacity: 40, transientBuffer: 10, subscriberQuota: 0, version: 0 }));
  });

  it('a load error shows the server message with Retry', async () => {
    h.get.mockRejectedValueOnce({ response: { status: 500, data: { title: 'Lỗi hệ thống', detail: 'Lỗi hệ thống' } } });
    renderPage();

    expect(await screen.findByText('Could not load capacity')).toBeTruthy();
    expect(screen.getByText('Lỗi hệ thống')).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Retry' }));
    await loaded();
  });

  it('403 shows the no-permission state', async () => {
    h.get.mockRejectedValue({ response: { status: 403 } });
    renderPage();
    expect(await screen.findByText('Only administrators can change lot capacity.')).toBeTruthy();
  });
});
