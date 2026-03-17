using BOG.RelayHub.Client.Entity;
using BOG.SwissArmyKnife;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Security;

namespace BOG.RelayHub.Client
{
    public enum RelayHubApiResultHandling : int
    {
        Indeterminate = -1,
        Success = 0,
        NoSuchChannel = 1,
        ItemDoesNotExist = 2,
        AuthError = 3,
        SubmissionError = 4,
        LimitationError = 5,
        ServerError = 6,
        UnhandledError = 7
    }

    public class RelayHubRestCalls
    {
        private readonly HttpClient _httpClientExecutiveToken;
        private readonly HttpClient _httpClientAdminToken;
        private readonly HttpClient _httpClientUserToken;
        private readonly HttpClient _httpClientUserTokenList;
        private readonly HttpClient _httpClient;
        private readonly HttpClientHandler _HttpClientHandler;
        private readonly RelayHubApiConfig _ClientConfig = new RelayHubApiConfig();

        private static readonly string[] _SslPolicyErrors_Names = Enum.GetNames<SslPolicyErrors>();
        private static readonly SslPolicyErrors[] _SslPolicyErrors_Values = Enum.GetValues<SslPolicyErrors>();

        private readonly ILogger _Logger;

        /// <summary>
        /// Instantiation
        /// </summary>
        /// <param name="config"></param>
        /// <param name="logger"></param>
        /// <exception cref="Exception"></exception>
        public RelayHubRestCalls(RelayHubApiConfig config, ILogger logger)
        {
            _ClientConfig = config ?? throw new Exception("RelayHubApiConfig object not in configuration or in error.");
            _Logger = logger;
            _HttpClientHandler = new HttpClientHandler();
            _httpClient = new HttpClient(_HttpClientHandler);
            _httpClientExecutiveToken = new HttpClient(_HttpClientHandler);
            _httpClientAdminToken = new HttpClient(_HttpClientHandler);
            _httpClientUserToken = new HttpClient(_HttpClientHandler);
            _httpClientUserTokenList = new HttpClient(_HttpClientHandler);
            Initialize();
        }

        /// <summary>
        /// Instantiation with local logging object.
        /// </summary>
        /// <param name="config"></param>
        /// <exception cref="Exception"></exception>
        public RelayHubRestCalls(RelayHubApiConfig config)
        {
            _ClientConfig = config ?? throw new Exception("RelayHubApiConfig object not in configuration or in error.");
            _Logger = new Microsoft.Extensions.Logging.LoggerFactory().CreateLogger("RelayHubRestCalls");
            _HttpClientHandler = new HttpClientHandler();
            _httpClient = new HttpClient(_HttpClientHandler);
            _httpClientExecutiveToken = new HttpClient(_HttpClientHandler);
            _httpClientAdminToken = new HttpClient(_HttpClientHandler);
            _httpClientUserToken = new HttpClient(_HttpClientHandler);
            _httpClientUserTokenList = new HttpClient(_HttpClientHandler);
            Initialize();
        }

        private void Initialize()
        {
            _ClientConfig.Validate();

            _httpClient.DefaultRequestHeaders.Remove("Accept");
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/plain"));
            _httpClient.Timeout = TimeSpan.FromSeconds(_ClientConfig.TimeoutSeconds);

            _httpClientExecutiveToken.DefaultRequestHeaders.Remove("Accept");
            _httpClientExecutiveToken.DefaultRequestHeaders.Accept.Clear();
            _httpClientExecutiveToken.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/plain"));
            _httpClientExecutiveToken.DefaultRequestHeaders.Add("AuthToken", _ClientConfig.ExecutiveTokenValue);
            _httpClientExecutiveToken.Timeout = TimeSpan.FromSeconds(_ClientConfig.TimeoutSeconds);

            _httpClientAdminToken.DefaultRequestHeaders.Remove("Accept");
            _httpClientAdminToken.DefaultRequestHeaders.Accept.Clear();
            _httpClientAdminToken.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/plain"));
            _httpClientAdminToken.DefaultRequestHeaders.Add("AuthToken", _ClientConfig.AdministrativeTokenValue);
            _httpClientAdminToken.Timeout = TimeSpan.FromSeconds(_ClientConfig.TimeoutSeconds);

            _httpClientUserToken.DefaultRequestHeaders.Remove("Accept");
            _httpClientUserToken.DefaultRequestHeaders.Accept.Clear();
            _httpClientUserToken.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/plain"));
            _httpClientUserToken.DefaultRequestHeaders.Add("AuthToken", _ClientConfig.UsageTokenValue);
            _httpClientUserToken.Timeout = TimeSpan.FromSeconds(_ClientConfig.TimeoutSeconds);

            _httpClientUserTokenList.DefaultRequestHeaders.Remove("Accept");
            _httpClientUserTokenList.DefaultRequestHeaders.Accept.Clear();
            _httpClientUserTokenList.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            _httpClientUserTokenList.DefaultRequestHeaders.Add("AuthToken", _ClientConfig.UsageTokenValue);
            _httpClientUserTokenList.Timeout = TimeSpan.FromSeconds(_ClientConfig.TimeoutSeconds);

            if (_ClientConfig.IgnoreBadSSL)
            {
                _HttpClientHandler.ServerCertificateCustomValidationCallback += (requestMessage, certificate, chain, sslErrors) =>
                {
                    _Logger.LogTrace($"=============================================================================");
                    _Logger.LogTrace($"Check certificate for https operation.");
                    _Logger.LogTrace($"Requested URI: {requestMessage.RequestUri}");
                    if (certificate != null)
                    {
                        _Logger.LogTrace($"Effective date: {certificate.GetEffectiveDateString()}");
                        _Logger.LogTrace($"Expiration date: {certificate.GetExpirationDateString()}");
                        _Logger.LogTrace($"Issuer: {certificate.Issuer}");
                        _Logger.LogTrace($"Subject: {certificate.Subject}");
                    }
                    else
                    {
                        _Logger.LogTrace($"No certificate information object");
                    }
                    _Logger.LogTrace($"SslPolicyErrors:");
                    for (var enumIndex = 0; enumIndex < _SslPolicyErrors_Names.Length; enumIndex++)
                        if ((_SslPolicyErrors_Values[enumIndex] | sslErrors) == _SslPolicyErrors_Values[enumIndex])
                            _Logger.LogTrace($" {_SslPolicyErrors_Names}");
                    _Logger.LogTrace(string.Empty);

                    var result = false;
                    switch (sslErrors)
                    {
                        case SslPolicyErrors.None:
                            result = true;
                            break;

                        case SslPolicyErrors.RemoteCertificateChainErrors:
                        case SslPolicyErrors.RemoteCertificateNameMismatch:
                        case SslPolicyErrors.RemoteCertificateNotAvailable:
                            result = true;
                            break;

                        default:
                            break;
                    }
                    _Logger.LogTrace($"Ignoring error(s): {result}");
                    return result;
                };
            }
        }

        ~RelayHubRestCalls()
        {
            if (_ClientConfig.IgnoreBadSSL)
            {
                _HttpClientHandler.Dispose();
            }
        }

        private RelayHubApiResultHandling GetRelayHubApiResultHandling(HttpStatusCode statusCode)
        {
            switch (statusCode)
            {
                case HttpStatusCode.OK:
                case HttpStatusCode.Created:
                    return RelayHubApiResultHandling.Success;
                case HttpStatusCode.NoContent:
                    return RelayHubApiResultHandling.ItemDoesNotExist;
                case HttpStatusCode.PreconditionRequired:
                    return RelayHubApiResultHandling.NoSuchChannel;
                case HttpStatusCode.Unauthorized:
                case HttpStatusCode.Forbidden:
                case HttpStatusCode.UnavailableForLegalReasons:
                    return RelayHubApiResultHandling.AuthError;
                case HttpStatusCode.BadRequest:
                    return RelayHubApiResultHandling.SubmissionError;
                case HttpStatusCode.Conflict:
                case HttpStatusCode.TooManyRequests:
                    return RelayHubApiResultHandling.LimitationError;
                case HttpStatusCode.InternalServerError:
                case HttpStatusCode.ServiceUnavailable:
                    return RelayHubApiResultHandling.ServerError;
                default:
                    return RelayHubApiResultHandling.UnhandledError;
            }
        }

        private void HydrateBaseResult(HttpResponseMessage response, RelayHubApiResult obj)
        {
            obj.StatusCode = (int)response.StatusCode;
            obj.HandleAs = GetRelayHubApiResultHandling(response.StatusCode);
            obj.StatusDetail = response.ReasonPhrase ?? response.StatusCode.ToString();
            obj.Payload = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// (PUBLIC): Respond with app info for heartbeat confirmation
        /// </summary>
        /// <returns></returns>
        public RelayHubApiResult CheckHeartbeat()
        {
            RelayHubApiResult result = new RelayHubApiResult();

            try
            {
                var response = _httpClient.GetAsync(
                    _ClientConfig.BaseURI + "/api/v1",
                    HttpCompletionOption.ResponseContentRead
                ).GetAwaiter().GetResult();

                HydrateBaseResult(response, result);
            }
            catch (Exception err)
            {
                result.HandleAs = RelayHubApiResultHandling.UnhandledError;
                result.StatusDetail = string.Empty;
                result.Payload = DetailedException.WithUserContent(ref err);

            }
            return result;
        }

        /// <summary>
        /// (Administrative): Determine is channel already exists.
        /// </summary>
        /// <returns></returns>
        public RelayHubApiResult ChannelExists(string channel)
        {
            RelayHubApiResult result = new RelayHubApiResult();

            try
            {
                var response = _httpClientAdminToken.GetAsync(
                    _ClientConfig.BaseURI + $"/api/v1/channel/manage/{channel}",
                    HttpCompletionOption.ResponseContentRead
                ).GetAwaiter().GetResult();

                HydrateBaseResult(response, result);
            }
            catch (Exception err)
            {
                result.HandleAs = RelayHubApiResultHandling.UnhandledError;
                result.StatusDetail = string.Empty;
                result.Payload = DetailedException.WithUserContent(ref err);

            }
            return result;

        }

        /// <summary>
        /// (Administrative): Create a channel.
        /// </summary>
        /// <returns></returns>
        public RelayHubApiResult ChannelCreate(string channel)
        {
            RelayHubApiResult result = new RelayHubApiResult();
            try
            {
                var response = _httpClientAdminToken.PostAsync(
                    _ClientConfig.BaseURI + $"/api/v1/channel/manage/{channel}",
                    new StringContent(string.Empty)
                ).GetAwaiter().GetResult();

                HydrateBaseResult(response, result);
            }
            catch (Exception err)
            {
                result.HandleAs = RelayHubApiResultHandling.UnhandledError;
                result.StatusDetail = string.Empty;
                result.Payload = DetailedException.WithUserContent(ref err);
            }
            return result;
        }

        /// <summary>
        /// (Administrative): Remove a channel and its content.
        /// </summary>
        /// <returns></returns>
        public RelayHubApiResult ChannelRemove(string channel)
        {
            RelayHubApiResult result = new RelayHubApiResult();
            try
            {
                var response = _httpClientAdminToken.DeleteAsync(
                    _ClientConfig.BaseURI + $"/api/v1/channel/manage/{channel}"
                ).GetAwaiter().GetResult();

                HydrateBaseResult(response, result);
            }
            catch (Exception err)
            {
                result.HandleAs = RelayHubApiResultHandling.UnhandledError;
                result.StatusDetail = string.Empty;
                result.Payload = DetailedException.WithUserContent(ref err);
            }
            return result;
        }

        /// <summary>
        /// (Executive): List all channel names
        /// </summary>
        /// <returns></returns>
        public RelayHubApiResult ListAllChannels()
        {
            RelayHubApiResult result = new RelayHubApiResult();
            try
            {
                var response = _httpClientExecutiveToken.GetAsync(
                    _ClientConfig.BaseURI + "/api/v1/executive/channels/list"
                ).GetAwaiter().GetResult();

                HydrateBaseResult(response, result);
            }
            catch (Exception err)
            {
                result.HandleAs = RelayHubApiResultHandling.UnhandledError;
                result.StatusDetail = string.Empty;
                result.Payload = DetailedException.WithUserContent(ref err);
            }
            return result;
        }

        /// <summary>
        /// (Administrative): Get statistics for a channel.
        /// </summary>
        /// <returns></returns>
        public RelayHubApiResult ChannelStatistics(string channel)
        {
            RelayHubApiResult result = new RelayHubApiResult();
            try
            {
                var response = _httpClientAdminToken.GetAsync(
                    _ClientConfig.BaseURI + $"/api/v1/channel/statistics/{channel}"
                ).GetAwaiter().GetResult();

                HydrateBaseResult(response, result);
            }
            catch (Exception err)
            {
                result.HandleAs = RelayHubApiResultHandling.UnhandledError;
                result.StatusDetail = string.Empty;
                result.Payload = DetailedException.WithUserContent(ref err);
            }
            return result;
        }

        /// <summary>
        /// (Executive): Delete all channels (soft restart).  Essentially a live equivalent to cmdline parm --FreshStart true
        /// </summary>
        /// <returns></returns>
        public RelayHubApiResult DeleteAllChannels()
        {
            RelayHubApiResult result = new RelayHubApiResult();
            try
            {
                var response = _httpClientExecutiveToken.DeleteAsync(
                    _ClientConfig.BaseURI + "/api/v1/executive/channels"
                ).GetAwaiter().GetResult();

                HydrateBaseResult(response, result);
            }
            catch (Exception err)
            {
                result.HandleAs = RelayHubApiResultHandling.UnhandledError;
                result.StatusDetail = string.Empty;
                result.Payload = DetailedException.WithUserContent(ref err);
            }
            return result;
        }

        /// <summary>
        /// (Executive): Get secuirty list (client using bad tokens)
        /// </summary>
        /// <returns></returns>
        public RelayHubApiResult GetSecurityList()
        {
            RelayHubApiResult result = new RelayHubApiResult();
            try
            {
                var response = _httpClientExecutiveToken.GetAsync(
                    _ClientConfig.BaseURI + "/api/v1/executive/security"
                ).GetAwaiter().GetResult();

                HydrateBaseResult(response, result);
            }
            catch (Exception err)
            {
                result.HandleAs = RelayHubApiResultHandling.UnhandledError;
                result.StatusDetail = string.Empty;
                result.Payload = DetailedException.WithUserContent(ref err);
            }
            return result;
        }

        /// <summary>
        /// (Executive): Remove all channels.  Essentially a soft reset.
        /// </summary>
        /// <returns></returns>
        public RelayHubApiResult StatisticsAllChannels()
        {
            RelayHubApiResult result = new RelayHubApiResult();
            try
            {
                var response = _httpClientExecutiveToken.DeleteAsync(
                    _ClientConfig.BaseURI + "/api/v1/executive/channels"
                ).GetAwaiter().GetResult();

                HydrateBaseResult(response, result);
            }
            catch (Exception err)
            {
                result.HandleAs = RelayHubApiResultHandling.UnhandledError;
                result.StatusDetail = string.Empty;
                result.Payload = DetailedException.WithUserContent(ref err);
            }
            return result;
        }

        /// <summary>
        /// (Executive): Get statistics for all channels
        /// </summary>
        /// <returns></returns>
        public RelayHubApiResult ResetSecurityList()
        {
            RelayHubApiResult result = new RelayHubApiResult();
            try
            {
                var response = _httpClientExecutiveToken.DeleteAsync(
                    _ClientConfig.BaseURI + "/api/v1/executive/security"
                ).GetAwaiter().GetResult();

                HydrateBaseResult(response, result);
            }
            catch (Exception err)
            {
                result.HandleAs = RelayHubApiResultHandling.UnhandledError;
                result.StatusDetail = string.Empty;
                result.Payload = DetailedException.WithUserContent(ref err);
            }
            return result;
        }

        /// <summary>
        /// (Executive): Initiate a shurdown (command-line), or service restart (IIS or systemctl)
        /// </summary>
        /// <returns></returns>
        public RelayHubApiResult InitiateShutdown()
        {
            RelayHubApiResult result = new RelayHubApiResult();
            try
            {
                var response = _httpClientExecutiveToken.DeleteAsync(
                    _ClientConfig.BaseURI + "/api/v1/executive/shutdown"
                ).GetAwaiter().GetResult();

                HydrateBaseResult(response, result);
            }
            catch (Exception err)
            {
                result.HandleAs = RelayHubApiResultHandling.UnhandledError;
                result.StatusDetail = string.Empty;
                result.Payload = DetailedException.WithUserContent(ref err);
            }
            return result;
        }

        /// <summary>
        /// (User): Enqueue a new item in the channel for a recipient
        /// </summary>
        /// <returns></returns>
        public RelayHubApiResult Enqueue(string channel, string recipient, string payload)
        {
            RelayHubApiResult result = new RelayHubApiResult();

            try
            {
                var response = _httpClientUserToken.PostAsync(
                    _ClientConfig.BaseURI + $"/api/v1/queue/{channel}/{recipient}",
                    new StringContent(payload, mediaType: new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain"))).GetAwaiter().GetResult();

                HydrateBaseResult(response, result);
            }
            catch (Exception err)
            {
                result.HandleAs = RelayHubApiResultHandling.UnhandledError;
                result.StatusDetail = string.Empty;
                result.Payload = DetailedException.WithUserContent(ref err);
            }
            return result;
        }

        /// <summary>
        /// (User): retrieve next queue item in the channel for a recipient
        /// </summary>
        /// <returns></returns>
        public RelayHubApiResult Dequeue(string channel, string recipient)
        {
            RelayHubApiResult result = new RelayHubApiResult();

            try
            {
                var response = _httpClientUserToken.GetAsync(
                    _ClientConfig.BaseURI + $"/api/v1/queue/{channel}/{recipient}",
                    HttpCompletionOption.ResponseContentRead
                ).GetAwaiter().GetResult();

                HydrateBaseResult(response, result);
            }
            catch (Exception err)
            {
                result.HandleAs = RelayHubApiResultHandling.UnhandledError;
                result.StatusDetail = string.Empty;
                result.Payload = DetailedException.WithUserContent(ref err);
            }
            return result;
        }

        /// <summary>
        /// (Administrative): Remove a recipient and its items from the queue in a channel.
        /// </summary>
        /// <returns></returns>
        public RelayHubApiResult DeleteQueueRecipient(string channel, string recipient)
        {
            RelayHubApiResult result = new RelayHubApiResult();

            try
            {
                var response = _httpClientUserToken.DeleteAsync(
                    _ClientConfig.BaseURI + $"/api/v1/queue/{channel}/{recipient}"
                ).GetAwaiter().GetResult();

                HydrateBaseResult(response, result);
            }
            catch (Exception err)
            {
                result.HandleAs = RelayHubApiResultHandling.UnhandledError;
                result.StatusDetail = string.Empty;
                result.Payload = DetailedException.WithUserContent(ref err);
            }
            return result;
        }

        /// <summary>
        /// (User): return a list of keys for all reference items in the channel.
        /// </summary>
        /// <returns></returns>
        public RelayHubApiResult ReferenceGetKeyList(string channel)
        {
            RelayHubApiResult result = new RelayHubApiResult();

            try
            {
                var response = _httpClientUserTokenList.GetAsync(
                    _ClientConfig.BaseURI + $"/api/v1/reference/{channel}",
                    HttpCompletionOption.ResponseContentRead
                ).GetAwaiter().GetResult();

                HydrateBaseResult(response, result);
            }
            catch (Exception err)
            {
                result.HandleAs = RelayHubApiResultHandling.UnhandledError;
                result.StatusDetail = string.Empty;
                result.Payload = DetailedException.WithUserContent(ref err);
            }
            return result;
        }

        /// <summary>
        /// (User): return the reference value in the channel for the key provided.
        /// </summary>
        /// <returns></returns>
        public RelayHubApiResult ReferenceRead(string channel, string key)
        {
            RelayHubApiResult result = new RelayHubApiResult();

            try
            {
                var response = _httpClientUserToken.GetAsync(
                    _ClientConfig.BaseURI + $"/api/v1/reference/{channel}/{key}",
                    HttpCompletionOption.ResponseContentRead
                ).GetAwaiter().GetResult();

                HydrateBaseResult(response, result);
            }
            catch (Exception err)
            {
                result.HandleAs = RelayHubApiResultHandling.UnhandledError;
                result.StatusDetail = string.Empty;
                result.Payload = DetailedException.WithUserContent(ref err);
            }
            return result;
        }

        /// <summary>
        /// (User): Add/Update a reference value in the channel for the key provided.
        /// </summary>
        /// <returns></returns>
        public RelayHubApiResult ReferenceWrite(string channel, string key, string payload)
        {
            RelayHubApiResult result = new RelayHubApiResult();
            try
            {
                var response = _httpClientUserToken.PostAsync(
                    _ClientConfig.BaseURI + $"/api/v1/reference/{channel}/{key}",
                    new StringContent(payload)
                ).GetAwaiter().GetResult();

                HydrateBaseResult(response, result);
            }
            catch (Exception err)
            {
                result.HandleAs = RelayHubApiResultHandling.UnhandledError;
                result.StatusDetail = string.Empty;
                result.Payload = DetailedException.WithUserContent(ref err);
            }
            return result;
        }

        /// <summary>
        /// (User): remove the reference in the channel for the key provided.
        /// </summary>
        /// <returns></returns>
        public RelayHubApiResult ReferenceDelete(string channel, string key)
        {
            RelayHubApiResult result = new RelayHubApiResult();
            try
            {
                var response = _httpClientUserToken.DeleteAsync(
                    _ClientConfig.BaseURI + $"/api/v1/reference/{channel}/{key}"
                ).GetAwaiter().GetResult();

                HydrateBaseResult(response, result);
            }
            catch (Exception err)
            {
                result.HandleAs = RelayHubApiResultHandling.UnhandledError;
                result.StatusDetail = string.Empty;
                result.Payload = DetailedException.WithUserContent(ref err);
            }
            return result;
        }
    }
}
