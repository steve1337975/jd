using System.Web;

namespace NChecker.models.vpn.protocols
{
    internal class HysteriaConfigData : VpnConfigBase
    {
        private string _auth, _sni, _obfs, _obfsPassword;

        internal HysteriaConfigData(string a1) : base(a1) { }

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
                    _auth = mainPart.Substring(0, atIndex).Trim();
                    mainPart = mainPart.Substring(atIndex + 1);
                }

                int portIndex = mainPart.LastIndexOf(':');
                if (portIndex != -1)
                {
                    Address = mainPart.Substring(0, portIndex).Trim('[', ']').ToLowerInvariant().TrimEnd('.');
                    if (int.TryParse(mainPart.Substring(portIndex + 1), out int parsedPort))
                    {
                        Port = parsedPort;
                    }
                }
                else
                {
                    Address = mainPart.Trim('[', ']').ToLowerInvariant().TrimEnd('.');
                }

                if (!string.IsNullOrEmpty(queryPart))
                {
                    var queryParams = HttpUtility.ParseQueryString(queryPart);

                    string sniRaw = queryParams["sni"]?.Trim().ToLowerInvariant().TrimEnd('.');
                    _sni = string.IsNullOrEmpty(sniRaw) ? null : sniRaw;

                    string obfsRaw = queryParams["obfs"]?.ToLowerInvariant().Trim();
                    _obfs = string.IsNullOrEmpty(obfsRaw) ? null : obfsRaw;

                    string obfsPasswordRaw = queryParams["obfs-password"]?.Trim();
                    _obfsPassword = string.IsNullOrEmpty(obfsPasswordRaw) ? null : obfsPasswordRaw;
                }

                if (string.IsNullOrEmpty(_auth)) _auth = null;

                // Если хост/SNI совпадает с obfuscation или пустышкой — убираем лишнее
                if (_sni == Address)
                {
                    // Можно оставить или сбросить, но для Hysteria SNI важен
                }
            }
            catch { }
        }

        internal override string GetCoreSignature()
        {
            string core = base.GetCoreSignature(); // База: ip + port

            if (!string.IsNullOrEmpty(_auth)) core += $"|auth={_auth}";
            if (!string.IsNullOrEmpty(_sni)) core += $"|sni={_sni}";
            if (!string.IsNullOrEmpty(_obfs)) core += $"|obfs={_obfs}";
            if (!string.IsNullOrEmpty(_obfsPassword)) core += $"|obfs-pwd={_obfsPassword}";

            return core;
        }
    }
}
