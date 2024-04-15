
namespace listen;

/// <summary>
/// Defines how a listener is accumulating lines of data sent by the camera.
/// </summary>
public enum ReadingMode
{
    /// <summary>
    /// Inactive, waiting for a header boundary.
    /// </summary>
    Idle,

    /// <summary>
    /// Reading a header section, waiting for a blank line which
    /// will transition to StartContent mode.
    /// </summary>
    Header,

    /// <summary>
    /// Receiving an event string. Transitions back to idle mode unless
    /// the string ends with a data key, in which case JSON content is
    /// expected to follow.
    /// </summary>
    StartContent,

    /// <summary>
    /// Reading an event string that includes JSON content. Transitions
    /// to discard mode when the closing brace is found or when a maximum
    /// line limit has been read.
    /// </summary>
    JsonContent,

    /// <summary>
    /// Reading and ignoring content until a new header boundary is seen.
    /// </summary>
    Discard,
}
