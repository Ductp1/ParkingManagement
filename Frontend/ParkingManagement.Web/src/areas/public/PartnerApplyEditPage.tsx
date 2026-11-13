import { ArrowRight, Check } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { AuthLayout } from '@/components/auth/AuthLayout';
import { getPartnerApplicationForEdit, resubmitPartnerApplication, type PartnerApplicationEditView } from '@/api/partnerApplicationsApi';
import { PartnerApplyWizard } from './PartnerApplyPage';
import { fromEditView, takeHashParams, toRequest } from './partnerApplyForm';
import './partnerApply.css';

const HOTLINE = '1900 6868';

const EDIT_INTROS = [
  'Kiểm tra và sửa thông tin theo yêu cầu của ParkMaster.',
  'Thông tin pháp lý in vào hợp đồng hợp tác của từng bãi.',
  'Chỉ để ParkMaster đánh giá sơ bộ – bạn khai báo từng bãi và nộp hồ sơ KYB sau khi có tài khoản.',
  'Kiểm tra lại rồi gửi bổ sung. Link chỉ dùng được một lần.',
];

type LoadState = { status: 'loading' } | { status: 'error'; message: string } | { status: 'ready'; view: PartnerApplicationEditView };

/**
 * /partner/apply/edit#code=PA-…&token=… – đối tác bổ sung hồ sơ theo yêu cầu của ParkMaster (US-116 T4). Link một lần, có hạn;
 * gửi xong hồ sơ quay lại "đang thẩm định".
 */
export function PartnerApplyEditPage() {
  const [link] = useState(() => {
    const params = takeHashParams();
    return { code: params.get('code') ?? '', token: params.get('token') ?? '' };
  });
  const [state, setState] = useState<LoadState>(() =>
    link.code && link.token ? { status: 'loading' } : { status: 'error', message: 'Link bổ sung hồ sơ không đầy đủ. Mở lại đúng link ParkMaster gửi cho bạn.' }
  );

  useEffect(() => {
    if (!link.code || !link.token) return;
    let cancelled = false;
    getPartnerApplicationForEdit(link.code, link.token)
      .then((view) => {
        if (!cancelled) setState({ status: 'ready', view });
      })
      .catch((err: unknown) => {
        if (!cancelled) setState({ status: 'error', message: err instanceof Error ? err.message : 'Không mở được hồ sơ.' });
      });
    return () => {
      cancelled = true;
    };
  }, [link]);

  if (state.status !== 'ready') {
    return (
      <AuthLayout>
        <div className="partner-apply-card" style={{ textAlign: 'center' }}>
          <h1 className="auth-heading">Bổ sung hồ sơ đối tác</h1>
          {state.status === 'loading' ? (
            <p className="auth-description" role="status">
              Đang mở hồ sơ…
            </p>
          ) : (
            <>
              <div role="alert" className="auth-alert auth-alert-error">
                {state.message}
              </div>
              <p className="auth-switch">
                Cần hỗ trợ? Gọi <a href="tel:19006868">{HOTLINE}</a>
              </p>
            </>
          )}
        </div>
      </AuthLayout>
    );
  }

  const { view } = state;
  return (
    <PartnerApplyWizard
      heading={`Bổ sung hồ sơ ${view.code}`}
      initial={fromEditView(view)}
      intros={EDIT_INTROS}
      submitLabel="Gửi bổ sung"
      notice={
        view.infoRequestNote && (
          <div role="note" className="auth-alert auth-alert-success" style={{ marginBottom: 18 }}>
            <strong>ParkMaster cần bổ sung:</strong> {view.infoRequestNote}
          </div>
        )
      }
      onSubmit={async (form) => (await resubmitPartnerApplication(link.code, link.token, toRequest(form))).code}
      renderDone={(code) => (
        <>
          <div className="auth-success-icon">
            <Check size={30} />
          </div>
          <h1 className="auth-heading">Đã gửi bổ sung</h1>
          <p className="auth-description">Hồ sơ đã chuyển lại cho ParkMaster thẩm định tiếp.</p>
          <span className="partner-apply-code">{code}</span>
          <Link className="auth-submit" to="/">
            Về trang chủ <ArrowRight size={17} />
          </Link>
        </>
      )}
    />
  );
}
