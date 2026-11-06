import React from 'react';
import { AlertOctagon, ShieldAlert } from 'lucide-react';

interface EmergencyStopAlertProps {
  isEmergencyStopped: boolean;
  emergencyReason?: string | null;
}

export const EmergencyStopAlert: React.FC<EmergencyStopAlertProps> = ({
  isEmergencyStopped,
  emergencyReason,
}) => {
  if (!isEmergencyStopped) return null;

  return (
    <div className="p-4 sm:p-5 rounded-2xl bg-rose-50 border-2 border-rose-300 text-rose-900 shadow-sm animate-pulse">
      <div className="flex items-start gap-3.5">
        <div className="p-2 rounded-xl bg-rose-200/80 text-rose-700 shrink-0 mt-0.5">
          <AlertOctagon className="w-5 h-5 sm:w-6 sm:h-6" />
        </div>
        <div className="flex-1 space-y-1">
          <div className="flex items-center gap-2">
            <h4 className="font-bold text-sm sm:text-base text-rose-950">
              Cảnh báo: Bãi đỗ xe đang trong trạng thái DỪNG KHẨN CẤP / TẠM ĐÌNH CHỈ
            </h4>
          </div>
          <p className="text-xs sm:text-sm text-rose-800 leading-relaxed">
            Hệ thống đang tạm ngừng toàn bộ hoạt động nhận đặt chỗ trực tuyến để đảm bảo an toàn.
            {emergencyReason && (
              <span className="block mt-1 font-semibold text-rose-950">
                Lý do ghi nhận: "{emergencyReason}"
              </span>
            )}
          </p>
          <div className="pt-1.5 flex items-center gap-2 text-xs font-semibold text-rose-700">
            <ShieldAlert className="w-4 h-4 shrink-0" />
            <span>Vui lòng liên hệ ban quản trị hệ thống nếu cần giải tỏa trạng thái này.</span>
          </div>
        </div>
      </div>
    </div>
  );
};
