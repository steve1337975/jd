using MaxMind.GeoIP2;
using NChecker.classes;
using NChecker.models;
using NChecker.utils;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
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
        private DatabaseReader reader;
        async internal Task Start()
        {
            await InitializeDatabaseAsync();

            bool isGitHub = Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";
            int currentChunk = int.Parse(Environment.GetEnvironmentVariable("GH_CHUNK") ?? "0");
            int totalChunks = int.Parse(Environment.GetEnvironmentVariable("GH_TOTAL_CHUNKS") ?? "1");

            int maxConcurrent = ConfigureThreadPool(isGitHub);

            Logger.Info($"Режим: {(isGitHub ? "GitHub Actions (Единый запуск)" : "Локальный ПК")}");
            Logger.Info($"Потоков выделено: {maxConcurrent}");

            var deadEndpoints = new ConcurrentDictionary<string, byte>();
            List<ServerData> finalAliveServers = new();

            var metrics = new ExecutionMetrics();
            var stopwatch = Stopwatch.StartNew();

            var channel = Channel.CreateBounded<ServerData>(new BoundedChannelOptions(50000)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = true,
                SingleReader = false
            });

            Task producerTask = StartProducer(channel.Writer, currentChunk, totalChunks, metrics);

            Logger.Info("Запуск асинхронного конвейера проверки...");
            await StartConsumer(channel.Reader, maxConcurrent, deadEndpoints, finalAliveServers, isGitHub, stopwatch, metrics);

            await producerTask;
            stopwatch.Stop();

            Logger.Info($"Проверка завершена за: {stopwatch.Elapsed.TotalSeconds:F2} сек.");
            Logger.Info($"Валидных серверов в этом инстансе: {finalAliveServers.Count}");

            await ExportResults(finalAliveServers);
        }

        private int ConfigureThreadPool(bool isGitHub)
        {
            int baseThreads = settingsManager.Settings.MaxConcurrentChecks > 0
                ? settingsManager.Settings.MaxConcurrentChecks
                : 1500;

            int maxConcurrent = isGitHub
                ? Math.Max(baseThreads / 2, 50)
                : baseThreads;

            ThreadPool.SetMinThreads(maxConcurrent + 100, maxConcurrent + 100);
            return maxConcurrent;
        }

        private Task StartProducer(ChannelWriter<ServerData> writer, int currentChunk, int totalChunks, ExecutionMetrics metrics)
        {
            return Task.Run(async () =>
            {
                int globalServerIndex = 0;
                int localTotalDiscovered = 0;
                try
                {
                    SubscriptionData[] subArray = await networkingManager.ParseSubscriptions(settingsManager.Settings, subscriptionsManager.GetSources());

                    List<ServerData> allParsed = new();
                    foreach (var subData in subArray)
                    {
                        allParsed.AddRange(subData.GetServers());
                    }

                    foreach (var serverData in allParsed)
                    {
                        if (globalServerIndex % totalChunks == currentChunk)
                        {
                            localTotalDiscovered++;
                            await writer.WriteAsync(serverData);
                        }
                        globalServerIndex++;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Ошибка продюсера => {ex.Message}");
                }
                finally
                {
                    metrics.SetTotalDiscovered(localTotalDiscovered);
                    writer.Complete();
                }
            });
        }

        private async Task StartConsumer(
            ChannelReader<ServerData> readerConnection,
            int maxConcurrent,
            ConcurrentDictionary<string, byte> deadEndpoints,
            List<ServerData> finalAliveServers,
            bool isGitHub,
            Stopwatch stopwatch,
            ExecutionMetrics metrics)
        {
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = maxConcurrent };

            await Parallel.ForEachAsync(readerConnection.ReadAllAsync(), parallelOptions, async (server, cancellationToken) =>
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
                    int currentChecked = metrics.IncrementCheckedCount();

                    if (settingsManager.Settings.Logging)
                    {
                        int logStep = isGitHub ? 1000 : 5000;
                        if (currentChecked % logStep == 0)
                        {
                            Logger.Info($"Проверено: {currentChecked} / ~{metrics.TotalDiscovered}... Время: {stopwatch.Elapsed.TotalSeconds:F0} сек.");
                        }
                    }
                }
            });
        }

        private async Task ExportResults(List<ServerData> finalAliveServers)
        {
            if (settingsManager.Settings.ShuffleServers && finalAliveServers.Count > 0)
            {
                Logger.Info("Перемешивание ФИНАЛЬНЫХ результатов для предотвращения кучности авторов...");
                RandomUtil.Shuffle(finalAliveServers);
            }

            ServerData[] black = finalAliveServers
                .Where(x => x.Subscription.FilterType == SubscriptionFilterType.Black)
                .ToArray();

            ServerData[] white = finalAliveServers
                .Where(x => x.Subscription.FilterType == SubscriptionFilterType.White || x.HasSniMasking())
                .ToArray();

            await exporterManager.ExportBlack(settingsManager.Settings, black);
            await exporterManager.ExportWhite(settingsManager.Settings, white);

            Logger.Info("Экспорт успешно завершен!");
        }

        private async Task InitializeDatabaseAsync()
        {
            try
            {
                using var client = new HttpClient();
                using var httpStream = await client.GetStreamAsync("https://raw.githack.com/wp-statistics/GeoLite2-Country/master/GeoLite2-Country.mmdb.gz");
                using var decompressionStream = new GZipStream(httpStream, CompressionMode.Decompress);
                using var memoryStream = new MemoryStream();

                await decompressionStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                reader = new DatabaseReader(memoryStream);

                Logger.Info("База GeoLite2 успешно загружена в память и готова к работе!");
            }
            catch (Exception ex)
            {
                Logger.Error($"Критическая ошибка при обновлении базы GeoIP => {ex.Message}");
            }
        }

        private class ExecutionMetrics
        {
            private int totalDiscovered;
            private int checkedCount;

            public int TotalDiscovered => Volatile.Read(ref totalDiscovered);

            public void SetTotalDiscovered(int value)
            {
                Interlocked.Exchange(ref totalDiscovered, value);
            }

            public int IncrementCheckedCount()
            {
                return Interlocked.Increment(ref checkedCount);
            }
        }
    }
}
