using Pdv.Application.Utilities;
using Xunit;

namespace Pdv.Tests;

public sealed class TextNormalizationTests
{
    [Fact]
    public void FormatTaxIdPartial_ShouldFormatCpfAndCnpj()
    {
        Assert.Equal("123.456.789-00", TextNormalization.FormatTaxIdPartial("12345678900"));
        Assert.Equal("12.345.678/0001-99", TextNormalization.FormatTaxIdPartial("12345678000199"));
    }

    [Fact]
    public void FormatTaxId_ShouldReturnNullForBlankValues()
    {
        Assert.Null(TextNormalization.FormatTaxId("   "));
        Assert.Equal("123.456.789-00", TextNormalization.FormatTaxId("123.456.789-00"));
    }

    [Fact]
    public void TrimHelpers_ShouldRemoveEdgeWhitespaceOnly()
    {
        Assert.Equal("abc", TextNormalization.TrimToEmpty("  abc  "));
        Assert.Null(TextNormalization.TrimToNull("   "));
    }
}
