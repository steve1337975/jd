using System.Web;

namespace NChecker.models.vpn.protocols
{
    internal class TrojanConfigData : VpnConfigBase
    {
        private string _password;
        private string _security = "tls";
        private string _sni;
        private string _type;
        private string _host;
        private string _path;
        private string _allowInsecure = "false";

        internal TrojanConfigData(string a1) : base(a1) { }

        protected override void Parse(string url)
        {
            try
            {
                int schemeIndex = url.IndexOf("://");
                if (schemeIndex == -1) return;
                string mainPart = url.Substring(schemeIndex + 3);

                int hashIndex = mainPart.IndexOf('#');
                if (hashIndex != -1)
                {
                    Name = HttpUtility.UrlDecode(mainPart.Substring(hashIndex + 1)).Trim();
                    mainPart = mainPart.Substring(0, hashIndex).Trim();
                }

                int queryIndex = mainPart.IndexOf('?');
                string queryPart = string.Empty;
                if (queryIndex != -1)
                {
                    queryPart = mainPart.Substring(queryIndex + 1);
                    mainPart = mainPart.Substring(0, queryIndex).Trim();
                }

                int atIndex = mainPart.LastIndexOf('@');
                if (atIndex != -1)
                {
                    _password = mainPart.Substring(0, atIndex).Trim();
                    mainPart = mainPart.Substring(atIndex + 1);
                }

                int portIndex = mainPart.LastIndexOf(':');
                if (portIndex != -1)
                {
                    string portStr = mainPart.Substring(portIndex + 1);
                    Address = mainPart.Substring(0, portIndex).Trim('[', ']');
                    if (int.TryParse(portStr, out int parsedPort))
                    {
                        Port = parsedPort;
                    }
                }
                else
                {
                    Address = mainPart.Trim('[', ']');
                }

                if (!string.IsNullOrEmpty(queryPart))
                {
                    var queryParams = HttpUtility.ParseQueryString(queryPart);

                    string secRaw = queryParams["security"]?.ToLowerInvariant().Trim();
                    if (!string.IsNullOrEmpty(secRaw))
                    {
                        _security = secRaw;
                    }

                    // Убираем точки на конце, приводим к нижнему регистру
                    string sniRaw = queryParams["sni"]?.Trim().ToLowerInvariant().TrimEnd('.');
                    _sni = string.IsNullOrEmpty(sniRaw) ? null : sniRaw;

                    // Нормализация транспорта (по умолчанию tls+tcp для трояна, но может быть ws или gRPC)
                    string rawType = queryParams["type"]?.ToLowerInvariant().Trim();
                    _type = string.IsNullOrEmpty(rawType) ? "tcp" : rawType;

                    // Если host не задан в ссылке, подставляем SNI по умолчанию
                    string hostRaw = queryParams["host"]?.Trim().ToLowerInvariant().TrimEnd('.');
                    _host = string.IsNullOrEmpty(hostRaw) ? _sni : hostRaw;

                    _path = queryParams["path"]?.Trim();

                    string insecureRaw = queryParams["allowInsecure"]?.ToLowerInvariant().Trim();
                    _allowInsecure = string.IsNullOrEmpty(insecureRaw) ? "false" : insecureRaw;
                }
                else
                {
                    _type = "tcp";
                    _allowInsecure = "false";
                }
            }
            catch { }
        }

        internal override string GetCoreSignature()
        {
            string core = base.GetCoreSignature();
            if (!string.IsNullOrEmpty(_password)) core += $"|pwd={_password}";
            if (!string.IsNullOrEmpty(_security)) core += $"|sec={_security}";
            if (!string.IsNullOrEmpty(_sni)) core += $"|sni={_sni}";
            if (!string.IsNullOrEmpty(_type)) core += $"|type={_type}";
            if (!string.IsNullOrEmpty(_host)) core += $"|host={_host}";
            if (!string.IsNullOrEmpty(_path)) core += $"|path={_path}";
            if (!string.IsNullOrEmpty(_allowInsecure)) core += $"|insecure={_allowInsecure}";
            return core;
        }
    }
}
