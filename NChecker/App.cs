using MaxMind.GeoIP2;
using NChecker.classes;
using NChecker.models;
using NChecker.models.local;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;

namespace NChecker
{
    internal class App
    {
        internal static App Instance { get; private set; } = new App();

        SettingsData settings;
        SettingsManager settingsManager = new SettingsManager();
        SubcriptionsManager subscriptionsManager = new SubcriptionsManager();
        NetworkingManager networkingManager = new NetworkingManager();
        ExporterManager exporterManager = new ExporterManager();
        DatabaseReader reader;

        internal App()
        {
            reader = new DatabaseReader(new MemoryStream(Properties.Resources.GeoLite2_Country));
            settings = settingsManager.Settings;
        }

        async internal Task Start()
        {
            bool isGitHub = Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";

            int maxConcurrent = isGitHub ? 150 : (settings.MaxConcurrentChecks > 0 ? settings.MaxConcurrentChecks : 1500);
            ThreadPool.SetMinThreads(maxConcurrent + 50, maxConcurrent + 50);

            var deadEndpoints = new ConcurrentDictionary<string, byte>();
            List<ServerData> finalAliveServers = new();

            int totalDiscovered = 0;
            int checkedCount = 0;

            Console.WriteLine($"[Инициализация] Режим: {(isGitHub ? "GitHub Actions (Единый запуск)" : "Локальный ПК")}");
            Console.WriteLine($"[Инициализация] Потоков выделено: {maxConcurrent}");

            var stopwatch = Stopwatch.StartNew();

            var channel = Channel.CreateBounded<ServerData>(new BoundedChannelOptions(50000)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = true,
                SingleReader = false
            });

            Task producerTask = Task.Run(async () =>
            {
                try
                {
                    SubscriptionData[] subArray = await networkingManager.ParseSubscriptions(settings, subscriptionsManager.Sources);

                    List<ServerData> allParsed = new();
                    foreach (var subData in subArray)
                    {
                        allParsed.AddRange(subData.GetServers());
                    }

                    totalDiscovered = allParsed.Count;

                    foreach (var serverData in allParsed)
                    {
                        await channel.Writer.WriteAsync(serverData);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Ошибка продюсера]: {ex.Message}");
                }
                finally
                {
                    channel.Writer.Complete();
                }
            });

            Console.WriteLine("Запуск асинхронного конвейера проверки...");
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = maxConcurrent };

            await Parallel.ForEachAsync(channel.Reader.ReadAllAsync(), parallelOptions, async (server, cancellationToken) =>
            {
                string endpointKey = $"{server.Address}:{server.Port}";

                if (deadEndpoints.ContainsKey(endpointKey)) return;

                try
                {
                    await server.TestCountry(reader);
                    await server.TestTcpPing(settings);

                    if (server.IsAlive)
                    {
                        lock (finalAliveServers)
                        {
                            finalAliveServers.Add(server);
                        }
                    }
                    else
                    {
                        deadEndpoints.TryAdd(endpointKey, 0);
                    }
                }
                catch
                {
                    deadEndpoints.TryAdd(endpointKey, 0);
                }
                finally
                {
                    int currentChecked = Interlocked.Increment(ref checkedCount);

                    if (settings.Logging)
                    {
                        int logStep = isGitHub ? 1000 : 5000;
                        if (currentChecked % logStep == 0)
                        {
                            Console.WriteLine($"[Прогресс] Проверено: {currentChecked} / ~{Volatile.Read(ref totalDiscovered)}... Время: {stopwatch.Elapsed.TotalSeconds:F0} сек.");
                        }
                    }
                }
            });

            await producerTask;
            stopwatch.Stop();

            Console.WriteLine($"\n[Финиш] Проверка завершена за: {stopwatch.Elapsed.TotalSeconds:F2} сек.");
            Console.WriteLine($"Валидных серверов в этом инстансе: {finalAliveServers.Count}");

            if (settings.ShuffleServers && finalAliveServers.Count > 0) Utils.Shuffle(finalAliveServers);

            ServerData[] black = finalAliveServers
                .Where(x => x.Subscription.FilterType == SubscriptionFilterType.Black)
                .OrderBy(x => x.TcpPing)
                .ToArray();
            ServerData[] white = finalAliveServers
                .Where(x => x.Subscription.FilterType == SubscriptionFilterType.White)
                .OrderBy(x => x.TcpPing)
                .ToArray();
            await exporterManager.ExportBlack(settings, black);
            await exporterManager.ExportWhite(settings, white);

            Console.WriteLine("Экспорт успешно завершен!");
        }
    }
}
