using NChecker.models;
using NChecker.models.subscription;
using NChecker.utils;
using System.Text;

namespace NChecker.classes
{
    internal class ExporterManager
    {
        private readonly string dumpPath = Path.Combine(Directory.GetCurrentDirectory(), "dump");

        internal ExporterManager()
        {
            if (Directory.Exists(dumpPath)) Directory.Delete(dumpPath, true);
            Directory.CreateDirectory(dumpPath);

            string[] types = Enum.GetNames<SubscriptionFilterType>();
            foreach (string type in types)
            {
                Directory.CreateDirectory(Path.Combine(dumpPath, type));
            }
        }

        async internal Task Export(SettingsData settings, SubscriptionData[] subscriptions)
        {
            var blackSubs = subscriptions.Where(x => x.FilterType == SubscriptionFilterType.Black).OrderBy(x => x.SortedIndex).ToArray();
            var whiteSubs = subscriptions.Where(x => x.FilterType == SubscriptionFilterType.White).OrderBy(x => x.SortedIndex).ToArray();

            List<ServerData> blackServers = new List<ServerData>();
            foreach (var sub in blackSubs)
            {
                blackServers.AddRange(sub.Servers);
            }

            List<ServerData> whiteServers = new List<ServerData>();
            foreach (var sub in whiteSubs)
            {
                whiteServers.AddRange(sub.Servers);
            }

            await ProcessExport(SubscriptionFilterType.Black, settings, blackServers.ToArray());
            await ProcessExport(SubscriptionFilterType.White, settings, whiteServers.ToArray());

            Logger.Info("Экспорт успешно завершен!");
        }

        private async Task ProcessExport(SubscriptionFilterType filterType, SettingsData settings, ServerData[] servers)
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

        // ==========================================
        // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ЭКСПОРТА
        // ==========================================

        private async Task ExportFilterFull(SubscriptionFilterType filterType, ServerData[] servers)
        {
            string path = Path.Combine(dumpPath, filterType.ToString());

            // Было: VPN White — Full / VPN Black — Full
            string headerName = filterType == SubscriptionFilterType.White
                ? "🚀 VALIDS — Глобальный список"
                : "❌ DEAD / INVALID — Черный список";

            SubscriptionHeaderData headerData = new SubscriptionHeaderData(headerName, servers.Length);

            string fileContent = headerData.GetHeaderString() + "\n" + string.Join('\n', servers.Select(s => s.Url));
            await File.WriteAllTextAsync(Path.Combine(path, "full.txt"), fileContent);
        }

        private async Task ExportByProtocolFull(SubscriptionFilterType filterType, ServerData[] servers, SettingsData settings)
        {
            var protocolGroups = servers.GroupBy(x => x.Protocol);

            foreach (var group in protocolGroups)
            {
                string protocolName = group.Key.ToString().ToUpper();
                ServerData[] protocolServers = group.ToArray();

                string path = Path.Combine(dumpPath, filterType.ToString(), "Protocols", protocolName);
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);

                // Было: VPN White VLESS — Full
                string headerName = filterType == SubscriptionFilterType.White
                    ? $"⚡ PROXIES — {protocolName}"
                    : $"🗑️ DEAD — {protocolName}";

                SubscriptionHeaderData headerData = new SubscriptionHeaderData(headerName, protocolServers.Length);

                string fileContent = headerData.GetHeaderString() + "\n" + string.Join('\n', protocolServers.Select(s => s.Url));
                await File.WriteAllTextAsync(Path.Combine(path, "full.txt"), fileContent);

                // Для порезанных файлов заголовок будет лаконичным (например, "⚡ PROXIES — VLESS")
                await WritePartiallyAsync(path, headerName, protocolServers, settings.MaxLinesPerFile);
            }
        }

        private async Task ExportByCountriesFull(SubscriptionFilterType filterType, ServerData[] servers, SettingsData settings)
        {
            var countryGroups = servers.GroupBy(x => x.GetCountryCode());

            foreach (var group in countryGroups)
            {
                string countryCode = group.Key.ToUpper();
                ServerData[] countryServers = group.ToArray();

                string path = Path.Combine(dumpPath, filterType.ToString(), "Countries", countryCode);
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);

                // Было: VPN White Country US — Full
                string headerName = filterType == SubscriptionFilterType.White
                    ? $"🌍 GEO — {countryCode}"
                    : $"❌ DEAD GEO — {countryCode}";

                SubscriptionHeaderData headerData = new SubscriptionHeaderData(headerName, countryServers.Length);

                string fileContent = headerData.GetHeaderString() + "\n" + string.Join('\n', countryServers.Select(s => s.Url));
                await File.WriteAllTextAsync(Path.Combine(path, "full.txt"), fileContent);

                await WritePartiallyAsync(path, headerName, countryServers, settings.MaxLinesPerFile);
            }
        }

        private async Task ExportFilterPartially(SubscriptionFilterType filterType, SettingsData settings, ServerData[] servers)
        {
            string path = Path.Combine(dumpPath, filterType.ToString());

            // Было: VPN White WHITE
            string baseHeaderName = filterType == SubscriptionFilterType.White
                ? "📦 VALIDS — Пачка"
                : "📦 DEAD — Пачка";

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

                // Добавляем красивый номер пачки без дубликатов
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
