
namespace listen;

public enum ReadingMode
{
    Idle,
    Discard,
    Header,
    StartContent,
    JsonContent,
}
