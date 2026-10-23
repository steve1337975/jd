using System.Web;

namespace NChecker.models.vpn.protocols
{
    internal class VlessConfigData : VpnConfigBase
    {
        private string _security, _sni, _type, _host, _path, _publicKey, _shortId, _encryption, _uuid;
        internal VlessConfigData(string a1) : base(a1) { }
        protected override void Parse(string url)
        {
            try
            {
                // Убираем схему vless://
                string mainPart = url.Substring(8);

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
                    _uuid = mainPart.Substring(0, atIndex);
                    mainPart = mainPart.Substring(atIndex + 1);
                }

                int portIndex = mainPart.LastIndexOf(':');
                if (portIndex != -1)
                {
                    string portStr = mainPart.Substring(portIndex + 1);
                    if (!portStr.Contains(']'))
                    {
                        Address = mainPart.Substring(0, portIndex).Trim('[', ']');
                        if (int.TryParse(portStr, out int parsedPort)) Port = parsedPort;
                    }
                    else
                    {
                        Address = mainPart.Trim('[', ']');
                    }
                }
                else
                {
                    Address = mainPart;
                }

                if (!string.IsNullOrEmpty(queryPart))
                {
                    var queryParams = HttpUtility.ParseQueryString(queryPart);

                    // Нормализация SNI (приводим к нижнему регистру, так как домены нечувствительны к регистру)
                    string sniRaw = queryParams["sni"]?.Trim().ToLowerInvariant();
                    _sni = string.IsNullOrEmpty(sniRaw) ? null : sniRaw;

                    string secRaw = queryParams["security"]?.ToLowerInvariant();
                    // Если security не указан, для VLESS это чаще всего none
                    _security = string.IsNullOrEmpty(secRaw) ? "none" : secRaw;

                    // Нормализация encryption (по умолчанию none)
                    string encRaw = queryParams["encryption"]?.ToLowerInvariant();
                    _encryption = string.IsNullOrEmpty(encRaw) ? "none" : encRaw;

                    // Нормализация транспорта: raw, tcp или пустота — это прямой транспорт по умолчанию
                    string rawType = queryParams["type"]?.ToLowerInvariant();
                    if (string.IsNullOrEmpty(rawType) || rawType == "raw" || rawType == "tcp")
                    {
                        _type = "tcp";
                    }
                    else
                    {
                        _type = rawType;
                    }

                    _host = queryParams["host"];
                    _path = queryParams["path"];
                    _publicKey = queryParams["pbk"];
                    _shortId = queryParams["sid"];
                }
                else
                {
                    // Если query параметров вообще нет, ставим дефолты
                    _type = "tcp";
                    _encryption = "none";
                    _security = "none";
                }
            }
            catch { }
        }

        internal override string GetCoreSignature()
        {
            string core = base.GetCoreSignature();

            core += $"|uuid={_uuid?.Trim()}";

            if (!string.IsNullOrEmpty(_encryption)) core += $"|enc={_encryption}";
            if (!string.IsNullOrEmpty(_security)) core += $"|sec={_security}";
            if (!string.IsNullOrEmpty(_sni)) core += $"|sni={_sni}";
            if (!string.IsNullOrEmpty(_type)) core += $"|type={_type}";
            if (!string.IsNullOrEmpty(_host)) core += $"|host={_host.Trim().ToLowerInvariant()}";
            if (!string.IsNullOrEmpty(_path)) core += $"|path={_path.Trim()}";
            if (!string.IsNullOrEmpty(_publicKey)) core += $"|pbk={_publicKey}";
            if (!string.IsNullOrEmpty(_shortId)) core += $"|sid={_shortId}";

            return core;
        }
    }
}
