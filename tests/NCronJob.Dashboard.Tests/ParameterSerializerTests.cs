using Shouldly;

namespace NCronJob.Dashboard.Tests;

public class ParameterSerializerTests
{
    [Fact]
    public void SerializesObjectsAsIndentedJson()
    {
        var result = ParameterSerializer.Serialize(new { Name = "worker", Count = 3 });

        result.ShouldContain("\"Name\": \"worker\"");
        result.ShouldContain("\"Count\": 3");
    }

    [Fact]
    public void SerializesNullAsJsonNull()
    {
        ParameterSerializer.Serialize(null).ShouldBe("null");
    }

    [Fact]
    public void FallsBackToToStringForUnsupportedValues()
    {
        ParameterSerializer.Serialize(new UnsupportedParameter()).ShouldBe("unsupported");
    }

    private sealed class UnsupportedParameter
    {
        public Type Value => GetType();
        public override string ToString() => "unsupported";
    }
}
