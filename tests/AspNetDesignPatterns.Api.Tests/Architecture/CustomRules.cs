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

    /// <summary>The type contains no <c>throw new …</c>. A bare <c>throw;</c> rethrow is a different opcode and is allowed.</summary>
    public static ICustomRule NeverThrow { get; } = new DoesNotThrowRule();

    /// <summary>The type references a production type whose name ends in <c>Service</c> or <c>Client</c>.</summary>
    public static ICustomRule DependsOnAServiceOrClient { get; } = new DependsOnServiceOrClientRule();

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
                .All(instruction => instruction.OpCode != OpCodes.Throw);
        }
    }

    private sealed class DependsOnServiceOrClientRule : ICustomRule
    {
        public bool MeetsRule(TypeDefinition type) => TypesAndNestedHelpers(type)
            .SelectMany(ReferencedTypeNames)
            .Any(name => name.EndsWith("Service", StringComparison.Ordinal)
                      || name.EndsWith("Client", StringComparison.Ordinal));

        private static IEnumerable<TypeDefinition> TypesAndNestedHelpers(TypeDefinition type) =>
            new[] { type }.Concat(type.NestedTypes.Where(IsCompilerGenerated));

        private static IEnumerable<string> ReferencedTypeNames(TypeDefinition type)
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

            return fromFields.Concat(fromSignatures).Concat(fromBodies)
                .SelectMany(Flatten)
                .Where(reference => reference.FullName.StartsWith(ArchitectureRules.RootNamespace, StringComparison.Ordinal))
                .Select(reference => reference.Name);
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
