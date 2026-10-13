using MaxMind.GeoIP2;
using NChecker.classes;
using NChecker.models;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;

namespace NChecker
{
    internal class App
    {
        internal static App Instance { get; private set; } = new App();

        private readonly SettingsManager settingsManager = new SettingsManager();
        private readonly SubcriptionsManager subscriptionsManager = new SubcriptionsManager();
        private readonly NetworkingManager networkingManager = new NetworkingManager();
        private readonly ExporterManager exporterManager = new ExporterManager();
        private readonly DatabaseReader reader;

        internal App()
        {
            reader = new DatabaseReader(new MemoryStream(Properties.Resources.GeoLite2_Country));
        }

        async internal Task Start()
        {
            bool isGitHub = Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";
            string envChunk = Environment.GetEnvironmentVariable("GH_CHUNK") ?? "0";
            string envTotal = Environment.GetEnvironmentVariable("GH_TOTAL_CHUNKS") ?? "1";

            int currentChunk = int.Parse(envChunk);
            int totalChunks = int.Parse(envTotal);

            int baseThreads = settingsManager.Settings.MaxConcurrentChecks > 0
                ? settingsManager.Settings.MaxConcurrentChecks
                : 1500;

            int maxConcurrent = isGitHub
                ? Math.Max(baseThreads / 2, 50)
                : baseThreads;

            ThreadPool.SetMinThreads(maxConcurrent + 100, maxConcurrent + 100);

            var deadEndpoints = new ConcurrentDictionary<string, byte>();
            List<ServerData> finalAliveServers = new();

            int totalDiscovered = 0;
            int checkedCount = 0;

            Console.WriteLine($"[Инициализация] Режим: {(isGitHub ? "GitHub Actions (Единый запуск)" : "Локальный ПК")}");
            Console.WriteLine($"[Инициализация] Потоков выделено: {maxConcurrent} (исходя из базовых {baseThreads})");

            var stopwatch = Stopwatch.StartNew();

            var channel = Channel.CreateBounded<ServerData>(new BoundedChannelOptions(50000)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = true,
                SingleReader = false
            });

            Task producerTask = Task.Run(async () =>
            {
                int globalServerIndex = 0;
                try
                {
                    SubscriptionData[] subArray = await networkingManager.ParseSubscriptions(settingsManager.Settings, subscriptionsManager.Sources);

                    List<ServerData> allParsed = new();
                    foreach (var subData in subArray)
                    {
                        allParsed.AddRange(subData.GetServers());
                    }

                    foreach (var serverData in allParsed)
                    {
                        if (globalServerIndex % totalChunks == currentChunk)
                        {
                            Interlocked.Increment(ref totalDiscovered);
                            await channel.Writer.WriteAsync(serverData);
                        }
                        globalServerIndex++;
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
                    await networkingManager.ExecuteCountryTestAsync(server, reader);
                    await networkingManager.ExecuteTcpPingTestAsync(server, settingsManager.Settings);

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

                    if (settingsManager.Settings.Logging)
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

            if (settingsManager.Settings.ShuffleServers && finalAliveServers.Count > 0)
            {
                Console.WriteLine("Перемешивание ФИНАЛЬНЫХ результатов для предотвращения кучности авторов...");
                Utils.Shuffle(finalAliveServers);
            }

            ServerData[] black = finalAliveServers
                .Where(x => x.Subscription.FilterType == SubscriptionFilterType.Black)
                .ToArray();

            ServerData[] white = finalAliveServers
                .Where(x => x.Subscription.FilterType == SubscriptionFilterType.White)
                .ToArray();

            await exporterManager.ExportBlack(settingsManager.Settings, black);
            await exporterManager.ExportWhite(settingsManager.Settings, white);

            Console.WriteLine("Экспорт успешно завершен!");
        }
    }
}