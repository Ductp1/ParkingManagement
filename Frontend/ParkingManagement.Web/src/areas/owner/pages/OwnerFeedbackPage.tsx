import React, { useEffect, useState, useCallback, useMemo } from 'react';
import {
  MessageSquareWarning,
  Star,
  RefreshCw,
  CheckCircle2,
} from 'lucide-react';
import { useAppSelector } from '@/store';
import { ownerFeedbackApi } from '@/api/ownerFeedbackApi';
import { ownerLotsApi } from '@/api/ownerLotsApi';
import type { OwnerParkingLotItemDto } from '@/types/ownerKyb';
import type {
  ComplaintItem,
  ComplaintDetail,
  PayoutHoldItem,
  LotReviewsResponse,
} from '@/types/feedback';
import { PayoutHoldsBanner } from '../components/feedback/PayoutHoldsBanner';
import { DisputeListTable } from '../components/feedback/DisputeListTable';
import { DisputeDetailModal } from '../components/feedback/DisputeDetailModal';
import { ReviewRatingSummary } from '../components/feedback/ReviewRatingSummary';
import { ReviewCard } from '../components/feedback/ReviewCard';
import { ReportReviewModal } from '../components/feedback/ReportReviewModal';

export const OwnerFeedbackPage: React.FC = () => {
  const { user } = useAppSelector((s) => s.auth);
  // Sử dụng ownerProfileId từ user, nếu không có thì fallback 1 cho môi trường dev test
  const ownerProfileId = user?.ownerProfileId || user?.id || 1;

  const [mainTab, setMainTab] = useState<'disputes' | 'reviews'>('disputes');

  // Trạng thái Khiếu nại & Tranh chấp
  const [complaints, setComplaints] = useState<ComplaintItem[]>([]);
  const [payoutHolds, setPayoutHolds] = useState<PayoutHoldItem[]>([]);
  const [loadingComplaints, setLoadingComplaints] = useState(true);
  const [selectedComplaint, setSelectedComplaint] = useState<ComplaintItem | null>(null);
  const [isDetailModalOpen, setIsDetailModalOpen] = useState(false);

  // Trạng thái Đánh giá
  const [reviewsData, setReviewsData] = useState<LotReviewsResponse | null>(null);
  const [loadingReviews, setLoadingReviews] = useState(false);
  const [lots, setLots] = useState<OwnerParkingLotItemDto[]>([]);
  const [selectedLotId, setSelectedLotId] = useState<number | null>(null);

  useEffect(() => {
    ownerLotsApi
      .getMyLots(ownerProfileId)
      .then((data) => {
        setLots(data || []);
        if (data && data.length > 0) {
          setSelectedLotId(data[0].id);
        } else {
          setSelectedLotId(null);
        }
      })
      .catch(() => {
        setLots([]);
        setSelectedLotId(null);
      });
  }, [ownerProfileId]);

  const [selectedStarFilter, setSelectedStarFilter] = useState<number | 'ALL'>('ALL');
  const [reportingReviewId, setReportingReviewId] = useState<number | null>(null);
  const [isReportModalOpen, setIsReportModalOpen] = useState(false);

  // Toast thông báo phản hồi thành công
  const [toastMessage, setToastMessage] = useState<string | null>(null);

  const showToast = (msg: string) => {
    setToastMessage(msg);
    setTimeout(() => setToastMessage(null), 4000);
  };

  // 1. Tải toàn bộ khiếu nại & danh sách phong tỏa
  const fetchComplaintsData = useCallback(async () => {
    setLoadingComplaints(true);
    try {
      const [complaintsList, holdsList] = await Promise.all([
        ownerFeedbackApi.listComplaints(ownerProfileId),
        ownerFeedbackApi.listPayoutHolds(ownerProfileId),
      ]);
      setComplaints(complaintsList);
      setPayoutHolds(holdsList);
    } catch (err) {
      console.error('Error fetching complaints data:', err);
    } finally {
      setLoadingComplaints(false);
    }
  }, [ownerProfileId]);

  // 2. Tải đánh giá bãi xe
  const fetchReviewsData = useCallback(async () => {
    if (!selectedLotId) {
      setReviewsData(null);
      return;
    }

    setLoadingReviews(true);
    try {
      const data = await ownerFeedbackApi.getLotReviews(selectedLotId, 30);
      setReviewsData(data);
    } catch (err) {
      console.error('Error fetching reviews data:', err);
      setReviewsData(null);
    } finally {
      setLoadingReviews(false);
    }
  }, [selectedLotId]);

  useEffect(() => {
    fetchComplaintsData();
  }, [fetchComplaintsData]);

  useEffect(() => {
    if (mainTab === 'reviews') {
      fetchReviewsData();
    }
  }, [mainTab, fetchReviewsData]);

  // Xử lý mở modal phản hồi tranh chấp
  const handleSelectComplaint = (complaint: ComplaintItem) => {
    setSelectedComplaint(complaint);
    setIsDetailModalOpen(true);
  };

  // Callback khi gửi giải trình tranh chấp thành công (Cập nhật in-place state!)
  const handleDisputeSuccess = (updatedDetail: ComplaintDetail) => {
    setComplaints((prev) =>
      prev.map((item) =>
        item.code === updatedDetail.code
          ? {
              ...item,
              status: updatedDetail.status,
              ownerResponse: updatedDetail.ownerResponse,
            }
          : item
      )
    );
    showToast(`Đã gửi giải trình tranh chấp ${updatedDetail.code} thành công.`);
  };

  // Callback khi gửi phản hồi đánh giá thành công
  const handleReviewReplySuccess = (reviewId: number, reply: string) => {
    if (!reviewsData) return;
    setReviewsData({
      ...reviewsData,
      latest: reviewsData.latest.map((r) =>
        r.id === reviewId
          ? {
              ...r,
              ownerReply: reply,
              ownerRepliedAtUtc: new Date().toISOString(),
            }
          : r
      ),
    });
    showToast('Đã gửi phản hồi đánh giá thành công.');
  };

  // Mở modal khiếu nại đánh giá vi phạm
  const handleOpenReportModal = (reviewId: number) => {
    setReportingReviewId(reviewId);
    setIsReportModalOpen(true);
  };

  // Callback khi gửi khiếu nại vi phạm thành công
  const handleReportSuccess = () => {
    if (!reviewsData || !reportingReviewId) return;
    setReviewsData({
      ...reviewsData,
      latest: reviewsData.latest.map((r) =>
        r.id === reportingReviewId ? { ...r, isFlagged: true } : r
      ),
    });
    showToast('Đã chuyển khiếu nại đánh giá cho ban quản trị Admin xem xét.');
  };

  // Lọc danh sách đánh giá theo số sao
  const filteredReviews = useMemo(() => {
    if (!reviewsData?.latest) return [];
    if (selectedStarFilter === 'ALL') return reviewsData.latest;
    return reviewsData.latest.filter((r) => r.rating === selectedStarFilter);
  }, [reviewsData, selectedStarFilter]);

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-10 flex flex-col gap-8">
      {/* Toast Notification */}
      {toastMessage && (
        <div className="fixed top-6 right-6 z-50 bg-emerald-600 text-white px-4 py-3 rounded-xl shadow-xl flex items-center gap-2.5 text-xs font-semibold animate-in fade-in slide-in-from-top-4 duration-300">
          <CheckCircle2 className="w-4 h-4 shrink-0" />
          <span>{toastMessage}</span>
        </div>
      )}

      {/* Page Header chuẩn Driver */}
      <div className="flex flex-col lg:flex-row lg:items-end justify-between gap-6 pb-6 border-b border-slate-200">
        <div className="flex flex-col gap-2 max-w-3xl">
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-blue-100 text-blue-800 w-fit text-xs font-bold tracking-wider uppercase">
            <span className="w-2 h-2 rounded-full bg-blue-600 animate-pulse"></span>
            CHĂM SÓC KHÁCH HÀNG • GIẢI TRÌNH TRANH CHẤP & ĐÁNH GIÁ (US-089)
          </div>
          <h1 className="text-3xl sm:text-4xl font-extrabold text-slate-900 tracking-tight">
            Phản hồi Khiếu nại & Đánh giá
          </h1>
          <p className="text-slate-500 text-sm leading-relaxed">
            Giải trình tranh chấp trong 48 giờ (US-089) và tương tác chăm sóc khách hàng qua đánh giá bãi xe (US-094) trên hệ thống ParkMaster.
          </p>
        </div>

        {/* Main Tabs Navigation (Segmented Pill Switcher) */}
        <div className="flex items-center gap-2 p-1.5 bg-slate-100/90 rounded-2xl border border-slate-200/80 shadow-2xs">
          <button
            type="button"
            onClick={() => setMainTab('disputes')}
            className={`flex items-center gap-2 px-4 py-2.5 rounded-xl text-xs sm:text-sm font-bold transition-all cursor-pointer ${
              mainTab === 'disputes'
                ? 'bg-white text-blue-700 shadow-xs'
                : 'text-slate-600 hover:text-slate-900 hover:bg-white/50'
            }`}
          >
            <MessageSquareWarning className="w-4 h-4" />
            <span>Tranh chấp & Khiếu nại</span>
            {complaints.filter((c) => c.status === 'AwaitingOwner').length > 0 && (
              <span className="w-2 h-2 rounded-full bg-rose-500 animate-ping" />
            )}
          </button>

          <button
            type="button"
            onClick={() => setMainTab('reviews')}
            className={`flex items-center gap-2 px-4 py-2.5 rounded-xl text-xs sm:text-sm font-bold transition-all cursor-pointer ${
              mainTab === 'reviews'
                ? 'bg-white text-blue-700 shadow-xs'
                : 'text-slate-600 hover:text-slate-900 hover:bg-white/50'
            }`}
          >
            <Star className="w-4 h-4" />
            <span>Đánh giá từ khách hàng</span>
          </button>
        </div>
      </div>

      {/* Main Content Area */}
      {mainTab === 'disputes' ? (
        <div className="flex flex-col gap-6">
          {/* Banner các khoản tiền đang bị phong tỏa quyết toán (US-089 AC3) */}
          <PayoutHoldsBanner holds={payoutHolds} />

          {/* Bảng danh sách khiếu nại và bộ lọc */}
          <DisputeListTable
            complaints={complaints}
            loading={loadingComplaints}
            onSelectComplaint={handleSelectComplaint}
            onRefresh={fetchComplaintsData}
          />
        </div>
      ) : (
        /* Tab 2: Quản lý đánh giá (US-094) */
        <div className="flex flex-col gap-6">
          {/* Bãi xe selector & Rating summary */}
          <div className="flex flex-wrap items-center justify-between gap-3 bg-white p-4 rounded-2xl border border-slate-200 shadow-xs">
            <div className="flex items-center gap-2">
              <span className="material-symbols-outlined text-[18px] text-blue-600">domain</span>
              <span className="text-xs font-bold text-slate-700">Xem đánh giá bãi xe:</span>

              {/* Resolved conflict: sử dụng danh sách bãi xe thực tế */}
              <select
                value={selectedLotId || ''}
                onChange={(e) =>
                  setSelectedLotId(e.target.value ? Number(e.target.value) : null)
                }
                disabled={lots.length === 0}
                className="text-xs bg-slate-50 border border-slate-200 rounded-xl px-3 py-1.5 font-semibold text-slate-800 focus:ring-2 focus:ring-blue-500 focus:outline-hidden cursor-pointer"
              >
                {lots.length === 0 ? (
                  <option value="">Chưa có bãi đỗ xe</option>
                ) : (
                  lots.map((lot) => (
                    <option key={lot.id} value={lot.id}>
                      {lot.name} (#{lot.id})
                    </option>
                  ))
                )}
              </select>
            </div>

            <button
              onClick={fetchReviewsData}
              disabled={loadingReviews}
              className="px-3 py-1.5 text-slate-600 hover:text-blue-600 hover:bg-slate-50 rounded-xl border border-slate-200 transition-colors flex items-center gap-1.5 text-xs font-semibold shadow-xs cursor-pointer"
            >
              <RefreshCw className={`w-3.5 h-3.5 ${loadingReviews ? 'animate-spin' : ''}`} />
              <span>Cập nhật</span>
            </button>
          </div>

          {reviewsData && (
            <ReviewRatingSummary
              averageRating={reviewsData.averageRating}
              reviewCount={reviewsData.reviewCount}
              starBreakdown={reviewsData.starBreakdown}
            />
          )}

          {/* Filter by star rating */}
          <div className="flex flex-wrap items-center justify-between gap-3">
            <h3 className="font-extrabold text-slate-900 text-sm tracking-tight">
              Danh sách nhận xét ({filteredReviews.length})
            </h3>

            <div className="flex items-center gap-1.5 overflow-x-auto text-xs">
              <span className="text-[11px] text-slate-400 font-semibold uppercase tracking-wider mr-1">
                Lọc sao:
              </span>
              {(['ALL', 5, 4, 3, 2, 1] as const).map((starVal) => (
                <button
                  key={starVal}
                  onClick={() => setSelectedStarFilter(starVal)}
                  className={`px-3 py-1 rounded-xl text-xs font-bold transition-all flex items-center gap-1 cursor-pointer ${
                    selectedStarFilter === starVal
                      ? 'bg-blue-600 text-white shadow-xs'
                      : 'bg-white text-slate-600 border border-slate-200 hover:bg-slate-50'
                  }`}
                >
                  {starVal === 'ALL' ? (
                    'Tất cả'
                  ) : (
                    <>
                      <span>{starVal}</span>
                      <Star className="w-3 h-3 fill-current text-amber-400" />
                    </>
                  )}
                </button>
              ))}
            </div>
          </div>

          {/* Reviews list */}
          {loadingReviews && !reviewsData ? (
            <div className="py-16 text-center text-slate-400">
              <RefreshCw className="w-6 h-6 animate-spin mx-auto mb-2 text-blue-500" />
              <span className="text-sm font-semibold">Đang tải danh sách đánh giá...</span>
            </div>
          ) : filteredReviews.length === 0 ? (
            <div className="bg-white rounded-3xl border border-dashed border-slate-300 p-12 text-center text-slate-400">
              <Star className="w-8 h-8 mx-auto mb-2 text-slate-300" />
              <p className="font-bold text-slate-700">Chưa có đánh giá nào</p>
              <p className="text-xs text-slate-400 mt-0.5">
                Chưa có đánh giá nào tương ứng với bộ lọc đã chọn
              </p>
            </div>
          ) : (
            <div className="space-y-4">
              {filteredReviews.map((review) => (
                <ReviewCard
                  key={review.id}
                  review={review}
                  onReplySuccess={handleReviewReplySuccess}
                  onReportClick={handleOpenReportModal}
                  ownerProfileId={ownerProfileId}
                />
              ))}
            </div>
          )}
        </div>
      )}

      {/* Modal chi tiết và phản hồi tranh chấp (US-089 AC1) */}
      <DisputeDetailModal
        complaint={selectedComplaint}
        isOpen={isDetailModalOpen}
        onClose={() => setIsDetailModalOpen(false)}
        onSuccess={handleDisputeSuccess}
        ownerProfileId={ownerProfileId}
      />

      {/* Modal khiếu nại đánh giá vi phạm (US-094 AC2) */}
      <ReportReviewModal
        reviewId={reportingReviewId}
        isOpen={isReportModalOpen}
        onClose={() => setIsReportModalOpen(false)}
        onSuccess={handleReportSuccess}
        ownerProfileId={ownerProfileId}
      />
    </div>
  );
};