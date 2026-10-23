using System.Text;
using System.Text.RegularExpressions;
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

                // Если нет '@', вся строка до решетки — это base64
                if (!mainPart.Contains('@'))
                {
                    mainPart = TryDecodeBase64(mainPart);
                }

                int atIndex = mainPart.LastIndexOf('@');
                if (atIndex != -1)
                {
                    string authPart = mainPart.Substring(0, atIndex);

                    // Если вся authPart целиком является Base64 (стандарт SIP002)
                    string decodedAuth = TryDecodeBase64(authPart);
                    if (!string.IsNullOrEmpty(decodedAuth) && decodedAuth.Contains(':'))
                    {
                        authPart = decodedAuth;
                    }

                    int colonIndex = authPart.IndexOf(':');
                    if (colonIndex != -1)
                    {
                        string encPart = authPart.Substring(0, colonIndex).Trim();

                        // Если шифр закодирован в Base64 (как в твоем примере YWVzLTI1Ni1nY20)
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

        private bool IsBase64String(string s)
        {
            s = s.Trim().Replace('-', '+').Replace('_', '/');
            return (s.Length % 4 == 0) && Regex.IsMatch(s, @"^[a-zA-Z0-9\+/]*={0,2}$", RegexOptions.None);
        }

        internal override string GetCoreSignature()
        {
            string core = base.GetCoreSignature();

            if (!string.IsNullOrEmpty(_encryption)) core += $"|enc={_encryption.Trim()}";
            if (!string.IsNullOrEmpty(_password)) core += $"|pwd={_password.Trim()}";

            return core;
        }
    }
}
