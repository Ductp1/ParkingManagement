import { cleanup, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { PartnerApplyPage } from './PartnerApplyPage';
import { emptyForm, stepErrors, toRequest, validate, type PartnerApplyForm } from './partnerApplyForm';

const h = vi.hoisted(() => ({ submit: vi.fn() }));
vi.mock('@/api/partnerApplicationsApi', () => ({ submitPartnerApplication: h.submit }));
vi.mock('@/components/auth/AuthLayout', () => ({ AuthLayout: ({ children }: { children: React.ReactNode }) => <main>{children}</main> }));

const byId = (key: string) => document.getElementById(`partner-${key}`) as HTMLInputElement;
const next = () => screen.getByRole('button', { name: /Tiếp tục/ });

const fillContact = async (user: ReturnType<typeof userEvent.setup>) => {
  await user.type(byId('fullName'), 'Trần Văn Minh');
  await user.type(byId('email'), 'Minh@Example.VN');
  await user.type(byId('phone'), '+84 901 234 567');
};

const fillBusiness = async (user: ReturnType<typeof userEvent.setup>) => {
  await user.click(screen.getByRole('radio', { name: 'Hộ kinh doanh' }));
  await user.type(byId('businessName'), 'Hộ kinh doanh Bãi xe Minh');
  await user.type(byId('taxCode'), '079201001234');
  await user.type(byId('businessAddress'), '12 Võ Văn Ngân, TP. Thủ Đức');
  await user.type(byId('legalRepresentativeName'), 'Trần Văn Minh');
};

const fillLots = async (user: ReturnType<typeof userEvent.setup>) => {
  await user.type(byId('primaryLotAddress'), '45 Kha Vạn Cân, TP. Thủ Đức');
  await user.type(byId('estimatedMotorbikeSlots'), '120');
};

const renderPage = () =>
  render(
    <MemoryRouter>
      <PartnerApplyPage />
    </MemoryRouter>
  );

beforeEach(() => h.submit.mockReset());
afterEach(cleanup);

describe('partnerApplyForm', () => {
  const filled = (patch: Partial<PartnerApplyForm> = {}): PartnerApplyForm => ({
    ...emptyForm(),
    fullName: 'Trần Văn Minh', email: 'Minh@Example.VN', phone: '84901234567', businessType: 'Company', businessName: 'Công ty A',
    taxCode: '0312 345 678', businessAddress: '12 Võ Văn Ngân', legalRepresentativeName: 'Trần Văn Minh', lotCount: '3',
    primaryLotAddress: '45 Kha Vạn Cân', consentAccepted: true, ...patch,
  });

  it('builds the request with normalized values and nulls for empty optional fields', () => {
    expect(toRequest(filled())).toMatchObject({
      email: 'minh@example.vn', phone: '0901234567', taxCode: '0312345678', lotCount: 3, position: null, estimatedCarSlots: null,
      operatingHours: null, consentAccepted: true,
    });
  });

  it('checks each step separately and the whole form at the end', () => {
    expect(validate(filled())).toEqual({});
    expect(Object.keys(stepErrors(emptyForm(), 0)).sort()).toEqual(['email', 'fullName', 'phone']);
    expect(Object.keys(stepErrors(emptyForm(), 2))).toEqual(['primaryLotAddress']);
    expect(validate(filled({ lotCount: '101', estimatedCarSlots: '1.5', taxCode: '12345' }))).toMatchObject({
      lotCount: 'Số bãi đỗ từ 1 đến 100.',
      taxCode: 'Mã số thuế gồm 10 số, 10-3 số (chi nhánh) hoặc 12 số (cá nhân).',
    });
  });
});

describe('PartnerApplyPage', () => {
  it('blocks a step with errors and shows them on the fields', async () => {
    const user = userEvent.setup();
    renderPage();

    await user.click(next());

    expect(screen.getByText('Vui lòng nhập họ và tên.')).toBeTruthy();
    expect(screen.getByText('Email chưa đúng định dạng.')).toBeTruthy();
    expect(byId('phone').getAttribute('aria-invalid')).toBe('true');
    expect(byId('fullName')).toBeTruthy();
  });

  it('walks through 4 steps, submits and shows the application code', async () => {
    const user = userEvent.setup();
    h.submit.mockResolvedValue({ code: 'PA-2026-0011', status: 'Submitted', submittedAtUtc: '2026-11-13T03:00:00Z' });
    renderPage();

    await fillContact(user);
    await user.click(next());
    await fillBusiness(user);
    await user.click(next());
    await fillLots(user);
    await user.click(next());

    expect(screen.getByText('Hộ kinh doanh Bãi xe Minh')).toBeTruthy();
    await user.click(screen.getByRole('button', { name: /Gửi hồ sơ/ }));
    expect(screen.getByText('Vui lòng đồng ý để ParkMaster xử lý thông tin đăng ký.')).toBeTruthy();
    expect(h.submit).not.toHaveBeenCalled();

    await user.click(byId('consentAccepted'));
    await user.click(screen.getByRole('button', { name: /Gửi hồ sơ/ }));

    await waitFor(() => expect(h.submit).toHaveBeenCalledTimes(1));
    expect(h.submit.mock.calls[0][0]).toMatchObject({
      email: 'minh@example.vn', phone: '0901234567', businessType: 'HouseholdBusiness', taxCode: '079201001234', lotCount: 1,
      estimatedMotorbikeSlots: 120, estimatedCarSlots: null, consentAccepted: true,
    });
    expect(await screen.findByRole('heading', { name: 'Đã nhận hồ sơ của bạn' })).toBeTruthy();
    expect(screen.getByText('PA-2026-0011')).toBeTruthy();
  });

  it('keeps entered values when going back and lets the review edit a step', async () => {
    const user = userEvent.setup();
    renderPage();

    await fillContact(user);
    await user.click(next());
    await user.click(screen.getByRole('button', { name: /Quay lại/ }));
    expect(byId('fullName').value).toBe('Trần Văn Minh');

    await user.click(next());
    await fillBusiness(user);
    await user.click(next());
    await fillLots(user);
    await user.click(next());
    await user.click(screen.getAllByRole('button', { name: 'Sửa' })[1]);
    expect(byId('businessName').value).toBe('Hộ kinh doanh Bãi xe Minh');
  });

  it('shows the server message on 409 and a friendly message on 429', async () => {
    const user = userEvent.setup();
    const conflict = 'Đã có hồ sơ đối tác đang được xử lý với email này. Vui lòng liên hệ hotline 1900 6868 để được hỗ trợ.';
    h.submit
      .mockRejectedValueOnce(Object.assign(new Error(conflict), { status: 409 }))
      .mockRejectedValueOnce(Object.assign(new Error('Request failed with status code 429'), { status: 429 }));
    renderPage();

    await fillContact(user);
    await user.click(next());
    await fillBusiness(user);
    await user.click(next());
    await fillLots(user);
    await user.click(next());
    await user.click(byId('consentAccepted'));

    await user.click(screen.getByRole('button', { name: /Gửi hồ sơ/ }));
    expect((await screen.findByRole('alert')).textContent).toBe(conflict);

    await user.click(screen.getByRole('button', { name: /Gửi hồ sơ/ }));
    await waitFor(() => expect(screen.getByRole('alert').textContent).toContain('quá nhiều hồ sơ trong một giờ'));
  });
});
