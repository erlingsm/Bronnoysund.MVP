// SPDX-License-Identifier: AGPL-3.0-or-later OR LicenseRef-Commercial

using System.Reflection;
using System.Xml.Linq;
using Bronnoysund.Lookup.ViewModels.Resources;
using FluentAssertions;

namespace Bronnoysund.Lookup.ViewModels.Tests;

/// <summary>Catches forgotten translations: every key in the default .resx must exist in nb-NO and nn-NO.</summary>
public sealed class SharedResourcesParityTests
{
    private static readonly Lazy<string> ResourcesDir = new(() =>
    {
        // tests/.../bin/Debug/net10.0  →  go up four levels to repo root, then into the resources folder.
        var asmDir = Path.GetDirectoryName(typeof(SharedResources).Assembly.Location)!;
        var current = new DirectoryInfo(asmDir);
        while (current is not null && current.GetDirectories("src").Length == 0)
            current = current.Parent;
        if (current is null) throw new InvalidOperationException("Couldn't find repo root.");
        return Path.Combine(current.FullName, "src", "Bronnoysund.Lookup.ViewModels", "Resources");
    });

    [Theory]
    [InlineData("nb-NO")]
    [InlineData("nn-NO")]
    public void Culture_resx_has_every_key_from_default(string culture)
    {
        var defaultKeys = LoadKeys(Path.Combine(ResourcesDir.Value, "SharedResources.resx"));
        var cultureKeys = LoadKeys(Path.Combine(ResourcesDir.Value, $"SharedResources.{culture}.resx"));

        var missing = defaultKeys.Except(cultureKeys).ToList();
        missing.Should().BeEmpty($"these keys are not translated in {culture}: {string.Join(", ", missing)}");
    }

    [Theory]
    [InlineData("nb-NO")]
    [InlineData("nn-NO")]
    public void Culture_resx_does_not_have_extra_keys(string culture)
    {
        var defaultKeys = LoadKeys(Path.Combine(ResourcesDir.Value, "SharedResources.resx"));
        var cultureKeys = LoadKeys(Path.Combine(ResourcesDir.Value, $"SharedResources.{culture}.resx"));

        var extra = cultureKeys.Except(defaultKeys).ToList();
        extra.Should().BeEmpty($"these keys in {culture} have no default fallback: {string.Join(", ", extra)}");
    }

    private static HashSet<string> LoadKeys(string path)
    {
        var doc = XDocument.Load(path);
        return doc.Root!.Elements("data")
            .Select(e => e.Attribute("name")!.Value)
            .ToHashSet();
    }
}
