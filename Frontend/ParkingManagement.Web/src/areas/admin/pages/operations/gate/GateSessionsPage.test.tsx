import { cleanup, fireEvent, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AdminApiError } from '@/api/adminClientApi';
import type { AdminParkingSessionDto } from '@/types/adminOperations';
import { GateSessionsPage } from './GateActivityPages';
import { renderAt, summary } from '../lots/lotTestFixtures';

const h = vi.hoisted(() => ({ lotsSummary: vi.fn(), occupancy: vi.fn(), getLotSessions: vi.fn(), getGateDevices: vi.fn() }));
vi.mock('@/api/adminLotsApi', () => ({ adminLotsApi: { lotsSummary: h.lotsSummary, occupancy: h.occupancy } }));
vi.mock('@/api/adminOperationsApi', () => ({ adminOperationsApi: { getLotSessions: h.getLotSessions, getGateDevices: h.getGateDevices } }));

const PATH = '/admin/operations/gate-activity';
const location = () => screen.getByTestId('location').textContent ?? '';
const picker = () => screen.getByLabelText('Lot') as HTMLSelectElement;

const session = (patch: Partial<AdminParkingSessionDto> = {}): AdminParkingSessionDto => ({
  id: 7,
  code: 'SS-0007',
  parkingLotId: 2,
  plateNumber: '43A12345',
  plateDisplay: '43A-123.45',
  isWalkIn: true,
  bookingCode: null,
  slotCode: null,
  status: 'Active',
  entryAtUtc: '2026-11-12T02:00:00Z',
  exitAtUtc: null,
  minutesParked: 30,
  checkInMethod: 'Qr',
  entryOcrConfidence: null,
  fee: null,
  ...patch,
});

beforeEach(() => {
  Object.values(h).forEach((fn) => fn.mockReset());
  h.lotsSummary.mockResolvedValue(summary());
  h.getLotSessions.mockResolvedValue([session()]);
});

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

describe('GateSessionsPage – lot picker (US-081)', () => {
  it('asks for a lot first and offers every lot by name instead of an ID box', async () => {
    renderAt(PATH, PATH, <GateSessionsPage />);

    expect(await screen.findByText('Choose a lot')).toBeTruthy();
    await waitFor(() => expect(picker().disabled).toBe(false));
    const options = within(picker()).getAllByRole('option').map((o) => o.textContent);
    expect(options).toEqual(['Choose a lot', 'Bãi Xanh · Hồ Chí Minh', 'Bãi Đỏ · Đà Nẵng', 'Bãi Vàng · Hồ Chí Minh']);
    expect(screen.queryByLabelText('Lot ID')).toBeNull();
    expect(h.getLotSessions).not.toHaveBeenCalled();
  });

  it('loads the sessions of the chosen lot and shows the lot name in the table', async () => {
    renderAt(PATH, PATH, <GateSessionsPage />);
    await waitFor(() => expect(picker().disabled).toBe(false));

    fireEvent.change(picker(), { target: { value: '2' } });

    await waitFor(() => expect(location()).toBe(`${PATH}?lot=2`));
    await waitFor(() => expect(h.getLotSessions).toHaveBeenCalledWith(2, undefined));
    const row = (await screen.findByText('SS-0007')).closest('tr') as HTMLElement;
    expect(within(row).getByText('Bãi Đỏ')).toBeTruthy();
    expect(within(row).queryByText('#2')).toBeNull();
  });

  it('keeps a lot id from the URL that is not in the list', async () => {
    renderAt(PATH, `${PATH}?lot=99`, <GateSessionsPage />);

    await waitFor(() => expect(picker().disabled).toBe(false));
    expect(picker().value).toBe('99');
    expect(within(picker()).getByRole('option', { name: 'Unknown lot' })).toBeTruthy();
    expect(h.getLotSessions).toHaveBeenCalledWith(99, undefined);
  });

  it('shows an error with retry when the lot list cannot be loaded', async () => {
    h.lotsSummary.mockRejectedValueOnce(new AdminApiError(500, 'Lỗi hệ thống', { title: 'Lỗi hệ thống', detail: 'Lỗi hệ thống' }));
    renderAt(PATH, PATH, <GateSessionsPage />);

    expect(await screen.findByText('Could not load parking lots')).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Retry' }));
    expect(await screen.findByText('Choose a lot')).toBeTruthy();
    expect(h.lotsSummary).toHaveBeenCalledTimes(2);
  });

  it('disables the picker while lots are loading', async () => {
    h.lotsSummary.mockReturnValue(new Promise(() => {}));
    renderAt(PATH, PATH, <GateSessionsPage />);

    expect(picker().disabled).toBe(true);
    expect(within(picker()).getByRole('option', { name: 'Loading…' })).toBeTruthy();
  });
});
