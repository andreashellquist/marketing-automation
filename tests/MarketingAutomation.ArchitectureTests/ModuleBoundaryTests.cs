using System.Reflection;
using NetArchTest.Rules;

namespace MarketingAutomation.ArchitectureTests;

public class ModuleBoundaryTests
{
    private static readonly string[] ModuleNames =
    [
        "Contacts", "Events", "Segments", "Campaigns", "Journeys",
        "Messaging", "Templates", "Analytics", "Ai", "Platform",
    ];

    public static TheoryData<string> Modules()
    {
        var data = new TheoryData<string>();
        foreach (var name in ModuleNames) data.Add(name);
        return data;
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Module_does_not_depend_on_other_modules(string moduleName)
    {
        var assembly = Assembly.Load($"MarketingAutomation.Modules.{moduleName}");
        var otherModules = ModuleNames
            .Where(m => m != moduleName)
            .Select(m => $"MarketingAutomation.Modules.{m}")
            .ToArray();

        var result = Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAny(otherModules)
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Module {moduleName} must not reference other modules. Violations: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void SharedKernel_does_not_depend_on_modules(string moduleName)
    {
        var assembly = Assembly.Load("MarketingAutomation.SharedKernel");

        var result = Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOn($"MarketingAutomation.Modules.{moduleName}")
            .GetResult();

        Assert.True(result.IsSuccessful);
    }
}
