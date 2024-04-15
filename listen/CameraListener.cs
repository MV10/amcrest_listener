
using System.Text;

namespace listen;

internal class CameraListener
{
    public CameraSettings Camera { get; set; }
    public HttpClient Client { get; set; }
    public HttpResponseMessage Response { get; set; }

    /*
    The camera sends messages prefixed with four lines (it's unclear how
    Content-Length relates to the actual payload, it doesn't seem to match):

        --myboundary
        Content-Type: text/plain
        Content-Length: ##
        [blank line]

    If we're receiving data but we didn't see the --myboundary then simply
    discard everything, we connected while output was already in progress, or
    we missed something, etc.

    After that header, either a single line payload will be sent:

        Code=VideoMotionInfo;action=State;index=0

    ...or a multi-line JSON payload, of which there are several; the presence
    of data={ indicates this type of payload:

        Code=SmartMotionVehicle;action=Start;index=0;data={
           "RegionName" : [ "Area1" ],
           "WindowId" : [ 0 ],
           "object" : [
              {
                 "Rect" : [ 4576, 5000, 4984, 5256 ],
                 "VehicleID" : 13421
              }
           ]
        }
        [blank line]

    */

    private ReadingMode Mode = ReadingMode.Idle;
    private StringBuilder Data = new StringBuilder();
    private int JsonCountdown;
    private readonly int MAX_JSON_LINES = 10;

    public async Task WaitForMessageAsync(CancellationToken cancellationToken)
    {
        using var stream = await Response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        while(!cancellationToken.IsCancellationRequested)
        {
            while (!reader.EndOfStream)
            {
                var text = (await reader.ReadLineAsync(cancellationToken)).Trim();

                var isHeader = text.Equals("--myboundary");

                switch(Mode)
                {
                    case ReadingMode.Idle:
                    {
                        Mode = isHeader ? ReadingMode.Header : ReadingMode.Discard;
                        break;
                    }

                    case ReadingMode.Header:
                    {
                        // look for blank line that indicates end of header
                        if (string.IsNullOrWhiteSpace(text)) Mode = ReadingMode.StartContent;
                        break;
                    }

                    case ReadingMode.StartContent:
                    {
                        if(isHeader)
                        {
                            Data.Clear();
                            Mode = ReadingMode.Header;
                            break;
                        }

                        Data.Append(text);

                        if (text.EndsWith(";data={"))
                        {
                            Mode = ReadingMode.JsonContent;
                            JsonCountdown = MAX_JSON_LINES;
                        }
                        else
                        {
                            Mode = ReadingMode.Idle;
                            ProcessData();
                        }

                        break;
                    }

                    case ReadingMode.JsonContent:
                    {
                        if (isHeader)
                        {
                            Data.Clear();
                            Mode = ReadingMode.Header;
                            break;
                        }

                        Data.Append(text);
                        if(text.Equals("}"))
                        {
                            // discard instead of idle because JSON has a trailing blank line (sigh)
                            Mode = ReadingMode.Discard;
                            ProcessData();
                        }
                        else
                        {
                            if(--JsonCountdown == 0)
                            {
                                Data.Clear();
                                Mode = ReadingMode.Discard;
                            }
                        }

                        break;
                    }

                    case ReadingMode.Discard:
                    {
                        if (isHeader) Mode = ReadingMode.Header;
                        break;
                    }
                }
            }
        }
    }

    private void ProcessData()
    {
        try
        {
            var payload = new CameraPayload()
            {
                Name = Camera.Name
            };

            var data = Data.ToString();
            Data.Clear();

            var jsonIndex = data.IndexOf("{");
            if (jsonIndex > -1)
            {
                payload.Data = data.Substring(jsonIndex);
                data = data.Substring(0, jsonIndex);
            }

            var fields = data.Split(";");
            payload.Code = fields[0].Split("=")[1];
            payload.Action = fields[1].Split("=")[1];
            payload.Index = fields[2].Split("=")[1];

            Program.Messages.Enqueue(payload);
        }
        catch { }
    }
}
