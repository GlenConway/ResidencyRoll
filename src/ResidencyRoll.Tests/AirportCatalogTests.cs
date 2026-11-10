using ResidencyRoll.Api.Services;
using Xunit;

namespace ResidencyRoll.Tests;

public class AirportCatalogTests
{
    private readonly AirportCatalog _catalog = new();

    [Fact]
    public void All_ShouldLoadTheSharedCatalogue()
    {
        Assert.True(_catalog.All.Count > 5000);
    }

    [Fact]
    public void FindByIataCode_ShouldIgnoreCase()
    {
        Assert.Equal("Toronto", _catalog.FindByIataCode("yyz")?.City);
        Assert.Equal("Gold Coast", _catalog.FindByIataCode("OOL")?.City);
        Assert.Equal("Australia/Brisbane", _catalog.FindByIataCode("ool")?.IanaTimezone);
        Assert.Equal("Canberra", _catalog.FindByIataCode("CBR")?.City);
        Assert.Equal("Casablanca", _catalog.FindByIataCode("CMN")?.City);
        Assert.Equal("Morocco", _catalog.FindByIataCode("CMN")?.Country);
        Assert.Null(_catalog.FindByIataCode("NOPE"));
    }

    [Fact]
    public void Search_ShouldRankAnExactCodeFirst()
    {
        Assert.Equal("YUL", _catalog.Search("YUL")[0].IataCode);
        Assert.Equal("YYZ", _catalog.Search("pearson")[0].IataCode);
        Assert.All(_catalog.Search("united kingdom"), airport => Assert.Equal("United Kingdom", airport.Country));
    }
}
