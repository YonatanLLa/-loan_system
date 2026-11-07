using LoanSystem.Common.Core.Application;
using Shouldly;
using Xunit;

namespace LoanSystem.Application.UnitTests;

public class ResultTests
{
    [Fact]
    public void Success_ShouldCreateSuccessfulResult()
    {
        var result = Result.Success();

        result.IsSuccess.ShouldBeTrue();
        result.IsFailure.ShouldBeFalse();
        result.Error.ShouldBe(Error.None);
    }

    [Fact]
    public void Failure_ShouldCreateFailedResult()
    {
        var error = Error.NotFound("Item.NotFound", "The item was not found.");
        var result = Result.Failure(error);

        result.IsSuccess.ShouldBeFalse();
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(error);
        result.Error.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public void GenericResult_Success_ShouldHoldValue()
    {
        var result = Result.Success(42);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
    }

    [Fact]
    public void GenericResult_Failure_AccessingValue_ShouldThrowInvalidOperationException()
    {
        var error = Error.Validation("Input.Invalid", "Bad input.");
        var result = Result.Failure<int>(error);

        result.IsFailure.ShouldBeTrue();
        Should.Throw<InvalidOperationException>(() =>
        {
            var _ = result.Value;
        });
    }

    [Fact]
    public void ImplicitOperator_FromValue_ShouldReturnSuccess()
    {
        Result<string> result = "Hello World";

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("Hello World");
    }
}
