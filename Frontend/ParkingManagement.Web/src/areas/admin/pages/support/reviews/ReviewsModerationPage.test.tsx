import { useEffect } from 'react';
import { cleanup, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AdminApiError } from '@/api/adminClientApi';
import type { ModeratedReviewDto } from '@/types/adminReviews';
import { AdminToastProvider } from '../../../components/AdminToastProvider';
import { summary } from '../../operations/lots/lotTestFixtures';
import { ReviewsModerationPage } from './ReviewsModerationPage';
import { filterReviews, lotsInQueue, parseRating, ratingCounts } from './reviewFormat';

const h = vi.hoisted(() => ({ reported: vi.fn(), hide: vi.fn(), dismissReport: vi.fn(), lotsSummary: vi.fn() }));
vi.mock('@/api/adminReviewsApi', () => ({ adminReviewsApi: { reported: h.reported, hide: h.hide, dismissReport: h.dismissReport } }));
vi.mock('@/api/adminLotsApi', () => ({ adminLotsApi: { lotsSummary: h.lotsSummary } }));

const review = (patch: Partial<ModeratedReviewDto> = {}): ModeratedReviewDto => ({
  id: 11,
  parkingLotId: 1,
  reviewerName: 'Nguyễn Văn A',
  rating: 1,
  comment: 'Bảo vệ thái độ rất tệ',
  ownerReply: null,
  ownerRepliedAtUtc: null,
  isReported: true,
  reportReason: 'Khách không đỗ xe tại bãi hôm đó',
  reportedAtUtc: '2026-11-12T03:00:00Z',
  isHidden: false,
  createdAtUtc: '2026-11-10T03:00:00Z',
  ...patch,
});

const QUEUE = () => [
  review(),
  review({ id: 12, parkingLotId: 2, reviewerName: 'Trần Thị B', rating: 2, comment: null, reportReason: 'Spam lặp lại nhiều lần' }),
  review({ id: 13, parkingLotId: 99, reviewerName: 'Lê C', rating: 1, comment: 'Giá cao', ownerReply: 'Giá đã niêm yết rõ.' }),
];

const seen = { search: '' };
const LocationSpy = () => {
  const { search } = useLocation();
  useEffect(() => {
    seen.search = search;
  }, [search]);
  return null;
};

const renderAt = (url = '/admin/support/reviews') =>
  render(
    <AdminToastProvider>
      <MemoryRouter initialEntries={[url]}>
        <Routes>
          <Route
            path="/admin/support/reviews"
            element={
              <>
                <ReviewsModerationPage />
                <LocationSpy />
              </>
            }
          />
        </Routes>
      </MemoryRouter>
    </AdminToastProvider>
  );

const reasonBox = (dialog: HTMLElement) => within(dialog).getByRole('textbox');
const dialogNamed = (name: string) => screen.getAllByRole('dialog').find((d) => within(d).queryByRole('heading', { name })) as HTMLElement;

beforeEach(() => {
  Object.values(h).forEach((fn) => fn.mockReset());
  h.reported.mockResolvedValue(QUEUE());
  h.lotsSummary.mockResolvedValue(summary());
  seen.search = '';
});
afterEach(cleanup);

describe('reviewFormat', () => {
  const name = (id: number) => ({ 1: 'Bãi Xanh', 2: 'Bãi Đỏ' })[id as 1 | 2];

  it('filters by search across reviewer, comment, reason, reply and lot name', () => {
    const rows = QUEUE();
    const f = (search: string) => filterReviews(rows, { search, rating: null, lotId: null }, name).map((r) => r.id);
    expect(f('trần')).toEqual([12]);
    expect(f('thái độ')).toEqual([11]);
    expect(f('spam')).toEqual([12]);
    expect(f('niêm yết')).toEqual([13]);
    expect(f('bãi đỏ')).toEqual([12]);
    expect(f('#13')).toEqual([13]);
  });

  it('filters by rating and lot, counts ratings and names unknown lots', () => {
    const rows = QUEUE();
    expect(filterReviews(rows, { search: '', rating: 1, lotId: null }, name).map((r) => r.id)).toEqual([11, 13]);
    expect(filterReviews(rows, { search: '', rating: null, lotId: 2 }, name).map((r) => r.id)).toEqual([12]);
    expect(Object.fromEntries(ratingCounts(rows))).toEqual({ 5: 0, 4: 0, 3: 0, 2: 1, 1: 2 });
    expect(lotsInQueue(rows, name).map((l) => l.label)).toEqual(['Bãi Đỏ', 'Bãi Xanh', 'Lot #99']);
    expect(parseRating('3')).toBe(3);
    expect(parseRating('7')).toBeNull();
  });
});

describe('ReviewsModerationPage', () => {
  it('lists reported reviews with lot names, rating counts and audit link', async () => {
    renderAt();

    expect(await screen.findByText('Nguyễn Văn A')).toBeTruthy();
    expect(screen.getByText('3 reported · oldest report first')).toBeTruthy();
    expect((await screen.findByRole('link', { name: 'Bãi Xanh' })).getAttribute('href')).toBe('/admin/operations/parking-lots/1');
    expect(screen.getByRole('link', { name: 'Lot #99' })).toBeTruthy();
    expect(screen.getByText('Rating only')).toBeTruthy();
    expect(screen.getByRole('link', { name: 'Audit log' }).getAttribute('href')).toBe('/admin/access/audit-log?service=SupportService&entity=Review');
    expect(screen.queryByText(/oldest reports\. Newer ones/)).toBeNull();
  });

  it('filters by rating from the URL and shows the filtered count', async () => {
    renderAt('/admin/support/reviews?rating=2');

    expect(await screen.findByText('Trần Thị B')).toBeTruthy();
    expect(screen.queryByText('Nguyễn Văn A')).toBeNull();
    expect(screen.getByText('1 of 3 reported · oldest report first')).toBeTruthy();
  });

  it('shows a no-match state and a queue-cap note', async () => {
    h.reported.mockResolvedValue(Array.from({ length: 100 }, (_, i) => review({ id: i + 1, rating: 5 })));
    renderAt('/admin/support/reviews?rating=1');

    expect(await screen.findByText('No reviews match')).toBeTruthy();
    expect(screen.getByText('Showing the 100 oldest reports. Newer ones appear as these are handled.')).toBeTruthy();
  });

  it('shows the empty queue and server errors', async () => {
    h.reported.mockResolvedValueOnce([]);
    renderAt();
    expect((await screen.findAllByText('No reported reviews')).length).toBeGreaterThan(0);
    expect(screen.getByText('Reviews appear here when a lot owner reports one from the owner portal.')).toBeTruthy();
    cleanup();

    h.reported.mockRejectedValueOnce(new AdminApiError(500, 'SupportService lỗi.', { title: 'Error', detail: 'SupportService lỗi.' }));
    renderAt();
    expect(await screen.findByText('Could not load reported reviews')).toBeTruthy();
  });

  it('opens the drawer from the URL with full text and history link', async () => {
    renderAt('/admin/support/reviews?review=13');

    const drawer = await screen.findByRole('dialog');
    expect(within(drawer).getByText('Giá đã niêm yết rõ.')).toBeTruthy();
    expect(within(drawer).getByRole('link', { name: 'View in audit log' }).getAttribute('href')).toBe(
      '/admin/access/audit-log?service=SupportService&entity=Review&entityId=13'
    );
  });

  it('hides a review with a validated reason, then toasts and refetches', async () => {
    const user = userEvent.setup();
    h.hide.mockResolvedValue(review({ isHidden: true }));
    renderAt('/admin/support/reviews?review=11');

    await user.click(await screen.findByRole('button', { name: 'Hide review' }));
    const dialog = dialogNamed('Hide review');
    await user.click(within(dialog).getByRole('button', { name: 'Hide review' }));
    expect(within(dialog).getByText('A reason is required.')).toBeTruthy();

    await user.type(reasonBox(dialog), 'Ngắn');
    expect(within(dialog).getByText('Use at least 10 characters.')).toBeTruthy();
    expect(h.hide).not.toHaveBeenCalled();

    await user.type(reasonBox(dialog), ' quá, lời lẽ xúc phạm');
    await user.click(within(dialog).getByRole('button', { name: 'Hide review' }));

    await waitFor(() => expect(h.hide).toHaveBeenCalledWith(11, 'Ngắn quá, lời lẽ xúc phạm'));
    expect(await screen.findByText('Review hidden')).toBeTruthy();
    await waitFor(() => expect(h.reported).toHaveBeenCalledTimes(2));
    await waitFor(() => expect(seen.search).toBe(''));
  });

  it('keeps the server message and reloads the queue on 409', async () => {
    const user = userEvent.setup();
    h.dismissReport.mockRejectedValue(
      new AdminApiError(409, 'Đánh giá không có khiếu nại đang chờ xử lý.', { title: 'Conflict', detail: 'Đánh giá không có khiếu nại đang chờ xử lý.' })
    );
    renderAt('/admin/support/reviews?review=11');

    await user.click(await screen.findByRole('button', { name: 'Dismiss report' }));
    const dialog = dialogNamed('Dismiss report');
    await user.type(reasonBox(dialog), 'Đánh giá đúng trải nghiệm thực tế');
    await user.click(within(dialog).getByRole('button', { name: 'Dismiss report' }));

    expect(await within(dialog).findByText('Đánh giá không có khiếu nại đang chờ xử lý.')).toBeTruthy();
    await waitFor(() => expect(h.reported).toHaveBeenCalledTimes(2));
  });

  it('renders no Vietnamese UI copy of its own', async () => {
    h.reported.mockResolvedValue([review({ comment: null, reportReason: null, reviewerName: 'Driver' })]);
    renderAt('/admin/support/reviews?review=11');
    await screen.findByRole('dialog');
    await waitFor(() => expect(screen.getAllByRole('link', { name: 'Bãi Xanh' }).length).toBe(2));
    const text = document.body.textContent ?? '';
    const withoutData = text.replaceAll('Bãi Xanh', '');
    expect(withoutData).not.toMatch(/[ăâđêôơưạảấầẩẫậắằẳẵặẹẻẽếềểễệỉịọỏốồổỗộớờởỡợụủứừửữựỳỵỷỹ]/i);
  });
});
