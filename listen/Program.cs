﻿
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Net;

namespace listen;

internal class Program
{
    static readonly string connectionTemplate = @"http://*/cgi-bin/eventManager.cgi?action=attach%26codes=%5BAll%5D";

    static IConfigurationRoot configRoot;
    static ServiceProvider services;
    static List<CameraSettings> cameras;
    static List<CameraListener> listeners;
    static CancellationTokenSource cts = new();

    public static ConcurrentQueue<CameraPayload> Messages { get; } = new();

    static async Task Main(string[] args)
    {
        if(args.Length > 1)
        {
            Console.WriteLine("Specify no arguments to connect to all cameras,\nor specify the name of a single camera.");
            Environment.Exit(-1);
        }

        GetCamerasFromConfig();
        GetCamerasToMonitor(args);
        await PrepareCameraListeners();

        List<Task> tasks = new(listeners.Count + 3)
        {
            Task.Run(() => WaitForEscapeKey(cts.Token)),
            Task.Run(() => MonitorMessageQueue(cts.Token)),
            new CancellationTokenTaskSource<int>(cts.Token).Task,
        };
        tasks.AddRange(listeners.Select(c => Task.Run(() => c.WaitForMessageAsync(cts.Token))).ToList());

        await Task.WhenAny(tasks);
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
            var handler = new HttpClientHandler()
            {
                Credentials = new NetworkCredential(cam.User, cam.Pass)
            };

            var listener = new CameraListener
            {
                Camera = cam,
                Client = new(handler)
                {
                    Timeout = Timeout.InfiniteTimeSpan
                }
            };

            Console.WriteLine($"Connecting to {cam.DisplayName}...");

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, connectionTemplate.Replace("*", cam.Addr));

                listener.Response = await listener.Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                if(!listener.Response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"\tFailed, status {listener.Response.StatusCode}");
                }
                else
                {
                    Console.WriteLine("\tConnected");
                    listeners.Add(listener);
                }
            }
            catch(HttpRequestException)
            {
                Console.WriteLine("\tNo response");
            }
            catch(Exception ex)
            {
                Console.WriteLine($"\tFailed, {ex.GetType()}");
            }
        }
    }

    static async Task WaitForEscapeKey(CancellationToken cancellationToken)
    {
        while(!Console.KeyAvailable && !cancellationToken.IsCancellationRequested)
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
                    Console.WriteLine($"{message.Name} {message.Timestamp}: {message.Code}");
                }
            }
            await Task.Yield();
        }
    }
}