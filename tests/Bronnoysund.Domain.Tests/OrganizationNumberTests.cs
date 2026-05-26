// SPDX-License-Identifier: MIT

using Bronnoysund.Domain;
using FluentAssertions;

namespace Bronnoysund.Domain.Tests;

[TestClass]
public class OrganizationNumberTests
{
    [TestMethod]
    [DataRow("919300388")] // Equinor (valid, starts with 9)
    [DataRow("933722821")] // Røa Systemutvikling AS (valid, starts with 9)
    [DataRow("974760843")] // Statens vegvesen (valid, starts with 9)
    public void TryCreate_ValidOrgNumber_ReturnsTrue(string raw)
    {
        var ok = OrganizationNumber.TryCreate(raw, out var value, out var error);

        ok.Should().BeTrue();
        value.Value.Should().Be(raw);
        error.Should().BeNull();
    }

    [TestMethod]
    [DataRow("919 300 388", "919300388")] // spaces are removed
    [DataRow("919-300-388", "919300388")] // hyphens are removed
    [DataRow(" 919300388 ", "919300388")] // padding is removed
    public void TryCreate_NormalizesSeparators(string input, string expected)
    {
        OrganizationNumber.TryCreate(input, out var value, out _).Should().BeTrue();
        value.Value.Should().Be(expected);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow(null)]
    public void TryCreate_EmptyInput_ReturnsFalse(string? raw)
    {
        OrganizationNumber.TryCreate(raw, out _, out var error).Should().BeFalse();
        error.Should().Contain("empty");
    }

    [TestMethod]
    [DataRow("12345")]      // too short
    [DataRow("9193003881")] // too long
    [DataRow("91930038")]   // 8 digits
    public void TryCreate_WrongLength_ReturnsFalse(string raw)
    {
        OrganizationNumber.TryCreate(raw, out _, out var error).Should().BeFalse();
        error.Should().Contain("9 digits");
    }

    [TestMethod]
    [DataRow("12345678a")]
    [DataRow("abcdefghi")]
    public void TryCreate_NotOnlyDigits_ReturnsFalse(string raw)
    {
        OrganizationNumber.TryCreate(raw, out _, out var error).Should().BeFalse();
        error.Should().NotBeNull();
    }

    [TestMethod]
    [DataRow("123456785")] // starts with 1
    [DataRow("712345678")] // starts with 7
    public void TryCreate_DoesNotStartWith8Or9_ReturnsFalse(string raw)
    {
        OrganizationNumber.TryCreate(raw, out _, out var error).Should().BeFalse();
        error.Should().Contain("8 or 9");
    }

    [TestMethod]
    [DataRow("919300389")] // wrong check digit
    [DataRow("919300387")] // wrong check digit
    public void TryCreate_WrongMod11_ReturnsFalse(string raw)
    {
        OrganizationNumber.TryCreate(raw, out _, out var error).Should().BeFalse();
        error.Should().Contain("MOD11");
    }

    [TestMethod]
    [DataRow("800000050")] // 8·3 + 0·2 + 0·7 + 0·6 + 0·5 + 0·4 + 0·3 + 5·2 = 34, 34 % 11 = 1
    [DataRow("800000053")] // same 8-digit prefix; any check digit is invalid when remainder is 1
    [DataRow("800000059")]
    public void TryCreate_Mod11RemainderEqualsOne_ReturnsFalse(string raw)
    {
        // The MOD11 algorithm rejects any orgnr whose first 8 weighted digits sum to a
        // remainder of 1 modulo 11 — the check digit would have been 10, which has no
        // single-digit representation. This branch (OrganizationNumber.cs IsValidMod11 → return false)
        // is otherwise impossible to exercise from "wrong check digit" inputs.
        OrganizationNumber.TryCreate(raw, out _, out var error).Should().BeFalse();
        error.Should().Contain("MOD11");
    }

    [TestMethod]
    public void Create_InvalidInput_Throws()
    {
        var act = () => OrganizationNumber.Create("12345");
        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void Equality_BasedOnNormalizedValue()
    {
        var a = OrganizationNumber.Create("919300388");
        var b = OrganizationNumber.Create("919 300 388");

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [TestMethod]
    public void ToString_ReturnsNormalizedValue()
    {
        var orgnr = OrganizationNumber.Create("919 300 388");
        orgnr.ToString().Should().Be("919300388");
    }
}
