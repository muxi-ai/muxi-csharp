using Xunit;
using Muxi;

namespace Muxi.Tests;

[Trait("Category", "Integration")]
public class IntegrationTests
{
    private static string Env(string name)
    {
        var val = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrEmpty(val))
            throw new SkipException($"{name} not set");
        return val;
    }

    private static ServerClient GetServerClient()
    {
        return new ServerClient(new ServerConfig
        {
            Url = Env("MUXI_SDK_E2E_SERVER_URL"),
            KeyId = Env("MUXI_SDK_E2E_KEY_ID"),
            SecretKey = Env("MUXI_SDK_E2E_SECRET_KEY")
        });
    }

    private static FormationClient GetFormationClient()
    {
        return new FormationClient(new FormationConfig
        {
            ServerUrl = Env("MUXI_SDK_E2E_SERVER_URL"),
            FormationId = Env("MUXI_SDK_E2E_FORMATION_ID"),
            ClientKey = Env("MUXI_SDK_E2E_CLIENT_KEY"),
            AdminKey = Env("MUXI_SDK_E2E_ADMIN_KEY")
        });
    }

    [SkippableFact]
    public async Task ServerPing()
    {
        var client = GetServerClient();
        var result = await client.PingAsync();
        Assert.True(result >= 0);
    }

    [SkippableFact]
    public async Task ServerHealth()
    {
        var client = GetServerClient();
        var result = await client.HealthAsync();
        Assert.NotNull(result);
    }

    [SkippableFact]
    public async Task ServerStatus()
    {
        var client = GetServerClient();
        var result = await client.StatusAsync();
        Assert.NotNull(result);
    }

    [SkippableFact]
    public async Task ServerListFormations()
    {
        var client = GetServerClient();
        var result = await client.ListFormationsAsync();
        Assert.NotNull(result);
    }

    [SkippableFact]
    public async Task FormationHealth()
    {
        var client = GetFormationClient();
        var result = await client.HealthAsync();
        Assert.NotNull(result);
    }

    [SkippableFact]
    public async Task FormationGetStatus()
    {
        var client = GetFormationClient();
        var result = await client.GetStatusAsync();
        Assert.NotNull(result);
    }

    [SkippableFact]
    public async Task FormationGetConfig()
    {
        var client = GetFormationClient();
        var result = await client.GetConfigAsync();
        Assert.NotNull(result);
    }

    [SkippableFact]
    public async Task FormationGetAgents()
    {
        var client = GetFormationClient();
        var result = await client.GetAgentsAsync();
        Assert.NotNull(result);
    }
}

public class SkipException : Exception
{
    public SkipException(string message) : base(message) { }
}

public class SkippableFactAttribute : FactAttribute
{
    public override string? Skip
    {
        get
        {
            var required = new[] { "MUXI_SDK_E2E_SERVER_URL", "MUXI_SDK_E2E_KEY_ID", "MUXI_SDK_E2E_SECRET_KEY", 
                                   "MUXI_SDK_E2E_FORMATION_ID", "MUXI_SDK_E2E_CLIENT_KEY", "MUXI_SDK_E2E_ADMIN_KEY" };
            foreach (var name in required)
            {
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name)))
                    return $"{name} not set";
            }
            return null;
        }
        set { }
    }
}
