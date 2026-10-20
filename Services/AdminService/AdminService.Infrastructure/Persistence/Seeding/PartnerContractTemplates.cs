namespace AdminService.Infrastructure.Persistence.Seeding;

/// <summary>
/// US-116 (dev): mẫu hợp đồng hợp tác khai thác MỘT bãi đỗ, theo bản mẫu "Hop dong mau HD-SP-2026-052 v2.pdf"
/// (12 điều, Phụ lục 01 = bãi đỗ của hợp đồng, Phụ lục 02 = tài khoản nhận thanh toán). Thông tin Bên A là giả định;
/// điều khoản là bản tham khảo – pháp chế phải rà soát trước khi dùng thật.
/// Cú pháp dòng xem <see cref="Documents.HtmlContractRenderer"/>; placeholder lấy từ LotContractSupport.Values.
/// </summary>
public static class PartnerContractTemplates
{
    public const int V3Version = 3;
    public const string V3Name = "Hợp đồng hợp tác khai thác bãi đỗ xe – theo từng bãi (mẫu tham khảo v3)";

    public const string V3Body = """
        !! CÔNG TY CỔ PHẦN CÔNG NGHỆ SMART PARKING !! CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM
        !! (tên giả định – bản mẫu) !! **Độc lập – Tự do – Hạnh phúc**
        !! Số: {{ContractNumber}} !! TP. Hồ Chí Minh, {{IssuedDateLong}}

        # Hợp đồng hợp tác khai thác bãi đỗ xe
        ^ **TRÊN NỀN TẢNG SMART PARKING MARKETPLACE**
        ^ ({{VersionNote}})

        ~ – Căn cứ Bộ luật Dân sự số 91/2015/QH13 ngày 24/11/2015;
        ~ – Căn cứ Luật Thương mại số 36/2005/QH11 ngày 14/6/2005 và các văn bản sửa đổi, bổ sung;
        ~ – Căn cứ Luật Giao dịch điện tử số 20/2023/QH15 ngày 22/6/2023;
        ~ – Căn cứ các quy định pháp luật hiện hành về thương mại điện tử và bảo vệ dữ liệu cá nhân;
        ~ – Căn cứ nhu cầu và khả năng của hai bên.
        Hôm nay, {{IssuedDateLong}}, tại TP. Hồ Chí Minh, chúng tôi gồm:

        ## BÊN A: CÔNG TY CỔ PHẦN CÔNG NGHỆ SMART PARKING (tên giả định)
        || Địa chỉ trụ sở: || Số 01 đường Mẫu, Phường Mẫu, TP. Hồ Chí Minh
        || Mã số doanh nghiệp: || 0123456789 (mẫu)
        || Điện thoại / Email: || 1900 0000 · partner@smartparking.example
        || Người đại diện: || Ông **NGUYỄN VĂN MẪU** – Chức vụ: Tổng Giám đốc
        ~ (Sau đây gọi là “Bên A” hoặc “Nền tảng”)

        ## BÊN B: {{BusinessName}}
        || Địa chỉ trụ sở: || {{BusinessAddress}}
        || Mã số doanh nghiệp: || {{TaxCode}}
        || Điện thoại / Email: || {{Phone}} · {{Email}}
        || Người đại diện: || **{{LegalRepresentativeName}}** – Chức vụ: {{LegalRepresentativePosition}}
        || Tài khoản nhận thanh toán: || Chi tiết tại Phụ lục 02
        ~ (Sau đây gọi là “Bên B” hoặc “Chủ bãi”)

        Mã hồ sơ đăng ký hợp tác: **{{ApplicationCode}}**. Hai bên thống nhất ký kết Hợp đồng hợp tác số {{ContractNumber}} (sau đây gọi là “Hợp đồng”) để khai thác bãi đỗ xe **{{LotName}}** trên Nền tảng, với các điều khoản sau:

        ## ĐIỀU 1. GIẢI THÍCH TỪ NGỮ
        1.1. **“Nền tảng”**: hệ thống Smart Parking Marketplace gồm ứng dụng web/di động dành cho Tài xế, cổng quản trị Chủ bãi, cổng Nhân viên và các giao diện lập trình (API) do Bên A vận hành.
        1.2. **“Bãi đỗ xe”**: địa điểm do Bên B sở hữu hoặc có quyền khai thác hợp pháp, nêu tại Phụ lục 01, đã được Bên A phê duyệt hồ sơ thẩm định (KYB).
        1.3. **“Lượt gửi xe”**: một giao dịch đặt chỗ (booking) hoặc gửi xe vãng lai được ghi nhận check-in/check-out trên Nền tảng.
        1.4. **“Doanh thu gộp (GMV)”**: tổng số tiền Tài xế thanh toán cho các Lượt gửi xe tại Bãi đỗ xe trong kỳ đối soát, trước khi trừ hoàn tiền và phí nền tảng.
        1.5. **“Phí nền tảng”**: khoản phí Bên B trả cho Bên A, tính bằng tỷ lệ phần trăm trên GMV theo Điều 4.
        1.6. **“Kỳ đối soát”**: chu kỳ tổng hợp giao dịch và quyết toán giữa hai bên theo Điều 4.3.

        ## ĐIỀU 2. PHẠM VI HỢP TÁC
        2.1. Bên A cung cấp cho Bên B quyền niêm yết Bãi đỗ xe, nhận đặt chỗ, thu tiền hộ qua các cổng thanh toán (VNPAY, MoMo, VietQR) và sử dụng các công cụ quản lý vận hành trên Nền tảng.
        2.2. Bên B cung cấp dịch vụ trông giữ phương tiện tại Bãi đỗ xe cho Tài xế đặt chỗ qua Nền tảng theo đúng thông tin, giá và điều kiện đã niêm yết.
        2.3. Bãi đỗ xe đã được Bên A phê duyệt hồ sơ thẩm định gồm: giấy tờ pháp lý, ảnh hiện trường và tọa độ GPS, giấy chứng nhận đủ điều kiện về phòng cháy chữa cháy, và khảo sát thực địa (nếu có yêu cầu). Bãi đỗ xe được hiển thị công khai và nhận đặt chỗ kể từ khi Hợp đồng này được ký.
        2.4. Mỗi bãi đỗ xe khác của Bên B được thẩm định và ký hợp đồng riêng; Hợp đồng này chỉ áp dụng cho Bãi đỗ xe tại Phụ lục 01.
        2.5. Hợp đồng này không tạo ra quan hệ lao động, liên doanh hay đại lý độc quyền giữa hai bên.

        ## ĐIỀU 3. THỜI HẠN HỢP ĐỒNG
        3.1. Hợp đồng có hiệu lực **{{TermMonthsText}} tháng**, kể từ ngày **{{EffectiveFrom}}** đến hết ngày **{{EffectiveTo}}**.
        3.2. Trước ngày hết hạn ít nhất 30 (ba mươi) ngày, nếu không bên nào thông báo bằng văn bản về việc không gia hạn, Hợp đồng được tự động gia hạn thêm {{TermMonths}} tháng với các điều khoản không đổi.

        ## ĐIỀU 4. PHÍ NỀN TẢNG, ĐỐI SOÁT VÀ THANH TOÁN
        4.1. Phí nền tảng: **{{CommissionRate}}%** trên GMV của mỗi Kỳ đối soát. Mức phí này chưa bao gồm thuế giá trị gia tăng (GTGT) theo quy định.
        4.2. Số tiền Bên B được nhận trong mỗi kỳ được xác định như sau:
        ^ **Số tiền quyết toán = GMV − Phí nền tảng − Tiền hoàn cho Tài xế − Khoản tạm giữ + Khoản điều chỉnh**
        4.3. Kỳ đối soát: **hằng tuần**, từ 00:00 thứ Hai đến 23:59 Chủ nhật (giờ Việt Nam). Bên A gửi bảng đối soát trên cổng Chủ bãi chậm nhất vào thứ Ba của tuần kế tiếp.
        4.4. Bên B có 02 (hai) ngày làm việc để phản hồi sai lệch. Quá thời hạn này, bảng đối soát được coi là đã được Bên B chấp thuận.
        4.5. Bên A chuyển khoản số tiền quyết toán vào tài khoản tại Phụ lục 02 trong vòng 03 (ba) ngày làm việc kể từ ngày bảng đối soát được chấp thuận. Phí chuyển khoản do Bên A chịu.
        4.6. **Khoản tạm giữ**: Bên A được tạm giữ phần doanh thu liên quan đến khiếu nại/tranh chấp chưa giải quyết. Khoản tạm giữ được hoàn trả hoặc khấu trừ ngay trong Kỳ đối soát kế tiếp sau khi có kết quả giải quyết.
        4.7. Hóa đơn: Bên A xuất hóa đơn điện tử cho Phí nền tảng; Bên B chịu trách nhiệm xuất hóa đơn cho dịch vụ trông giữ phương tiện theo quy định về thuế.
        4.8. Thay đổi mức phí nền tảng chỉ có hiệu lực khi hai bên ký phụ lục hoặc phiên bản hợp đồng mới.

        ## ĐIỀU 5. QUYỀN VÀ NGHĨA VỤ CỦA BÊN A
        5.1. Vận hành Nền tảng ổn định; thông báo trước ít nhất 24 giờ đối với việc bảo trì theo kế hoạch.
        5.2. Thu hộ tiền gửi xe, đối soát và thanh toán cho Bên B đúng hạn theo Điều 4.
        5.3. Cung cấp tài khoản quản trị cho Bên B, hỗ trợ kỹ thuật trong giờ hành chính và kênh hỗ trợ khẩn cấp.
        5.4. Được kiểm tra, yêu cầu bổ sung hồ sơ và thực hiện khảo sát thực địa đối với Bãi đỗ xe.
        5.5. Được tạm ẩn niêm yết, áp dụng chế tài theo Điều 7 khi Bên B vi phạm Hợp đồng hoặc quy chế của Nền tảng.
        5.6. Được thay mặt Bên B hoàn tiền cho Tài xế trong các trường hợp lỗi thuộc về Bãi đỗ xe và khấu trừ vào Kỳ đối soát.

        ## ĐIỀU 6. QUYỀN VÀ NGHĨA VỤ CỦA BÊN B
        6.1. Bảo đảm Bãi đỗ xe có đầy đủ giấy tờ pháp lý, điều kiện an toàn phòng cháy chữa cháy và các giấy phép cần thiết trong suốt thời hạn Hợp đồng; thông báo cho Bên A trong vòng 03 ngày khi có thay đổi.
        6.2. Niêm yết đúng giá, giờ hoạt động, chiều cao tối đa, sức chứa và tiện ích; không thu thêm bất kỳ khoản phí nào ngoài giá đã niêm yết.
        6.3. Phục vụ Tài xế đã đặt chỗ hợp lệ; cập nhật trạng thái chỗ đỗ kịp thời; thực hiện check-in/check-out trên Nền tảng.
        6.4. Phản hồi khiếu nại của Tài xế trên Nền tảng trong vòng **48 (bốn mươi tám) giờ** kể từ khi nhận thông báo.
        6.5. Trường hợp Tài xế đã đặt chỗ hợp lệ nhưng không còn chỗ đỗ khi đến nơi, Bên B chịu chi phí hoàn tiền và voucher bồi thường theo quy chế của Nền tảng.
        6.6. Quản lý tài khoản quản trị và tài khoản nhân viên; chịu trách nhiệm về mọi thao tác thực hiện bằng các tài khoản này.
        6.7. Được nhận đầy đủ, đúng hạn số tiền quyết toán; được truy cập báo cáo doanh thu, lịch sử giao dịch trên cổng Chủ bãi.

        ## ĐIỀU 7. TIÊU CHUẨN DỊCH VỤ VÀ CHẾ TÀI
        7.1. Khi Bên B vi phạm tiêu chuẩn dịch vụ (tỷ lệ từ chối đặt chỗ, khiếu nại có căn cứ, sai lệch giá, mất kết nối thiết bị kéo dài…), Bên A áp dụng chế tài theo 04 cấp:
        #| Cấp | Hình thức | Nội dung
        | 1 | Cảnh cáo | Thông báo bằng văn bản, yêu cầu khắc phục trong thời hạn ấn định.
        | 2 | Giảm thứ hạng hiển thị | Hạ thứ hạng Bãi đỗ xe trong kết quả tìm kiếm tối đa 30 ngày.
        | 3 | Tạm ngừng niêm yết | Ẩn Bãi đỗ xe, ngừng nhận đặt chỗ mới; booking đã xác nhận vẫn phải được phục vụ.
        | 4 | Chấm dứt hợp tác | Khóa vĩnh viễn tài khoản Chủ bãi và đơn phương chấm dứt Hợp đồng theo Điều 10.
        7.2. Ngoài chế tài trên, bên vi phạm phải chịu phạt vi phạm tối đa 8% giá trị phần nghĩa vụ bị vi phạm theo Điều 301 Luật Thương mại và bồi thường thiệt hại thực tế phát sinh.
        7.3. Bên B có quyền khiếu nại quyết định chế tài trong vòng 05 ngày làm việc kể từ ngày nhận thông báo.

        ## ĐIỀU 8. BẢO MẬT VÀ BẢO VỆ DỮ LIỆU CÁ NHÂN
        8.1. Mỗi bên có nghĩa vụ giữ bí mật thông tin kinh doanh, kỹ thuật và giá của bên kia trong thời hạn Hợp đồng và 02 năm sau khi chấm dứt.
        8.2. Dữ liệu cá nhân của Tài xế (họ tên, số điện thoại, biển số xe, hình ảnh tại cổng) chỉ được sử dụng để thực hiện dịch vụ trông giữ; Bên B không được sao chép, chuyển giao cho bên thứ ba hay sử dụng cho mục đích tiếp thị khi chưa có sự đồng ý của chủ thể dữ liệu.
        8.3. Hai bên tuân thủ quy định pháp luật về bảo vệ dữ liệu cá nhân; thông báo cho nhau trong vòng 24 giờ khi phát hiện sự cố lộ, mất dữ liệu.

        ## ĐIỀU 9. BẤT KHẢ KHÁNG
        9.1. Sự kiện bất khả kháng là sự kiện xảy ra khách quan, không thể lường trước và không thể khắc phục được mặc dù đã áp dụng mọi biện pháp cần thiết, bao gồm thiên tai, hỏa hoạn, dịch bệnh, chiến tranh, quyết định của cơ quan nhà nước có thẩm quyền.
        9.2. Bên gặp sự kiện bất khả kháng phải thông báo cho bên kia trong vòng 03 ngày và được miễn trách nhiệm đối với nghĩa vụ bị ảnh hưởng trong thời gian xảy ra sự kiện. Đóng bãi khẩn cấp phải được cập nhật ngay trên Nền tảng để thông báo cho Tài xế đã đặt chỗ.

        ## ĐIỀU 10. TẠM NGỪNG, CHẤM DỨT HỢP ĐỒNG
        10.1. Hợp đồng chấm dứt khi: (a) hết thời hạn mà không gia hạn; (b) hai bên thỏa thuận chấm dứt; (c) một bên đơn phương chấm dứt theo Điều 10.2 hoặc 10.3.
        10.2. Bên B có quyền xin đưa Bãi đỗ xe rời Nền tảng bằng thông báo trên cổng Chủ bãi hoặc văn bản gửi trước ít nhất **30 (ba mươi) ngày**. Trong thời gian báo trước, Bên B tiếp tục phục vụ các booking đã xác nhận.
        10.3. Bên A được đơn phương chấm dứt khi Bên B bị áp dụng chế tài cấp 4, cung cấp hồ sơ giả mạo, hoặc Bãi đỗ xe bị cơ quan có thẩm quyền đình chỉ hoạt động.
        10.4. Khi chấm dứt, hai bên hoàn tất đối soát và thanh toán các khoản còn lại trong vòng 15 ngày làm việc.

        ## ĐIỀU 11. GIẢI QUYẾT TRANH CHẤP
        11.1. Tranh chấp phát sinh được ưu tiên giải quyết bằng thương lượng, hòa giải trong vòng 30 ngày.
        11.2. Nếu không thương lượng được, một trong hai bên có quyền đưa tranh chấp ra Tòa án nhân dân có thẩm quyền tại TP. Hồ Chí Minh để giải quyết theo pháp luật Việt Nam.

        ## ĐIỀU 12. ĐIỀU KHOẢN CHUNG
        12.1. Các phụ lục là bộ phận không tách rời của Hợp đồng. Mọi sửa đổi, bổ sung phải lập thành văn bản hoặc phiên bản hợp đồng mới có chữ ký của đại diện hợp pháp hai bên.
        12.2. Phiên bản {{Version}} này thay thế toàn bộ phiên bản trước đó (nếu có) của Hợp đồng số {{ContractNumber}}.
        12.3. Hai bên đồng ý giao dịch, thông báo qua email đã đăng ký và cổng Chủ bãi có giá trị như văn bản theo Luật Giao dịch điện tử.
        12.4. Hợp đồng được lập thành 02 (hai) bản bằng tiếng Việt có giá trị pháp lý như nhau, mỗi bên giữ 01 bản, có hiệu lực kể từ ngày ký. Bản điện tử trên Nền tảng có cùng nội dung và mã SHA-256 được dùng để đối chiếu.

        !! ĐẠI DIỆN BÊN A !! ĐẠI DIỆN BÊN B
        !! (Ký, ghi rõ họ tên, đóng dấu) !! (Ký, ghi rõ họ tên, đóng dấu)
        !! !!
        !! **TỔNG GIÁM ĐỐC** !! **{{LegalRepresentativePosition}}**
        !! **Nguyễn Văn Mẫu** !! **{{LegalRepresentativeName}}**

        ---
        # Phụ lục 01
        ^ **BÃI ĐỖ XE HỢP TÁC**
        ^ (Kèm theo Hợp đồng số {{ContractNumber}} – Phiên bản {{Version}})
        #| Tên bãi | Địa chỉ | Sức chứa | Giờ hoạt động | Trạng thái thẩm định
        | {{LotName}} | {{LotAddress}} | {{LotCapacity}} | {{LotOpeningHours}} | Đã phê duyệt KYB
        Bãi đỗ xe được niêm yết công khai và nhận đặt chỗ kể từ khi Hợp đồng được ký. Thông tin chi tiết (bảng giá, sơ đồ chỗ đỗ, tiện ích) do Bên B cập nhật trên cổng Chủ bãi và là một phần của Phụ lục này.

        # Phụ lục 02
        ^ **THÔNG TIN TÀI KHOẢN NHẬN THANH TOÁN**
        ^ (Kèm theo Hợp đồng số {{ContractNumber}} – Phiên bản {{Version}})
        | **Tên chủ tài khoản** | {{BankAccountName}}
        | **Số tài khoản** | {{BankAccountNumber}}
        | **Ngân hàng** | {{BankName}} – BIN {{BankBin}}
        | **Hình thức nhận** | Chuyển khoản, chu kỳ hằng tuần theo Điều 4
        Bên B chịu trách nhiệm về tính chính xác của thông tin tài khoản. Thay đổi tài khoản nhận thanh toán phải được xác nhận bằng văn bản có chữ ký và con dấu của Bên B; Bên A áp dụng kể từ Kỳ đối soát kế tiếp.

        ~ Ghi chú: thông tin Bên A trong mẫu là giả định; nội dung điều khoản là bản tham khảo, cần bộ phận pháp chế rà soát trước khi sử dụng chính thức.
        """;
}
