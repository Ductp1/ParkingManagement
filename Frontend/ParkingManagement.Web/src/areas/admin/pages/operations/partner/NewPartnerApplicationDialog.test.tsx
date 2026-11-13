import { cleanup, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AdminApiError } from '@/api/adminClientApi';
import type { PartnerApplicationDetailDto } from '@/types/adminPartnerOnboarding';
import { AdminToastProvider } from '../../../components/AdminToastProvider';
import { PartnerListPage } from './PartnerListPage';
import { emptyIntake, intakeErrors, normalizePhone, toCreateRequest, type PartnerIntakeForm } from './partnerIntake';

const h = vi.hoisted(() => ({ list: vi.fn(), create: vi.fn() }));
vi.mock('@/api/adminPartnerOnboardingApi', () => ({ adminPartnerOnboardingApi: { list: h.list, create: h.create } }));

const filled = (patch: Partial<PartnerIntakeForm> = {}): PartnerIntakeForm => ({
  ...emptyIntake(),
  fullName: ' Trần Văn Minh ',
  email: ' Minh@Example.VN ',
  phone: '+84 901.234.567',
  businessType: 'Company',
  businessName: 'Công ty TNHH Bãi xe Thủ Đức',
  taxCode: '0312 345 678',
  businessAddress: '12 Võ Văn Ngân, TP. Thủ Đức',
  legalRepresentativeName: 'Trần Văn Minh',
  lotCount: '2',
  primaryLotAddress: '45 Kha Vạn Cân, TP. Thủ Đức',
  estimatedCarSlots: '40',
  consentAccepted: true,
  ...patch,
});

const created = (): PartnerApplicationDetailDto => ({
  id: 10, code: 'PA-2026-0010', status: 'Submitted', fullName: 'Trần Văn Minh', email: 'minh@example.vn', phone: '0901234567', position: null,
  businessType: 'Company', businessName: 'Công ty TNHH Bãi xe Thủ Đức', taxCode: '0312345678', businessAddress: '12 Võ Văn Ngân',
  legalRepresentativeName: 'Trần Văn Minh', legalRepresentativePosition: null, businessLicenseUrl: null, lotCount: 2, primaryLotAddress: '45 Kha Vạn Cân',
  estimatedCarSlots: 40, estimatedMotorbikeSlots: null, operatingHours: null, consentAcceptedAtUtc: '2026-11-13T02:00:00Z', reviewerAdminId: null,
  infoRequestNote: null, rejectReason: null, cancelReason: null, payoutBank: null, createdUserId: null, createdOwnerProfileId: null,
  createdAtUtc: '2026-11-13T02:00:00Z', updatedAtUtc: null, nextStatuses: ['UnderReview'],
});

const renderList = () =>
  render(
    <AdminToastProvider>
      <MemoryRouter initialEntries={['/admin/operations/partner-onboarding']}>
        <Routes>
          <Route path="/admin/operations/partner-onboarding" element={<PartnerListPage />} />
          <Route path="/admin/operations/partner-onboarding/:id" element={<p>Detail page</p>} />
        </Routes>
      </MemoryRouter>
    </AdminToastProvider>
  );

const field = (label: string) => screen.getByLabelText(label);

beforeEach(() => {
  h.list.mockReset().mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 20 });
  h.create.mockReset();
});
afterEach(cleanup);

describe('partnerIntake', () => {
  it('normalizes the form into the create request', () => {
    expect(toCreateRequest(filled({ position: '  ', intakeNote: ' Hotline 13/11 ' }))).toEqual({
      fullName: 'Trần Văn Minh', email: 'minh@example.vn', phone: '0901234567', position: null, businessType: 'Company',
      businessName: 'Công ty TNHH Bãi xe Thủ Đức', taxCode: '0312345678', businessAddress: '12 Võ Văn Ngân, TP. Thủ Đức',
      legalRepresentativeName: 'Trần Văn Minh', legalRepresentativePosition: null, lotCount: 2, primaryLotAddress: '45 Kha Vạn Cân, TP. Thủ Đức',
      estimatedCarSlots: 40, estimatedMotorbikeSlots: null, operatingHours: null, consentAccepted: true, intakeNote: 'Hotline 13/11',
    });
  });

  it('matches the server rules for phone, tax code, counts and consent', () => {
    expect(intakeErrors(filled())).toEqual({});
    expect(normalizePhone('84901234567')).toBe('0901234567');
    expect(intakeErrors(filled({ taxCode: '0312345678-001' })).taxCode).toBeUndefined();
    expect(intakeErrors(filled({ taxCode: '079201001234' })).taxCode).toBeUndefined();

    const errors = intakeErrors(filled({
      email: 'nope', phone: '12345', taxCode: '123', lotCount: '0', estimatedCarSlots: '-1', businessType: '', consentAccepted: false,
      businessAddress: 'abc',
    }));
    expect(Object.keys(errors).sort()).toEqual(
      ['businessAddress', 'businessType', 'consentAccepted', 'email', 'estimatedCarSlots', 'lotCount', 'phone', 'taxCode'].sort()
    );
  });
});

describe('New partner application dialog', () => {
  it('validates on submit without calling the API', async () => {
    const user = userEvent.setup();
    renderList();

    await user.click(await screen.findByRole('button', { name: 'New application' }));
    const dialog = screen.getByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: 'Create application' }));

    expect(within(dialog).getByText('Contact name must be 2–200 characters.')).toBeTruthy();
    expect(within(dialog).getByText('Choose the business type.')).toBeTruthy();
    expect(within(dialog).getByText('Confirm the partner agreed to data processing on the call.')).toBeTruthy();
    expect(h.create).not.toHaveBeenCalled();
  });

  it('creates the application, toasts and opens its detail page', async () => {
    const user = userEvent.setup();
    h.create.mockResolvedValue(created());
    renderList();

    await user.click(await screen.findByRole('button', { name: 'New application' }));
    await user.type(field('Contact name'), 'Trần Văn Minh');
    await user.type(field('Email'), 'minh@example.vn');
    await user.type(field('Phone'), '0901 234 567');
    await user.selectOptions(field('Business type'), 'Company');
    await user.type(field('Tax code'), '0312345678');
    await user.type(field('Business name'), 'Công ty TNHH Bãi xe Thủ Đức');
    await user.type(field('Business address'), '12 Võ Văn Ngân, TP. Thủ Đức');
    await user.type(field('Legal representative'), 'Trần Văn Minh');
    await user.type(field('Main lot address'), '45 Kha Vạn Cân, TP. Thủ Đức');
    await user.click(screen.getByRole('switch'));
    await user.type(field('Call note'), 'Hotline 13/11');
    await user.click(screen.getByRole('button', { name: 'Create application' }));

    await waitFor(() => expect(h.create).toHaveBeenCalledTimes(1));
    expect(h.create.mock.calls[0][0]).toMatchObject({
      phone: '0901234567', businessType: 'Company', lotCount: 1, consentAccepted: true, intakeNote: 'Hotline 13/11', estimatedCarSlots: null,
    });
    expect(await screen.findByText('Detail page')).toBeTruthy();
    expect(screen.getByText('Application created')).toBeTruthy();
    expect(screen.getByText('PA-2026-0010 · Công ty TNHH Bãi xe Thủ Đức is waiting for review.')).toBeTruthy();
  });

  it('keeps the dialog open with the server message on 409', async () => {
    const user = userEvent.setup();
    const message = 'Hồ sơ PA-2026-0001 đang được xử lý với cùng email. Mở hồ sơ đó thay vì tạo mới.';
    h.create.mockRejectedValue(new AdminApiError(409, message, { title: 'Conflict', detail: message }));
    renderList();

    await user.click(await screen.findByRole('button', { name: 'New application' }));
    await user.type(field('Contact name'), 'Trần Văn Minh');
    await user.type(field('Email'), 'minh@example.vn');
    await user.type(field('Phone'), '0901234567');
    await user.selectOptions(field('Business type'), 'HouseholdBusiness');
    await user.type(field('Tax code'), '079201001234');
    await user.type(field('Business name'), 'Hộ kinh doanh Minh');
    await user.type(field('Business address'), '12 Võ Văn Ngân, TP. Thủ Đức');
    await user.type(field('Legal representative'), 'Trần Văn Minh');
    await user.type(field('Main lot address'), '45 Kha Vạn Cân, TP. Thủ Đức');
    await user.click(screen.getByRole('switch'));
    await user.click(screen.getByRole('button', { name: 'Create application' }));

    expect(await screen.findByText(message)).toBeTruthy();
    expect(screen.getByRole('dialog')).toBeTruthy();
    expect(screen.queryByText('Detail page')).toBeNull();
  });
});
