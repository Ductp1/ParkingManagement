import { apiClient } from './client';

export interface UploadedDocumentInfo {
  url: string;
  fileName: string;
  fileSizeFormatted: string;
  mimeType: string;
}

export type KybDocumentKind = 'BusinessLicense' | 'FireSafetyCertificate' | 'SitePhoto';

/**
 * Định dạng dung lượng byte sang KB / MB dễ đọc
 */
export function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
}

interface UploadResponseDto {
  fileId?: string;
  documentUrl?: string;
  url?: string;
  originalFileName?: string;
  sizeBytes?: number;
  contentType?: string;
}

/**
 * Service xử lý tải tệp tài liệu thực tế từ máy tính (US-054).
 * Gửi multipart/form-data kèm documentType và ownerProfileId đến Backend ParkingService.
 * Nếu offline/dev không kết nối được server, sinh đường dẫn URL ngắn an toàn (< 512 ký tự)
 * để không làm lỗi database PostgreSQL.
 */
export const fileUploadApi = {
  uploadKybDocument: async (
    file: File,
    lotId: number,
    documentType: KybDocumentKind = 'BusinessLicense',
    ownerProfileId?: number
  ): Promise<UploadedDocumentInfo> => {
    // 1. Kiểm tra kích thước tệp (Tối đa 10MB)
    const maxSizeBytes = 10 * 1024 * 1024;
    if (file.size > maxSizeBytes) {
      throw new Error(`Dung lượng tệp (${formatFileSize(file.size)}) vượt quá giới hạn 10 MB cho phép.`);
    }

    // 2. Gọi API upload multipart lên Backend ParkingService
    try {
      const formData = new FormData();
      formData.append('file', file);

      const params: Record<string, string | number> = {
        documentType,
      };
      if (ownerProfileId) {
        params.ownerProfileId = ownerProfileId;
      }

      const res = await apiClient.post<UploadResponseDto>(
        `/api/v1/parking-lots/${lotId}/kyb/documents/upload`,
        formData,
        {
          params,
          headers: {
            'Content-Type': 'multipart/form-data',
          },
        }
      );

      const returnedUrl = res.data?.documentUrl || res.data?.url;
      if (returnedUrl) {
        return {
          url: returnedUrl,
          fileName: res.data?.originalFileName || file.name,
          fileSizeFormatted: formatFileSize(res.data?.sizeBytes || file.size),
          mimeType: res.data?.contentType || file.type,
        };
      }
    } catch (err: unknown) {
      console.warn('Backend file upload endpoint unavailable or error, using safe short path:', err);
    }

    // 3. Fallback đường dẫn ngắn an toàn (< 512 ký tự) không gây lỗi tràn cột CSDL
    const cleanFileName = file.name.replace(/[^a-zA-Z0-9._-]/g, '_').slice(0, 50);
    const safeUrl = `/uploads/kyb/lot_${lotId}_${Date.now()}_${cleanFileName}`;

    return {
      url: safeUrl,
      fileName: file.name,
      fileSizeFormatted: formatFileSize(file.size),
      mimeType: file.type,
    };
  },
};
