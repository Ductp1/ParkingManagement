import type {
  ContractPlaceholderDto,
  PlatformPartyDto,
  TemplateAppendixKind,
  TemplateContentDto,
} from '@/types/adminContractTemplates';

export const CONTRACT_TEMPLATE_BASE = '/admin/operations/contract-templates';

export const TEMPLATE_NAME_MAX = 200;
export const TEMPLATE_NOTE_MAX = 1000;
export const TEMPLATE_BODY_MAX = 100_000;
export const PLATFORM_FIELD_MAX = 300;
export const DOCX_MAX_BYTES = 10 * 1024 * 1024;

/** Nhãn các trường Bên A theo thứ tự hiển thị (UI tiếng Anh). */
export const PLATFORM_FIELDS: Array<{ key: keyof PlatformPartyDto; label: string; placeholder: string }> = [
  { key: 'name', label: 'Company name', placeholder: 'CÔNG TY CỔ PHẦN …' },
  { key: 'taxCode', label: 'Enterprise code', placeholder: '0123456789' },
  { key: 'address', label: 'Head-office address', placeholder: 'Số …, Phường …, TP. Hồ Chí Minh' },
  { key: 'phone', label: 'Phone', placeholder: '1900 0000' },
  { key: 'email', label: 'Email', placeholder: 'partner@…' },
  { key: 'representativeName', label: 'Representative', placeholder: 'NGUYỄN VĂN …' },
  { key: 'representativePosition', label: 'Representative position', placeholder: 'Tổng Giám đốc' },
];

export const APPENDIX_KINDS: Array<{ value: TemplateAppendixKind; label: string }> = [
  { value: 'LotInfo', label: 'Parking lot table (filled by the system)' },
  { value: 'PayoutAccount', label: 'Payout bank account table (filled by the system)' },
  { value: 'Text', label: 'Text only' },
];

/** Đổi chỗ một mục trong danh sách (lên / xuống). */
export const move = <T,>(items: T[], from: number, to: number): T[] => {
  if (to < 0 || to >= items.length) return items;
  const next = [...items];
  const [item] = next.splice(from, 1);
  next.splice(to, 0, item);
  return next;
};

export const kindLabel = (kind: string | null | undefined) =>
  kind === 'Document' ? 'DOCX file' : kind === 'Markup' ? 'Legacy markup' : kind === 'Structured' ? 'Form' : '—';

export const SPECIAL_TERMS_LEAD =
  'Các thỏa thuận dưới đây chỉ áp dụng cho Bãi đỗ xe tại phụ lục và được ưu tiên áp dụng nếu khác với các điều khoản khác của Hợp đồng:';

const PLACEHOLDER = /\{\{([A-Za-z]+)\}\}/g;

/** Mọi đoạn chữ Admin nhập trong nội dung form. */
const contentTexts = (c: TemplateContentDto): string[] => [
  c.header.placeOfSigning,
  c.header.title,
  c.header.subtitle ?? '',
  ...c.legalBases,
  c.introduction ?? '',
  ...c.articles.flatMap((a) => [a.title, ...a.clauses]),
  ...c.appendices.flatMap((a) => [a.title, a.subtitle ?? '', ...a.notes]),
  c.signNote ?? '',
  c.footnote ?? '',
];

/** Trường (placeholder) không có trong danh mục, hoặc {{SpecialTerms}} gõ tay (không lặp, theo thứ tự xuất hiện). */
export const unknownFields = (texts: string[], catalog: ContractPlaceholderDto[]): string[] => {
  const known = new Set(catalog.filter((p) => !p.block).map((p) => p.key));
  return [...new Set(texts.flatMap((t) => [...t.matchAll(PLACEHOLDER)].map((m) => m[1])))].filter((k) => !known.has(k));
};

/**
 * Điều kiện áp dụng mẫu form (cùng quy tắc ContractTemplateRules.EnsureReadyToActivate, server kiểm tra lại):
 * có tiêu đề, ≥ 1 điều, đúng một điều Thỏa thuận riêng, đủ trường bắt buộc, đủ Bên A, không có trường lạ.
 */
export const structuredBlockers = (c: TemplateContentDto, platform: PlatformPartyDto, catalog: ContractPlaceholderDto[]): string[] => {
  const texts = contentTexts(c);
  const used = new Set(texts.flatMap((t) => [...t.matchAll(PLACEHOLDER)].map((m) => m[1])));
  // Hệ thống tự in: số hợp đồng (đầu trang), tên đối tác (khối Bên B), tên bãi (bảng phụ lục bãi).
  used.add('ContractNumber');
  used.add('BusinessName');
  if (c.appendices.some((a) => a.kind === 'LotInfo')) used.add('LotName');
  const missing = catalog.filter((p) => p.required && !p.block && !used.has(p.key)).map((p) => `{{${p.key}}}`);
  const unknown = unknownFields(texts, catalog);
  const emptyPlatform = PLATFORM_FIELDS.filter((f) => platform[f.key].trim() === '').map((f) => f.label.toLowerCase());
  const special = c.articles.filter((a) => a.isSpecialTerms).length;
  return [
    ...(c.header.title.trim() === '' ? ['Contract title is empty.'] : []),
    ...(c.articles.length === 0 ? ['Add at least one article.'] : []),
    ...(special !== 1 ? ['Mark exactly one article as "Special terms".'] : []),
    ...(c.articles.some((a) => a.title.trim() === '') ? ['Every article needs a title.'] : []),
    ...(missing.length ? [`Missing fields: ${missing.join(', ')}.`] : []),
    ...(unknown.length ? [`Unknown fields: ${unknown.map((k) => `{{${k}}}`).join(', ')}.`] : []),
    ...(emptyPlatform.length ? [`Party A is incomplete: ${emptyPlatform.join(', ')}.`] : []),
  ];
};

/** Nội dung gửi lên: bỏ dòng / mục trống (server chuẩn hóa lại). */
export const cleanContent = (c: TemplateContentDto): TemplateContentDto => ({
  ...c,
  legalBases: c.legalBases.filter((b) => b.trim() !== ''),
  articles: c.articles.map((a) => ({ ...a, clauses: a.clauses.filter((x) => x.trim() !== '') })),
  appendices: c.appendices.map((a) => ({ ...a, notes: a.notes.filter((x) => x.trim() !== '') })),
});

/** Cú pháp markup (khớp HtmlContractRenderer) – chỉ dùng cho mẫu cũ trong phần Advanced. */
export const MARKUP_HELP: Array<{ syntax: string; meaning: string }> = [
  { syntax: '# Title', meaning: 'Document title' },
  { syntax: '## ĐIỀU 1. …', meaning: 'Article heading' },
  { syntax: '1.1. Text', meaning: 'Clause (indented)' },
  { syntax: '^ Text / ~ Text / - Text', meaning: 'Centred / italic / bullet' },
  { syntax: '|| Label: || Value', meaning: 'Two-column info row' },
  { syntax: '!! Left !! Right', meaning: 'Header / signature block' },
  { syntax: '#| Col | Col  then  | a | b', meaning: 'Bordered table' },
  { syntax: '--- / **bold** / {{Key}} alone', meaning: 'Page break / bold / multi-line block' },
];
