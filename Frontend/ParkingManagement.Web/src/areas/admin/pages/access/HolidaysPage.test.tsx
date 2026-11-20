import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AdminApiError } from '@/api/adminClientApi';
import type { HolidayDto } from '@/types/adminHolidays';
import { AdminToastProvider } from '../../components/AdminToastProvider';
import { HolidaysPage } from './HolidaysPage';
import { currentIctYear, formatHolidayDate, holidayWeekday, holidayYearOptions, parseHolidayYear } from './holidayFormat';

const h = vi.hoisted(() => ({ list: vi.fn(), create: vi.fn(), remove: vi.fn() }));
vi.mock('@/api/adminHolidaysApi', () => ({ adminHolidaysApi: h }));

const PATH = '/admin/access/holidays';
const holiday = (id: number, date: string, name: string, city: string | null = null): HolidayDto => ({ id, date, name, city });
const LABOUR = holiday(8, '2026-05-01', 'International Labour Day');
const LOCAL = holiday(9, '2026-09-02', 'National Day', 'Đà Nẵng');

const Probe = () => {
  const location = useLocation();
  return <output data-testid="location">{location.pathname + location.search}</output>;
};

const renderPage = (url = PATH) =>
  render(
    <AdminToastProvider>
      <MemoryRouter initialEntries={[url]}>
        <Routes>
          <Route path={PATH} element={<><HolidaysPage /><Probe /></>} />
        </Routes>
      </MemoryRouter>
    </AdminToastProvider>
  );

const location = () => screen.getByTestId('location').textContent ?? '';

const openAdd = async () => {
  await screen.findByText('International Labour Day');
  fireEvent.click(screen.getByRole('button', { name: 'Add holiday' }));
  return screen.getByRole('dialog', { name: 'Add holiday' });
};

beforeEach(() => {
  // 12/11/2026 10:00 ICT → năm hiện tại = 2026.
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(new Date('2026-11-12T03:00:00Z'));
  Object.values(h).forEach((fn) => fn.mockReset());
  h.list.mockResolvedValue([LABOUR, LOCAL]);
});

afterEach(() => {
  cleanup();
  vi.useRealTimers();
  vi.restoreAllMocks();
});

describe('HolidaysPage (US-071)', () => {
  it('lists the holidays of the current year with a real row count and Nationwide for empty city', async () => {
    renderPage();

    const row = (await screen.findByText('International Labour Day')).closest('tr') as HTMLElement;
    expect(h.list).toHaveBeenCalledWith(2026);
    expect(within(row).getByText('01/05/2026')).toBeTruthy();
    expect(within(row).getByText('Friday')).toBeTruthy();
    expect(within(row).getByText('Nationwide')).toBeTruthy();
    expect(within(screen.getByText('National Day').closest('tr') as HTMLElement).getByText('Đà Nẵng')).toBeTruthy();
    expect(screen.getByText('2 holidays in 2026 · holiday rates apply at every lot')).toBeTruthy();
  });

  it('changes year on the URL and reloads that year', async () => {
    renderPage();
    await screen.findByText('International Labour Day');

    fireEvent.change(screen.getByLabelText('Year'), { target: { value: '2027' } });

    await waitFor(() => expect(location()).toBe(`${PATH}?year=2027`));
    await waitFor(() => expect(h.list).toHaveBeenLastCalledWith(2027));
  });

  it('opens the year from the URL and falls back to the current year for an invalid value', async () => {
    renderPage(`${PATH}?year=2031`);
    await waitFor(() => expect(h.list).toHaveBeenCalledWith(2031));
    cleanup();

    h.list.mockClear();
    renderPage(`${PATH}?year=abc`);
    await waitFor(() => expect(h.list).toHaveBeenCalledWith(2026));
  });

  it('shows an empty state and no count when the year has no holidays', async () => {
    h.list.mockResolvedValue([]);
    renderPage();

    expect(await screen.findByText('No holidays in 2026')).toBeTruthy();
    expect(screen.queryByText(/holidays? in 2026 ·/)).toBeNull();
  });

  it('shows the server message on error and retries', async () => {
    h.list.mockRejectedValueOnce(new AdminApiError(500, 'Lỗi hệ thống', { title: 'Lỗi hệ thống', detail: 'Lỗi hệ thống' }));
    renderPage();

    expect(await screen.findByText('Could not load holidays')).toBeTruthy();
    expect(screen.getByText('Lỗi hệ thống')).toBeTruthy();
    expect(screen.queryByText(/holidays? in 2026 ·/)).toBeNull();

    fireEvent.click(screen.getByRole('button', { name: 'Retry' }));
    expect(await screen.findByText('International Labour Day')).toBeTruthy();
    expect(h.list).toHaveBeenCalledTimes(2);
  });

  it('add: date and name are required; the form has no City field', async () => {
    renderPage();
    const dialog = await openAdd();

    expect(within(dialog).queryByLabelText(/City/)).toBeNull();
    fireEvent.click(within(dialog).getByRole('button', { name: 'Add holiday' }));

    expect(within(dialog).getByText('Date is required.')).toBeTruthy();
    expect(within(dialog).getByText('Name is required.')).toBeTruthy();
    expect(h.create).not.toHaveBeenCalled();
  });

  it('add: sends a nationwide holiday, shows a toast and reloads the list', async () => {
    h.create.mockResolvedValue(holiday(20, '2026-12-24', 'Christmas Eve'));
    renderPage();
    const dialog = await openAdd();

    fireEvent.change(within(dialog).getByLabelText(/Date/), { target: { value: '2026-12-24' } });
    fireEvent.change(within(dialog).getByLabelText(/Name/), { target: { value: '  Christmas Eve ' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Add holiday' }));

    await waitFor(() => expect(h.create).toHaveBeenCalledWith({ date: '2026-12-24', name: 'Christmas Eve' }));
    expect(await screen.findByText('Holiday added')).toBeTruthy();
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Add holiday' })).toBeNull());
    await waitFor(() => expect(h.list).toHaveBeenCalledTimes(2));
  });

  it('add: a holiday in another year switches the year filter to it', async () => {
    h.create.mockResolvedValue(holiday(21, '2027-01-01', "New Year's Day"));
    renderPage();
    const dialog = await openAdd();

    fireEvent.change(within(dialog).getByLabelText(/Date/), { target: { value: '2027-01-01' } });
    fireEvent.change(within(dialog).getByLabelText(/Name/), { target: { value: "New Year's Day" } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Add holiday' }));

    await waitFor(() => expect(location()).toBe(`${PATH}?year=2027`));
    await waitFor(() => expect(h.list).toHaveBeenLastCalledWith(2027));
  });

  it('add: keeps the dialog open and shows the server detail on 400', async () => {
    h.create.mockRejectedValue(
      new AdminApiError(400, "Ngày lễ '2026-05-01' trên toàn quốc đã tồn tại trong hệ thống.", {
        title: 'Validation',
        detail: "Ngày lễ '2026-05-01' trên toàn quốc đã tồn tại trong hệ thống.",
      })
    );
    renderPage();
    const dialog = await openAdd();

    fireEvent.change(within(dialog).getByLabelText(/Date/), { target: { value: '2026-05-01' } });
    fireEvent.change(within(dialog).getByLabelText(/Name/), { target: { value: 'Labour Day' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Add holiday' }));

    expect(await within(dialog).findByText("Ngày lễ '2026-05-01' trên toàn quốc đã tồn tại trong hệ thống.")).toBeTruthy();
    expect(screen.getByRole('dialog', { name: 'Add holiday' })).toBeTruthy();
    expect(h.list).toHaveBeenCalledTimes(1);
  });

  it('add: rejects a date outside the supported years without calling the API', async () => {
    renderPage();
    const dialog = await openAdd();

    fireEvent.change(within(dialog).getByLabelText(/Date/), { target: { value: '1999-12-31' } });
    fireEvent.change(within(dialog).getByLabelText(/Name/), { target: { value: 'Old' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Add holiday' }));

    expect(within(dialog).getByText('Pick a date between 2000 and 2100.')).toBeTruthy();
    expect(h.create).not.toHaveBeenCalled();
  });

  it('delete: asks for confirmation, then deletes, shows a toast and reloads', async () => {
    h.remove.mockResolvedValue(undefined);
    renderPage();
    await screen.findByText('International Labour Day');

    fireEvent.click(screen.getByRole('button', { name: 'Delete International Labour Day (01/05/2026)' }));
    const dialog = screen.getByRole('dialog', { name: 'Delete holiday' });
    expect(within(dialog).getByText(/International Labour Day \(01\/05\/2026\) will be removed/)).toBeTruthy();
    expect(h.remove).not.toHaveBeenCalled();

    fireEvent.click(within(dialog).getByRole('button', { name: 'Delete holiday' }));

    await waitFor(() => expect(h.remove).toHaveBeenCalledWith(8));
    expect(await screen.findByText('Holiday deleted')).toBeTruthy();
    await waitFor(() => expect(h.list).toHaveBeenCalledTimes(2));
  });

  it('delete: cancel does not call the API', async () => {
    renderPage();
    await screen.findByText('International Labour Day');

    fireEvent.click(screen.getByRole('button', { name: 'Delete National Day (02/09/2026)' }));
    fireEvent.click(within(screen.getByRole('dialog', { name: 'Delete holiday' })).getByRole('button', { name: 'Cancel' }));

    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Delete holiday' })).toBeNull());
    expect(h.remove).not.toHaveBeenCalled();
  });

  it('delete: shows the server detail when the holiday no longer exists', async () => {
    h.remove.mockRejectedValue(new AdminApiError(404, 'Không tìm thấy Ngày lễ với id 8.', { title: 'Not found', detail: 'Không tìm thấy Ngày lễ với id 8.' }));
    renderPage();
    await screen.findByText('International Labour Day');

    fireEvent.click(screen.getByRole('button', { name: 'Delete International Labour Day (01/05/2026)' }));
    const dialog = screen.getByRole('dialog', { name: 'Delete holiday' });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Delete holiday' }));

    expect(await within(dialog).findByText('Không tìm thấy Ngày lễ với id 8.')).toBeTruthy();
  });
});

describe('holidayFormat', () => {
  it('uses the ICT calendar year for the current year', () => {
    vi.setSystemTime(new Date('2026-12-31T17:30:00Z')); // 01/01/2027 00:30 ICT
    expect(currentIctYear()).toBe(2027);
  });

  it('formats DateOnly values without shifting the day', () => {
    expect(formatHolidayDate('2027-01-01')).toBe('01/01/2027');
    expect(holidayWeekday('2027-01-01')).toBe('Friday');
    expect(holidayWeekday('2026-09-02')).toBe('Wednesday');
  });

  it('parses the year param within 2000–2100', () => {
    expect(parseHolidayYear('2028', 2026)).toBe(2028);
    expect(parseHolidayYear('', 2026)).toBe(2026);
    expect(parseHolidayYear('1999', 2026)).toBe(2026);
    expect(parseHolidayYear('2026.5', 2026)).toBe(2026);
  });

  it('offers previous, current and next two years plus the selected one', () => {
    expect(holidayYearOptions(2026, 2026)).toEqual([2025, 2026, 2027, 2028]);
    expect(holidayYearOptions(2026, 2031)).toEqual([2025, 2026, 2027, 2028, 2031]);
  });
});
