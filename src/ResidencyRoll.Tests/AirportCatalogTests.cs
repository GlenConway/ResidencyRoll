using ResidencyRoll.Api.Services;
using Xunit;

namespace ResidencyRoll.Tests;

public class AirportCatalogTests
{
    private readonly AirportCatalog _catalog = new();

    [Fact]
    public void All_ShouldLoadTheSharedCatalogue()
    {
        Assert.Equal(109, _catalog.All.Count);
    }

    [Fact]
    public void FindByIataCode_ShouldIgnoreCase()
    {
        Assert.Equal("Toronto", _catalog.FindByIataCode("yyz")?.City);
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
