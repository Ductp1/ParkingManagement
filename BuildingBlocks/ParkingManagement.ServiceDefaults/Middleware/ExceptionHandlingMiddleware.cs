using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Exceptions;

namespace ParkingManagement.ServiceDefaults.Middleware;

/// <summary>
/// Bắt exception từ các lớp bên dưới và chuyển thành HTTP ProblemDetails (RFC 7807).
/// Nhờ vậy Controller không cần try/catch. Dùng chung cho cả 9 service.
/// </summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var (status, title) = ex switch
            {
                ValidationException => (StatusCodes.Status400BadRequest, "Dữ liệu không hợp lệ"),
                AuthenticationException => (StatusCodes.Status401Unauthorized, "Xác thực không hợp lệ"),
                ForbiddenException => (StatusCodes.Status403Forbidden, "Không có quyền"),
                DependencyUnavailableException => (StatusCodes.Status503ServiceUnavailable, "Dịch vụ chưa sẵn sàng"),
                NotFoundException   => (StatusCodes.Status404NotFound, "Không tìm thấy"),
                ConflictException   => (StatusCodes.Status409Conflict, "Xung đột nghiệp vụ"),
                Microsoft.EntityFrameworkCore.DbUpdateException { InnerException: Npgsql.PostgresException { SqlState: "23505" } }
                                    => (StatusCodes.Status409Conflict, "Dữ liệu đã tồn tại"),
                _                   => (StatusCodes.Status500InternalServerError, "Lỗi hệ thống")
            };

            if (status == StatusCodes.Status500InternalServerError)
                logger.LogError(ex, "Lỗi không mong đợi");
            else
                logger.LogWarning("Trả {Status}: {Message}", status, ex.Message);

            var problem = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = status == 500 ? "Đã có lỗi xảy ra, vui lòng thử lại."
                    : ex is Microsoft.EntityFrameworkCore.DbUpdateException ? "Định danh hoặc dữ liệu đã được sử dụng." : ex.Message,
                Instance = context.Request.Path
            };
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(problem);
        }
    }
}
