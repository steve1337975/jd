using Newtonsoft.Json;
using System.Text;
using System.Web;

namespace NChecker.models.vpn.protocols
{
    internal class VmessConfigData : VpnConfigBase
    {
        private string _id, _network, _type, _host, _path, _tls, _sni;

        internal VmessConfigData(string a1) : base(a1) { }

        protected override void Parse(string url)
        {
            try
            {
                int schemeIndex = url.IndexOf("://");
                if (schemeIndex == -1) return;
                string base64Part = url.Substring(schemeIndex + 3).Trim();

                int hashIndex = base64Part.IndexOf('#');
                if (hashIndex != -1)
                {
                    Name = HttpUtility.UrlDecode(base64Part.Substring(hashIndex + 1)).Trim();
                    base64Part = base64Part.Substring(0, hashIndex).Trim();
                }

                string jsonString = TryDecodeBase64(base64Part);
                if (string.IsNullOrEmpty(jsonString)) return;

                var jsonModel = JsonConvert.DeserializeObject<VmessJsonModel>(jsonString);
                if (jsonModel != null)
                {
                    Address = jsonModel.Address?.Trim().ToLowerInvariant();
                    Port = Convert.ToInt32(jsonModel.Port ?? 0);

                    string rawId = jsonModel.Id ?? string.Empty;
                    _id = new string(rawId.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
                    if (string.IsNullOrEmpty(_id)) _id = null;

                    string netRaw = jsonModel.Net?.ToLowerInvariant().Trim();
                    _network = string.IsNullOrEmpty(netRaw) ? "tcp" : netRaw;

                    string typeRaw = jsonModel.Type?.ToLowerInvariant().Trim();
                    _type = (string.IsNullOrEmpty(typeRaw) || typeRaw == "raw" || typeRaw == "tcp") ? null : typeRaw;

                    string tlsRaw = jsonModel.Tls?.ToLowerInvariant().Trim();
                    _tls = string.IsNullOrEmpty(tlsRaw) ? null : tlsRaw;

                    string sniRaw = jsonModel.Sni?.Trim().ToLowerInvariant().TrimEnd('.');
                    _sni = string.IsNullOrEmpty(sniRaw) ? null : sniRaw;

                    string hostRaw = jsonModel.Host?.Trim().ToLowerInvariant().TrimEnd('.');
                    _host = string.IsNullOrEmpty(hostRaw) ? null : hostRaw;

                    _path = jsonModel.Path?.Trim();

                    // Тотальная зачистка мусорных путей (метрики, хэши, трюки с `=` и `%3D`)
                    if (string.IsNullOrEmpty(_path) || _path == "/" || _path.Contains("=") || _path.Contains("%3D") || _path.Contains("metrics"))
                    {
                        _path = null;
                    }

                    // Если host дублирует SNI, сбрасываем в null
                    if (_host == _sni)
                    {
                        _host = null;
                    }

                    if (!string.IsNullOrEmpty(jsonModel.Remarks) && string.IsNullOrEmpty(Name))
                    {
                        Name = HttpUtility.UrlDecode(jsonModel.Remarks).Trim();
                    }
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

        internal override string GetCoreSignature()
        {
            string core = base.GetCoreSignature(); // База: ip + port

            if (!string.IsNullOrEmpty(_id)) core += $"|id={_id}";
            if (!string.IsNullOrEmpty(_network)) core += $"|net={_network}";
            if (!string.IsNullOrEmpty(_type)) core += $"|type={_type}";
            if (!string.IsNullOrEmpty(_tls)) core += $"|tls={_tls}";
            if (!string.IsNullOrEmpty(_sni)) core += $"|sni={_sni}";
            if (!string.IsNullOrEmpty(_host)) core += $"|host={_host}";
            if (!string.IsNullOrEmpty(_path)) core += $"|path={_path}";

            return core;
        }
    }
}
