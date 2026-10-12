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

        private SettingsData settings;
        private SettingsManager settingsManager = new SettingsManager();
        private SubcriptionsManager subscriptionsManager = new SubcriptionsManager();
        private NetworkingManager networkingManager = new NetworkingManager();
        private ExporterManager exporterManager = new ExporterManager();
        private DatabaseReader reader;

        // Внутренние поля для синхронизации и шаринга данных между фрагментами
        private readonly ConcurrentDictionary<string, byte> _deadEndpoints = new();
        private readonly List<ServerData> _finalAliveServers = new();

        private bool _isGitHub;
        private int _currentChunk;
        private int _totalChunks;
        private int _maxConcurrent;
        private int _totalDiscovered = 0;
        private int _checkedCount = 0;

        internal App()
        {
            reader = new DatabaseReader(new MemoryStream(Properties.Resources.GeoLite2_Country));
            settings = settingsManager.Settings;
        }

        /// <summary>
        /// Главная точка запуска (Диспетчер)
        /// </summary>
        async internal Task Start()
        {
            // Шаг 1: Инициализация окружения
            InitializeEnvironment();

            var stopwatch = Stopwatch.StartNew();

            // Создаем буферный канал конвейера
            var channel = Channel.CreateBounded<ServerData>(new BoundedChannelOptions(50000)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = true,
                SingleReader = false
            });

            // Шаг 2: Фоновый запуск продюсера (парсинг подписок)
            Task producerTask = StartProducer(channel);

            // Шаг 3: Запуск конвейера сетевых проверок
            await RunVerificationPipeline(channel);

            // Гарантируем закрытие таски продюсера
            await producerTask;
            stopwatch.Stop();

            // Шаг 4: Финальное перемешивание и экспорт
            await ProcessFinalExport(stopwatch.Elapsed.TotalSeconds);
        }

        #region Фрагменты логики метода Start



        /// <summary>
        /// ФРАГМЕНТ 1: Инициализация параметров среды и пула потоков
        /// </summary>
        private void InitializeEnvironment()
        {
            _isGitHub = Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";
            _currentChunk = int.Parse(Environment.GetEnvironmentVariable("GH_CHUNK") ?? "0");
            _totalChunks = int.Parse(Environment.GetEnvironmentVariable("GH_TOTAL_CHUNKS") ?? "1");

            _maxConcurrent = _isGitHub ? 150 : (settings.MaxConcurrentChecks > 0 ? settings.MaxConcurrentChecks : 1500);
            ThreadPool.SetMinThreads(_maxConcurrent + 50, _maxConcurrent + 50);

            Console.WriteLine($"[Инициализация] Режим: {(_isGitHub ? "GitHub Actions (Единый запуск)" : "Локальный ПК")}");
            Console.WriteLine($"[Инициализация] Потоков выделено: {_maxConcurrent}");
            Console.WriteLine($"[Инициализация] Режим пинга: {(settings.IsHttpPing ? "HTTP GET" : "TCP Connection")}");
        }

        /// <summary>
        /// ФРАГМЕНТ 2: Фоновый сбор серверов из подписок
        /// </summary>
        private Task StartProducer(Channel<ServerData, ServerData> channel)
        {
            return Task.Run(async () =>
            {
                try
                {
                    SubscriptionData[] subArray = await networkingManager.ParseSubscriptions(settings, subscriptionsManager.Sources);

                    List<ServerData> allParsed = new();
                    foreach (var subData in subArray)
                    {
                        allParsed.AddRange(subData.GetServers());
                    }

                    _totalDiscovered = allParsed.Count;

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
        }

        /// <summary>
        /// ФРАГМЕНТ 3: Высокоскоростной асинхронный конвейер проверки сокетов
        /// </summary>
        private async Task RunVerificationPipeline(Channel<ServerData, ServerData> channel)
        {
            Console.WriteLine("Запуск асинхронного конвейера проверки...");

            // Инициализируем HttpClient один раз на всю фазу проверки
            using var handler = new SocketsHttpHandler
            {
                MaxConnectionsPerServer = _maxConcurrent,
                PooledConnectionIdleTimeout = TimeSpan.FromSeconds(5),
                ConnectTimeout = TimeSpan.FromSeconds(Math.Min(settings.Timeout, 3))
            };
            using var httpClient = new HttpClient(handler);
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");

            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = _maxConcurrent };
            var stopwatch = Stopwatch.StartNew();

            await Parallel.ForEachAsync(channel.Reader.ReadAllAsync(), parallelOptions, async (server, cancellationToken) =>
            {
                string endpointKey = $"{server.Address}:{server.Port}";

                if (_deadEndpoints.ContainsKey(endpointKey)) return;

                try
                {
                    // Проверяем геолокацию
                    await networkingManager.ExecuteCountryTestAsync(server, reader);

                    if (settings.IsHttpPing)
                    {
                        await networkingManager.ExecuteHttpPingTestAsync(server, settings);
                    }
                    else
                    {
                        await networkingManager.ExecuteTcpPingTestAsync(server, settings);
                    }

                    if (server.IsAlive)
                    {
                        lock (_finalAliveServers)
                        {
                            _finalAliveServers.Add(server);
                        }
                    }
                    else
                    {
                        _deadEndpoints.TryAdd(endpointKey, 0);
                    }
                }
                catch
                {
                    _deadEndpoints.TryAdd(endpointKey, 0);
                }
                finally
                {
                    int currentChecked = Interlocked.Increment(ref _checkedCount);

                    if (settings.Logging)
                    {
                        int logStep = _isGitHub ? 1000 : 5000;
                        if (currentChecked % logStep == 0)
                        {
                            Console.WriteLine($"[Прогресс] Проверено: {currentChecked} / ~{_totalDiscovered}... Время: {stopwatch.Elapsed.TotalSeconds:F0} сек.");
                        }
                    }
                }
            });
        }

        /// <summary>
        /// ФРАГМЕНТ 4: Финальное перемешивание результатов и вызов менеджеров экспорта
        /// </summary>
        private async Task ProcessFinalExport(double elapsedSeconds)
        {
            Console.WriteLine($"\n[Финиш] Проверка завершена за: {elapsedSeconds:F2} сек.");
            Console.WriteLine($"Валидных серверов в этом инстансе: {_finalAliveServers.Count}");

            // 1. ДИНАМИЧЕСКОЕ ПЕРЕИМЕНОВАНИЕ (Новая фича)
            // Если флаг Renaming активен в настройках, перезаписываем имена серверов перед экспортом
            if (settings.Renaming)
            {
                Console.WriteLine("Применение опции Renaming: изменение названий серверов по локации...");
                int renamedCount = 0;

                foreach (var server in _finalAliveServers)
                {
                    // Получаем полное название страны и флаг на основе CountryCode, определенного MaxMind
                    string countryInfo = GetCountryNameWithFlag(server.CountryCode);

                    // Формируем красивое структурированное имя, например: "NChecker | Germany 🇩🇪 [VLESS] #1"
                    // Используем Interlocked, чтобы иметь уникальный порядковый номер для каждого сервера
                    int currentNumber = Interlocked.Increment(ref renamedCount);

                    // Перезаписываем свойство Name. Внимание: саму ссылку Url мы пока не меняем, 
                    // так как для изменения имени в Url требуется пересборка строки (зависит от протокола).
                    // Но твои экспортеры (ExporterManager) при записи в файл должны собирать итоговую строку 
                    // на основе Address, Port, Protocol и обновленного свойства Name!
                    server.Name = $"{countryInfo} [{server.Protocol.ToUpper()}] #{currentNumber}";
                }
            }

            // 2. Тотальное перемешивание всего списка ЖИВЫХ результатов перед разбивкой на файлы
            if (settings.ShuffleServers && _finalAliveServers.Count > 0)
            {
                Console.WriteLine("Перемешивание ФИНАЛЬНЫХ результатов для предотвращения кучности авторов...");
                Utils.Shuffle(_finalAliveServers);
            }

            // 3. Выполняем разделение на Black и White списки из уже полностью обработанного набора
            ServerData[] black = _finalAliveServers
                .Where(x => x.Subscription.FilterType == SubscriptionFilterType.Black)
                .ToArray();

            ServerData[] white = _finalAliveServers
                .Where(x => x.Subscription.FilterType == SubscriptionFilterType.White)
                .ToArray();

            await exporterManager.ExportBlack(settings, black);
            await exporterManager.ExportWhite(settings, white);

            Console.WriteLine("Экспорт успешно завершен!");
        }

        /// <summary>
        /// Вспомогательный метод: сопоставляет ISO-код с полным именем страны и флагом
        /// </summary>
        private string GetCountryNameWithFlag(string countryCode)
        {
            if (string.IsNullOrEmpty(countryCode) || countryCode == "UNK")
                return "Unknown Location 🌐";

            // Словарь основных локаций. Можно дополнять любыми странами.
            var countries = new Dictionary<string, (string Name, string Flag)>(StringComparer.OrdinalIgnoreCase)
            {
                { "RU", ("Russia", "🇷🇺") },
                { "US", ("United States", "🇺🇸") },
                { "DE", ("Germany", "🇩🇪") },
                { "NL", ("Netherlands", "🇳🇱") },
                { "GB", ("United Kingdom", "🇬🇧") },
                { "FR", ("France", "🇫🇷") },
                { "FI", ("Finland", "🇫🇮") },
                { "SG", ("Singapore", "🇸🇬") },
                { "HK", ("Hong Kong", "🇭🇰") },
                { "JP", ("Japan", "🇯🇵") },
                { "PL", ("Poland", "🇵🇱") },
                { "TR", ("Turkey", "🇹🇷") },
                { "UA", ("Ukraine", "🇺🇦") },
                { "KZ", ("Kazakhstan", "🇰🇿") },
                { "SE", ("Sweden", "🇸🇪") },
                { "CH", ("Switzerland", "🇨🇭") }
            };

            if (countries.TryGetValue(countryCode, out var info))
            {
                return $"{info.Name} {info.Flag}";
            }

            // Дефолтный вариант для экзотических стран, которых нет в словаре
            return $"{countryCode} 🏳️";
        }

        #endregion

        #region Вынесенные внутренние сетевые тесты

        #endregion
    }
}