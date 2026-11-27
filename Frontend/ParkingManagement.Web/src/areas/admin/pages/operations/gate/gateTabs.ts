/** Sub-tabs Gate Activity (Figma "Sub-tabs": Sessions / Devices / Shifts). */
export const GATE_BASE = '/admin/operations/gate-activity';

export const GATE_SUB_TABS = [
  { label: 'Sessions', to: GATE_BASE },
  { label: 'Devices', to: `${GATE_BASE}/devices` },
  { label: 'Shifts', to: `${GATE_BASE}/shifts` },
];
