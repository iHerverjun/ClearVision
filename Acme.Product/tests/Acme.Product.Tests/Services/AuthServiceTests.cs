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

    private static IConfigurationService CreateConfigurationService(int sessionTimeoutMinutes, int loginFailureLockoutCount)
    {
        var configurationService = Substitute.For<IConfigurationService>();
        configurationService.GetCurrent().Returns(new AppConfig
        {
            Security = new SecurityConfig
            {
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
