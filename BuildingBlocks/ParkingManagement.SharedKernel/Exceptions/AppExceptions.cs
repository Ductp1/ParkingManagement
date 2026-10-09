namespace ParkingManagement.SharedKernel.Exceptions;

/// <summary>Không tìm thấy tài nguyên → API trả 404.</summary>
public sealed class NotFoundException(string resource, object key)
    : Exception($"{resource} với Id = '{key}' không tồn tại hoặc chưa được công khai.");

/// <summary>Dữ liệu đầu vào không hợp lệ → API trả 400.</summary>
public sealed class ValidationException(string message) : Exception(message);

/// <summary>Thông tin xác thực hoặc phiên đăng nhập không hợp lệ → API trả 401.</summary>
public sealed class AuthenticationException(string message) : Exception(message);
public sealed class ForbiddenException(string message) : Exception(message);
public sealed class DependencyUnavailableException(string message) : Exception(message);

/// <summary>Vi phạm quy tắc nghiệp vụ / xung đột trạng thái (slot đã bị giữ, booking đã hủy...) → API trả 409.</summary>
public sealed class ConflictException(string message) : Exception(message);
