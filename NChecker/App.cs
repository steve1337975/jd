using NChecker.classes;
using NChecker.models;
using NChecker.models.subscription;
using NChecker.utils;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;

namespace NChecker
{
    internal class App
    {
        internal static App Instance { get; private set; } = new App();
        private readonly ExporterManager exporterManager = new ExporterManager();
        private readonly NetworkingManager networkingManager = new NetworkingManager();
        private readonly FilesManager filesManager = new FilesManager();
        private SubscriptionData[] subscriptions;

        internal App() { }

        internal async Task Start()
        {
            await networkingManager.Init();

            bool isGitHub = Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";
            int currentChunk = int.Parse(Environment.GetEnvironmentVariable("GH_CHUNK") ?? "0");
            int totalChunks = int.Parse(Environment.GetEnvironmentVariable("GH_TOTAL_CHUNKS") ?? "1");

            int maxConcurrent = ConfigureThreadPool(isGitHub);

            Logger.Info($"Режим: {(isGitHub ? "GitHub Actions (Единый запуск)" : "Локальный ПК")}");
            Logger.Info($"Потоков выделено: {maxConcurrent}");

            var deadEndpoints = new ConcurrentDictionary<string, byte>();
            var finalAliveServers = new ConcurrentBag<ServerData>();

            var metrics = new ExecutionMetrics();
            var stopwatch = Stopwatch.StartNew();

            // Увеличиваем емкость канала до 200 000 для поддержки сотен тысяч серверов
            var channel = Channel.CreateBounded<ServerData>(new BoundedChannelOptions(200000)
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
            int baseThreads = filesManager.Settings.MaxConcurrentChecks > 0
                ? filesManager.Settings.MaxConcurrentChecks
                : 1500;

            int maxConcurrent = isGitHub
                ? Math.Max(baseThreads / 2, 100)
                : Math.Min(baseThreads, 2000); // Ограничиваем разумным пределом во избежание нехватки сокетов

            ThreadPool.SetMinThreads(maxConcurrent + 200, maxConcurrent + 200);
            return maxConcurrent;
        }

        private Task StartProducer(ChannelWriter<ServerData> writer, int currentChunk, int totalChunks, ExecutionMetrics metrics)
        {
            return Task.Run(async () =>
            {
                int localTotalDiscovered = 0;
                try
                {
                    subscriptions = await networkingManager.ParseSubscriptions(filesManager.Settings, filesManager.GetSources());

                    var allParsed = subscriptions
                        .Where(sub => sub?.Servers != null)
                        .SelectMany(sub => sub.Servers)
                        .ToList();

                    int totalCount = allParsed.Count;

                    // Молниеносный и надежный обход гигантского списка без лишней магии
                    for (int i = 0; i < totalCount; i++)
                    {
                        if (totalChunks <= 1 || i % totalChunks == currentChunk)
                        {
                            localTotalDiscovered++;
                            await writer.WriteAsync(allParsed[i]);
                        }
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
                    Logger.Info($"Продюсер завершил работу. Отправлено в конвейер: {localTotalDiscovered}");
                }
            });
        }

        private async Task StartConsumer(
            ChannelReader<ServerData> readerConnection,
            int maxConcurrent,
            ConcurrentDictionary<string, byte> deadEndpoints,
            ConcurrentBag<ServerData> finalAliveServers,
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
                    await networkingManager.ExecuteCountryTestAsync(server);
                    await networkingManager.ExecuteTcpPingTestAsync(server, filesManager.Settings);

                    if (server.IsAlive)
                    {
                        finalAliveServers.Add(server);
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

                    if (filesManager.Settings.Logging)
                    {
                        int logStep = isGitHub ? 5000 : 10000;
                        if (currentChecked % logStep == 0)
                        {
                            Logger.Info($"Проверено: {currentChecked} / ~{metrics.TotalDiscovered}... Время: {stopwatch.Elapsed.TotalSeconds:F0} сек.");
                        }
                    }
                }
            });
        }

        private async Task ExportResults(ConcurrentBag<ServerData> finalAliveServers)
        {
            var aliveServersSet = new HashSet<ServerData>(finalAliveServers);

            foreach (var sub in subscriptions)
            {
                sub.Servers = sub.Servers
                    .Where(s => aliveServersSet.Contains(s))
                    .OrderBy(s => s.SortedIndex)
                    .ToList();
            }

            await exporterManager.Export(filesManager.Settings, subscriptions);
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
