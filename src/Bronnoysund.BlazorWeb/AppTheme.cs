// SPDX-License-Identifier: MIT

using MudBlazor;

namespace Bronnoysund.BlazorWeb;

internal static class AppTheme
{
    public static MudTheme Instance { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#cc3333",
            PrimaryDarken = "#a82929",
            PrimaryLighten = "#d65c5c",
            AppbarBackground = "#cc3333",
            AppbarText = "#ffffff",
            Background = "#ffffff",
            Surface = "#ffffff",
            DrawerBackground = "#ffffff",
            DrawerText = "#333333",
            TextPrimary = "#333333",
            TextSecondary = "#666666",
            ActionDefault = "#666666",
            LinesDefault = "#cccccc",
            DividerLight = "#cccccc",
            TableLines = "#cccccc",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Roboto", "Verdana", "Arial", "Helvetica", "sans-serif"],
            },
        },
    };
}
