import React from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { AdminTitleBar } from '../layout/AdminTitleBar';
import { firstTabPath, resolveAdminLocation } from '../navigation';
import { AdminButton } from './AdminButton';
import { StateBlock } from './StateBlock';

interface ComingSoonStateProps {
  backTo: string;
  backLabel: string;
  bare?: boolean;
}

/** EmptyState "comingSoon": không bảng, không bộ lọc giả – chỉ một đường quay lại trang có thật. */
export const ComingSoonState: React.FC<ComingSoonStateProps> = ({ backTo, backLabel, bare }) => {
  const navigate = useNavigate();
  return (
    <StateBlock
      type="comingSoon"
      bare={bare}
      action={
        <AdminButton variant="secondary" icon="chevronLeft" onClick={() => navigate(backTo)}>
          {backLabel}
        </AdminButton>
      }
    />
  );
};

interface ComingSoonScreenProps {
  /** Tên màn trên title bar; mặc định là nhãn tab hiện tại. */
  title?: string;
  /** Đích nút quay lại; mặc định là tab đầu tiên dùng được của nhóm. */
  backTo?: string;
  backLabel?: string;
}

/**
 * Route của tab / màn con chưa có nguồn dữ liệu (P0-10). Tab đã bị ẩn khỏi menu; ai vào thẳng URL
 * (bookmark, link cũ) thấy màn này thay vì bị redirect.
 */
export const ComingSoonScreen: React.FC<ComingSoonScreenProps> = ({ title, backTo, backLabel }) => {
  const { pathname } = useLocation();
  const location = resolveAdminLocation(pathname);
  const section = location?.section;
  return (
    <>
      <AdminTitleBar title={title ?? location?.tab?.label ?? section?.label ?? 'Admin'} />
      <ComingSoonState
        backTo={backTo ?? (section ? firstTabPath(section) : '/admin')}
        backLabel={backLabel ?? `Back to ${section?.label ?? 'Admin'}`}
      />
    </>
  );
};
