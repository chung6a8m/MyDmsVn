namespace MyDmsVn.Contracts
{
    public sealed class LoginRequest
    {
        public LoginRequest(string username, string password)
        {
            Username = username;
            Password = password;
        }

        public string Username { get; }
        public string Password { get; }
    }

    public sealed class CurrentUserDto
    {
        public CurrentUserDto(int userId, string username, string displayName)
        {
            UserId = userId;
            Username = username;
            DisplayName = displayName;
        }

        public int UserId { get; }
        public string Username { get; }
        public string DisplayName { get; }
    }
}
