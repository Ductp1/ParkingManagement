import React, { useEffect, useRef, useState, useCallback } from 'react';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import { MapPin, Navigation, Compass, RefreshCw } from 'lucide-react';
import { geocodingApi } from '@/api/geocodingApi';

interface LocationPickerMapProps {
  latitude: number;
  longitude: number;
  onChange: (lat: number, lng: number) => void;
  searchAddress?: string;
  district?: string;
  city?: string;
  disabled?: boolean;
}

export const LocationPickerMap: React.FC<LocationPickerMapProps> = ({
  latitude,
  longitude,
  onChange,
  searchAddress = '',
  district = '',
  city = '',
  disabled = false,
}) => {
  const mapContainerRef = useRef<HTMLDivElement | null>(null);
  const mapInstanceRef = useRef<L.Map | null>(null);
  const markerRef = useRef<L.Marker | null>(null);

  const [geocodingBusy, setGeocodingBusy] = useState(false);
  const [statusText, setStatusText] = useState<string>('Kéo ghim hoặc bấm vào bản đồ để chọn cổng bãi xe');
  const [gpsLoading, setGpsLoading] = useState(false);

  // Tạo Custom Pin Icon với SVG hiện đại
  const createPinIcon = useCallback(() => {
    const html = `
      <div style="position: relative; display: flex; flex-direction: column; align-items: center; cursor: grab;">
        <div style="
          width: 38px;
          height: 38px;
          border-radius: 50% 50% 50% 0;
          background: #2563eb;
          transform: rotate(-45deg);
          border: 3px solid #ffffff;
          box-shadow: 0 4px 14px rgba(37, 99, 235, 0.45);
          display: flex;
          align-items: center;
          justify-content: center;
        ">
          <span style="
            transform: rotate(45deg);
            color: #ffffff;
            font-size: 16px;
            font-weight: 900;
            line-height: 1;
          ">P</span>
        </div>
        <div style="
          width: 8px;
          height: 8px;
          background: #1d4ed8;
          border-radius: 50%;
          margin-top: 2px;
          box-shadow: 0 0 8px rgba(0,0,0,0.3);
        "></div>
      </div>
    `;

    return L.divIcon({
      className: 'custom-lot-picker-pin',
      html,
      iconSize: [38, 48],
      iconAnchor: [19, 46],
    });
  }, []);

  // 1. Khởi tạo bản đồ Leaflet
  useEffect(() => {
    if (!mapContainerRef.current) return;

    if (!mapInstanceRef.current) {
      const initialLat = latitude && !isNaN(latitude) ? latitude : 10.776889;
      const initialLng = longitude && !isNaN(longitude) ? longitude : 106.700806;

      const map = L.map(mapContainerRef.current, {
        center: [initialLat, initialLng],
        zoom: 16,
        zoomControl: false,
        attributionControl: false,
      });

      // Lớp bản đồ Google Standard Tile / OSM
      L.tileLayer('https://{s}.google.com/vt/lyrs=m&x={x}&y={y}&z={z}', {
        maxZoom: 20,
        subdomains: ['mt0', 'mt1', 'mt2', 'mt3'],
      }).addTo(map);

      // Thêm nút Zoom góc phải
      L.control.zoom({ position: 'bottomright' }).addTo(map);

      // Thêm Marker có thể kéo thả
      const marker = L.marker([initialLat, initialLng], {
        icon: createPinIcon(),
        draggable: !disabled,
        autoPan: true,
      }).addTo(map);

      // Xử lý sự kiện kéo thả marker
      marker.on('dragend', () => {
        const pos = marker.getLatLng();
        const newLat = parseFloat(pos.lat.toFixed(6));
        const newLng = parseFloat(pos.lng.toFixed(6));
        onChange(newLat, newLng);
        setStatusText(`Đã ghim vị trí: ${newLat}, ${newLng}`);
      });

      // Xử lý sự kiện click trên bản đồ để di chuyển ghim
      map.on('click', (e: L.LeafletMouseEvent) => {
        if (disabled) return;
        const newLat = parseFloat(e.latlng.lat.toFixed(6));
        const newLng = parseFloat(e.latlng.lng.toFixed(6));
        marker.setLatLng([newLat, newLng]);
        onChange(newLat, newLng);
        setStatusText(`Đã chọn vị trí: ${newLat}, ${newLng}`);
      });

      mapInstanceRef.current = map;
      markerRef.current = marker;

      // Fix render lỗi tile khi bản đồ nằm trong modal
      setTimeout(() => {
        map.invalidateSize();
      }, 250);
    }

    return () => {
      if (mapInstanceRef.current) {
        mapInstanceRef.current.remove();
        mapInstanceRef.current = null;
        markerRef.current = null;
      }
    };
  }, []); // Chỉ khởi tạo một lần khi mount

  // 2. Đồng bộ vị trí Marker khi props latitude / longitude thay đổi từ bên ngoài
  useEffect(() => {
    if (!mapInstanceRef.current || !markerRef.current) return;
    if (!latitude || !longitude || isNaN(latitude) || isNaN(longitude)) return;

    const currentPos = markerRef.current.getLatLng();
    const isSamePos =
      Math.abs(currentPos.lat - latitude) < 0.000001 &&
      Math.abs(currentPos.lng - longitude) < 0.000001;

    if (!isSamePos) {
      markerRef.current.setLatLng([latitude, longitude]);
      mapInstanceRef.current.setView([latitude, longitude], mapInstanceRef.current.getZoom(), {
        animate: true,
      });
    }
  }, [latitude, longitude]);

  // 3. Tự động Geocoding khi người dùng nhập Địa chỉ / Quận / Thành phố (Debounced)
  useEffect(() => {
    const fullQuery = [searchAddress, district, city].filter(Boolean).join(' ').trim();
    if (!fullQuery || fullQuery.length < 5) return;

    const timer = setTimeout(async () => {
      setGeocodingBusy(true);
      setStatusText('Đang tự động xác định vị trí theo địa chỉ...');

      try {
        const result = await geocodingApi.geocodeAddress(searchAddress, district, city);
        if (result && mapInstanceRef.current && markerRef.current) {
          onChange(result.latitude, result.longitude);
          markerRef.current.setLatLng([result.latitude, result.longitude]);
          mapInstanceRef.current.flyTo([result.latitude, result.longitude], 17, {
            duration: 1.2,
          });

          setStatusText(
            result.source === 'nominatim'
              ? 'Đã tìm thấy vị trí chính xác từ địa chỉ'
              : 'Đã định vị khu vực tương ứng (kéo ghim để chỉnh chuẩn)'
          );
        }
      } catch {
        setStatusText('Không thể tìm tự động, bạn có thể bấm trực tiếp lên bản đồ');
      } finally {
        setGeocodingBusy(false);
      }
    }, 700);

    return () => clearTimeout(timer);
  }, [searchAddress, district, city]);

  // 4. Lấy GPS thiết bị hiện tại (Tùy chọn phụ khi đang đứng tại bãi)
  const handleGetCurrentLocation = () => {
    if (!navigator.geolocation) {
      alert('Trình duyệt của bạn không hỗ trợ định vị GPS.');
      return;
    }

    setGpsLoading(true);
    setStatusText('Đang lấy tín hiệu GPS thiết bị...');

    navigator.geolocation.getCurrentPosition(
      (pos) => {
        const lat = parseFloat(pos.coords.latitude.toFixed(6));
        const lng = parseFloat(pos.coords.longitude.toFixed(6));
        onChange(lat, lng);

        if (mapInstanceRef.current && markerRef.current) {
          markerRef.current.setLatLng([lat, lng]);
          mapInstanceRef.current.flyTo([lat, lng], 17, { duration: 1.2 });
        }
        setStatusText('Đã lấy tọa độ GPS từ thiết bị hiện tại');
        setGpsLoading(false);
      },
      (err) => {
        setGpsLoading(false);
        setStatusText(`Không thể lấy GPS: ${err.message}`);
      },
      { enableHighAccuracy: true, timeout: 8000 }
    );
  };

  return (
    <div className="space-y-2">
      {/* Thanh thông tin trạng thái & Nút GPS */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2">
        <div className="flex items-center gap-1.5 text-xs font-semibold text-slate-700">
          <MapPin size={15} className="text-blue-600 shrink-0" />
          <span>Vị trí bãi xe trên bản đồ</span>
          {geocodingBusy && (
            <span className="flex items-center gap-1 text-[11px] text-blue-600 font-normal">
              <RefreshCw size={12} className="animate-spin" />
              Đang định vị địa chỉ...
            </span>
          )}
        </div>

        <button
          type="button"
          onClick={handleGetCurrentLocation}
          disabled={disabled || gpsLoading}
          className="text-[11px] font-semibold text-blue-600 hover:text-blue-700 hover:bg-blue-50 px-2.5 py-1 rounded-lg border border-blue-200 transition-colors inline-flex items-center gap-1.5 cursor-pointer disabled:opacity-50 self-start sm:self-auto"
          title="Lấy tọa độ nơi bạn đang đứng hiện tại"
        >
          <Navigation size={12} className={gpsLoading ? 'animate-spin' : ''} />
          <span>Lấy GPS vị trí tôi</span>
        </button>
      </div>

      {/* Container Leaflet Map */}
      <div className="relative w-full h-64 sm:h-72 rounded-2xl overflow-hidden border border-slate-300 shadow-inner bg-slate-100">
        <div ref={mapContainerRef} className="w-full h-full z-0" />

        {/* Overlay Badge hướng dẫn ở góc trên */}
        <div className="absolute top-2.5 left-2.5 z-10 bg-white/95 backdrop-blur-xs px-3 py-1.5 rounded-xl border border-slate-200/90 shadow-xs flex items-center gap-2 max-w-[85%] text-slate-700">
          <Compass size={14} className="text-blue-600 shrink-0" />
          <span className="text-[11px] font-medium truncate">{statusText}</span>
        </div>

        {/* Overlay Badge hiển thị tọa độ góc dưới trái */}
        <div className="absolute bottom-2.5 left-2.5 z-10 bg-slate-900/85 backdrop-blur-xs text-white px-3 py-1 rounded-lg shadow-sm font-mono text-[11px] flex items-center gap-2">
          <span>
            {latitude.toFixed(6)}, {longitude.toFixed(6)}
          </span>
        </div>
      </div>

      <p className="text-[11px] text-slate-500 leading-relaxed">
        * Hệ thống tự động bay đến vị trí theo địa chỉ bạn nhập bên trên. Bạn có thể kéo thả ghim màu xanh để tinh chỉnh đúng lối vào cổng bãi xe.
      </p>
    </div>
  );
};
