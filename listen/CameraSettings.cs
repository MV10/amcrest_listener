
namespace listen;

internal class CameraSettings
{
    public string Name { get; set; }
    public string Addr { get; set; }
    public string User { get; set; }
    public string Pass { get; set; }

    public string DisplayName => $"{Name} ({Addr})";
}
