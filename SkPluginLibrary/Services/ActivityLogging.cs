using Microsoft.Extensions.Logging;
using System.Diagnostics;


namespace SkPluginLibrary.Services;


public class ActivityLogging
{
    private readonly ILogger<ActivityLogging> _logger;

    public ActivityLogging(ILogger<ActivityLogging> logger)
    {
        _logger = logger;

        var listener = new ActivityListener
        {
            // Define what happens when an activity starts
            ActivityStarted = activity =>
            {
                var activityDisplayName = activity.DisplayName;
                _logger.LogInformation("Activity Started: {activityDisplayName}", activityDisplayName);
            },
            // Define what happens when an activity stops
            ActivityStopped = activity =>
            {
                var activityDisplayName = activity.DisplayName;
                _logger.LogInformation("Activity Stopped: {activityDisplayName}", activityDisplayName);
            },
            // Define the sampling behavior
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
            // Define the activity source predicate
            ShouldListenTo = activitySource => activitySource.Name.StartsWith("Microsoft.SemanticKernel", StringComparison.Ordinal)
        };

        // Subscribe to the listener
        ActivitySource.AddActivityListener(listener);
    }
}
