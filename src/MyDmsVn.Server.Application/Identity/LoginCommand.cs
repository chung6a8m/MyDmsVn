using MyDmsVn.Contracts;

namespace MyDmsVn.Server.Application.Identity
{
    public sealed class LoginCommand : ApplicationRequest<CurrentUserDto>
    {
        public LoginCommand(string username, string password)
        {
            Username = username;
            Password = password;
        }

        public string Username { get; }
        public string Password { get; }
    }
}
