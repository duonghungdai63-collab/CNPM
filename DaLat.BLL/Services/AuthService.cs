using DaLat.BLL.Common;
using DaLat.DAL.Repositories;

namespace DaLat.BLL.Services;

public class AuthService(IAdminUserRepository users, IJwtTokenIssuer issuer)
{
    // Băm giả để thời gian phản hồi không lộ việc tên đăng nhập có tồn tại hay không.
    private static readonly string DummyHash = PasswordHasher.Hash("dummy-password-for-timing");

    public async Task<LoginResponse> LoginAsync(LoginRequest r)
    {
        var user = await users.FindByUsernameAsync((r.Username ?? "").Trim());
        var valid = PasswordHasher.Verify(r.Password ?? "", user?.PasswordHash ?? DummyHash);
        if (user == null || !user.IsActive || !valid)
            throw new AppException("Sai tên đăng nhập hoặc mật khẩu.", 401);

        var (token, expires) = issuer.Issue(user.Id, user.Username, user.DisplayName);
        return new LoginResponse(token, expires, user.DisplayName);
    }

    /// <summary>Đổi mật khẩu: cần mật khẩu hiện tại, mật khẩu mới tối thiểu 8 ký tự (BR8).</summary>
    public async Task ChangePasswordAsync(int userId, ChangePasswordRequest r)
    {
        var user = await users.FindByIdAsync(userId) ?? throw new NotFoundException("Không tìm thấy tài khoản.");
        if (!PasswordHasher.Verify(r.CurrentPassword ?? "", user.PasswordHash))
            throw new AppException("Mật khẩu hiện tại không đúng.");
        if ((r.NewPassword ?? "").Length < 8)
            throw new AppException("Mật khẩu mới phải có ít nhất 8 ký tự.");
        user.PasswordHash = PasswordHasher.Hash(r.NewPassword!);
        await users.SaveAsync();
    }
}
