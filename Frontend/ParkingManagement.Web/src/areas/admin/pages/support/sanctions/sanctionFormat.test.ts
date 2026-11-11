import { describe, expect, it } from 'vitest';
import { fieldsForLevel, ictInputToUtcIso, levelLabel, toIssueRequest, utcToIctInput, validateIssueForm, type IssueForm } from './sanctionFormat';

// Thứ Sáu 06/11/2026 08:00Z = 15:00 ICT.
const NOW = Date.parse('2026-11-06T08:00:00Z');

const form = (patch: Partial<IssueForm>): IssueForm => ({
  ownerProfileId: 2,
  ownerUserId: 3,
  level: 'Warning',
  lotIds: [],
  reason: 'Từ chối 5 booking',
  endsAt: '',
  correctionDue: '2026-11-13T15:00',
  penalty: '',
  evidence: '',
  confirmOwner: '',
  ...patch,
});

describe('sanctionFormat (US-097) – quy tắc theo cấp khớp server', () => {
  it('giờ nhập hiểu theo ICT', () => {
    expect(ictInputToUtcIso('2026-11-13T15:00')).toBe('2026-11-13T08:00:00.000Z');
    expect(utcToIctInput(NOW)).toBe('2026-11-06T15:00');
    expect(ictInputToUtcIso('')).toBeNull();
  });

  it('trường hiển thị theo cấp', () => {
    expect(fieldsForLevel('Warning')).toEqual({ correctionDue: true, endsAt: false, lots: true, confirmOwner: false });
    expect(fieldsForLevel('RankingPenalty')).toEqual({ correctionDue: false, endsAt: true, lots: true, confirmOwner: false });
    expect(fieldsForLevel('TemporarySuspension')).toEqual({ correctionDue: false, endsAt: true, lots: true, confirmOwner: false });
    expect(fieldsForLevel('PermanentBan')).toEqual({ correctionDue: false, endsAt: false, lots: false, confirmOwner: true });
  });

  it('cảnh cáo cần hạn khắc phục trong tương lai', () => {
    expect(validateIssueForm(form({}), NOW)).toEqual({});
    expect(validateIssueForm(form({ correctionDue: '' }), NOW).correctionDue).toBe('A correction deadline is required.');
    expect(validateIssueForm(form({ correctionDue: '2026-11-06T14:59' }), NOW).correctionDue).toBe('Must be after the current time.');
  });

  it('hạ thứ hạng tối đa 30 ngày', () => {
    expect(validateIssueForm(form({ level: 'RankingPenalty', endsAt: '2026-12-06T15:00' }), NOW)).toEqual({});
    expect(validateIssueForm(form({ level: 'RankingPenalty', endsAt: '2026-12-06T15:01' }), NOW).endsAt).toContain('at most 30 days');
    expect(validateIssueForm(form({ level: 'TemporarySuspension', endsAt: '' }), NOW).endsAt).toBe('An end time is required.');
  });

  it('cấp 4 phải gõ đúng mã chủ bãi; lý do, tiền phạt, chủ bãi bắt buộc hợp lệ', () => {
    const ban = form({ level: 'PermanentBan', correctionDue: '' });
    expect(validateIssueForm(ban, NOW).confirmOwner).toBe('Type 2 to confirm the permanent ban.');
    expect(validateIssueForm({ ...ban, confirmOwner: ' 2 ' }, NOW)).toEqual({});
    const errors = validateIssueForm(form({ ownerProfileId: null, reason: '  ', penalty: '1.234' }), NOW);
    expect(Object.keys(errors).sort()).toEqual(['owner', 'penalty', 'reason']);
    expect(validateIssueForm(form({ lotIds: Array.from({ length: 51 }, (_, i) => i + 1) }), NOW).lots).toBe('At most 50 lots per request.');
  });

  it('body gửi server chỉ chứa trường của cấp đã chọn', () => {
    expect(toIssueRequest(form({ level: 'PermanentBan', lotIds: [4], correctionDue: '2026-11-13T15:00', evidence: 'BK-1', penalty: '500000' }))).toEqual({
      ownerProfileId: 2,
      ownerUserId: 3,
      level: 'PermanentBan',
      parkingLotIds: [],
      reason: 'Từ chối 5 booking',
      evidenceJson: '{"note":"BK-1"}',
      endsAtUtc: null,
      correctionDueAtUtc: null,
      penaltyAmount: 500000,
    });
  });

  it('nhãn cấp 3: theo bãi = tạm ngừng niêm yết, toàn chủ bãi = khóa tài khoản có thời hạn', () => {
    expect(levelLabel('TemporarySuspension', 4)).toBe('L3 · Listing suspension');
    expect(levelLabel('TemporarySuspension', null)).toBe('L3 · Temporary account lock');
  });
});
