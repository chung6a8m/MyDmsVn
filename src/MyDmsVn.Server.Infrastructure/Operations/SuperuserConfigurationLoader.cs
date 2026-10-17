using System;

namespace MyDmsVn.Server.Infrastructure.Operations;

public sealed class SuperuserConfigurationLoader
{
    public const string EnvironmentVariable = "Security__SuperuserUsername";

    private readonly Func<string, string?> _readEnvironment;

    public SuperuserConfigurationLoader()
        : this(Environment.GetEnvironmentVariable)
    {
    }

    public SuperuserConfigurationLoader(Func<string, string?> readEnvironment)
    {
        _readEnvironment = readEnvironment ?? throw new ArgumentNullException(nameof(readEnvironment));
    }

    public string Load()
    {
        var username = _readEnvironment(EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new OperationalConfigurationException(
                "Startup.SuperuserConfigurationMissing",
                "Thiếu cấu hình Security:SuperuserUsername. Hãy liên hệ quản trị viên triển khai.");
        }

        return username!.Trim();
    }
}
