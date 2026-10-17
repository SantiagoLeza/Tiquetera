using Cinema.Domain.Common;

namespace Cinema.Domain.Tests.Common;

public class ResultTests
{
    private static readonly Error SomeError = new("test.error", "Algo falló.", ErrorType.Conflict);

    [Fact]
    public void Success_has_no_error()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Error_converts_implicitly_to_failed_result()
    {
        Result result = SomeError;

        Assert.True(result.IsFailure);
        Assert.Same(SomeError, result.Error);
    }

    [Fact]
    public void Value_converts_implicitly_to_successful_generic_result()
    {
        Result<int> result = 42;

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Error_converts_implicitly_to_failed_generic_result()
    {
        Result<int> result = SomeError;

        Assert.True(result.IsFailure);
        Assert.Same(SomeError, result.Error);
    }

    [Fact]
    public void Reading_value_of_failed_result_throws()
    {
        Result<int> result = SomeError;

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }
}
