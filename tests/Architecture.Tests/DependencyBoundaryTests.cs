using System.Reflection;

namespace Architecture.Tests;

public class DependencyBoundaryTests
{
    private static readonly Assembly CoreAssembly =
        typeof(Discourser.Core.Models.Document).Assembly;

    private static readonly Assembly ServerAssembly =
        typeof(Discourser.Server.DiscourserOptions).Assembly;

    [Fact]
    public void Core_DoesNotReference_ModelContextProtocol()
    {
        var refs = CoreAssembly.GetReferencedAssemblies();
        Assert.DoesNotContain(refs, r => r.Name!.StartsWith("ModelContextProtocol"));
    }

    [Fact]
    public void Core_DoesNotReference_AspNetCore()
    {
        var refs = CoreAssembly.GetReferencedAssemblies();
        Assert.DoesNotContain(refs, r => r.Name!.Contains("AspNetCore"));
    }

    [Fact]
    public void Server_DoesNotReference_SqliteDirectly()
    {
        var refs = ServerAssembly.GetReferencedAssemblies();
        Assert.DoesNotContain(refs, r => r.Name == "Microsoft.Data.Sqlite");
    }
}
