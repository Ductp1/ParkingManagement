/**
 * Nền minh hoạ cho các trang xác thực: đường chân trời TP.HCM nhiều lớp (xa nhạt, gần đậm),
 * mặt trời + mây, phía trước là con đường có biển báo bãi đỗ "P". Thuần SVG, co giãn phủ kín màn hình.
 */
export function AuthScene({ className = 'auth-scene' }: { className?: string }) {
  return (
    <svg className={className} viewBox="0 0 1440 900" preserveAspectRatio="xMidYMid slice" aria-hidden="true">
      <defs>
        <linearGradient id="auth-sky" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#cddffb" />
          <stop offset="0.6" stopColor="#e8f0fe" />
          <stop offset="1" stopColor="#f3f7ff" />
        </linearGradient>
        <linearGradient id="auth-road" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#1d3557" />
          <stop offset="1" stopColor="#0b1626" />
        </linearGradient>
      </defs>

      {/* Bầu trời, mặt trời, mây */}
      <rect width="1440" height="900" fill="url(#auth-sky)" />
      <circle cx="720" cy="230" r="170" fill="#ffffff" opacity="0.92" />
      <g fill="#ffffff" opacity="0.9">
        <ellipse cx="160" cy="430" rx="230" ry="70" />
        <ellipse cx="330" cy="400" rx="150" ry="80" />
        <ellipse cx="1180" cy="380" rx="200" ry="85" />
        <ellipse cx="1360" cy="420" rx="190" ry="70" />
        <ellipse cx="980" cy="440" rx="170" ry="55" />
      </g>

      {/* Lớp nhà xa */}
      <g fill="#c4d6f6">
        <rect x="0" y="520" width="90" height="260" />
        <rect x="80" y="470" width="70" height="310" />
        <rect x="150" y="540" width="110" height="240" />
        <rect x="250" y="500" width="60" height="280" />
        <rect x="470" y="530" width="120" height="250" />
        <rect x="600" y="490" width="80" height="290" />
        <rect x="760" y="520" width="100" height="260" />
        <rect x="870" y="480" width="70" height="300" />
        <rect x="1200" y="510" width="90" height="270" />
        <rect x="1290" y="470" width="80" height="310" />
        <rect x="1370" y="530" width="70" height="250" />
      </g>

      {/* Lớp giữa: có dáng Bitexco (trái) và Landmark 81 (phải) */}
      <g fill="#93b4ea">
        <rect x="20" y="600" width="120" height="180" />
        <rect x="140" y="570" width="80" height="210" />
        {/* Bitexco: thân thon, sân đáp nhô ra */}
        <path d="M 330 780 L 345 420 Q 360 380 375 420 L 390 780 Z" />
        <rect x="360" y="470" width="44" height="10" rx="3" />
        <rect x="420" y="610" width="100" height="170" />
        <rect x="520" y="580" width="70" height="200" />
        <rect x="640" y="620" width="130" height="160" />
        <rect x="900" y="600" width="90" height="180" />
        {/* Landmark 81: các khối bậc thang thu nhỏ dần lên chóp */}
        <path d="M 1010 780 V 470 H 1030 V 400 H 1046 V 320 H 1058 V 250 L 1066 200 L 1074 250 V 320 H 1086 V 400 H 1102 V 470 H 1122 V 780 Z" />
        <rect x="1160" y="590" width="110" height="190" />
        <rect x="1300" y="620" width="140" height="160" />
      </g>

      {/* Lớp gần */}
      <g fill="#5b84c9">
        <rect x="0" y="680" width="200" height="120" />
        <rect x="190" y="650" width="90" height="150" />
        <rect x="430" y="690" width="160" height="110" />
        <rect x="840" y="670" width="120" height="130" />
        <rect x="1180" y="660" width="100" height="140" />
        <rect x="1280" y="700" width="160" height="100" />
      </g>
      <g fill="#ffffff" opacity="0.35">
        {[0, 1, 2, 3].map((r) =>
          [20, 50, 80, 110, 140, 170].map((c) => <rect key={`${r}-${c}`} x={c} y={700 + r * 22} width="14" height="9" rx="1" />),
        )}
        {[0, 1, 2, 3].map((r) =>
          [1300, 1330, 1360, 1390, 1420].map((c) => <rect key={`r${r}-${c}`} x={c} y={718 + r * 20} width="12" height="8" rx="1" />),
        )}
      </g>

      {/* Con đường phía trước */}
      <rect x="0" y="780" width="1440" height="120" fill="url(#auth-road)" />
      <g fill="#ffffff" opacity="0.5">
        {Array.from({ length: 16 }).map((_, i) => (
          <rect key={i} x={i * 96 + 20} y="836" width="48" height="5" rx="2" />
        ))}
      </g>

      {/* Biển báo bãi đỗ */}
      <g transform="translate(150 640)">
        <rect x="18" y="56" width="6" height="90" fill="#0b1626" />
        <rect width="42" height="58" rx="6" fill="#2563eb" stroke="#ffffff" strokeWidth="3" />
        <text x="21" y="42" textAnchor="middle" fontSize="34" fontWeight="800" fill="#ffffff" fontFamily="system-ui, sans-serif">P</text>
      </g>
      <g transform="translate(1250 650)">
        <rect x="16" y="50" width="6" height="88" fill="#0b1626" />
        <rect width="38" height="52" rx="6" fill="#2563eb" stroke="#ffffff" strokeWidth="3" />
        <text x="19" y="38" textAnchor="middle" fontSize="30" fontWeight="800" fill="#ffffff" fontFamily="system-ui, sans-serif">P</text>
      </g>
    </svg>
  );
}
