namespace Injaz.Application.Security;

public interface IPasswordHasher
{
    string Hash(string password);

    PasswordVerification Verify(string passwordHash, string password);
}

public enum PasswordVerification
{
    Failed = 0,
    Success = 1,

    /// <summary>Correct password, but the hash uses outdated parameters and should be replaced.</summary>
    SuccessRehashNeeded = 2,
}
