namespace AdminService.Infrastructure.Persistence.Seeding;

/// <summary>
/// US-116 (dev): nội dung mẫu hợp đồng hợp tác đối tác bãi đỗ. BẢN MẪU THAM KHẢO do dev soạn để xem trước / kiểm thử –
/// pháp chế phải rà soát trước khi dùng thật (thông tin Bên A, tỷ lệ, thời hạn đối soát là giá trị đề xuất).
/// Cú pháp dòng xem <see cref="Documents.HtmlContractRenderer"/>; placeholder lấy từ PartnerContractSupport.Values.
/// </summary>
public static class PartnerContractTemplates
{
    public const int V2Version = 2;
    public const string V2Name = "Hợp đồng hợp tác kinh doanh dịch vụ đỗ xe (mẫu tham khảo v2)";

    public const string V2Body = """
        ^ CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM
        ^ Độc lập – Tự do – Hạnh phúc
        ^ ———————————

        # Hợp đồng hợp tác kinh doanh dịch vụ đỗ xe
        ^ Số: {{ContractNumber}} · Phiên bản: {{Version}}
        ^ (Bản mẫu tham khảo – cần pháp chế rà soát trước khi sử dụng)

        Căn cứ:
        - Bộ luật Dân sự số 91/2015/QH13;
        - Luật Thương mại số 36/2005/QH11;
        - Luật Giao dịch điện tử số 20/2023/QH15;
        - Nghị định số 13/2023/NĐ-CP về bảo vệ dữ liệu cá nhân;
        - Hồ sơ đăng ký đối tác số {{ApplicationCode}} và nhu cầu, khả năng của hai bên.

        Hôm nay, ngày {{IssuedDate}}, chúng tôi gồm:

        ## BÊN A: ĐƠN VỊ VẬN HÀNH NỀN TẢNG SMART PARKING
        || Tên doanh nghiệp: || [Tên pháp nhân vận hành nền tảng – chờ xác nhận]
        || Mã số thuế: || [Mã số thuế Bên A]
        || Địa chỉ trụ sở: || [Địa chỉ Bên A]
        || Người đại diện: || [Họ tên] – Chức vụ: [Chức vụ]
        || Email liên hệ: || partner@smartparking.vn

        ## BÊN B: ĐỐI TÁC CHỦ BÃI ĐỖ XE
        || Tên doanh nghiệp: || {{BusinessName}}
        || Mã số thuế: || {{TaxCode}}
        || Địa chỉ trụ sở: || {{BusinessAddress}}
        || Người đại diện: || {{LegalRepresentativeName}} – Chức vụ: {{LegalRepresentativePosition}}
        || Người liên hệ: || {{ContactName}} – Điện thoại: {{Phone}} – Email: {{Email}}
        || Tài khoản nhận tiền: || {{BankAccountNumber}} – {{BankAccountName}} – {{BankName}} (BIN {{BankBin}})

        Hai bên thống nhất ký kết hợp đồng hợp tác (sau đây gọi là "Hợp đồng") với các điều khoản sau:

        ## Điều 1. Định nghĩa
        - "Nền tảng": hệ thống Smart Parking (website, ứng dụng di động, API) do Bên A vận hành, cho phép tài xế tìm, đặt chỗ và thanh toán phí đỗ xe.
        - "Bãi đỗ": địa điểm đỗ xe do Bên B quản lý hợp pháp và được đưa lên Nền tảng sau khi được Bên A duyệt hồ sơ xác minh (KYB).
        - "Tài xế": người sử dụng Nền tảng để đặt chỗ và sử dụng dịch vụ đỗ xe tại Bãi đỗ.
        - "Giao dịch hoàn tất": lượt đặt chỗ đã được thanh toán qua Nền tảng và tài xế đã ra khỏi bãi (hoặc hết thời gian đặt chỗ mà không hủy hợp lệ).
        - "Doanh thu giao dịch": tổng số tiền tài xế thanh toán qua Nền tảng cho các Giao dịch hoàn tất, đã trừ khoản hoàn tiền.
        - "Kỳ đối soát": khoảng thời gian từ ngày 01 đến ngày cuối cùng của mỗi tháng dương lịch.

        ## Điều 2. Phạm vi hợp tác
        - Bên B đưa các Bãi đỗ thuộc quyền quản lý của mình lên Nền tảng để cung cấp dịch vụ đỗ xe cho Tài xế; Bên A cung cấp Nền tảng, kênh thanh toán và hỗ trợ khách hàng.
        - Quy mô dự kiến: {{LotCount}} bãi đỗ, địa chỉ bãi chính: {{PrimaryLotAddress}}.
        - Mỗi Bãi đỗ chỉ được hiển thị và nhận đặt chỗ sau khi hồ sơ KYB của bãi (giấy phép kinh doanh, ảnh hiện trường và tọa độ, chứng nhận PCCC, khảo sát thực địa khi cần) được Bên A phê duyệt.
        - Thông tin chi tiết từng Bãi đỗ (sức chứa, giờ hoạt động, bảng giá) do Bên B khai báo trên Nền tảng và là một phần của Hợp đồng.

        ## Điều 3. Thời hạn hợp đồng
        - Hợp đồng có hiệu lực từ ngày {{EffectiveFrom}} đến hết ngày {{EffectiveTo}} ({{TermMonths}} tháng).
        - Trước khi hết hạn ít nhất 30 ngày, hai bên trao đổi việc gia hạn. Việc gia hạn hoặc thay đổi điều khoản được thực hiện bằng một phiên bản Hợp đồng mới có cùng số hợp đồng.
        - Nếu hết hạn mà chưa ký gia hạn, các Bãi đỗ của Bên B tự động ngừng nhận đặt chỗ mới; các lượt đặt chỗ đã xác nhận vẫn phải được phục vụ.

        ## Điều 4. Phí dịch vụ nền tảng (hoa hồng)
        - Bên B trả cho Bên A phí dịch vụ bằng {{CommissionRate}}% Doanh thu giao dịch của mỗi Kỳ đối soát.
        - Mức phí trên chưa bao gồm thuế giá trị gia tăng (VAT) theo quy định. Phí cổng thanh toán do Bên A chịu.
        - Bên A không thu thêm bất kỳ khoản phí nào khác ngoài phí quy định tại Điều này nếu không có thỏa thuận bằng văn bản.
        - Mọi thay đổi tỷ lệ phí chỉ có hiệu lực khi hai bên ký phiên bản Hợp đồng mới.

        ## Điều 5. Thanh toán và đối soát
        - Bên A thu hộ toàn bộ tiền đặt chỗ từ Tài xế qua Nền tảng.
        - Trong vòng 05 ngày làm việc sau khi kết thúc Kỳ đối soát, Bên A gửi Bảng đối soát cho Bên B (số giao dịch, Doanh thu giao dịch, khoản hoàn tiền, phí dịch vụ, số tiền thanh toán).
        - Bên B có 07 ngày làm việc để xác nhận hoặc khiếu nại Bảng đối soát. Quá thời hạn này mà không có phản hồi, Bảng đối soát được coi là đã xác nhận.
        - Trong vòng 07 ngày làm việc sau khi Bảng đối soát được xác nhận, Bên A chuyển số tiền thanh toán (Doanh thu giao dịch trừ phí dịch vụ) vào tài khoản của Bên B nêu tại phần thông tin các bên.
        - Bên A xuất hóa đơn cho phí dịch vụ; Bên B tự chịu trách nhiệm kê khai thuế với phần doanh thu của mình.
        - Bên B thông báo bằng văn bản ít nhất 10 ngày trước khi thay đổi tài khoản nhận tiền.

        ## Điều 6. Quyền và nghĩa vụ của Bên A
        ### 6.1. Quyền của Bên A
        - Kiểm tra, xác minh hồ sơ và yêu cầu Bên B bổ sung thông tin về Bãi đỗ trước và trong thời gian hợp tác.
        - Tạm ẩn Bãi đỗ hoặc tạm khóa tài khoản Bên B khi phát hiện vi phạm theo Điều 12.
        - Khấu trừ phí dịch vụ và các khoản hoàn tiền hợp lệ trước khi thanh toán cho Bên B.
        ### 6.2. Nghĩa vụ của Bên A
        - Duy trì Nền tảng hoạt động ổn định (mục tiêu sẵn sàng 99% mỗi tháng, trừ thời gian bảo trì đã báo trước).
        - Cung cấp cho Bên B công cụ quản lý bãi, theo dõi đặt chỗ và báo cáo doanh thu.
        - Thanh toán đúng hạn theo Điều 5.
        - Hỗ trợ Tài xế và phối hợp với Bên B xử lý khiếu nại liên quan đến đặt chỗ, thanh toán.

        ## Điều 7. Quyền và nghĩa vụ của Bên B
        ### 7.1. Quyền của Bên B
        - Tự quyết định bảng giá, giờ hoạt động và số chỗ mở bán trên Nền tảng trong giới hạn sức chứa đã khai báo.
        - Nhận thanh toán đầy đủ, đúng hạn theo Điều 5 và được cung cấp số liệu đối soát chi tiết.
        ### 7.2. Nghĩa vụ của Bên B
        - Có đầy đủ giấy phép kinh doanh, quyền sử dụng mặt bằng và chứng nhận phòng cháy chữa cháy còn hiệu lực cho từng Bãi đỗ; cung cấp bản sao khi Bên A yêu cầu.
        - Cập nhật chính xác, kịp thời tình trạng chỗ trống, giờ hoạt động, giá và các thay đổi của Bãi đỗ trên Nền tảng.
        - Phục vụ đầy đủ mọi lượt đặt chỗ đã được xác nhận; không thu thêm của Tài xế bất kỳ khoản nào ngoài giá đã niêm yết trên Nền tảng.
        - Bảo đảm an ninh, trật tự, vệ sinh, an toàn phòng cháy tại Bãi đỗ; trông giữ và bảo quản phương tiện theo quy định pháp luật.
        - Duy trì bảo hiểm trách nhiệm đối với phương tiện gửi tại bãi (khuyến nghị).
        - Thông báo cho Bên A trong vòng 24 giờ khi có sự cố ảnh hưởng đến việc phục vụ (đóng cửa đột xuất, mất an ninh, cháy nổ…).

        ## Điều 8. Chất lượng dịch vụ
        - Bên B phản hồi khiếu nại của Tài xế do Bên A chuyển đến trong vòng 24 giờ.
        - Tỷ lệ từ chối phục vụ lượt đặt chỗ đã xác nhận không vượt quá 2% mỗi Kỳ đối soát.
        - Vi phạm chất lượng dịch vụ được xử lý theo chính sách chế tài của Nền tảng (nhắc nhở, tạm ẩn bãi, tạm khóa tài khoản) đã công bố cho đối tác.

        ## Điều 9. Bảo vệ dữ liệu cá nhân
        - Mỗi bên chỉ xử lý dữ liệu cá nhân của Tài xế (họ tên, số điện thoại, biển số xe…) để thực hiện Hợp đồng, theo Nghị định số 13/2023/NĐ-CP.
        - Bên B không sao chép, lưu trữ ngoài mục đích phục vụ đỗ xe, không chia sẻ cho bên thứ ba và không dùng dữ liệu để quảng cáo khi chưa có sự đồng ý của Tài xế.
        - Khi phát hiện sự cố lộ, mất dữ liệu cá nhân, bên phát hiện phải thông báo cho bên kia trong vòng 24 giờ để phối hợp xử lý.

        ## Điều 10. Bảo mật thông tin
        - Hai bên giữ bí mật nội dung Hợp đồng, số liệu doanh thu và thông tin kinh doanh của nhau trong thời hạn Hợp đồng và 02 năm sau khi Hợp đồng chấm dứt, trừ trường hợp phải cung cấp theo yêu cầu của cơ quan nhà nước có thẩm quyền.

        ## Điều 11. Trách nhiệm bồi thường
        - Bên B chịu trách nhiệm bồi thường thiệt hại đối với mất mát, hư hỏng phương tiện xảy ra trong thời gian phương tiện được gửi tại Bãi đỗ, trừ trường hợp do lỗi của chính Tài xế.
        - Bên A chịu trách nhiệm về lỗi của Nền tảng (sai thông tin đặt chỗ, sai số tiền thanh toán) và hoàn tiền cho Tài xế trong các trường hợp đó.
        - Bên vi phạm nghĩa vụ thanh toán phải trả lãi chậm trả theo mức lãi suất quy định tại Điều 357 Bộ luật Dân sự trên số tiền chậm trả.

        ## Điều 12. Tạm ngưng và chấm dứt hợp đồng
        - Bên A được tạm ẩn Bãi đỗ hoặc tạm khóa tài khoản Bên B khi: Bên B cung cấp thông tin sai sự thật; giấy phép hoặc chứng nhận PCCC hết hiệu lực; có khiếu nại nghiêm trọng đã được xác minh; vi phạm Điều 7, Điều 9.
        - Mỗi bên được đơn phương chấm dứt Hợp đồng khi bên kia vi phạm nghiêm trọng và không khắc phục trong 15 ngày kể từ ngày nhận thông báo bằng văn bản.
        - Mỗi bên được chấm dứt Hợp đồng vì lý do khác nếu thông báo bằng văn bản cho bên kia trước ít nhất 30 ngày.
        - Khi Hợp đồng chấm dứt, Bên B vẫn phải phục vụ các lượt đặt chỗ đã xác nhận trước ngày chấm dứt; hai bên thực hiện đối soát cuối cùng trong vòng 30 ngày.

        ## Điều 13. Sự kiện bất khả kháng
        - Bên gặp sự kiện bất khả kháng (thiên tai, dịch bệnh, hỏa hoạn không do lỗi của bên đó, quyết định của cơ quan nhà nước…) được miễn trách nhiệm đối với nghĩa vụ bị ảnh hưởng, với điều kiện thông báo cho bên kia trong vòng 03 ngày và áp dụng mọi biện pháp cần thiết để hạn chế thiệt hại.

        ## Điều 14. Giải quyết tranh chấp
        - Tranh chấp phát sinh được giải quyết trước hết bằng thương lượng, hòa giải trong vòng 30 ngày.
        - Nếu không thương lượng được, một trong hai bên có quyền đưa tranh chấp ra Tòa án nhân dân có thẩm quyền tại nơi Bên A đặt trụ sở để giải quyết theo pháp luật Việt Nam.

        ## Điều 15. Điều khoản chung
        - Phụ lục 01 và các phiên bản sửa đổi được hai bên ký là bộ phận không tách rời của Hợp đồng. Phiên bản có số lớn hơn thay thế phiên bản trước đó.
        - Mọi sửa đổi, bổ sung phải được lập thành văn bản (phiên bản mới) và có chữ ký của đại diện hợp pháp hai bên.
        - Hợp đồng được lập thành 02 bản có giá trị pháp lý như nhau, mỗi bên giữ 01 bản; bản điện tử trên Nền tảng có cùng nội dung và mã SHA-256 được dùng để đối chiếu.

        !! ĐẠI DIỆN BÊN A !! ĐẠI DIỆN BÊN B
        !! (Ký, ghi rõ họ tên, đóng dấu) !! (Ký, ghi rõ họ tên, đóng dấu)
        !! !!
        !! [Họ tên đại diện Bên A] !! {{LegalRepresentativeName}}

        ---
        # Phụ lục 01 – Thông tin hợp tác
        ^ (Kèm theo Hợp đồng số {{ContractNumber}} – phiên bản {{Version}})

        ## 1. Thông tin đối tác
        || Mã hồ sơ đăng ký: || {{ApplicationCode}}
        || Tên doanh nghiệp: || {{BusinessName}}
        || Mã số thuế: || {{TaxCode}}
        || Người liên hệ: || {{ContactName}} – {{Phone}} – {{Email}}

        ## 2. Bãi đỗ dự kiến
        || Số lượng bãi: || {{LotCount}}
        || Địa chỉ bãi chính: || {{PrimaryLotAddress}}
        || Điều kiện hoạt động: || Từng bãi phải được duyệt hồ sơ KYB trước khi nhận đặt chỗ

        ## 3. Điều khoản thương mại
        || Phí dịch vụ nền tảng: || {{CommissionRate}}% Doanh thu giao dịch (chưa VAT)
        || Thời hạn: || {{TermMonths}} tháng, từ {{EffectiveFrom}} đến {{EffectiveTo}}
        || Kỳ đối soát: || Hằng tháng; thanh toán trong 07 ngày làm việc sau khi xác nhận
        || Tài khoản nhận tiền: || {{BankAccountNumber}} – {{BankAccountName}}
        || Ngân hàng: || {{BankName}} (BIN {{BankBin}})

        !! ĐẠI DIỆN BÊN A !! ĐẠI DIỆN BÊN B
        !! !!
        !! [Họ tên đại diện Bên A] !! {{LegalRepresentativeName}}
        """;
}
