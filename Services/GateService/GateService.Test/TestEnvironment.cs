using System.Runtime.CompilerServices;
using GateService.Application.Features.GateConsole;

namespace GateService.Test;

// BookingQrTokenValidator đọc secret từ biến môi trường QRTOKEN_SECRET (giống production).
// Đặt giá trị test ngay khi nạp assembly để mọi test KHÔNG phụ thuộc biến môi trường trên máy chạy test/CI.
internal static class TestEnvironment
{
    [ModuleInitializer]
    internal static void Initialize()
        => Environment.SetEnvironmentVariable(BookingQrTokenValidator.SecretEnvVar, TestQrTokenFactory.BookingServiceSecret);
}
