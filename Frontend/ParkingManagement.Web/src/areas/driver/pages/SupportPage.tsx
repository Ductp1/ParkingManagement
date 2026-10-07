import React, { useState } from 'react';
import { useAppSelector } from '@/store';

interface FaqItem {
  id: string;
  category: 'booking' | 'anpr' | 'payment' | 'ev';
  question: string;
  answer: string;
}

const FAQ_DATA: FaqItem[] = [
  {
    id: 'Q1',
    category: 'anpr',
    question: 'Nếu camera không nhận diện được biển số xe khi đến bãi thì xử lý thế nào?',
    answer:
      'Hệ thống tự động đồng bộ mã QR đặt chỗ trong ứng dụng. Bạn chỉ cần mở mục "Xe của tôi" hoặc "Mã vé" quét trực tiếp tại trụ cảm biến ở cổng vào. Ngoài ra, bạn có thể nhấn nút "Gọi hỗ trợ tại cổng" trên màn hình cảm ứng của barie, trung tâm giám sát từ xa sẽ xác thực và mở barie cho bạn trong vòng dưới 15 giây.',
  },
  {
    id: 'Q2',
    category: 'booking',
    question: 'Chính sách hủy chỗ và thời gian hoàn tiền ra sao?',
    answer:
      'Bạn được miễn phí hủy 100% khi thực hiện hủy trước giờ hẹn từ 30 phút trở lên. Tiền cọc hoặc phí đã thanh toán sẽ được hoàn trực tiếp về tài khoản ngân hàng hoặc ví điện tử liên kết trong vòng 30 giây. Nếu hủy trong vòng dưới 30 phút, phí giữ chỗ tối thiểu (15.000 VNĐ) sẽ được áp dụng.',
  },
  {
    id: 'Q3',
    category: 'ev',
    question: 'Làm thế nào để sử dụng trạm sạc xe điện EV tại các bãi xe ParkMaster?',
    answer:
      'Khi đặt chỗ trên sơ đồ bãi, chọn ô đỗ có biểu tượng cổng sạc màu xanh lá (ví dụ: ô EV-01, B-04). Khi xe tiến vào vị trí, bạn chỉ cần cắm đầu súng sạc Type 2 hoặc CCS2 vào xe. Ứng dụng ParkMaster sẽ nhận diện tín hiệu súng sạc, tự động đo lường số kWh tiêu thụ và kết thúc phiên sạc tự động.',
  },
  {
    id: 'Q4',
    category: 'payment',
    question: 'Làm sao để xuất hóa đơn VAT điện tử cho công ty?',
    answer:
      'Truy cập mục "Lịch sử đặt" trên thanh điều hướng, chọn lượt đỗ xe cần xuất hóa đơn và nhấn nút "Xuất hóa đơn VAT". Nhập mã số thuế công ty, tên đơn vị và địa chỉ email nhận. Hóa đơn điện tử có mã của cơ quan Thuế sẽ tự động gửi về hòm thư của bạn trong vòng tối đa 5 phút làm việc.',
  },
  {
    id: 'Q5',
    category: 'booking',
    question: 'Tôi có thể gia hạn thời gian giữ chỗ nếu đến muộn không?',
    answer:
      'Có. Bạn có thể bấm nút "Gia hạn thời gian đến" trực tiếp trên thẻ đặt chỗ trong vòng 15 phút trước giờ hẹn đã đặt. Hệ thống cho phép gia hạn tối đa thêm 45 phút để đảm bảo ô đỗ không bị hủy chuyển cho tài xế khác.',
  },
];

export const SupportPage: React.FC = () => {
  const { user } = useAppSelector((state) => state.auth);

  const [activeFaqCategory, setActiveFaqCategory] = useState<'all' | 'booking' | 'anpr' | 'payment' | 'ev'>('all');
  const [openFaqIds, setOpenFaqIds] = useState<string[]>(['Q1', 'Q2']);

  // Ticket form
  const [fullName, setFullName] = useState(user?.fullName || 'Nguyễn Văn An');
  const [phone, setPhone] = useState('0912 345 678');
  const [issueType, setIssueType] = useState('barie');
  const [identifier, setIdentifier] = useState('51K-889.32 / PKM-2025-8842');
  const [content, setContent] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [ticketCreated, setTicketCreated] = useState<string | null>(null);

  // Live Chat Drawer state
  const [isChatOpen, setIsChatOpen] = useState(false);
  const [chatMessages, setChatMessages] = useState<Array<{ sender: 'bot' | 'user'; text: string; time: string }>>([
    {
      sender: 'bot',
      text: 'Xin chào anh Nguyễn Văn An! Em là trợ lý ParkMaster AI. Em có thể hỗ trợ anh về Barie, hóa đơn hay trạm sạc điện thoại hôm nay?',
      time: '14:20',
    },
  ]);
  const [inputChat, setInputChat] = useState('');

  const toggleFaq = (id: string) => {
    setOpenFaqIds((prev) =>
      prev.includes(id) ? prev.filter((item) => item !== id) : [...prev, id]
    );
  };

  const handleTicketSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);
    setTimeout(() => {
      const generatedCode = `#TK-2025-${Math.floor(1000 + Math.random() * 9000)}`;
      setTicketCreated(generatedCode);
      setIsSubmitting(false);
      setContent('');
    }, 800);
  };

  const handleSendChatMessage = (e: React.FormEvent) => {
    e.preventDefault();
    if (!inputChat.trim()) return;

    const userMsg = inputChat.trim();
    const newChatList = [
      ...chatMessages,
      { sender: 'user' as const, text: userMsg, time: '14:22' },
    ];
    setChatMessages(newChatList);
    setInputChat('');

    setTimeout(() => {
      setChatMessages((prev) => [
        ...prev,
        {
          sender: 'bot' as const,
          text: `Dạ em đã ghi nhận yêu cầu "${userMsg}". Kỹ thuật viên phụ trách bãi Landmark 81 đang kiểm tra camera barie cho anh ngay lập tức ạ!`,
          time: '14:22',
        },
      ]);
    }, 1000);
  };

  const filteredFaqs = FAQ_DATA.filter((item) => {
    if (activeFaqCategory === 'all') return true;
    return item.category === activeFaqCategory;
  });

  return (
    <div className="min-h-screen bg-slate-50 flex flex-col font-sans">
      <main className="flex-1 w-full max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        {/* SECTION 1: HERO HEADER */}
        <div className="text-center max-w-3xl mx-auto mb-10">
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-blue-50 text-blue-700 text-xs font-bold uppercase tracking-wider mb-3">
            <span className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse"></span>
            TRUNG TÂM CSKH 24/7 • PHẢN HỒI DƯỚI 30 GIÂY
          </div>
          <h1 className="text-3xl sm:text-4xl font-extrabold text-slate-900 tracking-tight mb-3">
            Chúng tôi luôn sẵn sàng hỗ trợ bạn
          </h1>
          <p className="text-sm text-slate-500 leading-relaxed">
            Hệ thống điều hành bãi đỗ xe thông minh ParkMaster kết nối trực tuyến với tổng đài viên và kỹ thuật viên tại 24 cụm bãi đỗ trọng điểm trên toàn quốc.
          </p>
        </div>

        {/* SECTION 2: 3 DIRECT CONTACT CHANNELS */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mb-10">
          {/* Channel 1: Hotline */}
          <div className="bg-white p-6 rounded-2xl shadow-xs border border-slate-200 flex flex-col justify-between hover:shadow-md transition-shadow">
            <div>
              <div className="flex items-center gap-4 mb-4">
                <div className="w-12 h-12 rounded-2xl bg-blue-50 text-blue-600 flex items-center justify-center">
                  <span className="material-symbols-outlined text-[26px]">headset_mic</span>
                </div>
                <div>
                  <span className="text-[10px] font-bold text-red-600 bg-red-50 px-2 py-0.5 rounded-full uppercase">
                    Khẩn cấp 24/7
                  </span>
                  <h3 className="text-base font-bold text-slate-900 mt-1">Tổng đài Khẩn cấp</h3>
                </div>
              </div>
              <p className="text-xs text-slate-500 mb-3 leading-relaxed">
                Hỗ trợ xử lý kẹt barie, xe không nhận diện biển số ANPR, hoặc khẩn cấp tại cổng vào/ra.
              </p>
              <div className="text-2xl font-extrabold text-blue-600 mb-2">1900 6868</div>
              <p className="text-[11px] text-slate-400 mb-6">
                Phím 1: Khẩn cấp cổng • Phím 2: Hóa đơn & Đặt chỗ
              </p>
            </div>
            <a
              href="tel:19006868"
              className="w-full py-2.5 rounded-xl bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold text-center transition-colors flex items-center justify-center gap-2 shadow-xs"
            >
              <span className="material-symbols-outlined text-[18px]">call</span>
              <span>Gọi 1900 6868</span>
            </a>
          </div>

          {/* Channel 2: Live Chat */}
          <div className="bg-white p-6 rounded-2xl shadow-xs border border-slate-200 flex flex-col justify-between hover:shadow-md transition-shadow">
            <div>
              <div className="flex items-center gap-4 mb-4">
                <div className="w-12 h-12 rounded-2xl bg-emerald-50 text-emerald-600 flex items-center justify-center">
                  <span className="material-symbols-outlined text-[26px]">forum</span>
                </div>
                <div>
                  <span className="text-[10px] font-bold text-emerald-700 bg-emerald-50 px-2 py-0.5 rounded-full uppercase">
                    Trực tuyến
                  </span>
                  <h3 className="text-base font-bold text-slate-900 mt-1">Trò chuyện Trực tiếp</h3>
                </div>
              </div>
              <p className="text-xs text-slate-500 mb-3 leading-relaxed">
                Trợ lý ảo AI & Chuyên viên kỹ thuật điều hành bãi đỗ trực tuyến, hỗ trợ tức thì mọi vấn đề ứng dụng.
              </p>
              <p className="text-xs text-slate-600 mb-6 font-medium">
                Thời gian kết nối chat: <strong className="text-blue-600 font-bold">dưới 30 giây</strong>.
              </p>
            </div>
            <button
              type="button"
              onClick={() => setIsChatOpen(true)}
              className="w-full py-2.5 rounded-xl bg-blue-50 hover:bg-blue-100 text-blue-700 text-xs font-bold text-center transition-colors flex items-center justify-center gap-2"
            >
              <span className="material-symbols-outlined text-[18px]">chat</span>
              <span>Mở khung chat</span>
            </button>
          </div>

          {/* Channel 3: Mail Support */}
          <div className="bg-white p-6 rounded-2xl shadow-xs border border-slate-200 flex flex-col justify-between hover:shadow-md transition-shadow">
            <div>
              <div className="flex items-center gap-4 mb-4">
                <div className="w-12 h-12 rounded-2xl bg-purple-50 text-purple-600 flex items-center justify-center">
                  <span className="material-symbols-outlined text-[26px]">mark_email_unread</span>
                </div>
                <div>
                  <span className="text-[10px] font-bold text-purple-700 bg-purple-50 px-2 py-0.5 rounded-full uppercase">
                    Email 2h SLA
                  </span>
                  <h3 className="text-base font-bold text-slate-900 mt-1">Hòm thư Tiếp nhận Sự cố</h3>
                </div>
              </div>
              <p className="text-xs text-slate-500 mb-3 leading-relaxed">
                Gửi email tới: <strong className="text-slate-800">hotro@parkmaster.vn</strong>. Cam kết giải quyết khiếu nại trong vòng 2 giờ làm việc.
              </p>
              <p className="text-xs text-slate-400 mb-6">
                Hỗ trợ đối soát hóa đơn VAT & khiếu nại hoàn cọc.
              </p>
            </div>
            <a
              href="mailto:hotro@parkmaster.vn"
              className="w-full py-2.5 rounded-xl bg-slate-100 hover:bg-slate-200 text-slate-800 text-xs font-bold text-center transition-colors flex items-center justify-center gap-2"
            >
              <span className="material-symbols-outlined text-[18px]">mail</span>
              <span>Gửi Email</span>
            </a>
          </div>
        </div>

        {/* SECTION 3: SLA SERVICE COMMITMENTS BANNER */}
        <div className="w-full bg-white p-6 sm:p-8 rounded-2xl shadow-xs border border-slate-200 mb-10">
          <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 mb-6">
            <div>
              <span className="text-xs font-bold text-blue-600 uppercase tracking-wider">
                Tiêu chuẩn vận hành thông minh
              </span>
              <h2 className="text-xl font-bold text-slate-900 mt-0.5">Cam kết Chất lượng Dịch vụ (SLA) ParkMaster</h2>
            </div>
            <div className="flex items-center gap-1.5 text-xs text-slate-500 font-medium">
              <span className="w-2 h-2 rounded-full bg-emerald-500"></span>
              <span>Giám sát thời gian thực từ 24 cụm bãi đỗ</span>
            </div>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            <div className="p-4 rounded-xl bg-slate-50 border border-slate-200 flex flex-col justify-between">
              <div className="flex items-center justify-between mb-2">
                <span className="text-[11px] text-slate-500 uppercase font-bold">Mở barie khẩn cấp</span>
                <span className="material-symbols-outlined text-[18px] text-blue-600">bolt</span>
              </div>
              <div className="text-2xl font-extrabold text-blue-600">&lt; 30 Giây</div>
              <p className="text-xs text-slate-500 mt-1">Mở khóa thanh toán hoặc mở barie khẩn cấp từ xa qua tổng đài viên.</p>
            </div>

            <div className="p-4 rounded-xl bg-slate-50 border border-slate-200 flex flex-col justify-between">
              <div className="flex items-center justify-between mb-2">
                <span className="text-[11px] text-slate-500 uppercase font-bold">Bảo an phương tiện</span>
                <span className="material-symbols-outlined text-[18px] text-emerald-600">security</span>
              </div>
              <div className="text-2xl font-extrabold text-emerald-600">100%</div>
              <p className="text-xs text-slate-500 mt-1">Bảo hiểm rủi ro ParkMaster Care toàn diện cho mọi xe trong thời gian lưu bãi.</p>
            </div>

            <div className="p-4 rounded-xl bg-slate-50 border border-slate-200 flex flex-col justify-between">
              <div className="flex items-center justify-between mb-2">
                <span className="text-[11px] text-slate-500 uppercase font-bold">Hoàn tiền tức thì</span>
                <span className="material-symbols-outlined text-[18px] text-purple-600">autorenew</span>
              </div>
              <div className="text-2xl font-extrabold text-purple-600">30 Phút</div>
              <p className="text-xs text-slate-500 mt-1">Hoàn tiền tự động 100% khi thực hiện hủy đặt chỗ trước giờ hẹn tối thiểu 30 phút.</p>
            </div>

            <div className="p-4 rounded-xl bg-slate-50 border border-slate-200 flex flex-col justify-between">
              <div className="flex items-center justify-between mb-2">
                <span className="text-[11px] text-slate-500 uppercase font-bold">Độ hài lòng dịch vụ</span>
                <span className="material-symbols-outlined text-[18px] text-amber-500">star</span>
              </div>
              <div className="text-2xl font-extrabold text-slate-900">4.9 / 5</div>
              <p className="text-xs text-slate-500 mt-1">Điểm đánh giá mức độ hài lòng ghi nhận từ hơn 48.000 lượt yêu cầu thành công.</p>
            </div>
          </div>
        </div>

        {/* SECTION 4: MAIN 2-COLUMN SECTION (FAQ & SUPPORT REQUEST FORM) */}
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-8 items-start">
          {/* LEFT COLUMN: FAQ ACCORDIONS (7 Cols) */}
          <div className="lg:col-span-7 flex flex-col gap-6">
            <div className="bg-white p-6 rounded-2xl shadow-xs border border-slate-200">
              <div className="flex items-center justify-between mb-4">
                <div>
                  <span className="text-xs font-bold text-blue-600 uppercase tracking-wider">Cơ sở dữ liệu tri thức</span>
                  <h2 className="text-lg font-bold text-slate-900 mt-0.5">Câu hỏi thường gặp (FAQ)</h2>
                </div>
                <span className="text-xs text-slate-500 bg-slate-100 px-2.5 py-1 rounded-full font-semibold">24 bài viết</span>
              </div>

              {/* Category Pills */}
              <div className="flex items-center gap-2 overflow-x-auto pb-1">
                <button
                  type="button"
                  onClick={() => setActiveFaqCategory('all')}
                  className={`px-3 py-1.5 rounded-lg text-xs font-bold transition-all shrink-0 ${
                    activeFaqCategory === 'all' ? 'bg-blue-600 text-white shadow-xs' : 'bg-slate-100 text-slate-600 hover:text-slate-900'
                  }`}
                >
                  Tất cả
                </button>
                <button
                  type="button"
                  onClick={() => setActiveFaqCategory('booking')}
                  className={`px-3 py-1.5 rounded-lg text-xs font-bold transition-all shrink-0 ${
                    activeFaqCategory === 'booking' ? 'bg-blue-600 text-white shadow-xs' : 'bg-slate-100 text-slate-600 hover:text-slate-900'
                  }`}
                >
                  Đặt chỗ & Giữ chỗ
                </button>
                <button
                  type="button"
                  onClick={() => setActiveFaqCategory('anpr')}
                  className={`px-3 py-1.5 rounded-lg text-xs font-bold transition-all shrink-0 ${
                    activeFaqCategory === 'anpr' ? 'bg-blue-600 text-white shadow-xs' : 'bg-slate-100 text-slate-600 hover:text-slate-900'
                  }`}
                >
                  Vào bãi & Camera ANPR
                </button>
                <button
                  type="button"
                  onClick={() => setActiveFaqCategory('payment')}
                  className={`px-3 py-1.5 rounded-lg text-xs font-bold transition-all shrink-0 ${
                    activeFaqCategory === 'payment' ? 'bg-blue-600 text-white shadow-xs' : 'bg-slate-100 text-slate-600 hover:text-slate-900'
                  }`}
                >
                  Thanh toán & Hóa đơn
                </button>
                <button
                  type="button"
                  onClick={() => setActiveFaqCategory('ev')}
                  className={`px-3 py-1.5 rounded-lg text-xs font-bold transition-all shrink-0 ${
                    activeFaqCategory === 'ev' ? 'bg-blue-600 text-white shadow-xs' : 'bg-slate-100 text-slate-600 hover:text-slate-900'
                  }`}
                >
                  Trạm sạc EV
                </button>
              </div>
            </div>

            {/* Accordion List */}
            <div className="flex flex-col gap-3">
              {filteredFaqs.map((faq) => {
                const isOpen = openFaqIds.includes(faq.id);
                return (
                  <div
                    key={faq.id}
                    className="bg-white rounded-2xl shadow-xs border border-slate-200 overflow-hidden transition-all"
                  >
                    <button
                      type="button"
                      onClick={() => toggleFaq(faq.id)}
                      className="w-full p-4 flex items-center justify-between text-left gap-4 hover:bg-slate-50 transition-colors cursor-pointer"
                    >
                      <div className="flex items-center gap-3">
                        <span className="w-8 h-8 rounded-lg bg-blue-50 text-blue-600 flex items-center justify-center font-bold text-xs shrink-0">
                          {faq.id}
                        </span>
                        <span className="font-bold text-sm text-slate-900">{faq.question}</span>
                      </div>
                      <span
                        className={`material-symbols-outlined text-slate-400 transition-transform duration-200 ${
                          isOpen ? 'rotate-180 text-blue-600' : ''
                        }`}
                      >
                        expand_more
                      </span>
                    </button>
                    {isOpen && (
                      <div className="px-4 pb-4">
                        <div className="p-4 rounded-xl bg-slate-50 text-slate-600 text-xs leading-relaxed border border-slate-100">
                          {faq.answer}
                        </div>
                      </div>
                    )}
                  </div>
                );
              })}
            </div>

            {/* Documentation Visual Banner */}
            <div className="bg-white p-5 rounded-2xl shadow-xs border border-slate-200 flex items-center justify-between gap-4">
              <div className="flex items-center gap-4">
                <div className="w-12 h-12 rounded-xl bg-blue-50 flex items-center justify-center text-blue-600 shrink-0">
                  <span className="material-symbols-outlined text-[28px]">menu_book</span>
                </div>
                <div>
                  <h4 className="text-sm font-bold text-slate-900">Sổ tay hướng dẫn & Video minh họa</h4>
                  <p className="text-xs text-slate-500">Xem sơ đồ chỉ dẫn luồng xe chạy và tài liệu PDF chi tiết cho 24 bãi đỗ liên kết.</p>
                </div>
              </div>
              <button
                type="button"
                className="px-4 py-2 bg-slate-100 hover:bg-slate-200 text-slate-800 text-xs font-bold rounded-xl shrink-0 transition-colors"
              >
                Tải cẩm nang
              </button>
            </div>
          </div>

          {/* RIGHT COLUMN: SUPPORT REQUEST TICKET FORM (5 Cols) */}
          <div className="lg:col-span-5 bg-white p-6 rounded-2xl shadow-xs border border-slate-200">
            <div className="mb-6">
              <div className="flex items-center gap-1.5 text-blue-600 text-xs uppercase font-bold mb-1">
                <span className="material-symbols-outlined text-[16px]">support_agent</span>
                <span>Phiếu hỗ trợ kỹ thuật</span>
              </div>
              <h2 className="text-lg font-bold text-slate-900">Gửi yêu cầu hỗ trợ kỹ thuật</h2>
              <p className="text-xs text-slate-500 mt-1">
                Chúng tôi sẽ phản hồi qua Email hoặc gọi điện trong tối đa 15 phút làm việc.
              </p>
            </div>

            {ticketCreated ? (
              <div className="p-6 rounded-2xl bg-emerald-50 border border-emerald-200 text-center flex flex-col items-center">
                <div className="w-12 h-12 rounded-full bg-emerald-100 text-emerald-600 flex items-center justify-center mb-3">
                  <span className="material-symbols-outlined text-[28px]">check_circle</span>
                </div>
                <h3 className="text-base font-bold text-slate-900">Đã gửi phiếu hỗ trợ!</h3>
                <p className="text-xs text-slate-600 mt-1 mb-2">
                  Mã phiếu yêu cầu của bạn là: <strong className="text-emerald-700">{ticketCreated}</strong>
                </p>
                <p className="text-[11px] text-slate-500 mb-4">
                  Chuyên viên hỗ trợ sẽ liên hệ với bạn trong vòng 15 phút.
                </p>
                <button
                  type="button"
                  onClick={() => setTicketCreated(null)}
                  className="px-4 py-2 rounded-xl bg-emerald-600 text-white text-xs font-bold"
                >
                  Tạo phiếu khác
                </button>
              </div>
            ) : (
              <form onSubmit={handleTicketSubmit} className="flex flex-col gap-4 text-xs">
                {/* Full Name */}
                <div className="flex flex-col gap-1">
                  <label className="font-bold text-slate-700">
                    Họ và tên người gửi <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="text"
                    required
                    value={fullName}
                    onChange={(e) => setFullName(e.target.value)}
                    className="w-full h-10 px-3 rounded-xl bg-slate-50 border border-slate-200 text-slate-800 focus:bg-white focus:outline-hidden focus:ring-2 focus:ring-blue-500/20 transition-all"
                  />
                </div>

                {/* Phone */}
                <div className="flex flex-col gap-1">
                  <label className="font-bold text-slate-700">
                    Số điện thoại liên hệ <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="tel"
                    required
                    value={phone}
                    onChange={(e) => setPhone(e.target.value)}
                    className="w-full h-10 px-3 rounded-xl bg-slate-50 border border-slate-200 text-slate-800 focus:bg-white focus:outline-hidden focus:ring-2 focus:ring-blue-500/20 transition-all"
                  />
                </div>

                {/* Issue Category */}
                <div className="flex flex-col gap-1">
                  <label className="font-bold text-slate-700">
                    Loại sự cố / Vấn đề <span className="text-red-500">*</span>
                  </label>
                  <div className="relative">
                    <select
                      value={issueType}
                      onChange={(e) => setIssueType(e.target.value)}
                      className="w-full h-10 px-3 rounded-xl bg-slate-50 border border-slate-200 text-slate-800 focus:bg-white focus:outline-hidden focus:ring-2 focus:ring-blue-500/20 appearance-none transition-all pr-8 cursor-pointer"
                    >
                      <option value="barie">Sự cố Barie cổng vào/ra</option>
                      <option value="payment">Thanh toán & Hoàn tiền</option>
                      <option value="ev">Trạm sạc xe điện EV</option>
                      <option value="feedback">Góp ý & Khiếu nại dịch vụ</option>
                    </select>
                    <span className="material-symbols-outlined absolute right-2.5 top-2.5 text-slate-400 pointer-events-none text-[20px]">
                      expand_more
                    </span>
                  </div>
                </div>

                {/* License Plate or Booking Code */}
                <div className="flex flex-col gap-1">
                  <label className="font-bold text-slate-700">Biển số xe hoặc Mã đặt chỗ</label>
                  <input
                    type="text"
                    value={identifier}
                    onChange={(e) => setIdentifier(e.target.value)}
                    placeholder="VD: 51K-889.32 hoặc PKM-12345"
                    className="w-full h-10 px-3 rounded-xl bg-slate-50 border border-slate-200 text-slate-800 focus:bg-white focus:outline-hidden focus:ring-2 focus:ring-blue-500/20 transition-all"
                  />
                </div>

                {/* Details */}
                <div className="flex flex-col gap-1">
                  <label className="font-bold text-slate-700">
                    Nội dung chi tiết <span className="text-red-500">*</span>
                  </label>
                  <textarea
                    rows={3}
                    required
                    value={content}
                    onChange={(e) => setContent(e.target.value)}
                    placeholder="Mô tả cụ thể sự cố, thời gian và bãi đỗ bạn gặp phải..."
                    className="w-full p-3 rounded-xl bg-slate-50 border border-slate-200 text-slate-800 focus:bg-white focus:outline-hidden focus:ring-2 focus:ring-blue-500/20 transition-all resize-none"
                  />
                </div>

                {/* File Upload Dropzone */}
                <div className="flex flex-col gap-1">
                  <label className="font-bold text-slate-700">
                    Tải lên ảnh chụp hiện trường hoặc vé xe (nếu có)
                  </label>
                  <div className="w-full p-4 rounded-xl border-2 border-dashed border-slate-200 hover:border-blue-400 bg-slate-50 hover:bg-slate-100/50 cursor-pointer transition-colors flex flex-col items-center justify-center text-center">
                    <span className="material-symbols-outlined text-[24px] text-blue-600 mb-1">cloud_upload</span>
                    <span className="text-xs font-semibold text-slate-700">Nhấp để tải ảnh lên</span>
                    <span className="text-[10px] text-slate-400">Định dạng JPG, PNG, PDF (tối đa 10MB)</span>
                  </div>
                </div>

                {/* Submit */}
                <button
                  type="submit"
                  disabled={isSubmitting}
                  className="w-full py-3 rounded-xl bg-blue-600 hover:bg-blue-700 text-white font-bold text-xs shadow-md shadow-blue-500/20 hover:shadow-lg transition-all flex items-center justify-center gap-2 mt-2 cursor-pointer disabled:opacity-50"
                >
                  <span className="material-symbols-outlined text-[16px]">send</span>
                  <span>{isSubmitting ? 'Đang gửi phiếu...' : 'Gửi phiếu hỗ trợ'}</span>
                </button>
              </form>
            )}
          </div>
        </div>
      </main>

      {/* Live Chat Right Drawer */}
      {isChatOpen && (
        <div className="fixed inset-0 z-50 flex justify-end bg-slate-900/40 backdrop-blur-xs animate-in fade-in">
          <div className="w-full max-w-md bg-white h-full shadow-2xl flex flex-col justify-between animate-in slide-in-from-right duration-200">
            {/* Header */}
            <div className="p-4 bg-blue-600 text-white flex items-center justify-between">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-full bg-white/20 flex items-center justify-center font-bold">
                  🤖
                </div>
                <div>
                  <h3 className="font-bold text-sm">ParkMaster AI & Live Support</h3>
                  <div className="flex items-center gap-1.5 text-[11px] text-emerald-200">
                    <span className="w-2 h-2 rounded-full bg-emerald-400 animate-pulse"></span>
                    <span>Đang trực tuyến • Sẵn sàng hỗ trợ</span>
                  </div>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setIsChatOpen(false)}
                className="p-1 rounded-lg hover:bg-white/10 text-white transition-colors"
              >
                <span className="material-symbols-outlined text-[22px]">close</span>
              </button>
            </div>

            {/* Chat Body */}
            <div className="flex-1 p-4 overflow-y-auto flex flex-col gap-3 bg-slate-50 text-xs">
              {chatMessages.map((msg, idx) => (
                <div
                  key={idx}
                  className={`flex flex-col ${msg.sender === 'user' ? 'items-end' : 'items-start'}`}
                >
                  <div
                    className={`max-w-[80%] p-3 rounded-2xl shadow-2xs ${
                      msg.sender === 'user'
                        ? 'bg-blue-600 text-white rounded-tr-none'
                        : 'bg-white text-slate-800 border border-slate-200 rounded-tl-none'
                    }`}
                  >
                    {msg.text}
                  </div>
                  <span className="text-[10px] text-slate-400 mt-1 px-1">{msg.time}</span>
                </div>
              ))}
            </div>

            {/* Input Bar */}
            <form onSubmit={handleSendChatMessage} className="p-3 bg-white border-t border-slate-200 flex items-center gap-2">
              <input
                type="text"
                value={inputChat}
                onChange={(e) => setInputChat(e.target.value)}
                placeholder="Nhập tin nhắn..."
                className="flex-1 h-10 px-3 rounded-xl bg-slate-100 border border-slate-200 text-xs text-slate-800 focus:outline-hidden focus:bg-white focus:ring-2 focus:ring-blue-500/20"
              />
              <button
                type="submit"
                className="w-10 h-10 rounded-xl bg-blue-600 hover:bg-blue-700 text-white flex items-center justify-center shrink-0 shadow-xs"
              >
                <span className="material-symbols-outlined text-[18px]">send</span>
              </button>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
