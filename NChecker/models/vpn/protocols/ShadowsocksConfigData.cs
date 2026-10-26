using System.Text;
using System.Web;

namespace NChecker.models.vpn.protocols
{
    internal class ShadowsocksConfigData : VpnConfigBase
    {
        private string _encryption, _password;

        internal ShadowsocksConfigData(string a1) : base(a1) { }

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

                if (!mainPart.Contains('@'))
                {
                    mainPart = TryDecodeBase64(mainPart);
                }

                int atIndex = mainPart.LastIndexOf('@');
                if (atIndex != -1)
                {
                    string authPart = mainPart.Substring(0, atIndex);

                    string decodedAuth = TryDecodeBase64(authPart);
                    if (!string.IsNullOrEmpty(decodedAuth) && decodedAuth.Contains(':'))
                    {
                        authPart = decodedAuth;
                    }

                    int colonIndex = authPart.IndexOf(':');
                    if (colonIndex != -1)
                    {
                        string encPart = authPart.Substring(0, colonIndex).Trim();

                        string decodedEnc = TryDecodeBase64(encPart);
                        if (!string.IsNullOrEmpty(decodedEnc) && !decodedEnc.Any(char.IsControl))
                        {
                            _encryption = decodedEnc.ToLowerInvariant();
                        }
                        else
                        {
                            _encryption = encPart.ToLowerInvariant();
                        }

                        _password = authPart.Substring(colonIndex + 1).Trim();
                    }
                    else
                    {
                        _password = authPart.Trim();
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

                // Нормализация в null
                if (string.IsNullOrEmpty(_encryption)) _encryption = null;
                if (string.IsNullOrEmpty(_password)) _password = null;
            }
            catch { }
        }

        private string TryDecodeBase64(string base64)
        {
            try
            {
                base64 = base64.Replace('-', '+').Replace('_', '/');
                switch (base64.Length % 4)
                {
                    case 2: base64 += "=="; break;
                    case 3: base64 += "="; break;
                }
                byte[] bytes = Convert.FromBase64String(base64);
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return string.Empty;
            }
        }

        internal override string GetCoreSignature()
        {
            string core = base.GetCoreSignature(); // База: ip + port

            if (!string.IsNullOrEmpty(_encryption)) core += $"|enc={_encryption}";
            if (!string.IsNullOrEmpty(_password)) core += $"|pwd={_password}";

            return core;
        }
    }
}
