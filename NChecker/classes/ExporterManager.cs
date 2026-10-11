using NChecker.models;
using NChecker.models.local;
using System.Text;

namespace NChecker.classes
{
    internal class ExporterManager
    {
        string dumpPath = Path.Combine(Directory.GetCurrentDirectory(), "dump");
        internal ExporterManager()
        {
            if (Directory.Exists(dumpPath)) Directory.Delete(dumpPath, true);
            Directory.CreateDirectory(dumpPath);
            string[] types = Enum.GetNames<SubscriptionFilterType>();
            foreach (string type in types) Directory.CreateDirectory(Path.Combine(dumpPath, type));
        }
        // ==========================================
        // 1. ОСНОВНЫЕ МЕТОДЫ ЭКСПОРТА (BLACK / WHITE)
        // ==========================================

        async internal Task ExportBlack(SettingsData settings, ServerData[] servers)
        {
            await ExportFilter(SubscriptionFilterType.Black, settings, servers);
        }

        async internal Task ExportWhite(SettingsData settings, ServerData[] servers)
        {
            await ExportFilter(SubscriptionFilterType.White, settings, servers);
        }

        private async Task ExportFilter(SubscriptionFilterType filterType, SettingsData settings, ServerData[] servers)
        {
            try
            {
                string path = Path.Combine(dumpPath, filterType.ToString());
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);

                // Запуск всех видов экспорта
                await ExportFilterFull(filterType, servers);
                await ExportByProtocolFull(filterType, servers, settings);
                await ExportByCountriesFull(filterType, servers, settings);
                await ExportFilterPartially(filterType, settings, servers);
            }
            catch (Exception exc)
            {
                Console.WriteLine($"[Ошибка экспорта {filterType}]: {exc}");
            }
        }

        // ==========================================
        // 2. ВСПОМОГАТЕЛЬНЫЕ ПОЛНЫЕ ЭКСПОРТЫ
        // ==========================================

        async Task ExportFilterFull(SubscriptionFilterType filterType, ServerData[] servers)
        {
            string path = Path.Combine(dumpPath, filterType.ToString());

            string headerName = $"VPN {filterType.ToString().ToUpper()} — Full";
            SubscriptionHeaderData headerData = new(headerName, servers.Length);

            string fileContent = headerData.GetHeaderString() + "\n" + string.Join('\n', servers.Select(s => s.Url));
            await File.WriteAllTextAsync(Path.Combine(path, "full.txt"), fileContent);
        }

        async Task ExportByProtocolFull(SubscriptionFilterType filterType, ServerData[] servers, SettingsData settings)
        {
            var protocolGroups = servers.GroupBy(x => x.Protocol);

            foreach (var group in protocolGroups)
            {
                string protocolName = group.Key ?? "UNKNOWN";
                ServerData[] protocolServers = group.ToArray();

                // Складываем строго в подпапку "Protocols"
                string path = Path.Combine(dumpPath, filterType.ToString(), "Protocols", protocolName);
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);

                SubscriptionHeaderData headerData = new($"VPN {protocolName.ToUpper()} — Full", protocolServers.Length);

                string fileContent = headerData.GetHeaderString() + "\n" + string.Join('\n', protocolServers.Select(s => s.Url));
                await File.WriteAllTextAsync(Path.Combine(path, "full.txt"), fileContent);

                await WritePartiallyAsync(path, $"VPN {protocolName.ToUpper()}", protocolServers, settings.MaxLinesPerFile);
            }
        }

        async Task ExportByCountriesFull(SubscriptionFilterType filterType, ServerData[] servers, SettingsData settings)
        {
            var countryGroups = servers.GroupBy(x => string.IsNullOrWhiteSpace(x.CountryCode) ? "UNKNOWN" : x.CountryCode.ToUpper());

            foreach (var group in countryGroups)
            {
                string countryCode = group.Key;
                ServerData[] countryServers = group.ToArray();

                string path = Path.Combine(dumpPath, filterType.ToString(), "Countries", countryCode);
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);

                SubscriptionHeaderData headerData = new($"VPN Country {countryCode} — Full", countryServers.Length);

                string fileContent = headerData.GetHeaderString() + "\n" + string.Join('\n', countryServers.Select(s => s.Url));
                await File.WriteAllTextAsync(Path.Combine(path, "full.txt"), fileContent);

                await WritePartiallyAsync(path, $"VPN Country {countryCode}", countryServers, settings.MaxLinesPerFile);
            }
        }

        // ==========================================
        // 3. ПОЧАСТЕВЫЙ ЭКСПОРТ (УНИВЕРСАЛЬНЫЙ)
        // ==========================================

        async Task ExportFilterPartially(SubscriptionFilterType filterType, SettingsData settings, ServerData[] servers)
        {
            string path = Path.Combine(dumpPath, filterType.ToString());
            string baseHeaderName = $"VPN {filterType.ToString().ToUpper()}";

            await WritePartiallyAsync(path, baseHeaderName, servers, settings.MaxLinesPerFile);
        }

        async Task WritePartiallyAsync(string targetPath, string baseHeaderName, ServerData[] servers, int maxLines)
        {
            if (!Directory.Exists(targetPath)) Directory.CreateDirectory(targetPath);

            int totalServers = servers.Length;
            int filesCount = (int)Math.Ceiling((double)totalServers / maxLines);

            for (int i = 0; i < filesCount; i++)
            {
                int startIndex = i * maxLines;
                int serversInThisFile = Math.Min(maxLines, totalServers - startIndex);

                SubscriptionHeaderData headerData = new($"{baseHeaderName} #{(i + 1)}", serversInThisFile);

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
