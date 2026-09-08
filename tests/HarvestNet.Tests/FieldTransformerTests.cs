using HarvestNet.Core.Extraction;
using Xunit;

namespace HarvestNet.Tests;

public class FieldTransformerTests
{
    [Fact]
    public void Apply_NoTransforms_ReturnsValueUnchanged()
    {
        Assert.Equal("  Hello  ", FieldTransformer.Apply("  Hello  ", null));
    }

    [Fact]
    public void Apply_Trim_RemovesLeadingAndTrailingWhitespace()
    {
        var result = FieldTransformer.Apply("  Hello  ", new[] { FieldTransform.Trim });
        Assert.Equal("Hello", result);
    }

    [Fact]
    public void Apply_Lowercase_ConvertsToLowerCase()
    {
        var result = FieldTransformer.Apply("HELLO", new[] { FieldTransform.Lowercase });
        Assert.Equal("hello", result);
    }

    [Fact]
    public void Apply_Uppercase_ConvertsToUpperCase()
    {
        var result = FieldTransformer.Apply("hello", new[] { FieldTransform.Uppercase });
        Assert.Equal("HELLO", result);
    }

    [Fact]
    public void Apply_CollapseWhitespace_MergesRunsOfWhitespace()
    {
        var result = FieldTransformer.Apply("Hello   \n  World", new[] { FieldTransform.CollapseWhitespace });
        Assert.Equal("Hello World", result);
    }

    [Fact]
    public void Apply_StripNonDigits_KeepsOnlyDigitsAndDecimalPoint()
    {
        var result = FieldTransformer.Apply("1,299 items", new[] { FieldTransform.StripNonDigits });
        Assert.Equal("1299", result);
    }

    [Fact]
    public void Apply_StripCurrencySymbols_KeepsDigitsAndSeparators()
    {
        var result = FieldTransformer.Apply("$1,299.00", new[] { FieldTransform.StripCurrencySymbols });
        Assert.Equal("1,299.00", result);
    }

    [Fact]
    public void Apply_MultipleTransforms_RunInOrder()
    {
        var result = FieldTransformer.Apply("  HELLO WORLD  ", new[] { FieldTransform.Trim, FieldTransform.Lowercase });
        Assert.Equal("hello world", result);
    }

    [Fact]
    public void Apply_NullValue_ReturnsNull()
    {
        Assert.Null(FieldTransformer.Apply(null, new[] { FieldTransform.Trim }));
    }
}
