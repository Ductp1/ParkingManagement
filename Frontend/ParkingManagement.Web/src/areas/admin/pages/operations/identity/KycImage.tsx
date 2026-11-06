import React, { useEffect, useState } from 'react';
import { adminKycApi } from '@/api/adminIdentityApi';
import { AdminApiError } from '@/api/adminClientApi';
import type { KycImageSide } from '@/types/adminIdentity';

const SIDE_LABEL: Record<KycImageSide, string> = { Front: 'Front side', Back: 'Back side' };

/**
 * Ảnh giấy tờ tải qua phiên đăng nhập (Blob → object URL, thu hồi khi rời màn).
 * Bấm ảnh để mở cỡ gốc ở tab mới; ảnh lỗi/không có thì báo rõ thay vì khung trống.
 */
export const KycImage: React.FC<{ userId: number; side: KycImageSide }> = ({ userId, side }) => {
  const [url, setUrl] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [missing, setMissing] = useState(false);

  useEffect(() => {
    let objectUrl: string | null = null;
    let cancelled = false;
    setUrl(null);
    setError(null);
    setMissing(false);
    adminKycApi
      .image(userId, side)
      .then((blob) => {
        if (cancelled) return;
        objectUrl = URL.createObjectURL(blob);
        setUrl(objectUrl);
      })
      .catch((err: unknown) => {
        if (cancelled) return;
        // 404 = người dùng xác minh chỉ bằng số CCCD, không gửi ảnh
        if (err instanceof AdminApiError && err.status === 404) setMissing(true);
        else setError(err instanceof Error ? err.message : 'Could not load the image.');
      });
    return () => {
      cancelled = true;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [userId, side]);

  return (
    <figure className="m-0 flex flex-col gap-1.5">
      <figcaption className="adm-type-body-strong text-(--admin-text-secondary)">{SIDE_LABEL[side]}</figcaption>
      <div className="flex aspect-[85.6/54] items-center justify-center overflow-hidden rounded-[2px] border border-(--admin-border-default) bg-(--admin-surface-row-alt)">
        {url ? (
          <a href={url} target="_blank" rel="noreferrer" title="Open full size" className="block h-full w-full">
            <img src={url} alt={`${SIDE_LABEL[side]} of the submitted document`} className="h-full w-full object-contain" />
          </a>
        ) : missing ? (
          <span className="adm-type-body-regular-12 px-3 text-center text-(--admin-text-secondary)">No image — submitted with the ID number only</span>
        ) : error ? (
          <span className="adm-type-body-regular-12 px-3 text-center text-(--admin-status-danger)">{error}</span>
        ) : (
          <span className="adm-type-body-regular-12 text-(--admin-text-placeholder)">Loading…</span>
        )}
      </div>
    </figure>
  );
};
