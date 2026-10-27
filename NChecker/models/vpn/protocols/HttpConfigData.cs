using System.Web;

namespace NChecker.models.vpn.protocols
{
    internal class HttpConfigData : VpnConfigBase
    {
        private string _username;
        private string _password;
        bool _tls;

        internal HttpConfigData(string a1) : base(a1) { }

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

                // Очищаем query-параметры, если они есть у http(s) прокси
                int queryIndex = mainPart.IndexOf('?');
                if (queryIndex != -1)
                {
                    mainPart = mainPart.Substring(0, queryIndex).Trim();
                }

                int atIndex = mainPart.LastIndexOf('@');
                if (atIndex != -1)
                {
                    string authPart = mainPart.Substring(0, atIndex);
                    int colonIndex = authPart.IndexOf(':');
                    if (colonIndex != -1)
                    {
                        _username = authPart.Substring(0, colonIndex).Trim();
                        _password = authPart.Substring(colonIndex + 1).Trim();
                    }
                    else
                    {
                        _username = authPart.Trim();
                    }

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

                if (string.IsNullOrEmpty(_username)) _username = null;
                if (string.IsNullOrEmpty(_password)) _password = null;
            }
            catch { }
        }

        internal override string GetCoreSignature()
        {
            string core = base.GetCoreSignature(); // База: ip + port

            if (!string.IsNullOrEmpty(_username)) core += $"|user={_username}";
            if (!string.IsNullOrEmpty(_password)) core += $"|pwd={_password}";

            return core;
        }
    }
}
