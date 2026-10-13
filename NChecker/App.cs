using MaxMind.GeoIP2;
using NChecker.classes;
using NChecker.models;
using NChecker.models.local;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
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
            // ==========================================
            // 1. ИНИЦИАЛИЗАЦИЯ И НАСТРОЙКА ОКРУЖЕНИЯ
            // ==========================================
            bool isGitHub = Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";
            string envChunk = Environment.GetEnvironmentVariable("GH_CHUNK") ?? "0";
            string envTotal = Environment.GetEnvironmentVariable("GH_TOTAL_CHUNKS") ?? "1";

            int currentChunk = int.Parse(envChunk);
            int totalChunks = int.Parse(envTotal);

            int maxConcurrent = isGitHub ? 150 : (settings.MaxConcurrentChecks > 0 ? settings.MaxConcurrentChecks : 1500);
            ThreadPool.SetMinThreads(maxConcurrent + 100, maxConcurrent + 100);

            var deadEndpoints = new ConcurrentDictionary<string, byte>();
            List<ServerData> finalAliveServers = new();

            int totalDiscovered = 0;
            int checkedCount = 0;

            Console.WriteLine($"[Инициализация] Режим: {(isGitHub ? "GitHub Actions (Единый запуск)" : "Локальный ПК")}");
            Console.WriteLine($"[Инициализация] Потоков выделено: {maxConcurrent}");

            var stopwatch = Stopwatch.StartNew();

            // Создаем буферный канал передачи данных
            var channel = Channel.CreateBounded<ServerData>(new BoundedChannelOptions(50000)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = true,
                SingleReader = false
            });

            // ==========================================
            // 2. ПОТОК-ПРОДЮСЕР (Парсинг подписок)
            // ==========================================
            Task producerTask = Task.Run(async () =>
            {
                int globalServerIndex = 0;
                try
                {
                    SubscriptionData[] subArray = await networkingManager.ParseSubscriptions(settings, subscriptionsManager.Sources);

                    List<ServerData> allParsed = new();
                    foreach (var subData in subArray)
                    {
                        allParsed.AddRange(subData.GetServers());
                    }

                    foreach (var serverData in allParsed)
                    {
                        // Распределяем сервера по машинам-чанкам GitHub (если используются)
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

            // ==========================================
            // 3. УЛЬТРА-СКОРОСТНОЙ КОНВЕЙЕР ПРОВЕРКИ
            // ==========================================
            Console.WriteLine("Запуск асинхронного конвейера проверки...");
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = maxConcurrent };

            await Parallel.ForEachAsync(channel.Reader.ReadAllAsync(), parallelOptions, async (server, cancellationToken) =>
            {
                string endpointKey = $"{server.Address}:{server.Port}";

                if (deadEndpoints.ContainsKey(endpointKey)) return;

                try
                {
                    // Вызываем асинхронные тесты, вынесенные ниже
                    await ExecuteCountryTestAsync(server);
                    await ExecuteTcpPingTestAsync(server);

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

            // ==========================================
            // 4. ПЕРЕМЕШИВАНИЕ И ФИНАЛЬНЫЙ ЭКСПОРТ
            // ==========================================
            Console.WriteLine($"\n[Финиш] Проверка завершена за: {stopwatch.Elapsed.TotalSeconds:F2} сек.");
            Console.WriteLine($"Валидных серверов в этом инстансе: {finalAliveServers.Count}");

            // Тотальное перемешивание всего списка живых результатов перед разбивкой на файлы
            if (settings.ShuffleServers && finalAliveServers.Count > 0)
            {
                Console.WriteLine("Перемешивание ФИНАЛЬНЫХ результатов для предотвращения кучности авторов...");
                Utils.Shuffle(finalAliveServers);
            }

            // Из уже перемешанного списка делаем чистые выборки по фильтрам без лишних OrderBy
            ServerData[] black = finalAliveServers
                .Where(x => x.Subscription.FilterType == SubscriptionFilterType.Black)
                .ToArray();

            ServerData[] white = finalAliveServers
                .Where(x => x.Subscription.FilterType == SubscriptionFilterType.White)
                .ToArray();

            // Передаем готовые перемешанные массивы напрямую в экспортеры
            await exporterManager.ExportBlack(settings, black);
            await exporterManager.ExportWhite(settings, white);

            Console.WriteLine("Экспорт успешно завершен!");
        }

        #region Вынесенные асинхронные методы тестирования

        private async Task ExecuteCountryTestAsync(ServerData server)
        {
            try
            {
                if (server.Address == "unknown" || server.Port == 0 || server.Protocol == "broken") return;

                if (!IPAddress.TryParse(server.Address, out IPAddress? ip))
                {
                    using var cts = new CancellationTokenSource(800);
                    IPAddress[] addresses = await Dns.GetHostAddressesAsync(server.Address, cts.Token);

                    ip = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                         ?? addresses.FirstOrDefault();
                }

                if (ip == null) return;

                var response = reader.Country(ip);
                server.CountryCode = response.Country.IsoCode ?? "UNK";
            }
            catch
            {
                server.CountryCode = "UNK";
            }
        }

        private async Task ExecuteTcpPingTestAsync(ServerData server)
        {
            server.IsAlive = false;
            server.Delay = -1;

            if (server.Address == "unknown" || server.Port == 0 || server.Protocol == "broken") return;

            try
            {
                using TcpClient client = new TcpClient();
                client.LingerState = new LingerOption(true, 0);

                Stopwatch stopwatch = Stopwatch.StartNew();

                int timeoutSeconds = Math.Min(settings.Timeout, 3);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

                await client.ConnectAsync(server.Address, server.Port, cts.Token);
                stopwatch.Stop();

                if (client.Connected)
                {
                    server.Delay = stopwatch.ElapsedMilliseconds;
                    server.IsAlive = true;
                    return;
                }
            }
            catch { }
        }

        #endregion
    }

}