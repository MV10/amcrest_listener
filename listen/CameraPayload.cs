
namespace listen;

/// <summary>
/// Event data received from the camera.
/// </summary>
internal class CameraPayload
{
    /// <summary>
    /// Name of the camera (from configuration).
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// When the event was parsed.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>
    /// Type of event.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// State of the event.
    /// </summary>
    public string Action { get; set; }

    /// <summary>
    /// Numeric indexer for cameras with parameterized events.
    /// </summary>
    public string Index { get; set; }

    /// <summary>
    /// JSON payload for events with detailed data.
    /// </summary>
    public string Data { get; set; }

    /// <summary>
    /// The event data as it was received before parsing.
    /// </summary>
    public string RawPayload { get; set; }
}
