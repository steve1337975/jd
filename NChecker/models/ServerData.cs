using Newtonsoft.Json;
using System.Text;
using System.Web;

namespace NChecker.models
{
    internal class ServerData
    {
        internal string Url { get; private set; }
        internal string Protocol { get; private set; }
        internal string Address { get; private set; }
        internal int Port { get; private set; }
        internal string Name { get; private set; }
        internal string CountryCode { get; set; } = "UNK";
        internal long Delay { get; set; } = -1;
        internal bool IsAlive { get; set; }
        internal SubscriptionData Subscription { get; set; }
        internal ServerData(string row)
        {
            if (string.IsNullOrWhiteSpace(row)) return;

            row = row.Trim();
            Url = row;

            try
            {
                if (row.StartsWith("vmess://", StringComparison.OrdinalIgnoreCase))
                {
                    ParseVmess(row);
                }
                else if (row.StartsWith("vless://", StringComparison.OrdinalIgnoreCase) ||
                         row.StartsWith("ss://", StringComparison.OrdinalIgnoreCase) ||
                         row.StartsWith("trojan://", StringComparison.OrdinalIgnoreCase) ||
                         row.StartsWith("shadowsocks://", StringComparison.OrdinalIgnoreCase))
                {
                    ParseStandardVpnUri(row);
                }
                else
                {
                    ParseGenericFallback(row);
                }
            }
            catch (Exception exc)
            {
                Console.WriteLine(Url);
                Console.WriteLine(exc.ToString());
                Protocol = "broken";
            }
        }

        #region Приватные методы безопасного парсинга строк

        private void ParseVmess(string row)
        {
            Protocol = "vmess";
            string base64Part = row.Substring(8).Trim();

            int hashIndex = base64Part.IndexOf('#');
            string nameFromHash =null;

            if (hashIndex != -1)
            {
                string rawName = base64Part.Substring(hashIndex + 1);
                nameFromHash = HttpUtility.UrlDecode(rawName).Trim();
                base64Part = base64Part.Substring(0, hashIndex).Trim();
            }

            int lastEqualsIndex = base64Part.LastIndexOf('=');
            if (lastEqualsIndex != -1)
            {
                base64Part = base64Part.Substring(0, lastEqualsIndex + 1);
            }
            else if (base64Part.EndsWith("vmess", StringComparison.OrdinalIgnoreCase))
            {
                base64Part = base64Part.Substring(0, base64Part.Length - 5).Trim();
            }

            int mod4 = base64Part.Length % 4;
            if (mod4 > 0) base64Part += new string('=', 4 - mod4);

            byte[] bytes = Convert.FromBase64String(base64Part);
            string jsonString = Encoding.UTF8.GetString(bytes).Trim();

            var jsonModel = JsonConvert.DeserializeObject<VmessJsonModel>(jsonString);
            if (jsonModel != null)
            {
                Address = jsonModel.Address;
                Port = Convert.ToInt32(jsonModel.Port ?? 0);

                string jsonName = !string.IsNullOrEmpty(jsonModel.Remarks)
                    ? HttpUtility.UrlDecode(jsonModel.Remarks).Trim()
                    : null;

                Name = jsonName ?? nameFromHash;
            }
        }

        private void ParseStandardVpnUri(string row)
        {
            int schemeBounds = row.IndexOf("://");
            if (schemeBounds == -1) return;

            Protocol = row.Substring(0, schemeBounds).ToLower();

            string mainPart = row.Substring(schemeBounds + 3);
            int hashIndex = mainPart.IndexOf('#');
            if (hashIndex != -1)
            {
                string rawName = mainPart.Substring(hashIndex + 1);
                Name = HttpUtility.UrlDecode(rawName).Trim();
                mainPart = mainPart.Substring(0, hashIndex);
            }

            int queryIndex = mainPart.IndexOf('?');
            if (queryIndex != -1)
            {
                mainPart = mainPart.Substring(0, queryIndex);
            }

            int atIndex = mainPart.LastIndexOf('@');
            string connectionPart = atIndex != -1 ? mainPart.Substring(atIndex + 1) : mainPart;

            int portIndex = connectionPart.LastIndexOf(':');
            if (portIndex != -1)
            {
                Address = connectionPart.Substring(0, portIndex).Trim('[', ']');
                if (int.TryParse(connectionPart.Substring(portIndex + 1), out int parsedPort))
                {
                    Port = parsedPort;
                }
            }
            else
            {
                Address = connectionPart;
            }
        }

        private void ParseGenericFallback(string row)
        {
            int portIndex = row.LastIndexOf(':');
            if (portIndex != -1 && int.TryParse(row.Substring(portIndex + 1), out int parsedPort))
            {
                Address = row.Substring(0, portIndex);
                Port = parsedPort;
                Protocol = "raw";
            }
        }

        #endregion
    }
}
