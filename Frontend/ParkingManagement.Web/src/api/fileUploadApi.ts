import { apiClient } from './client';

export interface UploadedDocumentInfo {
  url: string;
  fileName: string;
  fileSizeFormatted: string;
  mimeType: string;
}

/**
 * Định dạng dung lượng byte sang KB / MB dễ đọc
 */
export function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
}

/**
 * Service xử lý tải tệp tài liệu thực tế từ máy tính (US-054).
 * Áp dụng cơ chế Hybrid:
 * 1. Cố gắng gửi multipart/form-data lên Gateway/Backend.
 * 2. Nếu Backend chưa mở endpoint upload file vật lý, tự động đọc file thành DataURL
 *    và định danh đường dẫn chuẩn /uploads/kyb/{timestamp}_{filename} để đảm bảo
 *    giao diện hoạt động trơn tru không bị gián đoạn.
 */
export const fileUploadApi = {
  uploadKybDocument: async (
    file: File,
    lotId: number
  ): Promise<UploadedDocumentInfo> => {
    // 1. Kiểm tra kích thước tệp (Tối đa 10MB)
    const maxSizeBytes = 10 * 1024 * 1024;
    if (file.size > maxSizeBytes) {
      throw new Error(`Dung lượng tệp (${formatFileSize(file.size)}) vượt quá giới hạn 10 MB cho phép.`);
    }

    // 2. Upload multipart lên ParkingService. BE trả { fileId, documentUrl, originalFileName, sizeBytes, contentType }
    //    (UploadKybDocumentUseCase). Không còn fallback đọc file thành data URL: chuỗi base64 dài hàng triệu ký tự
    //    làm BusinessLicenseUrl / FireSafetyCertificateUrl (varchar 512) tràn → nộp hồ sơ lỗi 500 (US-054).
    const formData = new FormData();
    formData.append('file', file);
    formData.append('lotId', lotId.toString());

    const res = await apiClient.post<{ documentUrl?: string; url?: string; contentType?: string }>(
      `/api/v1/parking-lots/${lotId}/kyb/documents/upload`,
      formData,
      {
        headers: {
          'Content-Type': 'multipart/form-data',
        },
      }
    );

    const url = res.data?.documentUrl ?? res.data?.url;
    if (!url) {
      throw new Error('Máy chủ không trả về đường dẫn tài liệu. Vui lòng tải lại tệp.');
    }
    return {
      url,
      fileName: file.name,
      fileSizeFormatted: formatFileSize(file.size),
      mimeType: res.data?.contentType ?? file.type,
    };
  },
};
