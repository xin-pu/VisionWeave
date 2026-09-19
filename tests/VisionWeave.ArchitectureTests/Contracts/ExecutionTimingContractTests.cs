using System.Reflection;
using Shouldly;
using VisionWeave.Contracts.Execution;

namespace VisionWeave.ArchitectureTests.Contracts;

/// <summary>
/// Enforces ADR-0013: the run owns every duration it reports, so an executor is
/// handed no clock and answers with no duration. The public surface of the
/// execution contract is read here because nothing else stops a timing member
/// from returning one member at a time, and a field an executor fills and the
/// run ignores is exactly the second meaning the decision rejected.
/// </summary>
public sealed class ExecutionTimingContractTests
{
    private static readonly Type[] TimingShaped =
    [
        typeof(TimeSpan),
        typeof(TimeSpan?),
        typeof(TimeProvider),
        typeof(DateTime),
        typeof(DateTimeOffset),
    ];

    [Fact]
    public void Execution_contract_carries_no_timing_and_no_clock()
    {
        List<string> offenders = [];

        foreach (Type contract in new[] { typeof(NodeExecutionRequest), typeof(NodeExecutionResult) })
        {
            const BindingFlags Surface =
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            foreach (PropertyInfo property in contract.GetProperties(Surface))
            {
                if (IsTimingShaped(property.PropertyType))
                {
                    offenders.Add($"{contract.Name}.{property.Name} is {property.PropertyType.Name}");
                }
            }

            foreach (MethodInfo method in contract.GetMethods(Surface))
            {
                if (IsTimingShaped(method.ReturnType))
                {
                    offenders.Add($"{contract.Name}.{method.Name} returns {method.ReturnType.Name}");
                }

                foreach (ParameterInfo parameter in method.GetParameters())
                {
                    if (IsTimingShaped(parameter.ParameterType))
                    {
                        offenders.Add($"{contract.Name}.{method.Name} takes {parameter.ParameterType.Name}");
                    }
                }
            }
        }

        offenders.ShouldBeEmpty(
            $"An executor is given no clock and reports no duration, so the run is the only measurer: {string.Join("; ", offenders)}");
    }

    private static bool IsTimingShaped(Type type) => TimingShaped.Contains(type);
}
