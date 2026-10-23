using NChecker.models;
using NChecker.models.subscription;
using NChecker.utils;
using System.Text;

namespace NChecker.classes
{
    internal class ExporterManager
    {
        private readonly string dumpPath = Path.Combine(Directory.GetCurrentDirectory(), "dump");

        // Словарь для красивых эмодзи главных файлов
        private static readonly Dictionary<SubscriptionType, string> FilterEmojis = new()
        {
            { SubscriptionType.White, "🚀" },
            { SubscriptionType.Black, "🛡️" },
            { SubscriptionType.Universal, "🌐" }
        };

        internal ExporterManager()
        {
            if (Directory.Exists(dumpPath)) Directory.Delete(dumpPath, true);
            Directory.CreateDirectory(dumpPath);

            string[] types = Enum.GetNames<SubscriptionType>();
            foreach (string type in types)
            {
                Directory.CreateDirectory(Path.Combine(dumpPath, type));
            }
        }

        internal async Task Export(SettingsData settings, SubscriptionData[] subscriptions)
        {
            // Группируем подписки по FilterType и сразу сортируем внутри каждой группы
            var sortedSubsDict = subscriptions
                .GroupBy(x => x.FilterType)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(x => x.SortedIndex).ToArray()
                );

            // Динамически получаем серверы для каждого типа фильтра
            ServerData[] blackServers = GetServersForType(SubscriptionType.Black, sortedSubsDict);
            ServerData[] whiteServers = GetServersForType(SubscriptionType.White, sortedSubsDict);
            ServerData[] universalServers = GetServersForType(SubscriptionType.Universal, sortedSubsDict);

            // Запускаем экспорт
            await ProcessExport(SubscriptionType.Black, settings, blackServers);
            await ProcessExport(SubscriptionType.White, settings, whiteServers);
            await ProcessExport(SubscriptionType.Universal, settings, universalServers);

            Logger.Info("Экспорт успешно завершен!");
        }

        private ServerData[] GetServersForType(SubscriptionType type, Dictionary<SubscriptionType, SubscriptionData[]> dict)
        {
            if (!dict.TryGetValue(type, out var subs)) return Array.Empty<ServerData>();

            List<ServerData> servers = new List<ServerData>();
            foreach (var sub in subs)
            {
                servers.AddRange(sub.Servers);
            }
            return servers.ToArray();
        }

        private async Task ProcessExport(SubscriptionType filterType, SettingsData settings, ServerData[] servers)
        {
            try
            {
                string path = Path.Combine(dumpPath, filterType.ToString());
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);

                await ExportFilterFull(filterType, servers);
                await ExportByProtocolFull(filterType, servers, settings);
                await ExportByCountriesFull(filterType, servers, settings);
                await ExportFilterPartially(filterType, settings, servers);
            }
            catch (Exception exc)
            {
                Logger.Error($"Ошибка экспорта {filterType} => {exc.Message}");
            }
        }

        private async Task ExportFilterFull(SubscriptionType filterType, ServerData[] servers)
        {
            string path = Path.Combine(dumpPath, filterType.ToString());
            string filterUpper = filterType.ToString().ToUpper();

            // Берём эмодзи из словаря, либо ставим дефолтный 📦 для любого нового типа
            string emoji = FilterEmojis.GetValueOrDefault(filterType, "📦");
            string headerName = $"{emoji} VPN — {filterUpper} LIST";

            SubscriptionHeaderData headerData = new SubscriptionHeaderData(headerName, servers.Length);

            string fileContent = headerData.GetHeaderString() + "\n" + string.Join('\n', servers.Select(s => s.Url));
            await File.WriteAllTextAsync(Path.Combine(path, "full.txt"), fileContent);
        }

        private async Task ExportByProtocolFull(SubscriptionType filterType, ServerData[] servers, SettingsData settings)
        {
            var protocolGroups = servers.GroupBy(x => x.Protocol);
            string typeLabel = filterType.ToString().ToUpper();

            foreach (var group in protocolGroups)
            {
                string protocolName = group.Key.ToString().ToUpper();
                ServerData[] protocolServers = group.ToArray();

                string path = Path.Combine(dumpPath, filterType.ToString(), "Protocols", protocolName);
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);

                string headerName = $"⚡ {protocolName} — {typeLabel}";

                SubscriptionHeaderData headerData = new SubscriptionHeaderData(headerName, protocolServers.Length);

                string fileContent = headerData.GetHeaderString() + "\n" + string.Join('\n', protocolServers.Select(s => s.Url));
                await File.WriteAllTextAsync(Path.Combine(path, "full.txt"), fileContent);

                await WritePartiallyAsync(path, headerName, protocolServers, settings.MaxLinesPerFile);
            }
        }

        private async Task ExportByCountriesFull(SubscriptionType filterType, ServerData[] servers, SettingsData settings)
        {
            var countryGroups = servers.GroupBy(x => x.GetCountryCode());
            string typeLabel = filterType.ToString().ToUpper();

            foreach (var group in countryGroups)
            {
                string countryCode = group.Key.ToUpper();
                ServerData[] countryServers = group.ToArray();

                string path = Path.Combine(dumpPath, filterType.ToString(), "Countries", countryCode);
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);

                string headerName = $"🌍 GEO [{countryCode}] — {typeLabel}";

                SubscriptionHeaderData headerData = new SubscriptionHeaderData(headerName, countryServers.Length);

                string fileContent = headerData.GetHeaderString() + "\n" + string.Join('\n', countryServers.Select(s => s.Url));
                await File.WriteAllTextAsync(Path.Combine(path, "full.txt"), fileContent);

                await WritePartiallyAsync(path, headerName, countryServers, settings.MaxLinesPerFile);
            }
        }

        private async Task ExportFilterPartially(SubscriptionType filterType, SettingsData settings, ServerData[] servers)
        {
            string path = Path.Combine(dumpPath, filterType.ToString());
            string filterUpper = filterType.ToString().ToUpper();

            string baseHeaderName = $"📦 VPN {filterUpper}";

            await WritePartiallyAsync(path, baseHeaderName, servers, settings.MaxLinesPerFile);
        }

        private async Task WritePartiallyAsync(string targetPath, string baseHeaderName, ServerData[] servers, int maxLines)
        {
            if (!Directory.Exists(targetPath)) Directory.CreateDirectory(targetPath);

            int totalServers = servers.Length;
            int filesCount = (int)Math.Ceiling((double)totalServers / maxLines);

            for (int i = 0; i < filesCount; i++)
            {
                int startIndex = i * maxLines;
                int serversInThisFile = Math.Min(maxLines, totalServers - startIndex);

                SubscriptionHeaderData headerData = new SubscriptionHeaderData($"{baseHeaderName} #{i + 1}", serversInThisFile);

                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendLine(headerData.GetHeaderString());

                for (int j = 0; j < serversInThisFile; j++)
                {
                    stringBuilder.AppendLine(servers[startIndex + j].Url);
                }

                string fileName = $"part_{i + 1}.txt";
                await File.WriteAllTextAsync(Path.Combine(targetPath, fileName), stringBuilder.ToString());
            }
        }
    }
}
