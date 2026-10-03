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
                NotFoundException   => (StatusCodes.Status404NotFound, "Không tìm thấy"),
                ConflictException   => (StatusCodes.Status409Conflict, "Xung đột nghiệp vụ"),
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
                Detail = status == 500 ? "Đã có lỗi xảy ra, vui lòng thử lại." : ex.Message,
                Instance = context.Request.Path
            };
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(problem);
        }
    }
}
