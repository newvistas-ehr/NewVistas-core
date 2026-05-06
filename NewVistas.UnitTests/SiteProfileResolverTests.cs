// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using NewVistas.SiloHost.Infrastructure.Profiles;

namespace NewVistas.UnitTests;

[TestFixture]
public class SiteProfileResolverTests
{
    private static IConfiguration EmptyConfig() => new ConfigurationBuilder().Build();

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "NewVistas.UnitTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    [TearDown]
    public void ClearEnvVar()
    {
        // Resolver consults a process env var; isolate tests from each other and the dev shell.
        Environment.SetEnvironmentVariable(SiteProfileResolver.ProfileEnvironmentVariable, null);
    }

    [Test]
    public void Resolve_DefaultsToLocalhostDev_InDevelopment()
    {
        ISiteProfile profile = SiteProfileResolver.Resolve(
            EmptyConfig(),
            args: [],
            env: new FakeHostEnvironment { EnvironmentName = Environments.Development });

        Assert.That(profile, Is.InstanceOf<LocalhostDevProfile>());
    }

    [Test]
    public void Resolve_DefaultsToAzureCloud_OutsideDevelopment()
    {
        ISiteProfile profile = SiteProfileResolver.Resolve(
            EmptyConfig(),
            args: [],
            env: new FakeHostEnvironment { EnvironmentName = Environments.Production });

        Assert.That(profile, Is.InstanceOf<AzureCloudProfile>());
    }

    [Test]
    public void Resolve_LegacySqlExpressFlag_PicksDemoProfile()
    {
        ISiteProfile profile = SiteProfileResolver.Resolve(
            EmptyConfig(),
            args: ["--use-sqlexpress"],
            env: new FakeHostEnvironment { EnvironmentName = Environments.Development });

        Assert.That(profile, Is.InstanceOf<SqlExpressDemoProfile>());
    }

    [Test]
    public void Resolve_ProfileArgWinsOverSqlExpressFlag()
    {
        ISiteProfile profile = SiteProfileResolver.Resolve(
            EmptyConfig(),
            args: ["--profile=azure-cloud", "--use-sqlexpress"],
            env: new FakeHostEnvironment { EnvironmentName = Environments.Development });

        Assert.That(profile, Is.InstanceOf<AzureCloudProfile>());
    }

    [Test]
    public void Resolve_EnvironmentVariable_PicksRemoteOnline()
    {
        Environment.SetEnvironmentVariable(SiteProfileResolver.ProfileEnvironmentVariable, "remote-online");

        ISiteProfile profile = SiteProfileResolver.Resolve(
            EmptyConfig(),
            args: [],
            env: new FakeHostEnvironment { EnvironmentName = Environments.Development });

        Assert.That(profile, Is.InstanceOf<RemoteOnlineProfile>());
    }

    [Test]
    public void Resolve_ProfileArg_BeatsEnvironmentVariable()
    {
        Environment.SetEnvironmentVariable(SiteProfileResolver.ProfileEnvironmentVariable, "remote-online");

        ISiteProfile profile = SiteProfileResolver.Resolve(
            EmptyConfig(),
            args: ["--profile=remote-offline"],
            env: new FakeHostEnvironment { EnvironmentName = Environments.Development });

        Assert.That(profile, Is.InstanceOf<RemoteOfflineProfile>());
    }

    [Test]
    public void Resolve_AllNamedProfiles_ResolveByName()
    {
        var cases = new (string ArgName, Type ExpectedType)[]
        {
            ("localhost-dev", typeof(LocalhostDevProfile)),
            ("sql-express-demo", typeof(SqlExpressDemoProfile)),
            ("azure-cloud", typeof(AzureCloudProfile)),
            ("remote-online", typeof(RemoteOnlineProfile)),
            ("remote-offline", typeof(RemoteOfflineProfile)),
            ("ihs-tribal", typeof(IhsTribalSiteProfile)),
        };

        foreach ((string name, Type expectedType) in cases)
        {
            ISiteProfile profile = SiteProfileResolver.Resolve(
                EmptyConfig(),
                args: [$"--profile={name}"],
                env: new FakeHostEnvironment { EnvironmentName = Environments.Production });

            Assert.That(profile, Is.InstanceOf(expectedType), $"profile name '{name}'");
            Assert.That(profile.Name, Is.EqualTo(name), $"profile name '{name}'");
        }
    }

    [Test]
    public void Resolve_UnknownProfileName_Throws()
    {
        Assert.That(
            () => SiteProfileResolver.Resolve(
                EmptyConfig(),
                args: ["--profile=does-not-exist"],
                env: new FakeHostEnvironment()),
            Throws.InvalidOperationException);
    }

    [Test]
    public void UsesSqlExpressFlag_DetectsTheArg()
    {
        Assert.That(SiteProfileResolver.UsesSqlExpressFlag(["--use-sqlexpress"]), Is.True);
        Assert.That(SiteProfileResolver.UsesSqlExpressFlag(["--profile=sql-express-demo"]), Is.False);
        Assert.That(SiteProfileResolver.UsesSqlExpressFlag([]), Is.False);
    }
}
