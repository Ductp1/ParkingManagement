import type { KycDocumentType } from '@/types/account';
import { kycLabels } from './accountFormat';

interface Props {
  type?: KycDocumentType | null;
  /** Số đã che từ máy chủ, hoặc số người dùng đang nhập. */
  number?: string | null;
  holder: string;
  status?: string;
  settle?: boolean;
}

const kinds: Record<KycDocumentType, { title: string; sub: string; css: string }> = {
  CitizenId: { title: 'Căn cước công dân', sub: 'Cộng hòa xã hội chủ nghĩa Việt Nam', css: 'citizen' },
  DriverLicense: { title: 'Giấy phép lái xe', sub: 'Bộ Công an cấp', css: 'license' },
};

/** Nhóm 12 chữ số thành 4-4-4 để dễ đối chiếu với giấy tờ thật; ô trống hiện gạch chờ. */
function group(raw: string) {
  const chars = raw.padEnd(12, '–').slice(0, 12);
  return `${chars.slice(0, 4)} ${chars.slice(4, 8)} ${chars.slice(8)}`;
}

export function DocumentCard({ type, number, holder, status, settle }: Props) {
  const kind = type ? kinds[type] : null;
  const stamp = status ? kycLabels[status] : undefined;
  return <figure className={`doc-card ${kind?.css ?? 'empty'}${settle ? ' just-submitted' : ''}`} aria-label={
    kind ? `${kind.title}, số ${number ?? 'chưa nhập'}${stamp ? `, ${stamp.text}` : ''}` : 'Chưa có giấy tờ'}>
    <div className="doc-card-top">
      <div className="doc-card-kind"><strong>{kind?.title ?? 'Chưa chọn giấy tờ'}</strong>{kind?.sub ?? 'CCCD gắn chip hoặc GPLX mẫu mới'}</div>
      <span className="doc-chip" aria-hidden="true" />
    </div>
    <div className="doc-card-number" aria-hidden="true">{group((number ?? '').replace(/\s/g, ''))}</div>
    <figcaption className="doc-card-foot">
      <span className="doc-card-holder">{holder || 'Họ và tên'}</span>
      {stamp && status !== 'NotSubmitted' && <span className={`doc-stamp ${stamp.tone}`}>{stamp.text}</span>}
    </figcaption>
  </figure>;
}
