namespace DaLat.BLL.Common;

/// <summary>Lỗi nghiệp vụ có mã HTTP; middleware API chuyển thành JSON { message }.</summary>
public class AppException(string message, int statusCode = 400) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public class NotFoundException(string message) : AppException(message, 404);
public class ConflictException(string message) : AppException(message, 409);
