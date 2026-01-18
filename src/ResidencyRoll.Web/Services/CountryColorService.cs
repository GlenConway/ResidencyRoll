using System.Collections.Generic;
using System.Linq;

namespace ResidencyRoll.Web.Services;

/// <summary>
/// Service for managing country-specific colors in the timeline view.
/// Provides a consistent color palette for different countries and supports
/// home country designation for visual distinction.
/// </summary>
public class CountryColorService
{
    /// <summary>
    /// A predefined palette of distinct colors for countries
    /// </summary>
    private static readonly List<string> ColorPalette = new()
    {
        "#1f77b4", // blue
        "#ff7f0e", // orange
        "#2ca02c", // green
        "#d62728", // red
        "#9467bd", // purple
        "#8c564b", // brown
        "#e377c2", // pink
        "#7f7f7f", // gray
        "#bcbd22", // olive
        "#17becf", // cyan
        "#aec7e8", // light blue
        "#ffbb78", // light orange
        "#98df8a", // light green
        "#ff9896", // light red
        "#c5b0d5", // light purple
        "#c49c94", // light brown
        "#f7b6d2", // light pink
        "#c7c7c7", // light gray
        "#dbbd22", // darker olive
        "#9edae5", // darker cyan
    };

    /// <summary>
    /// Mapping of country names to their assigned colors
    /// </summary>
    private Dictionary<string, string> _countryColorMap = new();

    /// <summary>
    /// The currently designated home country (displayed with a distinct appearance)
    /// </summary>
    public string? HomeCountry { get; set; }

    /// <summary>
    /// Gets the color for a specific country. Assigns a color if not already assigned.
    /// Home country gets a special saturated version of its color.
    /// </summary>
    public string GetCountryColor(string country)
    {
        if (string.IsNullOrEmpty(country))
            return "#808080"; // gray for unknown

        if (!_countryColorMap.ContainsKey(country))
        {
            // Assign a color from the palette based on existing entries
            var colorIndex = _countryColorMap.Count % ColorPalette.Count;
            _countryColorMap[country] = ColorPalette[colorIndex];
        }

        return _countryColorMap[country];
    }

    /// <summary>
    /// Gets the color with adjusted brightness for a country (used for borders/highlights)
    /// </summary>
    public string GetCountryColorDark(string country)
    {
        var color = GetCountryColor(country);
        return DarkenColor(color, 0.8);
    }

    /// <summary>
    /// Sets a specific color for a country
    /// </summary>
    public void SetCountryColor(string country, string hexColor)
    {
        _countryColorMap[country] = hexColor;
    }

    /// <summary>
    /// Gets all known country colors
    /// </summary>
    public Dictionary<string, string> GetCountryColors() => new(_countryColorMap);

    /// <summary>
    /// Registers multiple countries to ensure consistent color assignment
    /// </summary>
    public void RegisterCountries(IEnumerable<string> countries)
    {
        foreach (var country in countries)
        {
            if (!_countryColorMap.ContainsKey(country))
            {
                // Pre-register to ensure consistent ordering
                var colorIndex = _countryColorMap.Count % ColorPalette.Count;
                _countryColorMap[country] = ColorPalette[colorIndex];
            }
        }
    }

    /// <summary>
    /// Clears all registered colors and the home country
    /// </summary>
    public void Reset()
    {
        _countryColorMap.Clear();
        HomeCountry = null;
    }

    /// <summary>
    /// Darkens a hex color by a multiplier (0-1)
    /// </summary>
    private static string DarkenColor(string hexColor, double factor)
    {
        if (!hexColor.StartsWith("#") || hexColor.Length != 7)
            return hexColor;

        var r = int.Parse(hexColor.Substring(1, 2), System.Globalization.NumberStyles.HexNumber);
        var g = int.Parse(hexColor.Substring(3, 2), System.Globalization.NumberStyles.HexNumber);
        var b = int.Parse(hexColor.Substring(5, 2), System.Globalization.NumberStyles.HexNumber);

        r = (int)(r * factor);
        g = (int)(g * factor);
        b = (int)(b * factor);

        return $"#{r:X2}{g:X2}{b:X2}";
    }

    /// <summary>
    /// Gets a semi-transparent version of a color for background use
    /// </summary>
    public static string GetColorWithAlpha(string hexColor, double alpha = 0.1)
    {
        if (!hexColor.StartsWith("#") || hexColor.Length != 7)
            return hexColor;

        var r = int.Parse(hexColor.Substring(1, 2), System.Globalization.NumberStyles.HexNumber);
        var g = int.Parse(hexColor.Substring(3, 2), System.Globalization.NumberStyles.HexNumber);
        var b = int.Parse(hexColor.Substring(5, 2), System.Globalization.NumberStyles.HexNumber);

        return $"rgba({r}, {g}, {b}, {alpha})";
    }
}
