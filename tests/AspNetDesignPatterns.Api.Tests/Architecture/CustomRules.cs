using Mono.Cecil;
using Mono.Cecil.Cil;

using NetArchTest.Rules;

namespace AspNetDesignPatterns.Api.Tests.Architecture;

/// <summary>
/// NetArchTest ships type-level predicates; these <see cref="ICustomRule"/> implementations
/// reach into method signatures and IL to express the two rules that are about
/// <em>behaviour</em>, not structure.
/// </summary>
internal static class CustomRules
{
    /// <summary>Every public instance method returns <c>Result</c> / <c>Result&lt;T&gt;</c> (optionally wrapped in a task).</summary>
    public static ICustomRule ReturnResult { get; } = new ReturnsResultRule();

    /// <summary>
    /// The type contains no <c>throw new …</c>. Only a <c>throw</c> of a freshly constructed object
    /// counts: a bare <c>throw;</c> is a different opcode, and the compiler's own lowering of
    /// <c>await using</c> and of <c>await</c> inside a <c>catch</c> rethrows a captured exception
    /// with a plain <c>throw</c> that no source line wrote.
    /// </summary>
    public static ICustomRule NeverThrow { get; } = new DoesNotThrowRule();

    /// <summary>
    /// The type references a production type whose name ends in <c>Service</c>, <c>Client</c> or
    /// <c>Repository</c> — the layers below a handler.
    /// </summary>
    public static ICustomRule DependsOnALayerBelowTheHandler { get; } = new ReferencesTypeRule(reference =>
        IsProductionTypeNamed(reference, "Service", "Client", "Repository"));

    /// <summary>
    /// The type references the gateway layer: a production type whose name ends in <c>Client</c>
    /// or <c>Repository</c>, or anything in <c>KAM.Common.Data</c> (<c>IUnitOfWork</c>,
    /// <c>IDbConnectionFactory</c>) — what only a service, or the gateway itself, should touch.
    /// </summary>
    public static ICustomRule DependsOnTheGatewayLayer { get; } = new ReferencesTypeRule(reference =>
        IsProductionTypeNamed(reference, "Client", "Repository")
        || string.Equals(reference.Namespace, "KAM.Common.Data", StringComparison.Ordinal));

    private sealed class ReturnsResultRule : ICustomRule
    {
        public bool MeetsRule(TypeDefinition type) =>
            type.Methods.Where(IsPublicBehaviourMethod).All(method => ReturnsResult(method.ReturnType));

        private static bool IsPublicBehaviourMethod(MethodDefinition method) =>
            method is { IsPublic: true, IsStatic: false, IsConstructor: false, IsGetter: false, IsSetter: false, IsSpecialName: false }
            && !IsCompilerGenerated(method);

        private static bool ReturnsResult(TypeReference returnType)
        {
            if (returnType.Name is "Result" or "Result`1")
            {
                return true;
            }

            return returnType is GenericInstanceType { ElementType.Name: "Task`1" or "ValueTask`1" } task
                && ReturnsResult(task.GenericArguments[0]);
        }
    }

    private sealed class DoesNotThrowRule : ICustomRule
    {
        public bool MeetsRule(TypeDefinition type)
        {
            IEnumerable<MethodDefinition> ownMethods = type.Methods.Where(m => m.HasBody && !IsCompilerGenerated(m));
            IEnumerable<MethodDefinition> stateMachineMethods = type.NestedTypes
                .Where(IsCompilerGenerated)
                .SelectMany(nested => nested.Methods)
                .Where(m => m.HasBody);

            return ownMethods.Concat(stateMachineMethods)
                .SelectMany(method => method.Body.Instructions)
                .All(instruction => !(instruction.OpCode == OpCodes.Throw && instruction.Previous?.OpCode == OpCodes.Newobj));
        }
    }

    private static bool IsProductionTypeNamed(TypeReference reference, params string[] suffixes) =>
        reference.FullName.StartsWith(ArchitectureRules.RootNamespace, StringComparison.Ordinal)
        && suffixes.Any(suffix => reference.Name.EndsWith(suffix, StringComparison.Ordinal));

    /// <summary>Met when the type (or its compiler-generated helpers) references a type matching the predicate.</summary>
    private sealed class ReferencesTypeRule(Func<TypeReference, bool> matches) : ICustomRule
    {
        public bool MeetsRule(TypeDefinition type) => TypesAndNestedHelpers(type)
            .SelectMany(ReferencedTypes)
            .Any(matches);

        private static IEnumerable<TypeDefinition> TypesAndNestedHelpers(TypeDefinition type) =>
            new[] { type }.Concat(type.NestedTypes.Where(IsCompilerGenerated));

        private static IEnumerable<TypeReference> ReferencedTypes(TypeDefinition type)
        {
            IEnumerable<TypeReference> fromFields = type.Fields.Select(field => field.FieldType);

            IEnumerable<TypeReference> fromSignatures = type.Methods.SelectMany(method =>
                method.Parameters.Select(p => p.ParameterType).Append(method.ReturnType));

            IEnumerable<TypeReference> fromBodies = type.Methods
                .Where(method => method.HasBody)
                .SelectMany(method => method.Body.Instructions)
                .Select(instruction => instruction.Operand switch
                {
                    MethodReference method => method.DeclaringType,
                    FieldReference field => field.DeclaringType,
                    TypeReference typeReference => typeReference,
                    _ => null,
                })
                .OfType<TypeReference>();

            return fromFields.Concat(fromSignatures).Concat(fromBodies).SelectMany(Flatten);
        }

        private static IEnumerable<TypeReference> Flatten(TypeReference reference)
        {
            yield return reference;

            if (reference is GenericInstanceType generic)
            {
                foreach (TypeReference argument in generic.GenericArguments.SelectMany(Flatten))
                {
                    yield return argument;
                }
            }
        }
    }

    private static bool IsCompilerGenerated(ICustomAttributeProvider member) =>
        member.CustomAttributes.Any(attribute =>
            attribute.AttributeType.Name == nameof(System.Runtime.CompilerServices.CompilerGeneratedAttribute));
}
