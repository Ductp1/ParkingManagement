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
    h.capacityChanged = onCapacityChanged;
  },
}));

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

beforeEach(() => {
  vi.clearAllMocks();
  h.get.mockResolvedValue(snapshot);
  h.put.mockResolvedValue({ saved: true, warnings: [], version: 5, snapshot: { ...snapshot, version: 5 } });
});

// RTL auto-cleanup chỉ hoạt động với test globals → chủ động dọn DOM giữa các test.
afterEach(() => cleanup());

const setValue = async (testid: string, text: string) => {
  const user = userEvent.setup();
  await user.clear(screen.getByTestId(testid));
  if (text !== '') await user.type(screen.getByTestId(testid), text);
};

describe('CapacityConfigPage (US-066) – hiển thị ban đầu', () => {
  it('mới vào trang: KHÔNG viền đỏ/lỗi nào, chỉ có gợi ý xám; nút Lưu khóa vì chưa có thay đổi', async () => {
    render(<CapacityConfigPage lotId={3} />);
    await waitFor(() => expect(screen.getByTestId('runtime-total').textContent).toBe('30'));

    expect(screen.queryByTestId('error-capacity')).toBeNull();
    expect(screen.queryByTestId('error-transientBuffer')).toBeNull();
    expect(screen.queryByTestId('error-subscriberQuota')).toBeNull();
    expect(screen.queryByTestId('summary-error')).toBeNull();
    expect(screen.getByText('Ví dụ: 100')).toBeTruthy();
    expect(screen.getByText('Xe vào ra lẻ, không đăng ký trước')).toBeTruthy();
    expect(screen.getByText('Khách thuê tháng hoặc đặt trước')).toBeTruthy();
    expect(screen.getByTestId('shared-zone').textContent).toContain('Ai đến trước được trước');
    expect(screen.getByTestId('shared-zone-value').textContent).toBe('30'); // 50 - 10 - 10
    expect((screen.getByTestId('submit-button') as HTMLButtonElement).disabled).toBe(true);
  });
});

describe('CapacityConfigPage (US-066) – lỗi theo ô (blur / gõ lại)', () => {
  it('blur ô trống → hiện lỗi REQUIRED; chưa blur các ô khác thì không có lỗi', async () => {
    const user = userEvent.setup();
    render(<CapacityConfigPage lotId={3} />);
    await waitFor(() => expect(screen.getByTestId('runtime-total').textContent).toBe('30'));

    await user.clear(screen.getByTestId('input-capacity'));
    fireEvent.blur(screen.getByTestId('input-capacity'));

    expect(screen.getByTestId('error-capacity').textContent).toBe('Hãy nhập tổng số chỗ của bãi (ví dụ: 100).');
    expect(screen.queryByTestId('error-transientBuffer')).toBeNull();
    expect(screen.queryByTestId('error-subscriberQuota')).toBeNull();
  });

  it('ô đang đỏ mà gõ đúng → lỗi biến mất ngay (không cần blur lại)', async () => {
    const user = userEvent.setup();
    render(<CapacityConfigPage lotId={3} />);
    await waitFor(() => expect(screen.getByTestId('runtime-total').textContent).toBe('30'));

    await user.clear(screen.getByTestId('input-capacity'));
    fireEvent.blur(screen.getByTestId('input-capacity'));
    expect(screen.getByTestId('error-capacity')).toBeTruthy();

    await user.type(screen.getByTestId('input-capacity'), '60');
    expect(screen.queryByTestId('error-capacity')).toBeNull();
  });

  it('bấm Lưu khi thiếu dữ liệu → hiện lỗi của MỌI ô trống, không gọi PUT', async () => {
    const user = userEvent.setup();
    render(<CapacityConfigPage lotId={3} />);
    await waitFor(() => expect(screen.getByTestId('runtime-total').textContent).toBe('30'));

    await user.clear(screen.getByTestId('input-capacity'));
    await user.clear(screen.getByTestId('input-transientBuffer'));
    await user.clear(screen.getByTestId('input-subscriberQuota'));
    await user.click(screen.getByTestId('submit-button'));

    expect(screen.getByTestId('error-capacity').textContent).toBe('Hãy nhập tổng số chỗ của bãi (ví dụ: 100).');
    expect(screen.getByTestId('error-transientBuffer').textContent).toBe('Hãy nhập số chỗ cho xe vãng lai. Nhập 0 nếu không nhận.');
    expect(screen.getByTestId('error-subscriberQuota').textContent).toBe('Hãy nhập số chỗ cho khách đăng ký. Nhập 0 nếu không nhận.');
    expect(h.put).not.toHaveBeenCalled();
  });

  it('lỗi "vượt tổng" gắn đúng ô: B = 60 với C = 50 → chỉ ô vãng lai đỏ', async () => {
    render(<CapacityConfigPage lotId={3} />);
    await waitFor(() => expect(screen.getByTestId('runtime-total').textContent).toBe('30'));

    await setValue('input-transientBuffer', '60');
    fireEvent.blur(screen.getByTestId('input-transientBuffer'));

    expect(screen.getByTestId('error-transientBuffer').textContent)
      .toBe('Chỗ cho xe vãng lai (60) nhiều hơn tổng số chỗ (50). Hãy giảm xuống tối đa 50.');
    expect(screen.queryByTestId('error-capacity')).toBeNull();
  });
});

describe('CapacityConfigPage (US-066) – chỗ dùng chung', () => {
  it('thiếu dữ liệu → "—" kèm gợi ý "Nhập đủ 3 ô để xem"', async () => {
    const user = userEvent.setup();
    render(<CapacityConfigPage lotId={3} />);
    await waitFor(() => expect(screen.getByTestId('runtime-total').textContent).toBe('30'));

    await user.clear(screen.getByTestId('input-capacity'));

    expect(screen.getByTestId('shared-zone-value').textContent).toBe('—');
    expect(screen.getByTestId('shared-zone-hint').textContent).toBe('Nhập đủ 3 ô để xem');
  });

  it('nhập B âm → vẫn "—", không tính ra số dương (bug cũ đã sửa)', async () => {
    render(<CapacityConfigPage lotId={3} />);
    await waitFor(() => expect(screen.getByTestId('runtime-total').textContent).toBe('30'));

    // Gõ dấu trừ: input type=number có thể chặn, mô phỏng giá trị text sai qua fireEvent.change.
    fireEvent.change(screen.getByTestId('input-transientBuffer'), { target: { value: '-2' } });

    expect(screen.getByTestId('shared-zone-value').textContent).toBe('—');
    expect(screen.getByTestId('shared-zone').textContent).not.toContain('52'); // 50-(-2)-0=52 không được xuất hiện
  });

  it('B + Q > C → khối đỏ "Đang thiếu N chỗ" + khối lỗi tổng hợp phía trên nút Lưu', async () => {
    render(<CapacityConfigPage lotId={3} />);
    await waitFor(() => expect(screen.getByTestId('runtime-total').textContent).toBe('30'));

    await setValue('input-transientBuffer', '30');
    await setValue('input-subscriberQuota', '30');

    expect(screen.getByTestId('shared-zone').className).toContain('bg-red-50');
    expect(screen.getByTestId('shared-zone-value').textContent).toContain('Đang thiếu 10 chỗ');
    expect(screen.getByTestId('summary-error').textContent)
      .toBe('Vãng lai (30) + đăng ký (30) = 60, vượt tổng số chỗ 50. Hãy giảm bớt ít nhất 10 chỗ.');
    // Lỗi CLIENT: nút vẫn bật để bấm "Lưu" là hiện đầy đủ lỗi; nút chỉ KHÓA khi lỗi SERVER hoặc chưa có thay đổi.
    expect((screen.getByTestId('submit-button') as HTMLButtonElement).disabled).toBe(false);
  });
});

describe('CapacityConfigPage (US-066) – server interaction', () => {
  it('PUT thành công: hiện warnings từ server (luật 7)', async () => {
    h.put.mockResolvedValue({
      saved: true,
      warnings: ['Vùng đệm mới (B = 1) nhỏ hơn số xe vãng lai đang đỗ (V = 8)…'],
      version: 5,
      snapshot: { ...snapshot, version: 5 },
    });
    const user = userEvent.setup();
    render(<CapacityConfigPage lotId={3} />);
    await waitFor(() => expect(screen.getByTestId('runtime-total').textContent).toBe('30'));

    await setValue('input-capacity', '60');
    await user.click(screen.getByTestId('submit-button'));

    await waitFor(() => expect(h.put).toHaveBeenCalledWith(3, { capacity: 60, transientBuffer: 10, subscriberQuota: 10, version: 4 }));
    expect(screen.getByTestId('server-warnings').textContent).toContain('V = 8');
  });

  it('server 400: hiển thị lỗi server đúng chỗ + khóa nút cho tới khi sửa', async () => {
    h.put.mockRejectedValue({
      response: {
        status: 400,
        data: {
          errors: { transientBuffer: { code: 'TRANSIENT_EXCEEDS_CAPACITY', message: 'Chỗ cho xe vãng lai (60) nhiều hơn tổng số chỗ (50). Hãy giảm xuống tối đa 50.' } },
          summary: { code: 'TOTAL_EXCEEDS_CAPACITY', message: 'Vãng lai (30) + đăng ký (30) = 60, vượt tổng số chỗ 50. Hãy giảm bớt ít nhất 10 chỗ.' },
        },
      },
    });
    render(<CapacityConfigPage lotId={3} />);
    await waitFor(() => expect(screen.getByTestId('runtime-total').textContent).toBe('30'));

    const user = userEvent.setup();
    await setValue('input-capacity', '60');
    await user.click(screen.getByTestId('submit-button'));
    await waitFor(() => expect(screen.getByTestId('error-transientBuffer').textContent).toContain('nhiều hơn tổng số chỗ (50)'));

    expect(screen.getByTestId('summary-error').textContent).toContain('Vãng lai (30)');
    expect((screen.getByTestId('submit-button') as HTMLButtonElement).disabled).toBe(true); // còn lỗi server → khóa

    await setValue('input-transientBuffer', '5'); // sửa → mở khóa
    expect((screen.getByTestId('submit-button') as HTMLButtonElement).disabled).toBe(false);
  });

  it('409: báo "có người vừa sửa" và tải lại dữ liệu', async () => {
    h.put.mockRejectedValue({ response: { status: 409 } });
    const user = userEvent.setup();
    render(<CapacityConfigPage lotId={3} />);
    await waitFor(() => expect(h.get).toHaveBeenCalledTimes(1));

    await setValue('input-capacity', '60');
    await user.click(screen.getByTestId('submit-button'));

    await waitFor(() => expect(screen.getByTestId('conflict-banner').textContent).toContain('Có người vừa sửa'));
    expect(h.get).toHaveBeenCalledTimes(2);
  });

  it('nhận event CapacityChanged real-time: số chỗ trống + version cập nhật không cần F5', async () => {
    render(<CapacityConfigPage lotId={3} />);
    await waitFor(() => expect(screen.getByTestId('runtime-total').textContent).toBe('30'));

    act(() => {
      h.capacityChanged({
        lotId: 3, capacity: 60, transientBuffer: 12, subscriberQuota: 10,
        availableForTransient: 3, availableForSubscriber: 25, version: 9, timestampUtc: '2026-01-01T01:00:00Z',
      });
    });

    expect(screen.getByTestId('runtime-avail-transient').textContent).toBe('3');
    expect(screen.getByTestId('runtime-avail-subscriber').textContent).toBe('25');
    expect(screen.getByTestId('runtime-version').textContent).toBe('9');
  });
});