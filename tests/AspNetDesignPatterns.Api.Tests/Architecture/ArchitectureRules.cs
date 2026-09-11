using System.Reflection;

using NetArchTest.Rules;

namespace AspNetDesignPatterns.Api.Tests.Architecture;

/// <summary>
/// Shared scaffolding for the architecture tests: the assembly under test, the namespace
/// roots the rules talk about, and a single assertion that reports <em>which</em> types broke
/// a rule (NetArchTest's <see cref="TestResult"/> alone just says pass/fail).
/// </summary>
/// <remarks>
/// <see cref="ProductionAssembly"/> is only <c>AspNetDesignPatterns.Api</c> — Features/,
/// Program.cs, and (still) everything a rule here checks. It does <em>not</em> include
/// <c>AspNetDesignPatterns.Api.Shared</c> (DependencyInjection/ and Shared/, in the separate
/// project): a rule that needs to see into that assembly too would need its own
/// <c>Types.InAssembly</c> query, which none of the current rules do.
/// </remarks>
internal static class ArchitectureRules
{
    public const string RootNamespace = "AspNetDesignPatterns.Api";
    public const string FeaturesNamespace = RootNamespace + ".Features";

    public static readonly Assembly ProductionAssembly = typeof(Program).Assembly;

    /// <summary>A fresh <see cref="Types"/> query rooted at the production assembly.</summary>
    public static Types InProductionCode() => Types.InAssembly(ProductionAssembly);

    /// <summary>
    /// The distinct feature namespaces (<c>…Features.Weather</c>, …) — one per vertical slice,
    /// discovered rather than hard-coded so a new slice is covered automatically.
    /// </summary>
    public static IReadOnlyList<string> FeatureNamespaces { get; } =
    [
        .. ProductionAssembly.GetTypes()
            .Select(type => type.Namespace)
            .Where(ns => ns is not null && ns.StartsWith(FeaturesNamespace + ".", StringComparison.Ordinal))
            .Select(ns => string.Join('.', ns!.Split('.').Take(4)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(ns => ns, StringComparer.Ordinal),
    ];

    /// <summary>Asserts a rule holds, naming every offending type when it does not.</summary>
    public static void AssertRuleHolds(TestResult result, string because)
    {
        result.IsSuccessful.Should().BeTrue(
            "{0}, but these types break the rule: {1}",
            because,
            result.FailingTypeNames is null ? "—" : string.Join(", ", result.FailingTypeNames));
    }
}
