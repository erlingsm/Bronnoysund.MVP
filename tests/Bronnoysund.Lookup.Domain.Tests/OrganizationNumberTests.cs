// SPDX-License-Identifier: AGPL-3.0-or-later OR LicenseRef-Commercial

using Bronnoysund.Lookup.Domain;
using FluentAssertions;
using Xunit;

namespace Bronnoysund.Lookup.Domain.Tests;

public class OrganizationNumberTests
{
    [Theory]
    [InlineData("919300388")] // Equinor (valid, starts with 9)
    [InlineData("933722821")] // Røa Systemutvikling AS (valid, starts with 9)
    [InlineData("974760843")] // Statens vegvesen (valid, starts with 9)
    public void TryCreate_ValidOrgNumber_ReturnsTrue(string raw)
    {
        var ok = OrganizationNumber.TryCreate(raw, out var value, out var error);

        ok.Should().BeTrue();
        value.Value.Should().Be(raw);
        error.Should().BeNull();
    }

    [Theory]
    [InlineData("919 300 388", "919300388")] // spaces are removed
    [InlineData("919-300-388", "919300388")] // hyphens are removed
    [InlineData(" 919300388 ", "919300388")] // padding is removed
    public void TryCreate_NormalizesSeparators(string input, string expected)
    {
        OrganizationNumber.TryCreate(input, out var value, out _).Should().BeTrue();
        value.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TryCreate_EmptyInput_ReturnsFalse(string? raw)
    {
        OrganizationNumber.TryCreate(raw, out _, out var error).Should().BeFalse();
        error.Should().Contain("empty");
    }

    [Theory]
    [InlineData("12345")]      // too short
    [InlineData("9193003881")] // too long
    [InlineData("91930038")]   // 8 digits
    public void TryCreate_WrongLength_ReturnsFalse(string raw)
    {
        OrganizationNumber.TryCreate(raw, out _, out var error).Should().BeFalse();
        error.Should().Contain("9 digits");
    }

    [Theory]
    [InlineData("12345678a")]
    [InlineData("abcdefghi")]
    public void TryCreate_NotOnlyDigits_ReturnsFalse(string raw)
    {
        OrganizationNumber.TryCreate(raw, out _, out var error).Should().BeFalse();
        error.Should().NotBeNull();
    }

    [Theory]
    [InlineData("123456785")] // starts with 1
    [InlineData("712345678")] // starts with 7
    public void TryCreate_DoesNotStartWith8Or9_ReturnsFalse(string raw)
    {
        OrganizationNumber.TryCreate(raw, out _, out var error).Should().BeFalse();
        error.Should().Contain("8 or 9");
    }

    [Theory]
    [InlineData("919300389")] // wrong check digit
    [InlineData("919300387")] // wrong check digit
    public void TryCreate_WrongMod11_ReturnsFalse(string raw)
    {
        OrganizationNumber.TryCreate(raw, out _, out var error).Should().BeFalse();
        error.Should().Contain("MOD11");
    }

    [Fact]
    public void Create_InvalidInput_Throws()
    {
        var act = () => OrganizationNumber.Create("12345");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Equality_BasedOnNormalizedValue()
    {
        var a = OrganizationNumber.Create("919300388");
        var b = OrganizationNumber.Create("919 300 388");

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void ToString_ReturnsNormalizedValue()
    {
        var orgnr = OrganizationNumber.Create("919 300 388");
        orgnr.ToString().Should().Be("919300388");
    }
}
