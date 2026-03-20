using Acme.Product.Application.Services;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Interfaces;
using FluentAssertions;
using NSubstitute;

namespace Acme.Product.Tests.Services;

public class AuthServiceTests
{
    public AuthServiceTests()
    {
        AuthService.ResetInMemoryStateForTests();
    }

    [Fact]
    public async Task ValidateTokenAsync_ShouldRespectConfiguredSessionTimeout()
    {
        var now = new DateTime(2026, 3, 20, 10, 0, 0, DateTimeKind.Utc);
        var repository = Substitute.For<IUserRepository>();
        var passwordHasher = Substitute.For<IPasswordHasher>();
        var configurationService = CreateConfigurationService(sessionTimeoutMinutes: 1, loginFailureLockoutCount: 3);
        var user = CreateUser("tester", "hash");

        repository.GetByUsernameAsync("tester", Arg.Any<CancellationToken>()).Returns(user);
        passwordHasher.VerifyPassword("correct-password", user.PasswordHash).Returns(true);

        var service = new AuthService(repository, passwordHasher, configurationService)
        {
            UtcNowProvider = () => now
        };

        var login = await service.LoginAsync("tester", "correct-password");

        login.Success.Should().BeTrue();
        login.Token.Should().NotBeNullOrWhiteSpace();
        (await service.ValidateTokenAsync(login.Token!)).Should().BeTrue();

        service.UtcNowProvider = () => now.AddMinutes(2);

        (await service.ValidateTokenAsync(login.Token!)).Should().BeFalse();
        (await service.GetSessionAsync(login.Token!)).Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_ShouldTemporarilyLockUserAfterConfiguredFailureThreshold()
    {
        var now = new DateTime(2026, 3, 20, 10, 0, 0, DateTimeKind.Utc);
        var repository = Substitute.For<IUserRepository>();
        var passwordHasher = Substitute.For<IPasswordHasher>();
        var configurationService = CreateConfigurationService(sessionTimeoutMinutes: 30, loginFailureLockoutCount: 2);
        var user = CreateUser("locked-user", "hash");

        repository.GetByUsernameAsync("locked-user", Arg.Any<CancellationToken>()).Returns(user);
        passwordHasher.VerifyPassword("bad-password", user.PasswordHash).Returns(false);
        passwordHasher.VerifyPassword("correct-password", user.PasswordHash).Returns(true);

        var service = new AuthService(repository, passwordHasher, configurationService)
        {
            UtcNowProvider = () => now
        };

        var firstFailure = await service.LoginAsync("locked-user", "bad-password");
        var secondFailure = await service.LoginAsync("locked-user", "bad-password");
        var lockedAttempt = await service.LoginAsync("locked-user", "correct-password");

        firstFailure.Success.Should().BeFalse();
        firstFailure.ErrorMessage.Should().Be("用户名或密码错误");
        secondFailure.Success.Should().BeFalse();
        secondFailure.ErrorMessage.Should().Contain("临时锁定");
        lockedAttempt.Success.Should().BeFalse();
        lockedAttempt.ErrorMessage.Should().Contain("临时锁定");
        await repository.DidNotReceive().UpdateAsync(Arg.Any<User>());
    }

    [Fact]
    public async Task LoginAsync_ShouldClearFailureCountAfterSuccessfulLogin()
    {
        var now = new DateTime(2026, 3, 20, 10, 0, 0, DateTimeKind.Utc);
        var repository = Substitute.For<IUserRepository>();
        var passwordHasher = Substitute.For<IPasswordHasher>();
        var configurationService = CreateConfigurationService(sessionTimeoutMinutes: 30, loginFailureLockoutCount: 2);
        var user = CreateUser("recover-user", "hash");

        repository.GetByUsernameAsync("recover-user", Arg.Any<CancellationToken>()).Returns(user);
        passwordHasher.VerifyPassword("bad-password", user.PasswordHash).Returns(false);
        passwordHasher.VerifyPassword("correct-password", user.PasswordHash).Returns(true);

        var service = new AuthService(repository, passwordHasher, configurationService)
        {
            UtcNowProvider = () => now
        };

        var firstFailure = await service.LoginAsync("recover-user", "bad-password");
        var success = await service.LoginAsync("recover-user", "correct-password");
        var failureAfterSuccess = await service.LoginAsync("recover-user", "bad-password");
        var secondSuccess = await service.LoginAsync("recover-user", "correct-password");

        firstFailure.Success.Should().BeFalse();
        success.Success.Should().BeTrue();
        failureAfterSuccess.Success.Should().BeFalse();
        failureAfterSuccess.ErrorMessage.Should().Be("用户名或密码错误");
        secondSuccess.Success.Should().BeTrue();
        await repository.Received(2).UpdateAsync(user);
    }

    [Fact]
    public async Task LogoutAsync_ShouldInvalidateExistingSession()
    {
        var repository = Substitute.For<IUserRepository>();
        var passwordHasher = Substitute.For<IPasswordHasher>();
        var configurationService = CreateConfigurationService(sessionTimeoutMinutes: 30, loginFailureLockoutCount: 2);
        var user = CreateUser("logout-user", "hash");

        repository.GetByUsernameAsync("logout-user", Arg.Any<CancellationToken>()).Returns(user);
        passwordHasher.VerifyPassword("correct-password", user.PasswordHash).Returns(true);

        var service = new AuthService(repository, passwordHasher, configurationService);
        var login = await service.LoginAsync("logout-user", "correct-password");

        (await service.ValidateTokenAsync(login.Token!)).Should().BeTrue();

        await service.LogoutAsync(login.Token!);

        (await service.ValidateTokenAsync(login.Token!)).Should().BeFalse();
        (await service.GetSessionAsync(login.Token!)).Should().BeNull();
    }

    [Fact]
    public async Task ChangePasswordAsync_ShouldRejectWeakPasswordWithoutComplexity()
    {
        var repository = Substitute.For<IUserRepository>();
        var passwordHasher = Substitute.For<IPasswordHasher>();
        var configurationService = CreateConfigurationService(sessionTimeoutMinutes: 30, loginFailureLockoutCount: 2, passwordMinLength: 8);
        var user = CreateUser("weak-password-user", "hash");

        repository.GetByIdAsync(user.Id).Returns(user);

        var service = new AuthService(repository, passwordHasher, configurationService);

        var result = await service.ChangePasswordAsync(user.Id.ToString(), "CurrentPwd1", "lowercase1");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("新密码必须同时包含大写字母、小写字母和数字");
        await repository.DidNotReceive().UpdateAsync(Arg.Any<User>());
    }

    [Fact]
    public async Task ChangePasswordAsync_ShouldRejectPasswordReuse()
    {
        var repository = Substitute.For<IUserRepository>();
        var passwordHasher = Substitute.For<IPasswordHasher>();
        var configurationService = CreateConfigurationService(sessionTimeoutMinutes: 30, loginFailureLockoutCount: 2, passwordMinLength: 8);
        var user = CreateUser("reuse-user", "hash");

        repository.GetByIdAsync(user.Id).Returns(user);

        var service = new AuthService(repository, passwordHasher, configurationService);

        var result = await service.ChangePasswordAsync(user.Id.ToString(), "CurrentPwd1", "CurrentPwd1");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("新密码不能与当前密码相同");
        await repository.DidNotReceive().UpdateAsync(Arg.Any<User>());
    }

    [Fact]
    public async Task ChangePasswordAsync_ShouldUpdatePasswordWhenPolicySatisfied()
    {
        var repository = Substitute.For<IUserRepository>();
        var passwordHasher = Substitute.For<IPasswordHasher>();
        var configurationService = CreateConfigurationService(sessionTimeoutMinutes: 30, loginFailureLockoutCount: 2, passwordMinLength: 8);
        var user = CreateUser("change-success-user", "old-hash");

        repository.GetByIdAsync(user.Id).Returns(user);
        passwordHasher.VerifyPassword("CurrentPwd1", user.PasswordHash).Returns(true);
        passwordHasher.HashPassword("NewSecure1").Returns("new-hash");

        var service = new AuthService(repository, passwordHasher, configurationService);

        var result = await service.ChangePasswordAsync(user.Id.ToString(), "CurrentPwd1", "NewSecure1");

        result.Success.Should().BeTrue();
        await repository.Received(1).UpdateAsync(user);
        passwordHasher.Received(1).HashPassword("NewSecure1");
    }

    private static IConfigurationService CreateConfigurationService(int sessionTimeoutMinutes, int loginFailureLockoutCount, int passwordMinLength = 6)
    {
        var configurationService = Substitute.For<IConfigurationService>();
        configurationService.GetCurrent().Returns(new AppConfig
        {
            Security = new SecurityConfig
            {
                PasswordMinLength = passwordMinLength,
                SessionTimeoutMinutes = sessionTimeoutMinutes,
                LoginFailureLockoutCount = loginFailureLockoutCount
            }
        });
        return configurationService;
    }

    private static User CreateUser(string username, string passwordHash)
    {
        return User.Create(username, passwordHash, username, UserRole.Engineer);
    }
}
