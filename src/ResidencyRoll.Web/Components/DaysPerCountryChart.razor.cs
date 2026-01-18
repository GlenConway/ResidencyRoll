using Microsoft.AspNetCore.Components;

namespace ResidencyRoll.Web.Components;

public partial class DaysPerCountryChart
{
    [Parameter]
    public List<ChartData> ChartData { get; set; } = new();
}
