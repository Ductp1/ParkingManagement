import { describe, expect, it } from 'vitest';
import { DSR_AUDIT_ENTITY, KYC_AUDIT_ENTITY, auditEntityLink, auditLogListLink, withSelectedFacet } from './auditLogFormat';

describe('auditLogListLink', () => {
  it('returns the plain list without filters', () => {
    expect(auditLogListLink({})).toBe('/admin/access/audit-log');
  });

  it('adds the source service with entity filters (T-803)', () => {
    expect(auditLogListLink({ service: 'UserService', entity: DSR_AUDIT_ENTITY, entityId: '12' })).toBe(
      '/admin/access/audit-log?service=UserService&entity=DataSubjectRequest&entityId=12'
    );
    expect(auditLogListLink({ service: 'UserService', entity: KYC_AUDIT_ENTITY })).toBe('/admin/access/audit-log?service=UserService&entity=User');
  });

  it('keeps actor and owner filters', () => {
    expect(auditLogListLink({ userId: 3 })).toBe('/admin/access/audit-log?userId=3');
    expect(auditLogListLink({ ownerProfileId: 4, entityId: null })).toBe('/admin/access/audit-log?ownerProfileId=4');
  });
});

describe('auditEntityLink', () => {
  it('opens the affected user and data request', () => {
    expect(auditEntityLink(KYC_AUDIT_ENTITY, '7')).toBe('/admin/operations/users/7');
    expect(auditEntityLink(DSR_AUDIT_ENTITY, '15')).toBe('/admin/access/security-privacy?request=15');
  });

  it('keeps existing targets and returns null for unknown entities or missing ids', () => {
    expect(auditEntityLink('KybApplication', '2')).toBe('/admin/operations/kyb-review/2');
    expect(auditEntityLink('Review', '2')).toBeNull();
    expect(auditEntityLink(DSR_AUDIT_ENTITY, null)).toBeNull();
  });
});

describe('withSelectedFacet', () => {
  const facets = [{ value: 'ParkingService', count: 4 }];

  it('adds a linked filter value that has no entries yet with count 0', () => {
    expect(withSelectedFacet(facets, 'UserService')).toEqual([...facets, { value: 'UserService', count: 0 }]);
  });

  it('leaves the list unchanged when nothing is selected or the value exists', () => {
    expect(withSelectedFacet(facets, null)).toEqual(facets);
    expect(withSelectedFacet(facets, 'ParkingService')).toEqual(facets);
    expect(withSelectedFacet(undefined, '')).toEqual([]);
  });
});
