using Microsoft.AspNetCore.Components;

namespace ResidencyRoll.Web.Components;

public partial class DaysDistributionChart
{
    [Parameter]
    public List<DistributionData> DistributionData { get; set; } = new();
}
