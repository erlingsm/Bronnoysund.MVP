// SPDX-License-Identifier: AGPL-3.0-or-later OR LicenseRef-Commercial

using System.Globalization;
using Bronnoysund.Lookup.ViewModels.Resources;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace Bronnoysund.Lookup.ViewModels.Tests;

/// <summary>Verify that localized strings flow through the view-model and switch with CurrentUICulture.</summary>
public sealed class CompanyLookupViewModelTests
{
    private static IStringLocalizer<SharedResources> NewLocalizer()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization();
        var sp = services.BuildServiceProvider();
        return sp.GetRequiredService<IStringLocalizer<SharedResources>>();
    }

    [Fact]
    public void Localizer_returns_english_by_default()
    {
        var prev = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("en");
            var loc = NewLocalizer();
            loc["EnterOrgNumber"].Value.Should().Be("Enter an organization number.");
        }
        finally { CultureInfo.CurrentUICulture = prev; }
    }

    [Fact]
    public void Localizer_returns_bokmal_when_culture_set()
    {
        var prev = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("nb-NO");
            var loc = NewLocalizer();
            loc["EnterOrgNumber"].Value.Should().Be("Skriv inn et organisasjonsnummer.");
        }
        finally { CultureInfo.CurrentUICulture = prev; }
    }

    [Fact]
    public void Localizer_returns_nynorsk_when_culture_set()
    {
        var prev = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("nn-NO");
            var loc = NewLocalizer();
            loc["EnterOrgNumber"].Value.Should().Be("Skriv inn eit organisasjonsnummer.");
        }
        finally { CultureInfo.CurrentUICulture = prev; }
    }

    [Fact]
    public void Localizer_formats_with_arguments()
    {
        var prev = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("en");
            var loc = NewLocalizer();
            loc["NotFoundForOrgNumber", "974760843"].Value
                .Should().Be("No company with organization number 974760843 was found.");
        }
        finally { CultureInfo.CurrentUICulture = prev; }
    }
}
