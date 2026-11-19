import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { PartnerRegisterPage } from './PartnerRegisterPage';

const h = vi.hoisted(() => ({ post: vi.fn() }));
vi.mock('@/api/client', () => ({ apiClient: { post: (...args: unknown[]) => h.post(...args) } }));

const file = (name: string, type: string) => new File([new Uint8Array([1, 2, 3])], name, { type });

const type = (label: RegExp, value: string) => fireEvent.change(screen.getByLabelText(label), { target: { value } });
const upload = (label: string, ...files: File[]) => fireEvent.change(screen.getByLabelText(`Tải lên ${label}`), { target: { files } });

const fillValid = () => {
  type(/^Họ và tên/i, 'Trần Văn Minh');
  type(/^Email/i, 'minh@example.vn');
  type(/^Số điện thoại/i, '0901234567');
  type(/^Tên doanh nghiệp/i, 'Công ty TNHH Bãi xe Minh');
  type(/^Mã số thuế/i, '0312345678');
  type(/^Địa chỉ trụ sở/i, '12 Võ Văn Ngân, Thủ Đức');
  type(/^Người đại diện/i, 'Trần Văn Minh');
  type(/^Địa chỉ bãi chính/i, '45 Kha Vạn Cân, Thủ Đức');
  upload('Giấy đăng ký kinh doanh', file('dkkd.pdf', 'application/pdf'));
  upload('Giấy tờ pháp lý mặt bằng bãi', file('mat-bang.pdf', 'application/pdf'));
  upload('CCCD người đại diện – mặt trước', file('cccd-truoc.png', 'image/png'));
  upload('CCCD người đại diện – mặt sau', file('cccd-sau.jpg', 'image/jpeg'));
  fireEvent.click(screen.getByRole('checkbox'));
};

const renderPage = () =>
  render(
    <MemoryRouter>
      <PartnerRegisterPage />
    </MemoryRouter>
  );

beforeEach(() => h.post.mockReset());
afterEach(cleanup);

describe('PartnerRegisterPage (US-053 / US-116 – không cần đăng nhập)', () => {
  it('gửi multipart đúng tên trường BE (CCCD hai mặt riêng) và hiện mã hồ sơ', async () => {
    h.post.mockResolvedValue({ data: { code: 'PA-2026-0021', status: 'Submitted', submittedAtUtc: '2026-11-19T03:00:00Z' } });
    renderPage();

    fillValid();
    fireEvent.click(screen.getByRole('button', { name: /Gửi hồ sơ/ }));

    await waitFor(() => expect(h.post).toHaveBeenCalledTimes(1));
    const [url, form] = h.post.mock.calls[0] as [string, FormData];
    expect(url).toBe('/api/v1/admin/partner-registrations');
    expect((form.get('businessLicenseFiles') as File).name).toBe('dkkd.pdf');
    expect((form.get('lotLegalProofFiles') as File).name).toBe('mat-bang.pdf');
    expect((form.get('idCardFrontFiles') as File).name).toBe('cccd-truoc.png');
    expect((form.get('idCardBackFiles') as File).name).toBe('cccd-sau.jpg');
    expect(form.get('representativeIdFiles')).toBeNull();
    expect(form.get('consentAccepted')).toBe('true');
    expect(await screen.findByText('PA-2026-0021')).toBeTruthy();
  });

  it('thiếu giấy tờ mặt bằng (bắt buộc) → báo lỗi, không gửi', async () => {
    renderPage();
    fillValid();
    fireEvent.click(screen.getByRole('button', { name: /^Bỏ mat-bang\.pdf$/ }));

    fireEvent.click(screen.getByRole('button', { name: /Gửi hồ sơ/ }));

    expect(await screen.findByText('Vui lòng tải lên giấy tờ pháp lý mặt bằng bãi.')).toBeTruthy();
    expect(h.post).not.toHaveBeenCalled();
  });

  it('CCCD mỗi mặt chỉ nhận 1 ảnh JPG / PNG: chọn lại thì thay ảnh cũ, PDF bị từ chối', async () => {
    renderPage();

    upload('CCCD người đại diện – mặt trước', file('a.png', 'image/png'));
    upload('CCCD người đại diện – mặt trước', file('b.jpg', 'image/jpeg'));
    expect(screen.queryByText('a.png')).toBeNull();
    expect(screen.getByText('b.jpg')).toBeTruthy();

    upload('CCCD người đại diện – mặt sau', file('cccd.pdf', 'application/pdf'));
    expect(screen.getByText(/ảnh CCCD chỉ nhận JPG hoặc PNG/)).toBeTruthy();
    expect(screen.queryByText('cccd.pdf')).toBeNull();
  });
});
