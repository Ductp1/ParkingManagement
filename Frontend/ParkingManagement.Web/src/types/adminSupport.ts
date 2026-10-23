/**
 * DTO SupportService dùng trong Admin Portal (tab Communications / FAQ).
 * Nguồn: Services/SupportService/SupportService.Application/DTOs/FaqDtos.cs
 */
export interface FaqItemDto {
  id: number;
  category: string;
  audience: string;
  question: string;
  answer: string;
  sortOrder: number;
  viewCount: number;
}

export interface FaqCategoryDto {
  category: string;
  items: FaqItemDto[];
}

export interface ContactChannelDto {
  channel: string;
  value: string;
  note: string | null;
}

/** GET /api/v1/faqs?audience=Driver|LotOwner|All&category= – chỉ trả bài đã công khai. */
export interface HelpCenterDto {
  audience: string;
  categories: FaqCategoryDto[];
  contacts: ContactChannelDto[];
}

export type FaqAudience = 'Driver' | 'LotOwner' | 'All';
