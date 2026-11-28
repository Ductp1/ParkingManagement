/**
 * Service Geocoding (TASK-LOC-01):
 * Hỗ trợ chuyển đổi địa chỉ thành tọa độ GPS (Forward Geocoding)
 * và chuyển đổi tọa độ thành địa chỉ đọc được (Reverse Geocoding).
 * 
 * Sử dụng OpenStreetMap Nominatim API (miễn phí, không yêu cầu API key)
 * kèm danh mục tọa độ Fallback cho các tỉnh thành/quận huyện trọng điểm tại Việt Nam.
 */

export interface GeocodingResult {
  latitude: number;
  longitude: number;
  displayName: string;
  source: 'nominatim' | 'fallback';
}

// Bảng tọa độ dự phòng cho các quận/huyện & tỉnh thành phổ biến tại Việt Nam
const VIETNAM_LOCATION_FALLBACKS: Record<string, { lat: number; lng: number }> = {
  // TP. Hồ Chí Minh
  'quận 1': { lat: 10.776889, lng: 106.700806 },
  'quận 3': { lat: 10.784360, lng: 106.684440 },
  'quận 4': { lat: 10.757826, lng: 106.701297 },
  'quận 5': { lat: 10.754020, lng: 106.663375 },
  'quận 7': { lat: 10.734034, lng: 106.721752 },
  'quận 10': { lat: 10.771595, lng: 106.667232 },
  'bình thạnh': { lat: 10.810583, lng: 106.709142 },
  'thủ đức': { lat: 10.849409, lng: 106.753706 },
  'tân bình': { lat: 10.801466, lng: 106.652597 },
  'phú nhuận': { lat: 10.799194, lng: 106.680261 },
  'gò vấp': { lat: 10.838678, lng: 106.665213 },
  'tp. hồ chí minh': { lat: 10.776889, lng: 106.700806 },
  'hồ chí minh': { lat: 10.776889, lng: 106.700806 },

  // Hà Nội
  'hoàn kiếm': { lat: 21.028511, lng: 105.854444 },
  'ba đình': { lat: 21.034091, lng: 105.823906 },
  'cầu giấy': { lat: 21.036237, lng: 105.790583 },
  'đống đa': { lat: 21.018072, lng: 105.829949 },
  'hai bà trưng': { lat: 21.006691, lng: 105.852448 },
  'tây hồ': { lat: 21.071853, lng: 105.819777 },
  'nam từ liêm': { lat: 21.015243, lng: 105.765636 },
  'bắc từ liêm': { lat: 21.063162, lng: 105.756281 },
  'hà nội': { lat: 21.028511, lng: 105.854444 },

  // Đà Nẵng
  'hải châu': { lat: 16.054407, lng: 108.220833 },
  'sơn trà': { lat: 16.082725, lng: 108.243542 },
  'thanh khê': { lat: 16.062828, lng: 108.192534 },
  'đà nẵng': { lat: 16.054407, lng: 108.220833 },
};

/**
 * Tìm tọa độ dự phòng theo từ khóa địa phương trong chuỗi địa chỉ
 */
function findFallbackCoords(query: string): { lat: number; lng: number } | null {
  const normalized = query.toLowerCase().trim();
  for (const [key, coords] of Object.entries(VIETNAM_LOCATION_FALLBACKS)) {
    if (normalized.includes(key)) {
      return coords;
    }
  }
  return null;
}

export const geocodingApi = {
  /**
   * Chuyển đổi Địa chỉ văn bản sang Tọa độ GPS (Forward Geocoding).
   * Ví dụ: "720A Điện Biên Phủ, Phường 22, Bình Thạnh, TP.HCM"
   */
  geocodeAddress: async (
    address: string,
    district?: string,
    city?: string
  ): Promise<GeocodingResult | null> => {
    const parts = [address, district, city, 'Việt Nam'].filter(Boolean);
    const fullQuery = parts.join(', ').trim();

    if (!address.trim() && !district?.trim() && !city?.trim()) {
      return null;
    }

    try {
      // 1. Gọi OpenStreetMap Nominatim API
      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), 4000);

      const endpoint = `https://nominatim.openstreetmap.org/search?format=json&q=${encodeURIComponent(
        fullQuery
      )}&countrycodes=vn&limit=3&addressdetails=1`;

      const response = await fetch(endpoint, {
        signal: controller.signal,
        headers: {
          Accept: 'application/json',
          'User-Agent': 'ParkMaster-ParkingManagement/1.0',
        },
      });

      clearTimeout(timeoutId);

      if (response.ok) {
        const data = await response.json();
        if (Array.isArray(data) && data.length > 0) {
          const first = data[0];
          return {
            latitude: parseFloat(parseFloat(first.lat).toFixed(6)),
            longitude: parseFloat(parseFloat(first.lon).toFixed(6)),
            displayName: first.display_name,
            source: 'nominatim',
          };
        }
      }
    } catch {
      // Bỏ qua lỗi mạng/timeout để sang fallback
    }

    // 2. Nếu Nominatim không tìm ra hoặc lỗi mạng, dùng Fallback tọa độ quận/thành phố
    const fallback = findFallbackCoords(`${address} ${district || ''} ${city || ''}`);
    if (fallback) {
      return {
        latitude: fallback.lat,
        longitude: fallback.lng,
        displayName: `${district || ''}, ${city || 'Việt Nam'} (Vị trí xấp xỉ)`,
        source: 'fallback',
      };
    }

    // Mặc định trung tâm Quận 1, TP.HCM
    return {
      latitude: 10.776889,
      longitude: 106.700806,
      displayName: 'Trung tâm TP. Hồ Chí Minh (Vị trí mặc định)',
      source: 'fallback',
    };
  },

  /**
   * Chuyển đổi Tọa độ GPS thành Địa chỉ đọc được (Reverse Geocoding).
   */
  reverseGeocode: async (lat: number, lng: number): Promise<string | null> => {
    try {
      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), 3500);

      const endpoint = `https://nominatim.openstreetmap.org/reverse?format=json&lat=${lat}&lon=${lng}`;
      const response = await fetch(endpoint, {
        signal: controller.signal,
        headers: {
          Accept: 'application/json',
          'User-Agent': 'ParkMaster-ParkingManagement/1.0',
        },
      });

      clearTimeout(timeoutId);

      if (response.ok) {
        const data = await response.json();
        return data.display_name || null;
      }
    } catch {
      // Không chặn luồng nếu reverse geocode thất bại
    }
    return null;
  },
};
