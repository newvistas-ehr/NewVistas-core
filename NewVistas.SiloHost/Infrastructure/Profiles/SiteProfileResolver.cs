// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.SiloHost.Infrastructure.Profiles;

/// <summary>
/// Picks the <see cref="ISiteProfile"/> for this silo at startup.
///
/// Resolution order (first match wins):
/// <list type="number">
///   <item><description><c>--profile=&lt;name&gt;</c> CLI argument</description></item>
///   <item><description><c>NEWVISTAS_PROFILE</c> environment variable</description></item>
///   <item><description><c>--use-sqlexpress</c> CLI argument → <see cref="SqlExpressDemoProfile"/> (back-compat)</description></item>
///   <item><description><see cref="IHostEnvironment.IsDevelopment"/> → <see cref="LocalhostDevProfile"/></description></item>
///   <item><description>otherwise → <see cref="AzureCloudProfile"/></description></item>
/// </list>
///
/// If both <c>--profile</c> and <c>--use-sqlexpress</c> are supplied,
/// <c>--profile</c> wins. Unknown profile names throw at startup so misconfiguration
/// fails loudly rather than silently falling back to a default.
/// </summary>
public static class SiteProfileResolver
{
    public const string ProfileEnvironmentVariable = "NEWVISTAS_PROFILE";
    public const string ProfileArgPrefix = "--profile=";
    public const string SqlExpressArg = "--use-sqlexpress";

    public static ISiteProfile Resolve(IConfiguration config, string[] args, IHostEnvironment env)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(env);

        string? explicitProfile =
            ReadProfileArg(args)
            ?? Environment.GetEnvironmentVariable(ProfileEnvironmentVariable);

        if (!string.IsNullOrWhiteSpace(explicitProfile))
        {
            return Create(explicitProfile.Trim().ToLowerInvariant());
        }

        if (args.Contains(SqlExpressArg, StringComparer.Ordinal))
        {
            return new SqlExpressDemoProfile();
        }

        if (env.IsDevelopment())
        {
            return new LocalhostDevProfile();
        }

        return new AzureCloudProfile();
    }

    /// <summary>True when the <c>--use-sqlexpress</c> legacy flag was passed.</summary>
    public static bool UsesSqlExpressFlag(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return args.Contains(SqlExpressArg, StringComparer.Ordinal);
    }

    private static string? ReadProfileArg(string[] args)
    {
        foreach (string arg in args)
        {
            if (arg.StartsWith(ProfileArgPrefix, StringComparison.Ordinal))
            {
                return arg[ProfileArgPrefix.Length..];
            }
        }
        return null;
    }

    private static ISiteProfile Create(string profileName) => profileName switch
    {
        "localhost-dev" => new LocalhostDevProfile(),
        "sql-express-demo" => new SqlExpressDemoProfile(),
        "azure-cloud" => new AzureCloudProfile(),
        "remote-online" => new RemoteOnlineProfile(),
        "remote-offline" => new RemoteOfflineProfile(),
        "ihs-tribal" => new IhsTribalSiteProfile(),
        _ => throw new InvalidOperationException(
            $"Unknown site profile '{profileName}'. Valid: localhost-dev, sql-express-demo, azure-cloud, remote-online, remote-offline, ihs-tribal."),
    };
}
