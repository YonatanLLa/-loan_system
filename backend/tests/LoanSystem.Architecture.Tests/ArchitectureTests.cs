using System.Reflection;
using NetArchTest.Rules;
using Shouldly;
using Xunit;

namespace LoanSystem.Architecture.Tests;

public class ArchitectureTests
{
    private static readonly string[] Modules =
    [
        "Administration", "Collections", "Customers", "Integrations", "Loans", "Notifications",
        "Origination", "Payments", "Products", "Reports", "Security", "Treasury"
    ];

    private static readonly string[] InfrastructureNamespaces =
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore",
        "Npgsql",
        "StackExchange.Redis",
        "Dapper"
    ];

    public static TheoryData<string> ModuleNames => new(Modules);

    private static Assembly CoreAssembly(string module) => Assembly.Load($"LoanSystem.Modules.{module}.Core");
    private static Assembly AdaptersAssembly(string module) => Assembly.Load($"LoanSystem.Modules.{module}.Adapters");
    private static Assembly CommonCoreAssembly => typeof(LoanSystem.Common.Core.Domain.Money).Assembly;

    private static IEnumerable<Assembly> AllCoreAssemblies() =>
        Modules.Select(CoreAssembly).Prepend(CommonCoreAssembly);

    private static string Describe(TestResult result) =>
        string.Join(", ", result.FailingTypeNames ?? []);

    [Fact]
    public void CommonCore_ShouldNotDependOn_InfrastructureOrFrameworks()
    {
        var result = Types.InAssembly(CommonCoreAssembly)
            .ShouldNot()
            .HaveDependencyOnAny([.. InfrastructureNamespaces, "LoanSystem.Common.Adapters", "LoanSystem.Modules"])
            .GetResult();

        result.IsSuccessful.ShouldBeTrue($"Failing types: {Describe(result)}");
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void ModuleCore_ShouldNotDependOn_InfrastructureOrAdapters(string module)
    {
        var result = Types.InAssembly(CoreAssembly(module))
            .ShouldNot()
            .HaveDependencyOnAny([.. InfrastructureNamespaces, "LoanSystem.Common.Adapters", $"LoanSystem.Modules.{module}.Adapters"])
            .GetResult();

        result.IsSuccessful.ShouldBeTrue($"{module}.Core failing types: {Describe(result)}");
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Module_ShouldOnlyDependOn_OtherModulesContracts(string module)
    {
        // Other modules may only be reached through LoanSystem.Modules.<Other>.Core.Contracts
        var forbidden = Modules
            .Where(other => other != module)
            .SelectMany(other => new[]
            {
                $"LoanSystem.Modules.{other}.Core.Application",
                $"LoanSystem.Modules.{other}.Core.Domain",
                $"LoanSystem.Modules.{other}.Core.Ports",
                $"LoanSystem.Modules.{other}.Adapters"
            })
            .ToArray();

        foreach (var assembly in new[] { CoreAssembly(module), AdaptersAssembly(module) })
        {
            var result = Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOnAny(forbidden)
                .GetResult();

            result.IsSuccessful.ShouldBeTrue($"{assembly.GetName().Name} failing types: {Describe(result)}");
        }
    }

    [Fact]
    public void CoreAssemblies_ShouldNotUse_FloatingPointInMethodBodies()
    {
        // Locals, float constants/conversions and calls like Math.Pow(double, double)
        var violations = AllCoreAssemblies()
            .SelectMany(a => a.GetTypes())
            .SelectMany(FloatingPointIlScanner.FindViolations)
            .ToList();

        violations.ShouldBeEmpty($"Floating-point usage found: {string.Join(", ", violations)}");
    }

    [Fact]
    public void CoreAssemblies_ShouldNotExpose_FloatingPointMembers()
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                                   BindingFlags.Static | BindingFlags.DeclaredOnly;
        var floating = new[] { typeof(float), typeof(double), typeof(float?), typeof(double?) };

        var violations = AllCoreAssemblies()
            .SelectMany(a => a.GetTypes())
            .SelectMany(t =>
                t.GetProperties(flags).Where(p => floating.Contains(p.PropertyType)).Select(p => $"{t.FullName}.{p.Name}")
                    .Concat(t.GetFields(flags).Where(f => floating.Contains(f.FieldType)).Select(f => $"{t.FullName}.{f.Name}"))
                    .Concat(t.GetMethods(flags)
                        .Where(m => floating.Contains(m.ReturnType) || m.GetParameters().Any(p => floating.Contains(p.ParameterType)))
                        .Select(m => $"{t.FullName}.{m.Name}()")))
            .ToList();

        violations.ShouldBeEmpty($"Floating-point members found: {string.Join(", ", violations)}");
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void InboundAdapters_ShouldNotDependOn_OutboundAdapters(string module)
    {
        // Endpoints and jobs must reach persistence through inbound ports (use cases), never directly
        var result = Types.InAssembly(AdaptersAssembly(module))
            .That()
            .ResideInNamespace($"LoanSystem.Modules.{module}.Adapters.Inbound")
            .ShouldNot()
            .HaveDependencyOnAny(
                $"LoanSystem.Modules.{module}.Adapters.Outbound",
                $"LoanSystem.Modules.{module}.Core.Application",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue($"{module}.Adapters.Inbound failing types: {Describe(result)}");
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void ModuleCore_ShouldOnlyExpose_PortsAndContracts(string module)
    {
        // Use cases and domain are implementation details: other modules see Ports and Contracts only
        var result = Types.InAssembly(CoreAssembly(module))
            .That()
            .ResideInNamespace($"LoanSystem.Modules.{module}.Core.Application")
            .Or()
            .ResideInNamespace($"LoanSystem.Modules.{module}.Core.Domain")
            .ShouldNot()
            .BePublic()
            .GetResult();

        result.IsSuccessful.ShouldBeTrue($"{module}.Core public internals: {Describe(result)}");
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void OutboundAdapters_ShouldNotBePublic(string module)
    {
        // DbContexts and gateways are reached through the module's outbound ports, never by type
        var result = Types.InAssembly(AdaptersAssembly(module))
            .That()
            .ResideInNamespace($"LoanSystem.Modules.{module}.Adapters.Outbound")
            .ShouldNot()
            .BePublic()
            .GetResult();

        result.IsSuccessful.ShouldBeTrue($"{module}.Adapters.Outbound public types: {Describe(result)}");
    }

    [Theory]
    [InlineData("LoanSystem.WebApi")]
    [InlineData("LoanSystem.Workers")]
    public void Hosts_ShouldOnlyReachModules_ThroughAdapterEntryPoints(string host)
    {
        // Hosts compose modules via Add<M>Module / Map<M>Endpoints / Add<M>Jobs, never via their internals
        var forbidden = Modules
            .SelectMany(m => new[]
            {
                $"LoanSystem.Modules.{m}.Core.Application",
                $"LoanSystem.Modules.{m}.Core.Domain",
                $"LoanSystem.Modules.{m}.Adapters.Outbound"
            })
            .ToArray();

        var result = Types.InAssembly(Assembly.Load(host))
            .ShouldNot()
            .HaveDependencyOnAny(forbidden)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue($"{host} failing types: {Describe(result)}");
    }
}
