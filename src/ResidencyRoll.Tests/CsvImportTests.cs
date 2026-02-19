using System;
using System.Collections.Generic;
using System.Globalization;
using Xunit;

namespace ResidencyRoll.Tests;

public class CsvImportTests
{
    /// <summary>
    /// Parse a CSV line, handling quoted values
    /// </summary>
    private static string[] ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        values.Add(current.ToString());
        return values.ToArray();
    }

    private static bool IsValidIanaTimezone(string? timezone)
    {
        if (string.IsNullOrWhiteSpace(timezone))
            return false;

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timezone);
            return true;
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public void ParseCsvLine_WithValidTripsData_ParsesCorrectly()
    {
        // Arrange
        var line = "Canada,Halifax,2025-02-07 10:40,America/Halifax,YHZ,United Kingdom,London,2025-02-07 20:30,Europe/London,LHR";

        // Act
        var parts = ParseCsvLine(line);

        // Assert
        Assert.Equal(10, parts.Length);
        Assert.Equal("Canada", parts[0]);
        Assert.Equal("Halifax", parts[1]);
        Assert.Equal("2025-02-07 10:40", parts[2]);
        Assert.Equal("America/Halifax", parts[3]);
        Assert.Equal("YHZ", parts[4]);
        Assert.Equal("United Kingdom", parts[5]);
        Assert.Equal("London", parts[6]);
        Assert.Equal("2025-02-07 20:30", parts[7]);
        Assert.Equal("Europe/London", parts[8]);
        Assert.Equal("LHR", parts[9]);
    }

    [Fact]
    public void ParseDatetime_WithValidFormat_Succeeds()
    {
        // Arrange
        var dateString = "2025-02-07 10:40";

        // Act
        var success = DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result);

        // Assert
        Assert.True(success, $"Failed to parse date: {dateString}");
        Assert.Equal(2025, result.Year);
        Assert.Equal(2, result.Month);
        Assert.Equal(7, result.Day);
        Assert.Equal(10, result.Hour);
        Assert.Equal(40, result.Minute);
    }

    [Theory]
    [InlineData("America/Halifax")]
    [InlineData("Europe/London")]
    [InlineData("Australia/Sydney")]
    [InlineData("America/Toronto")]
    [InlineData("America/New_York")]
    public void TimezoneValidation_WithValidTimezones_ReturnsTrue(string timezone)
    {
        // Act
        var isValid = IsValidIanaTimezone(timezone);

        // Assert
        Assert.True(isValid, $"Timezone '{timezone}' should be valid");
    }

    [Theory]
    [InlineData("AST")]
    [InlineData("PST")]
    [InlineData("UTC+5")]
    public void TimezoneValidation_WithInvalidTimezones_ReturnsFalse(string timezone)
    {
        // Act
        var isValid = IsValidIanaTimezone(timezone);

        // Assert
        Assert.False(isValid, $"Timezone '{timezone}' should be invalid");
    }

    [Fact]
    public void ImportLogic_WithYourCsvData_ParsesAllRows()
    {
        // Arrange - Your actual CSV data
        var csvContent = """
DepartureCountry,DepartureCity,DepartureDateTime,DepartureTimezone,DepartureIataCode,ArrivalCountry,ArrivalCity,ArrivalDateTime,ArrivalTimezone,ArrivalIataCode
Canada,Halifax,2025-02-07 10:40,America/Halifax,YHZ,United Kingdom,London,2025-02-07 20:30,Europe/London,LHR
United Kingdom,London,2025-03-30 10:40,Europe/London,LHR,Canada,Halifax,2025-03-30 20:30,America/Halifax,YHZ
Canada,Halifax,2025-04-13 10:40,America/Halifax,YHZ,United Kingdom,London,2025-04-13 20:30,Europe/London,LHR
United Kingdom,London,2025-05-04 10:40,Europe/London,LHR,Canada,Halifax,2025-05-04 20:30,America/Halifax,YHZ
Canada,Halifax,2025-05-24 10:40,America/Halifax,YHZ,United Kingdom,London,2025-05-24 20:30,Europe/London,LHR
United Kingdom,London,2025-06-07 10:40,Europe/London,LHR,Canada,Halifax,2025-06-07 20:30,America/Halifax,YHZ
Canada,Halifax,2025-06-28 10:40,America/Halifax,YHZ,United Kingdom,London,2025-06-28 20:30,Europe/London,LHR
United Kingdom,London,2025-07-07 10:40,Europe/London,LHR,Canada,Halifax,2025-07-07 20:30,America/Halifax,YHZ
Canada,Halifax,2025-08-06 10:40,America/Halifax,YHZ,United Kingdom,London,2025-08-06 20:30,Europe/London,LHR
United Kingdom,London,2025-09-03 10:40,Europe/London,LHR,Canada,Halifax,2025-09-03 20:30,America/Halifax,YHZ
Canada,Halifax,2025-09-19 10:40,America/Halifax,YHZ,United Kingdom,London,2025-09-19 20:30,Europe/London,LHR
United Kingdom,London,2025-10-05 10:40,Europe/London,LHR,Canada,Halifax,2025-10-05 20:30,America/Halifax,YHZ
Canada,Halifax,2025-10-21 10:40,America/Halifax,YHZ,United Kingdom,London,2025-10-21 20:30,Europe/London,LHR
United Kingdom,London,2025-10-27 10:40,Europe/London,LHR,Canada,Halifax,2025-10-27 20:30,America/Halifax,YHZ
Canada,Halifax,2025-12-05 10:40,America/Halifax,YHZ,United Kingdom,London,2025-12-05 20:30,Europe/London,LHR
United Kingdom,London,2025-12-15 10:40,Europe/London,LHR,Canada,Halifax,2025-12-15 20:30,America/Halifax,YHZ
Canada,Halifax,2025-12-23 11:00,America/Halifax,YHZ,Australia,Sydney,2025-12-24 07:00,Australia/Sydney,SYD
Australia,Sydney,2026-01-15 11:00,Australia/Sydney,SYD,Canada,Halifax,2026-01-16 07:00,America/Halifax,YHZ
""";

        var lines = csvContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var newTrips = new List<object>();
        var isFirst = true;
        var invalidRows = new List<string>();
        var parseErrors = new List<string>();

        // Act
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (isFirst && line.StartsWith("DepartureCountry", StringComparison.OrdinalIgnoreCase))
            {
                isFirst = false;
                continue;
            }

            isFirst = false;

            var parts = ParseCsvLine(line);
            parseErrors.Add($"Row has {parts.Length} columns");

            if (parts.Length >= 10)
            {
                if (!DateTime.TryParse(parts[2], CultureInfo.InvariantCulture, DateTimeStyles.None, out var departureDateTime))
                {
                    invalidRows.Add($"Invalid departure date: {parts[2]}");
                    continue;
                }

                if (!DateTime.TryParse(parts[7], CultureInfo.InvariantCulture, DateTimeStyles.None, out var arrivalDateTime))
                {
                    invalidRows.Add($"Invalid arrival date: {parts[7]}");
                    continue;
                }

                if (!IsValidIanaTimezone(parts[3]))
                {
                    invalidRows.Add($"Invalid departure timezone: {parts[3]}");
                    continue;
                }

                if (!IsValidIanaTimezone(parts[8]))
                {
                    invalidRows.Add($"Invalid arrival timezone: {parts[8]}");
                    continue;
                }

                newTrips.Add(new { DepartureCountry = parts[0], ArrivalCountry = parts[5] });
            }
            else
            {
                invalidRows.Add($"Row has incorrect column count (got {parts.Length}, expected 10)");
            }
        }

        // Assert
        Assert.Empty(invalidRows);
        Assert.Equal(18, newTrips.Count); // All 18 data rows should parse
        
        // Show column counts for debugging
        var message = string.Join("\n", parseErrors.Take(3));
        Assert.NotEmpty(parseErrors);
    }
}
