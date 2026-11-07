using System.Reflection;
using Shouldly;

namespace NCronJob.Tests;

public sealed class ExtensionMethodGuardTests
{
    public static TheoryData<MethodInfo> ExtensionMethods()
    {
        var data = new TheoryData<MethodInfo>();
        var extensionTypes = new[]
        {
            typeof(IInstantJobRegistryExtensions),
            typeof(IRuntimeJobRegistryExtensions),
            typeof(IRuntimeJobBuilderExtensions),
        };

        foreach (var method in extensionTypes.SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static)))
        {
            data.Add(method.IsGenericMethodDefinition ? method.MakeGenericMethod(typeof(DummyJob)) : method);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ExtensionMethods))]
    public void ExtensionMethodsRejectNullReceiver(MethodInfo method)
    {
        var parameters = method.GetParameters();
        var arguments = parameters
            .Select(p => p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null)
            .ToArray();

        var exception = Should.Throw<TargetInvocationException>(() => method.Invoke(null, arguments));

        var guard = exception.InnerException.ShouldBeOfType<ArgumentNullException>();
        guard.ParamName.ShouldBe(parameters[0].Name);
    }

    [Fact]
    public async Task UseNCronJobAsyncRejectsNullHost()
    {
        var exception = await Should.ThrowAsync<ArgumentNullException>(async () =>
        {
            using var host = await NCronJobExtensions.UseNCronJobAsync(null!);
        });

        exception.ParamName.ShouldBe("host");
    }
}
