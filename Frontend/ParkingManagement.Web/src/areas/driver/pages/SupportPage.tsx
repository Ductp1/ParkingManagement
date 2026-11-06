import React, { useState } from 'react';
import { useAppSelector } from '@/store';

interface FaqItem {
  id: string;
  category: 'booking' | 'anpr' | 'payment' | 'ev';
  question: string;
  answer: string;
}

interface SupportTicket {
  id: string;
  code: string;
  issueType: 'barie' | 'payment' | 'ev' | 'feedback';
  issueLabel: string;
  identifier: string; // Biển số xe hoặc Mã booking
  location: string;
  content: string;
  createdAt: string;
  status: 'pending' | 'in_progress' | 'resolved' | 'closed';
  statusLabel: string;
  statusColor: string;
  assignedTo: string;
  resolutionNote?: string;
  resolutionAmount?: number;
  timeline: { step: string; time: string; done: boolean; current?: boolean }[];
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
      'Bạn được miễn phí hủy 100% khi thực hiện hủy trước giờ hẹn từ 1 giờ trở lên. Tiền cọc hoặc phí đã thanh toán sẽ được hoàn trực tiếp về Ví ParkMaster (ngay lập tức) hoặc thẻ ngân hàng (1-3 ngày). Nếu hủy trong vòng dưới 1 giờ trước giờ đỗ, phí giữ chỗ bãi xe (30%) sẽ được áp dụng và hoàn lại 70%.',
  },
  {
    id: 'Q3',
    category: 'ev',
    question: 'Làm thế nào để sử dụng trạm sạc xe điện EV tại các bãi xe ParkMaster?',
    answer:
      'Khi đặt chỗ trên sơ đồ bãi, chọn ô đỗ có biểu tượng cổng sạc màu xanh lá (ví dụ: ô EV-01, B-04). Khi xe tiến vào vị trí, bạn chỉ cần cắm đầu súng sạc Type 2 hoặc CCS2 vào xe. Ứng dụng ParkMaster sẽ nhận diện tín hiệu súng sạc và tự động cấp điện sau 5 giây.',
  },
  {
    id: 'Q4',
    category: 'payment',
    question: 'Tôi bị trừ tiền 2 lần qua cổng VNPAY/MoMo thì xử lý thế nào?',
    answer:
      'Đừng lo lắng! Hệ thống thanh toán ParkMaster có cơ chế tự động đối soát IPN và hoàn tiền trùng lặp (Double-charge Refund) trong vòng 15 phút. Nếu sau 30 phút chưa nhận được tiền hoàn, bạn chỉ cần gửi Phiếu hỗ trợ tại trang này, chuyên viên tài chính sẽ hoàn tiền trực tiếp vào ví của bạn.',
  },
];

const INITIAL_TICKETS: SupportTicket[] = [
  {
    id: '1',
    code: '#TCK-2026-1082',
    issueType: 'barie',
    issueLabel: 'Sự cố Barie cổng vào/ra',
    identifier: '51K-889.32 • #PKM-2025-8842',
    location: 'Landmark 81 Garage (Làn vào #01)',
    content: 'Camera ANPR quét chậm, barie không tự nhấc dù đã có mã đặt chỗ hợp lệ. Phải quét mã QR thủ công.',
    createdAt: 'Hôm nay, 10:15',
    status: 'in_progress',
    statusLabel: 'Kỹ thuật viên đang xử lý',
    statusColor: 'bg-blue-100 text-blue-800 border-blue-200',
    assignedTo: 'Kỹ sư Trần Minh (Tổ kỹ thuật Landmark 81)',
    timeline: [
      { step: 'Tiếp nhận phiếu hỗ trợ', time: '10:15', done: true },
      { step: 'Trích xuất camera ANPR làn vào', time: '10:20', done: true },
      { step: 'Cân chỉnh góc quét camera & độ sáng', time: '10:35', done: false, current: true },
      { step: 'Hoàn tất & Nghiệm thu', time: 'Dự kiến 11:00', done: false },
    ],
  },
  {
    id: '2',
    code: '#TCK-2026-0941',
    issueType: 'payment',
    issueLabel: 'Thanh toán & Hoàn tiền',
    identifier: '#PKM-2025-7621',
    location: 'Bitexco Financial Tower',
    content: 'Bị tính phụ trội 1 block giờ do xe bị kẹt ở làn ra lúc 12:45. Yêu cầu kiểm tra thời gian thực tế.',
    createdAt: '24/10/2026 14:30',
    status: 'resolved',
    statusLabel: 'Đã giải quyết (Hoàn tiền)',
    statusColor: 'bg-emerald-100 text-emerald-800 border-emerald-200',
    assignedTo: 'CSKH Nguyễn Thị Hoa',
    resolutionNote: 'Đã đối soát camera làn ra, xác nhận xe có mặt lúc 12:43 nhưng barie nghẽn 3 phút. Hệ thống đã hoàn trả 25.000 đ vào Ví ParkMaster của quý khách.',
    resolutionAmount: 25000,
    timeline: [
      { step: 'Tiếp nhận khiếu nại', time: '14:30', done: true },
      { step: 'Đối soát log cảm biến barie', time: '14:40', done: true },
      { step: 'Phê duyệt hoàn phí 25.000 đ', time: '14:52', done: true },
      { step: 'Cộng tiền vào ví & Đóng phiếu', time: '14:55', done: true },
    ],
  },
  {
    id: '3',
    code: '#TCK-2026-0819',
    issueType: 'ev',
    issueLabel: 'Trạm sạc xe điện EV',
    identifier: '51K-889.32',
    location: 'Saigon Centre / Takashimaya',
    content: 'Trụ sạc EV-03 ngắt điện sau 15 phút dù pin xe mới nạp được 30%.',
    createdAt: '18/10/2026 18:20',
    status: 'resolved',
    statusLabel: 'Đã giải quyết',
    statusColor: 'bg-emerald-100 text-emerald-800 border-emerald-200',
    assignedTo: 'Kỹ thuật viên VinFast EV Station',
    resolutionNote: 'Sự cố do cảm biến nhiệt độ súng sạc Type 2 tự kích hoạt bảo vệ. Đã thay cáp súng sạc mới tại trụ EV-03.',
    timeline: [
      { step: 'Tiếp nhận báo cáo', time: '18:20', done: true },
      { step: 'Kiểm tra phần cứng trụ sạc', time: '18:45', done: true },
      { step: 'Thay thế dây sạc & Đo điện áp', time: '19:10', done: true },
      { step: 'Xác nhận hoạt động bình thường', time: '19:30', done: true },
    ],
  },
];

export const SupportPage: React.FC = () => {
  const { user } = useAppSelector((state) => state.auth);

  // Main Tab State
  const [activeMainTab, setActiveMainTab] = useState<'faq' | 'create' | 'tracking'>('faq');

  // FAQ State
  const [activeFaqCategory, setActiveFaqCategory] = useState<'all' | 'booking' | 'anpr' | 'payment' | 'ev'>('all');
  const [openFaqIds, setOpenFaqIds] = useState<string[]>(['Q1', 'Q2']);

  // Ticket Form State
  const [fullName, setFullName] = useState(user?.fullName || 'Bùi Phạm Chí Nhân');
  const [phone, setPhone] = useState('0912 345 678');
  const [issueType, setIssueType] = useState<'barie' | 'payment' | 'ev' | 'feedback'>('barie');
  const [identifier, setIdentifier] = useState('51K-889.32 / PKM-2025-8842');
  const [locationName, setLocationName] = useState('Landmark 81 Garage');
  const [content, setContent] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Ticket List & Detail State
  const [tickets, setTickets] = useState<SupportTicket[]>(INITIAL_TICKETS);
  const [selectedTicket, setSelectedTicket] = useState<SupportTicket | null>(null);
  const [ticketFilterStatus, setTicketFilterStatus] = useState<'all' | 'in_progress' | 'resolved'>('all');
  const [toastMessage, setToastMessage] = useState<string | null>(null);

  // Live Chat Drawer State
  const [isChatOpen, setIsChatOpen] = useState(false);
  const [chatMessages, setChatMessages] = useState<Array<{ sender: 'bot' | 'user'; text: string; time: string }>>([
    {
      sender: 'bot',
      text: 'Xin chào anh/chị! Em là trợ lý thông minh ParkMaster AI. Em có thể hỗ trợ anh/chị về nhận diện biển số ANPR, chính sách hủy hoàn tiền 1 giờ, hoặc kiểm tra ô đỗ xe?',
      time: '11:20',
    },
  ]);
  const [inputChat, setInputChat] = useState('');

  const toggleFaq = (id: string) => {
    if (openFaqIds.includes(id)) {
      setOpenFaqIds(openFaqIds.filter((item) => item !== id));
    } else {
      setOpenFaqIds([...openFaqIds, id]);
    }
  };

  const handleTicketSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);

    setTimeout(() => {
      const newTicketCode = `#TCK-2026-${Math.floor(1000 + Math.random() * 9000)}`;
      const issueLabelMap = {
        barie: 'Sự cố Barie cổng vào/ra',
        payment: 'Thanh toán & Hoàn tiền',
        ev: 'Trạm sạc xe điện EV',
        feedback: 'Góp ý & Khiếu nại dịch vụ',
      };

      const newTicket: SupportTicket = {
        id: String(Date.now()),
        code: newTicketCode,
        issueType,
        issueLabel: issueLabelMap[issueType],
        identifier: identifier || '51K-889.32',
        location: locationName,
        content,
        createdAt: 'Vừa xong',
        status: 'pending',
        statusLabel: 'Đang chờ tiếp nhận',
        statusColor: 'bg-amber-100 text-amber-800 border-amber-200',
        assignedTo: 'Đội hỗ trợ trực ban ParkMaster 24/7',
        timeline: [
          { step: 'Tiếp nhận phiếu hỗ trợ', time: 'Vừa xong', done: true, current: true },
          { step: 'Phân công kỹ thuật viên kiểm tra', time: 'Dự kiến 5-10 phút', done: false },
          { step: 'Xử lý sự cố & Liên hệ tài xế', time: 'Dự kiến 15 phút', done: false },
          { step: 'Nghiệm thu & Đóng phiếu', time: 'Chờ xử lý', done: false },
        ],
      };

      setTickets([newTicket, ...tickets]);
      setIsSubmitting(false);
      setContent('');
      setActiveMainTab('tracking');
      setToastMessage(`Đã tạo phiếu hỗ trợ ${newTicketCode} thành công! Kỹ thuật viên sẽ kiểm tra trong 15 phút.`);
      setTimeout(() => setToastMessage(null), 5500);
    }, 800);
  };

  const getBotResponse = (raw: string): string => {
    const q = raw.toLowerCase().trim();

    if (q.includes('chào') || q.includes('hi') || q.includes('hello') || q.includes('alo') || q.includes('ơi')) {
      return 'Dạ em chào anh/chị! Em là trợ lý thông minh ParkMaster AI. Anh/chị đang cần hỗ trợ về việc nhận diện camera barie, sạc xe điện EV, chính sách hủy hoàn tiền hay thanh toán ạ?';
    }

    if (q.includes('barie') || q.includes('cổng') || q.includes('kẹt') || q.includes('không mở') || q.includes('anpr') || q.includes('biển số')) {
      return 'Nếu camera ANPR chưa tự động mở barie, anh/chị có thể:\n1. Mở mục "Lịch sử đặt" lấy mã QR đưa vào trụ quét của cổng.\n2. Bấm nút "Gọi hỗ trợ tại cổng" trên màn hình cảm ứng barie, kỹ thuật viên trực ban sẽ mở cổng từ xa trong vòng 15 giây ạ.';
    }

    if (q.includes('hủy') || q.includes('hoàn tiền') || q.includes('trả lại') || q.includes('cận giờ') || q.includes('refund')) {
      return 'Chính sách hoàn tiền tự động (UI-10) của ParkMaster:\n• Hủy trước giờ đỗ ≥ 1 tiếng: Được HOÀN TIỀN 100% vào Ví ngay lập tức.\n• Hủy cận giờ (< 1 tiếng trước giờ đỗ): Áp dụng phí giữ chỗ 30%, số tiền hoàn lại là 70% ạ.';
    }

    if (q.includes('giá') || q.includes('phí') || q.includes('bao nhiêu') || q.includes('tiền') || q.includes('bảng giá')) {
      return 'Bảng giá đỗ xe được tính lũy tiến theo block giờ và loại xe (ô tô thường, xe điện EV hoặc xe quá khổ). Anh/chị có thể xem chi tiết bảng giá từng bãi đỗ tại mục "Bãi đỗ & Sơ đồ" ạ.';
    }

    if (q.includes('sạc') || q.includes('ev') || q.includes('pin') || q.includes('điện')) {
      return 'Trạm sạc xe điện EV hỗ trợ cổng Type 2 (AC 11kW/22kW) và CCS2 (DC sạc nhanh 60kW - 120kW). Khi đặt chỗ, anh/chị chỉ cần chọn ô đỗ có biểu tượng sạc màu xanh lá (như ô B-04).';
    }

    if (q.includes('vnpay') || q.includes('momo') || q.includes('thanh toán') || q.includes('ví')) {
      return 'ParkMaster hỗ trợ thanh toán trực tuyến qua Ví ParkMaster, Ví MoMo AutoPay, VNPAY-QR Sandbox và tài khoản VETC/ePass. Giao dịch được mã hóa an toàn theo tiêu chuẩn PCI-DSS Level 1.';
    }

    if (q.includes('tìm') || q.includes('gần') || q.includes('vị trí') || q.includes('đâu')) {
      return 'Anh/chị vào trang "Tìm bãi đỗ gần tôi" và bật quyền định vị GPS, hệ thống sẽ tự động quét các bãi xe còn chỗ trống trong bán kính 2km - 10km gần vị trí hiện tại của anh/chị nhất ạ.';
    }

    if (q.includes('hotline') || q.includes('số điện thoại') || q.includes('tổng đài') || q.includes('sdt') || q.includes('liên hệ')) {
      return 'Tổng đài hỗ trợ khẩn cấp 24/7 của ParkMaster là: 1900 6868 (Miễn phí cước gọi) hoặc email hotro@parkmaster.vn ạ.';
    }

    return `Dạ em đã ghi nhận câu hỏi: "${raw}". Em đã chuyển thông tin tới đội ngũ kỹ thuật viên trực ban 24/7. Anh/chị cũng có thể gửi phiếu hỗ trợ ở tab "Gửi phiếu hỗ trợ" để được xử lý trực tiếp trong 15 phút ạ!`;
  };

  const handleSendChatMessage = (e?: React.FormEvent, customMsg?: string) => {
    if (e) e.preventDefault();
    const msgToSend = customMsg !== undefined ? customMsg : inputChat;
    if (!msgToSend.trim()) return;

    const nowTime = new Date().toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });

    setChatMessages((prev) => [...prev, { sender: 'user', text: msgToSend, time: nowTime }]);
    if (customMsg === undefined) setInputChat('');

    setTimeout(() => {
      const botReply = getBotResponse(msgToSend);
      setChatMessages((prev) => [...prev, { sender: 'bot', text: botReply, time: nowTime }]);
    }, 500);
  };

  const filteredTickets = tickets.filter((t) => {
    if (ticketFilterStatus === 'in_progress') return t.status === 'in_progress' || t.status === 'pending';
    if (ticketFilterStatus === 'resolved') return t.status === 'resolved' || t.status === 'closed';
    return true;
  });

  const filteredFaqs = FAQ_DATA.filter((item) => {
    if (activeFaqCategory === 'all') return true;
    return item.category === activeFaqCategory;
  });

  return (
    <div className="min-h-screen bg-slate-50 flex flex-col font-sans">
      {/* 1. Header Banner */}
      <section className="bg-white border-b border-slate-200">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 flex flex-col md:flex-row md:items-end justify-between gap-6">
          <div className="flex flex-col gap-2 max-w-2xl">
            <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-blue-100 text-blue-800 w-fit text-xs font-bold tracking-wider uppercase">
              <span className="w-2 h-2 rounded-full bg-blue-600 animate-pulse"></span>
              TRUNG TÂM HỖ TRỢ CSKH & THEO DÕI TICKET (UI-15)
            </div>
            <h1 className="text-3xl font-extrabold text-slate-900 tracking-tight">
              Hỗ trợ khách hàng 24/7
            </h1>
            <p className="text-slate-500 text-sm leading-relaxed">
              Giải đáp thắc mắc dịch vụ, gửi phiếu yêu cầu kỹ thuật tại cổng barie và theo dõi tiến độ xử lý khiếu nại theo thời gian thực.
            </p>
          </div>

          <div className="flex items-center gap-3">
            <button
              type="button"
              onClick={() => setIsChatOpen(true)}
              className="px-5 py-2.5 rounded-xl bg-blue-600 hover:bg-blue-700 text-white font-bold text-xs shadow-sm flex items-center gap-2 cursor-pointer transition-all"
            >
              <span className="material-symbols-outlined text-[18px]">forum</span>
              <span>Trợ lý AI & CSKH trực tuyến</span>
            </button>
          </div>
        </div>

        {/* Navigation Tabs Bar */}
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 flex items-center gap-2 border-t border-slate-100 pt-1">
          <button
            type="button"
            onClick={() => setActiveMainTab('faq')}
            className={`py-3 px-4 text-xs font-bold border-b-2 transition-all cursor-pointer flex items-center gap-2 ${
              activeMainTab === 'faq'
                ? 'border-blue-600 text-blue-600 bg-blue-50/30'
                : 'border-transparent text-slate-500 hover:text-slate-800'
            }`}
          >
            <span className="material-symbols-outlined text-[18px]">quiz</span>
            <span>Câu hỏi thường gặp (FAQ)</span>
          </button>

          <button
            type="button"
            onClick={() => setActiveMainTab('create')}
            className={`py-3 px-4 text-xs font-bold border-b-2 transition-all cursor-pointer flex items-center gap-2 ${
              activeMainTab === 'create'
                ? 'border-blue-600 text-blue-600 bg-blue-50/30'
                : 'border-transparent text-slate-500 hover:text-slate-800'
            }`}
          >
            <span className="material-symbols-outlined text-[18px]">add_task</span>
            <span>Gửi phiếu hỗ trợ kỹ thuật</span>
          </button>

          <button
            type="button"
            onClick={() => setActiveMainTab('tracking')}
            className={`py-3 px-4 text-xs font-bold border-b-2 transition-all cursor-pointer flex items-center gap-2 ${
              activeMainTab === 'tracking'
                ? 'border-blue-600 text-blue-600 bg-blue-50/30'
                : 'border-transparent text-slate-500 hover:text-slate-800'
            }`}
          >
            <span className="material-symbols-outlined text-[18px]">history_toggle_off</span>
            <span>Theo dõi Ticket đã gửi</span>
            <span className="px-2 py-0.5 rounded-full bg-blue-100 text-blue-700 text-[10px] font-extrabold">
              {tickets.length}
            </span>
          </button>
        </div>
      </section>

      {/* 2. Main Content */}
      <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 flex-1 w-full">
        {/* TAB 1: FAQ ACCORDION & GUIDES */}
        {activeMainTab === 'faq' && (
          <div className="grid grid-cols-1 lg:grid-cols-12 gap-8 items-start animate-in fade-in">
            <div className="lg:col-span-8 flex flex-col gap-6">
              {/* Category Filter Pills */}
              <div className="flex items-center gap-2 overflow-x-auto pb-1">
                {[
                  { id: 'all', label: 'Tất cả chủ đề' },
                  { id: 'anpr', label: 'Biển số & Barie' },
                  { id: 'booking', label: 'Đặt chỗ & Hủy hoàn tiền' },
                  { id: 'payment', label: 'Thanh toán VNPAY/MoMo' },
                  { id: 'ev', label: 'Trạm sạc xe điện EV' },
                ].map((cat) => (
                  <button
                    key={cat.id}
                    type="button"
                    onClick={() => setActiveFaqCategory(cat.id as any)}
                    className={`px-4 py-2 rounded-xl text-xs font-bold transition-all whitespace-nowrap cursor-pointer ${
                      activeFaqCategory === cat.id
                        ? 'bg-blue-600 text-white shadow-xs'
                        : 'bg-white text-slate-600 border border-slate-200 hover:bg-slate-50'
                    }`}
                  >
                    {cat.label}
                  </button>
                ))}
              </div>

              {/* Accordion List */}
              <div className="flex flex-col gap-3">
                {filteredFaqs.map((faq) => {
                  const isOpen = openFaqIds.includes(faq.id);
                  return (
                    <div
                      key={faq.id}
                      className="bg-white rounded-2xl border border-slate-200 overflow-hidden shadow-xs transition-all"
                    >
                      <button
                        type="button"
                        onClick={() => toggleFaq(faq.id)}
                        className="w-full p-4 text-left flex items-center justify-between gap-4 font-bold text-slate-800 text-xs sm:text-sm hover:text-blue-600 transition-colors cursor-pointer"
                      >
                        <div className="flex items-center gap-3">
                          <span className="w-6 h-6 rounded-lg bg-blue-50 text-blue-600 flex items-center justify-center text-xs font-extrabold shrink-0">
                            ?
                          </span>
                          <span>{faq.question}</span>
                        </div>
                        <span className={`material-symbols-outlined text-[20px] text-slate-400 transition-transform ${
                          isOpen ? 'rotate-180 text-blue-600' : ''
                        }`}>
                          expand_more
                        </span>
                      </button>

                      {isOpen && (
                        <div className="px-5 pb-5 pt-1 text-xs text-slate-600 leading-relaxed border-t border-slate-100 bg-slate-50/50">
                          {faq.answer}
                        </div>
                      )}
                    </div>
                  );
                })}
              </div>
            </div>

            {/* Right Card: Quick Contacts */}
            <div className="lg:col-span-4 flex flex-col gap-4">
              <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-xs flex flex-col gap-4">
                <h3 className="font-bold text-sm text-slate-900">Kênh hỗ trợ khẩn cấp</h3>
                <div className="flex flex-col gap-3 text-xs">
                  <div className="p-3.5 rounded-xl bg-slate-50 border border-slate-200 flex items-center gap-3">
                    <div className="w-10 h-10 rounded-xl bg-blue-50 text-blue-600 flex items-center justify-center shrink-0">
                      <span className="material-symbols-outlined text-[20px]">call</span>
                    </div>
                    <div className="flex flex-col">
                      <span className="text-slate-400 text-[10px] font-bold uppercase">Tổng đài 24/7</span>
                      <span className="font-extrabold text-slate-800 text-sm">1900 6868</span>
                    </div>
                  </div>

                  <div className="p-3.5 rounded-xl bg-slate-50 border border-slate-200 flex items-center gap-3">
                    <div className="w-10 h-10 rounded-xl bg-emerald-50 text-emerald-600 flex items-center justify-center shrink-0">
                      <span className="material-symbols-outlined text-[20px]">mail</span>
                    </div>
                    <div className="flex flex-col">
                      <span className="text-slate-400 text-[10px] font-bold uppercase">Email kỹ thuật</span>
                      <span className="font-bold text-slate-800">hotro@parkmaster.vn</span>
                    </div>
                  </div>
                </div>

                <button
                  type="button"
                  onClick={() => setActiveMainTab('create')}
                  className="w-full py-2.5 rounded-xl bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold transition-all shadow-xs flex items-center justify-center gap-2 cursor-pointer"
                >
                  <span className="material-symbols-outlined text-[16px]">add_circle</span>
                  <span>Gửi phiếu hỗ trợ sự cố</span>
                </button>
              </div>
            </div>
          </div>
        )}

        {/* TAB 2: CREATE SUPPORT TICKET FORM */}
        {activeMainTab === 'create' && (
          <div className="max-w-2xl mx-auto bg-white p-6 sm:p-8 rounded-3xl border border-slate-200 shadow-sm flex flex-col gap-6 animate-in fade-in">
            <div>
              <div className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-blue-100 text-blue-800 w-fit text-[11px] font-bold tracking-wider uppercase mb-2">
                PHIẾU HỖ TRỢ KỸ THUẬT & KHIẾU NẠI (SLA 15 PHÚT)
              </div>
              <h2 className="text-xl font-bold text-slate-900">Gửi yêu cầu hỗ trợ sự cố</h2>
              <p className="text-xs text-slate-500 mt-1">
                Kỹ thuật viên tại bãi xe và đội trực ban giám sát ANPR sẽ tiếp nhận và xử lý trong vòng tối đa 15 phút.
              </p>
            </div>

            <form onSubmit={handleTicketSubmit} className="flex flex-col gap-4 text-xs">
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="flex flex-col gap-1.5">
                  <label className="font-bold text-slate-700">Họ và tên người gửi *</label>
                  <input
                    type="text"
                    required
                    value={fullName}
                    onChange={(e) => setFullName(e.target.value)}
                    className="h-10 px-3.5 rounded-xl border border-slate-200 focus:outline-hidden focus:ring-2 focus:ring-blue-500/20"
                  />
                </div>

                <div className="flex flex-col gap-1.5">
                  <label className="font-bold text-slate-700">Số điện thoại liên hệ *</label>
                  <input
                    type="tel"
                    required
                    value={phone}
                    onChange={(e) => setPhone(e.target.value)}
                    className="h-10 px-3.5 rounded-xl border border-slate-200 focus:outline-hidden focus:ring-2 focus:ring-blue-500/20"
                  />
                </div>
              </div>

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="flex flex-col gap-1.5">
                  <label className="font-bold text-slate-700">Loại vấn đề sự cố *</label>
                  <select
                    value={issueType}
                    onChange={(e) => setIssueType(e.target.value as any)}
                    className="h-10 px-3.5 rounded-xl border border-slate-200 bg-white focus:outline-hidden focus:ring-2 focus:ring-blue-500/20"
                  >
                    <option value="barie">Sự cố Barie cổng vào/ra (ANPR)</option>
                    <option value="payment">Thanh toán & Hoàn tiền (VNPAY/MoMo)</option>
                    <option value="ev">Trạm sạc xe điện EV</option>
                    <option value="feedback">Góp ý & Khiếu nại dịch vụ</option>
                  </select>
                </div>

                <div className="flex flex-col gap-1.5">
                  <label className="font-bold text-slate-700">Bãi đỗ xe xảy ra sự cố *</label>
                  <input
                    type="text"
                    required
                    value={locationName}
                    onChange={(e) => setLocationName(e.target.value)}
                    className="h-10 px-3.5 rounded-xl border border-slate-200 focus:outline-hidden focus:ring-2 focus:ring-blue-500/20"
                  />
                </div>
              </div>

              <div className="flex flex-col gap-1.5">
                <label className="font-bold text-slate-700">Biển số xe hoặc Mã đặt chỗ liên quan</label>
                <input
                  type="text"
                  value={identifier}
                  onChange={(e) => setIdentifier(e.target.value)}
                  placeholder="VD: 51K-889.32 hoặc #PKM-2025-8842"
                  className="h-10 px-3.5 rounded-xl border border-slate-200 focus:outline-hidden focus:ring-2 focus:ring-blue-500/20"
                />
              </div>

              <div className="flex flex-col gap-1.5">
                <label className="font-bold text-slate-700">Mô tả chi tiết nội dung sự cố *</label>
                <textarea
                  rows={4}
                  required
                  value={content}
                  onChange={(e) => setContent(e.target.value)}
                  placeholder="Vui lòng mô tả cụ thể tình huống bạn gặp phải (ví dụ: thời gian đến cổng, camera báo lỗi gì, hay bị trừ tiền sai thế nào)..."
                  className="p-3.5 rounded-xl border border-slate-200 focus:outline-hidden focus:ring-2 focus:ring-blue-500/20 resize-none"
                />
              </div>

              <div className="flex items-center justify-end gap-3 pt-4 border-t border-slate-100">
                <button
                  type="button"
                  onClick={() => setActiveMainTab('faq')}
                  className="px-5 py-2.5 rounded-xl border border-slate-200 text-slate-700 hover:bg-slate-50 font-bold text-xs cursor-pointer"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  disabled={isSubmitting}
                  className="px-6 py-2.5 rounded-xl bg-blue-600 hover:bg-blue-700 disabled:bg-blue-400 text-white font-bold text-xs shadow-sm flex items-center gap-2 cursor-pointer transition-all"
                >
                  {isSubmitting ? (
                    <>
                      <span className="material-symbols-outlined text-[16px] animate-spin">refresh</span>
                      <span>Đang gửi phiếu…</span>
                    </>
                  ) : (
                    <>
                      <span className="material-symbols-outlined text-[16px]">send</span>
                      <span>Gửi phiếu hỗ trợ ngay</span>
                    </>
                  )}
                </button>
              </div>
            </form>
          </div>
        )}

        {/* TAB 3: TICKET TRACKING LIST (UI-15 CORE) */}
        {activeMainTab === 'tracking' && (
          <div className="flex flex-col gap-6 animate-in fade-in">
            {/* Filter Bar */}
            <div className="bg-white p-4 rounded-2xl border border-slate-200 shadow-xs flex flex-col sm:flex-row sm:items-center justify-between gap-4">
              <div className="flex items-center gap-2">
                <button
                  type="button"
                  onClick={() => setTicketFilterStatus('all')}
                  className={`px-3.5 py-1.5 rounded-xl text-xs font-bold transition-all cursor-pointer ${
                    ticketFilterStatus === 'all'
                      ? 'bg-blue-600 text-white shadow-xs'
                      : 'text-slate-600 hover:text-slate-900 bg-slate-50'
                  }`}
                >
                  Tất cả ({tickets.length})
                </button>
                <button
                  type="button"
                  onClick={() => setTicketFilterStatus('in_progress')}
                  className={`px-3.5 py-1.5 rounded-xl text-xs font-bold transition-all cursor-pointer ${
                    ticketFilterStatus === 'in_progress'
                      ? 'bg-blue-600 text-white shadow-xs'
                      : 'text-slate-600 hover:text-slate-900 bg-slate-50'
                  }`}
                >
                  Đang xử lý ({tickets.filter((t) => t.status === 'in_progress' || t.status === 'pending').length})
                </button>
                <button
                  type="button"
                  onClick={() => setTicketFilterStatus('resolved')}
                  className={`px-3.5 py-1.5 rounded-xl text-xs font-bold transition-all cursor-pointer ${
                    ticketFilterStatus === 'resolved'
                      ? 'bg-emerald-600 text-white shadow-xs'
                      : 'text-slate-600 hover:text-slate-900 bg-slate-50'
                  }`}
                >
                  Đã giải quyết ({tickets.filter((t) => t.status === 'resolved' || t.status === 'closed').length})
                </button>
              </div>

              <button
                type="button"
                onClick={() => setActiveMainTab('create')}
                className="px-4 py-2 rounded-xl bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold transition-all shadow-xs flex items-center gap-1.5 self-start sm:self-auto cursor-pointer"
              >
                <span className="material-symbols-outlined text-[16px]">add</span>
                <span>Tạo yêu cầu mới</span>
              </button>
            </div>

            {/* Ticket Cards Grid */}
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-5">
              {filteredTickets.map((ticket) => (
                <div
                  key={ticket.id}
                  className="bg-white rounded-3xl border border-slate-200/90 p-5 shadow-xs hover:shadow-md transition-all flex flex-col justify-between gap-4"
                >
                  <div className="flex flex-col gap-3">
                    <div className="flex items-center justify-between gap-2">
                      <span className="font-mono font-extrabold text-blue-600 text-sm">
                        {ticket.code}
                      </span>
                      <span className={`px-2.5 py-0.5 rounded-full text-[10px] font-bold border ${ticket.statusColor}`}>
                        {ticket.statusLabel}
                      </span>
                    </div>

                    <div>
                      <h4 className="font-bold text-slate-900 text-sm">{ticket.issueLabel}</h4>
                      <p className="text-[11px] text-slate-500 mt-0.5">{ticket.location}</p>
                    </div>

                    <p className="text-xs text-slate-600 line-clamp-2 leading-relaxed bg-slate-50 p-3 rounded-xl border border-slate-100">
                      "{ticket.content}"
                    </p>
                  </div>

                  <div className="pt-3 border-t border-slate-100 flex items-center justify-between text-[11px]">
                    <span className="text-slate-400">{ticket.createdAt}</span>
                    <button
                      type="button"
                      onClick={() => setSelectedTicket(ticket)}
                      className="text-blue-600 hover:text-blue-700 font-bold flex items-center gap-1 cursor-pointer"
                    >
                      <span>Xem tiến độ</span>
                      <span className="material-symbols-outlined text-[14px]">arrow_forward</span>
                    </button>
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}
      </main>

      {/* 3. TICKET DETAIL MODAL WITH SLA TIMELINE */}
      {selectedTicket && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs z-50 flex items-center justify-center p-4 animate-in fade-in">
          <div className="bg-white rounded-3xl shadow-2xl p-6 sm:p-8 w-full max-w-xl border border-slate-200 max-h-[92vh] overflow-y-auto flex flex-col gap-6">
            {/* Header */}
            <div className="flex items-start justify-between gap-4 pb-4 border-b border-slate-100">
              <div className="flex items-center gap-3">
                <div className="w-12 h-12 rounded-2xl bg-blue-50 text-blue-600 flex items-center justify-center shrink-0">
                  <span className="material-symbols-outlined text-[26px]">support_agent</span>
                </div>
                <div>
                  <div className="flex items-center gap-2">
                    <span className="font-mono font-bold text-blue-600 text-sm">{selectedTicket.code}</span>
                    <span className={`px-2 py-0.5 rounded-full text-[10px] font-bold border ${selectedTicket.statusColor}`}>
                      {selectedTicket.statusLabel}
                    </span>
                  </div>
                  <h3 className="font-extrabold text-lg text-slate-900 mt-0.5">{selectedTicket.issueLabel}</h3>
                  <p className="text-xs text-slate-500">{selectedTicket.location} • {selectedTicket.createdAt}</p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setSelectedTicket(null)}
                className="text-slate-400 hover:text-slate-700 p-1.5 rounded-xl hover:bg-slate-100 transition-colors"
              >
                <span className="material-symbols-outlined text-[20px]">close</span>
              </button>
            </div>

            {/* SLA Resolution Timeline */}
            <div className="p-4 rounded-2xl bg-slate-50 border border-slate-200 flex flex-col gap-3">
              <span className="text-[11px] font-bold text-slate-500 uppercase tracking-wider">
                Tiến độ xử lý (SLA Cam kết)
              </span>
              <div className="flex flex-col gap-3">
                {selectedTicket.timeline.map((step, idx) => (
                  <div key={idx} className="flex items-start gap-3 text-xs">
                    <div className={`w-6 h-6 rounded-full flex items-center justify-center shrink-0 mt-0.5 ${
                      step.done
                        ? 'bg-emerald-500 text-white'
                        : step.current
                        ? 'bg-blue-600 text-white animate-pulse'
                        : 'bg-slate-200 text-slate-500'
                    }`}>
                      {step.done ? (
                        <span className="material-symbols-outlined text-[14px]">check</span>
                      ) : (
                        <span>{idx + 1}</span>
                      )}
                    </div>
                    <div className="flex flex-col flex-1">
                      <span className={`font-bold ${step.done ? 'text-slate-800' : step.current ? 'text-blue-600' : 'text-slate-400'}`}>
                        {step.step}
                      </span>
                      <span className="text-[10px] text-slate-400">{step.time}</span>
                    </div>
                  </div>
                ))}
              </div>
            </div>

            {/* Incident Details Box */}
            <div className="rounded-2xl border border-slate-200 divide-y divide-slate-100 text-xs">
              <div className="p-3 bg-slate-50/50 flex justify-between">
                <span className="text-slate-500">Mã booking / Biển số:</span>
                <span className="font-bold text-slate-800">{selectedTicket.identifier}</span>
              </div>
              <div className="p-3 flex justify-between">
                <span className="text-slate-500">Cán bộ phụ trách:</span>
                <span className="font-semibold text-slate-800">{selectedTicket.assignedTo}</span>
              </div>
              <div className="p-3 flex flex-col gap-1">
                <span className="text-slate-500">Nội dung yêu cầu ban đầu:</span>
                <p className="text-slate-800 bg-slate-50 p-2.5 rounded-xl">{selectedTicket.content}</p>
              </div>
            </div>

            {/* Resolution Note If Resolved */}
            {selectedTicket.resolutionNote && (
              <div className="p-4 rounded-2xl bg-emerald-50 border border-emerald-200 flex flex-col gap-1.5 text-xs text-emerald-900">
                <span className="font-bold flex items-center gap-1.5 text-emerald-800">
                  <span className="material-symbols-outlined text-[16px]">verified</span>
                  Phản hồi kết quả giải quyết từ CSKH:
                </span>
                <p className="leading-relaxed">{selectedTicket.resolutionNote}</p>
              </div>
            )}

            <button
              type="button"
              onClick={() => setSelectedTicket(null)}
              className="w-full py-2.5 rounded-xl bg-slate-900 text-white font-bold text-xs hover:bg-slate-800 transition-colors"
            >
              Đóng chi tiết phiếu
            </button>
          </div>
        </div>
      )}

      {/* 4. Live Chat Drawer */}
      {isChatOpen && (
        <div className="fixed inset-0 z-50 flex justify-end bg-slate-900/40 backdrop-blur-xs animate-in fade-in">
          <div className="w-full max-w-md bg-white h-full shadow-2xl flex flex-col justify-between animate-in slide-in-from-right duration-200">
            <div className="p-4 bg-blue-600 text-white flex items-center justify-between">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-full bg-white/20 flex items-center justify-center font-bold">
                  🤖
                </div>
                <div>
                  <h3 className="font-bold text-sm">ParkMaster AI & Live Support</h3>
                  <div className="flex items-center gap-1.5 text-[11px] text-emerald-200">
                    <span className="w-2 h-2 rounded-full bg-emerald-400 animate-pulse"></span>
                    <span>Đang trực tuyến 24/7</span>
                  </div>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setIsChatOpen(false)}
                className="p-1 rounded-lg hover:bg-white/10 text-white transition-colors cursor-pointer"
              >
                <span className="material-symbols-outlined text-[22px]">close</span>
              </button>
            </div>

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

            <div className="p-3 bg-white border-t border-slate-200 flex flex-col gap-2">
              {/* Quick Suggestion Chips */}
              <div className="flex items-center gap-1.5 overflow-x-auto pb-1 text-[11px]">
                {[
                  '❓ Barie không mở',
                  '⏱ Chính sách hoàn tiền',
                  '⚡ Sạc xe điện EV',
                  '💰 Bảng giá đỗ xe',
                  '📞 Hotline CSKH',
                ].map((chip, idx) => (
                  <button
                    key={idx}
                    type="button"
                    onClick={() => handleSendChatMessage(undefined, chip)}
                    className="px-2.5 py-1 rounded-full bg-slate-100 hover:bg-blue-50 hover:text-blue-600 text-slate-600 whitespace-nowrap transition-colors cursor-pointer shrink-0 border border-slate-200/60"
                  >
                    {chip}
                  </button>
                ))}
              </div>

              <form onSubmit={handleSendChatMessage} className="flex items-center gap-2">
                <input
                  type="text"
                  value={inputChat}
                  onChange={(e) => setInputChat(e.target.value)}
                  placeholder="Nhập câu hỏi bất kỳ (VD: giá đỗ, sạc xe, barie...)"
                  className="flex-1 h-10 px-3 rounded-xl bg-slate-100 border border-slate-200 text-xs text-slate-800 focus:outline-hidden focus:bg-white focus:ring-2 focus:ring-blue-500/20"
                />
                <button
                  type="submit"
                  className="w-10 h-10 rounded-xl bg-blue-600 hover:bg-blue-700 text-white flex items-center justify-center shrink-0 shadow-xs cursor-pointer"
                >
                  <span className="material-symbols-outlined text-[18px]">send</span>
                </button>
              </form>
            </div>
          </div>
        </div>
      )}

      {/* 5. Notification Toast */}
      {toastMessage && (
        <div className="fixed bottom-6 right-6 z-50 animate-in fade-in slide-in-from-bottom-5">
          <div className="bg-slate-900 text-white px-5 py-4 rounded-2xl shadow-2xl flex items-center gap-3.5 border border-slate-700 max-w-md">
            <div className="w-8 h-8 rounded-full bg-emerald-500/20 text-emerald-400 flex items-center justify-center shrink-0">
              <span className="material-symbols-outlined text-[20px]">check_circle</span>
            </div>
            <div className="flex flex-col">
              <p className="font-bold text-xs text-white">Thành công!</p>
              <p className="text-[11px] text-slate-300 mt-0.5">{toastMessage}</p>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
