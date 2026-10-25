using System.Web;

namespace NChecker.models.vpn.protocols
{
    internal class VlessConfigData : VpnConfigBase
    {
        private string _uuid, _encryption, _security, _sni, _type, _host, _path, _publicKey, _shortId, _flow, _packetEncoding;
        // Поле _fp намеренно исключено, чтобы отпечатки пальцев не плодили дубликаты

        internal VlessConfigData(string a1) : base(a1) { }

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
                    _uuid = mainPart.Substring(0, atIndex).Trim().ToLowerInvariant();
                    mainPart = mainPart.Substring(atIndex + 1);
                }

                int portIndex = mainPart.LastIndexOf(':');
                if (portIndex != -1)
                {
                    string portStr = mainPart.Substring(portIndex + 1);
                    if (!portStr.Contains(']'))
                    {
                        Address = mainPart.Substring(0, portIndex).Trim('[', ']').ToLowerInvariant();
                        if (int.TryParse(portStr, out int parsedPort)) Port = parsedPort;
                    }
                    else
                    {
                        Address = mainPart.Trim('[', ']').ToLowerInvariant();
                    }
                }
                else
                {
                    Address = mainPart.Trim('[', ']').ToLowerInvariant();
                }

                if (!string.IsNullOrEmpty(queryPart))
                {
                    var queryParams = HttpUtility.ParseQueryString(queryPart);

                    string sniRaw = queryParams["sni"]?.Trim().ToLowerInvariant().TrimEnd('.');
                    _sni = string.IsNullOrEmpty(sniRaw) ? null : sniRaw;

                    string secRaw = queryParams["security"]?.ToLowerInvariant().Trim();
                    _security = string.IsNullOrEmpty(secRaw) ? "none" : secRaw;

                    string encRaw = queryParams["encryption"]?.ToLowerInvariant().Trim();
                    _encryption = string.IsNullOrEmpty(encRaw) ? "none" : encRaw;

                    string rawType = queryParams["type"]?.ToLowerInvariant().Trim();
                    _type = (string.IsNullOrEmpty(rawType) || rawType == "raw" || rawType == "tcp") ? "tcp" : rawType;

                    _host = queryParams["host"]?.Trim().ToLowerInvariant().TrimEnd('.');
                    _path = queryParams["path"]?.Trim();
                    _publicKey = queryParams["pbk"]?.Trim();
                    _shortId = queryParams["sid"]?.Trim();
                    _flow = queryParams["flow"]?.ToLowerInvariant().Trim();
                    _packetEncoding = queryParams["packetEncoding"]?.ToLowerInvariant().Trim();

                    // Нормализация и очистка мусорных host/path для Reality и TCP нод
                    if (_security == "reality")
                    {
                        if (string.IsNullOrEmpty(_host) || _host == _sni || _host.Contains("telegram") || _host.Contains("marambashi"))
                        {
                            _host = null;
                        }
                        if (string.IsNullOrEmpty(_path) || _path == "/" || _path.Contains("telegram") || _path.Contains("marambashi"))
                        {
                            _path = null;
                        }
                    }
                }
                else
                {
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

            if (!string.IsNullOrEmpty(_uuid)) core += $"|uuid={_uuid}";
            if (!string.IsNullOrEmpty(_encryption)) core += $"|enc={_encryption}";
            if (!string.IsNullOrEmpty(_security)) core += $"|sec={_security}";
            if (!string.IsNullOrEmpty(_sni)) core += $"|sni={_sni}";
            if (!string.IsNullOrEmpty(_type)) core += $"|type={_type}";
            if (!string.IsNullOrEmpty(_host)) core += $"|host={_host}";
            if (!string.IsNullOrEmpty(_path)) core += $"|path={_path}";
            if (!string.IsNullOrEmpty(_publicKey)) core += $"|pbk={_publicKey}";
            if (!string.IsNullOrEmpty(_shortId)) core += $"|sid={_shortId}";
            if (!string.IsNullOrEmpty(_flow)) core += $"|flow={_flow}";
            if (!string.IsNullOrEmpty(_packetEncoding)) core += $"|pe={_packetEncoding}";
            // _fp намеренно исключен, чтобы отпечатки (chrome/android/edge) не плодили дубликаты

            return core;
        }
    }
}
