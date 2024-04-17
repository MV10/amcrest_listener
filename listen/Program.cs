
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Net;

namespace listen;

internal class Program
{
    public static readonly string ConnectionTemplate = @"http://*/cgi-bin/eventManager.cgi?action=attach%26codes=%5BAll%5D";

    public static ConcurrentQueue<CameraPayload> Messages { get; } = new();

    static IConfigurationRoot configRoot;
    static ServiceProvider services;
    static List<CameraSettings> cameras;
    static List<CameraListener> listeners;

    static Task monitorAbort;
    static Task monitorQueue;
    static Task monitorToken;
    static List<Task> listenerTasks;
    static CancellationTokenSource cts = new();

    static async Task Main(string[] args)
    {
        if(args.Length > 1)
        {
            Console.WriteLine("Specify no arguments to connect to all cameras,\nor specify the name of a single camera.");
            Environment.Exit(-1);
        }

        // Basic setup
        GetCamerasFromConfig();
        GetCamerasToMonitor(args);
        
        // Establish connections
        await PrepareCameraListeners();

        // Processing loop
        await RunAsync();
    }

    static void GetCamerasFromConfig()
    {
        // Sadly, the ASP.NET Core tail wagging the dog means we can't just read
        // config without all this extra overhead (and packages). Starting to feel
        // awfully NPM-script-kiddie-dorkish around here lately.

        // I guess later on we could use the generic host, too... because reasons.
        // https://blog.xmi.fr/posts/dotnet-console-app-with-secrets/#sample-3-using-the-net-generic-host

        configRoot = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .Build();

        services = new ServiceCollection()
            .Configure<List<CameraSettings>>(configRoot.GetSection("Cameras"))
            .AddOptions()
            .BuildServiceProvider();

        cameras = services.GetService<IOptions<List<CameraSettings>>>().Value;
        if (cameras?.Count == 0)
        {
            Console.WriteLine("No cameras defined in configuration.");
            Environment.Exit(-1);
        }

        Console.WriteLine("Cameras:");
        foreach (var cam in cameras)
        {
            Console.WriteLine($"\t{cam.DisplayName}");
        }
    }

    static void GetCamerasToMonitor(string[] args)
    {
        // If a specific camera is requested, this validates the name, then discards all others.
        // Otherwise all of the cameras loaded from config are left unchanged.
        if (args.Length == 1)
        {
            var target = cameras.Where(c => c.Name.Equals(args[0], StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
            if (target is null)
            {
                Console.WriteLine("No camera by that name was found in configuration.");
                Environment.Exit(-1);
            }
            cameras.Clear();
            cameras.Add(target);
        }
    }

    static async Task PrepareCameraListeners()
    {
        listeners = new(cameras.Count);
        
        foreach(var cam in cameras)
        {
            Console.WriteLine($"Connecting to {cam.DisplayName}...");
            var listener = new CameraListener(cam);
            var err = await listener.TryConnect();
            if(string.IsNullOrWhiteSpace(err))
            {
                Console.WriteLine("\tSuccess");
                listeners.Add(listener);
            }
            else
            {
                Console.WriteLine(err);
            }
        }
    }

    static async Task WaitForEscapeKey(CancellationToken cancellationToken)
    {
        while(!cancellationToken.IsCancellationRequested)
        {
            if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape) cts.Cancel();
            if (cancellationToken.IsCancellationRequested) return;
            await Task.Yield();
        }
    }

    static async Task MonitorMessageQueue(CancellationToken cancellationToken)
    {
        while(!cancellationToken.IsCancellationRequested)
        {
            while(!Messages.IsEmpty)
            {
                if(Messages.TryDequeue(out var message))
                {
                    Console.WriteLine($"{message.Name} {message.Timestamp}: {message.Code} {message.Action}");
                }
            }
            await Task.Yield();
        }
    }

    static async Task RunAsync()
    {
        // Start and queue the non-camera tasks
        monitorToken = new CancellationTokenTaskSource<int>(cts.Token).Task;
        monitorAbort = Task.Run(() => WaitForEscapeKey(cts.Token));
        monitorQueue = Task.Run(() => MonitorMessageQueue(cts.Token));

        // Abort if we have multiple exceptions in rapid succession
        var exceptionCount = 0;
        var maxExceptions = 3;
        var clearExceptions = DateTime.MaxValue;
        var clearSeconds = 5;

        Console.WriteLine("\nPress ESC to exit\n");
        while(!cts.IsCancellationRequested)
        {
            try
            {
                // Create a list of the monitors and listeners and wait for one to finish
                var tasks = BuildTaskList();
                if (tasks.Count == 3) Console.WriteLine("\nNot connected to any cameras");
                await Task.WhenAny(tasks);
            }
            catch (Exception ex)
            {
                // Report exceptions and abort if the threshold is exceeded
                Console.WriteLine($"\n{ex}");
                if (DateTime.Now > clearExceptions)
                {
                    exceptionCount = 0;
                    clearExceptions = DateTime.Now.AddSeconds(clearSeconds);
                }
                else
                {
                    if (exceptionCount == 0) clearExceptions = DateTime.Now.AddSeconds(clearSeconds);
                }
                exceptionCount++;
                if (exceptionCount > maxExceptions)
                {
                    Console.WriteLine($"\nAborting: exception threshold exceeded ({maxExceptions} in {clearSeconds} seconds)");
                    cts.Cancel();
                }
            }
        }
    }

    static List<Task> BuildTaskList()
    {
        List<Task> tasks = new(listeners.Count + 3)
        {
            monitorToken,
            monitorAbort,
            monitorQueue,
        };

        if (listenerTasks is null)
        {
            // On first run, start all listeners
            listenerTasks = listeners.Select(c => Task.Run(() => c.WaitForMessageAsync(cts.Token))).ToList();
        }
        else
        {
            // On subsequent runs, find completed listeners, remove, and restart

            // TODO add listener disconnect / error handling (disposal?)
            // TODO decide how to simulate individual listener disconnect/fail/etc.
            // TODO find completed listeners, remove each from task list
            // TODO check connection state and re-connect if necessary
            // TODO start each new task, add to task list
        }

        tasks.AddRange(listenerTasks);

        return tasks;
    }

}
