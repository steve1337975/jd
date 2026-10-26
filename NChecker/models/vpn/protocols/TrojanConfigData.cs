using System.Web;

namespace NChecker.models.vpn.protocols
{
    internal class TrojanConfigData : VpnConfigBase
    {
        private string _password, _security, _sni, _type, _host, _path;

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
                    Address = mainPart.Substring(0, portIndex).Trim('[', ']').ToLowerInvariant();
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
                    _security = string.IsNullOrEmpty(secRaw) ? null : secRaw;

                    string sniRaw = queryParams["sni"]?.Trim().ToLowerInvariant().TrimEnd('.');
                    _sni = string.IsNullOrEmpty(sniRaw) ? null : sniRaw;

                    string rawType = queryParams["type"]?.ToLowerInvariant().Trim();
                    _type = (string.IsNullOrEmpty(rawType) || rawType == "raw" || rawType == "tcp") ? "tcp" : rawType;

                    string hostRaw = queryParams["host"]?.Trim().ToLowerInvariant().TrimEnd('.');
                    _host = string.IsNullOrEmpty(hostRaw) ? null : hostRaw;

                    _path = queryParams["path"]?.Trim();

                    // Жесткая зачистка мусорных путей
                    if (string.IsNullOrEmpty(_path) || _path == "/" || _path.Contains("=") || _path.Contains("%3D") || _path.Contains("metrics"))
                    {
                        _path = null;
                    }

                    // Если host совпадает с SNI, сбрасываем в null для чистоты сигнатуры
                    if (_host == _sni)
                    {
                        _host = null;
                    }
                }
                else
                {
                    _type = "tcp";
                }
            }
            catch { }
        }

        internal override string GetCoreSignature()
        {
            string core = base.GetCoreSignature(); // База: ip + port

            if (!string.IsNullOrEmpty(_password)) core += $"|pwd={_password}";
            if (!string.IsNullOrEmpty(_security)) core += $"|sec={_security}";
            if (!string.IsNullOrEmpty(_sni)) core += $"|sni={_sni}";
            if (!string.IsNullOrEmpty(_type)) core += $"|type={_type}";
            if (!string.IsNullOrEmpty(_host)) core += $"|host={_host}";
            if (!string.IsNullOrEmpty(_path)) core += $"|path={_path}";

            return core;
        }
    }
}
