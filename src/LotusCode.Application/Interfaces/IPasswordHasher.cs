namespace LotusCode.Application.Interfaces
{
    /// <summary>
    /// Defines password hashing operations used for securely storing
    /// and verifying user passwords.
    /// </summary>
    public interface IPasswordHasher
    {
        string Hash(string password);

        bool Verify(string password, string passwordHash);
    }
}
