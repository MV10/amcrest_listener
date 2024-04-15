
namespace listen;

internal class CameraPayload
{
    public string Name { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Code { get; set; }
    public string Action { get; set; }
    public string Index { get; set; }
    public string Data { get; set; }
}
