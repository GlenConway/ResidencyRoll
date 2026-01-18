using Microsoft.AspNetCore.Components;

namespace ResidencyRoll.Web.Components;

public partial class TripTimeline
{
    [Parameter]
    public List<TimelineItem> TimelineItems { get; set; } = new();

    public class TimelineItem
    {
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string DurationText { get; set; } = string.Empty;
    }
}
