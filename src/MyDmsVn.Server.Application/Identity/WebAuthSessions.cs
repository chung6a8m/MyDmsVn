using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyDmsVn.Server.Application.Identity;

public interface IWebAuthSessionStore
{
    Task<WebAuthSessionToken?> IssueAsync(int userId, CancellationToken cancellationToken);
    Task<WebAuthSessionToken?> RotateAsync(string refreshToken, CancellationToken cancellationToken);
    Task RevokeAsync(string refreshToken, CancellationToken cancellationToken);
    Task<CurrentUser?> GetActiveUserAsync(Guid sessionId, int userId, CancellationToken cancellationToken);
}

// Server-only handoff to the cookie writer. Never serialize this object into a response or log.
public sealed class WebAuthSessionToken
{
    public WebAuthSessionToken(Guid sessionId, CurrentUser user, string refreshToken,
        DateTime expiresAtUtc, DateTime absoluteExpiresAtUtc)
    {
        SessionId = sessionId;
        User = user;
        RefreshToken = refreshToken;
        ExpiresAtUtc = expiresAtUtc;
        AbsoluteExpiresAtUtc = absoluteExpiresAtUtc;
    }

    public Guid SessionId { get; }
    public CurrentUser User { get; }
    public string RefreshToken { get; }
    public DateTime ExpiresAtUtc { get; }
    public DateTime AbsoluteExpiresAtUtc { get; }
}
