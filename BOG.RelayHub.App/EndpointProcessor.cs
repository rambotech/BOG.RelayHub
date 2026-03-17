using BOG.RelayHub.Common.Entity;
using BOG.SwissArmyKnife;
using BOG.SwissArmyKnife.Extensions;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging.Configuration;
using System.Net;
using System.Runtime.InteropServices;
using System.Timers;

namespace BOG.RelayHub
{
    /// <summary>
    /// Endpoint methods implementations
    /// </summary>
    public class EndpointProcessor
    {
        private enum AuthorizationType : int
        {
            Blocked = -1, // quarantined currently due to too many failed attempts: token value was not even evaluated.
            None = 0, // matches no expected token values
            User = 1,
            Administrative = 2,
            Executive = 3
        }

        private enum QuarantinedStateAction : int
        {
            Disallow = 0,
            AllowWithoutReset = 1,
            AllowAndReset = 2
        }

        private const string IpAddressForNoClient = "169.254.86.86";

        private readonly ILogger _Logger;
        private readonly IConfiguration _Config;
        private readonly AssemblyVersion _AssemblyVersion;

        private readonly RelayHubConfig _RelayHubConfig;

        private readonly Dictionary<string, Entity.Channel> _Channels = new Dictionary<string, Entity.Channel>();
        private readonly Dictionary<string, object> _ChannelIsLocked = new Dictionary<string, object>();
        private readonly object _LockChannelList = new object();

        private readonly Dictionary<string, Security> _ClientSecurity = new Dictionary<string, Security>();

        readonly static System.Timers.Timer _AppShutdownTimer = new System.Timers.Timer();

        /// <summary>
        /// Processor for endpoint activity.
        /// </summary>
        /// <param name="config"></param>
        /// <param name="logger"></param>
        /// <param name="assemblyVersion"></param>
        /// <param name="relayHubConfig"></param>
        /// <exception cref="Exception"></exception>
        public EndpointProcessor(IConfiguration config, ILogger logger, IAssemblyVersion assemblyVersion, RelayHubConfig relayHubConfig)
        {
            _Config = config;
            var logFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddConfiguration(_Config)
                    .AddConsole();
            });
            _Logger = logger;

            _AssemblyVersion = (AssemblyVersion)assemblyVersion;

            _RelayHubConfig = relayHubConfig;

            if (!string.IsNullOrWhiteSpace(_RelayHubConfig.RootStoragePath))
            {
                _RelayHubConfig.RootStoragePath = ResolveLocalSpec("$HOME");
            }
            _RelayHubConfig.RootStoragePath = Path.Combine(_RelayHubConfig.RootStoragePath, "relayhub");
            _Logger.LogDebug($"Root Storage: {_RelayHubConfig.RootStoragePath}");

            if (File.Exists(_RelayHubConfig.RootStoragePath))
            {
                var msg = $"Root path is an existing file. Must be a path, or non-existent: {_RelayHubConfig.RootStoragePath}";
                _Logger.LogError($"Root Storage: {_RelayHubConfig.RootStoragePath}");
                throw new Exception(msg);
            }

            _Logger.LogDebug($"Folder repo setup");

            if (_RelayHubConfig.FreshStart && Directory.Exists(_RelayHubConfig.RootStoragePath))
            {
                Directory.Delete(_RelayHubConfig.RootStoragePath, true);
            }
            if (!Directory.Exists(_RelayHubConfig.RootStoragePath))
            {
                Directory.CreateDirectory(_RelayHubConfig.RootStoragePath);
            }
            if (!_RelayHubConfig.FreshStart)
            {
                _Logger.LogDebug($"Recover and index existing channels and queue filenames");
                // register any existing channel information needed in memory.
                foreach (var channelDir in Directory.EnumerateDirectories(_RelayHubConfig.RootStoragePath, "*.*", SearchOption.TopDirectoryOnly))
                {
                    var channelName = channelDir.Substring(_RelayHubConfig.RootStoragePath.Length + 1);
                    _Logger.LogTrace($" ...: {channelName}");
                    RecoverChannelEntry(channelName);
                }
            }

            _AppShutdownTimer.AutoReset = false;
            _AppShutdownTimer.Enabled = false;
            _AppShutdownTimer.Elapsed += (object? source, ElapsedEventArgs e) =>
            {
                _AppShutdownTimer.Enabled = false;
                System.Environment.Exit(0);
            };
            _AppShutdownTimer.Interval = 3000;
        }

        /// <summary>
        /// Destructor
        /// </summary>
        ~EndpointProcessor()
        {
            //_AppShutdownTimer.Enabled = false;
            //_AppShutdownTimer.Elapsed -= new ElapsedEventHandler(OnAppShutdownEvent);
        }
        #region Endpoint processing

        /// <summary>
        /// Heartbeat check
        /// </summary>
        /// <param name="thisContext"></param>
        public void Heartbeat(HttpContext thisContext)
        {
            _Logger.LogInformation($"Heartbeat");
            var payloadBody = string.Empty;
            try
            {
                thisContext.Response.StatusCode = (int)HttpStatusCode.OK;
                thisContext.Response.ContentType = "application/json";
                payloadBody = Serializer<AssemblyVersion>.ToJson(_AssemblyVersion);
            }
            catch (Exception ex)
            {
                _Logger.LogError(DetailedException.WithEnterpriseContent(ref ex));
                thisContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                thisContext.Response.ContentType = "text/plain";
                payloadBody = $"Exception: {ex.Message}\r\nStack Trace:\r\n{ex.StackTrace}";
            }
            using (var sw = new StreamWriter(thisContext.Response.Body))
            {
                sw.WriteAsync(payloadBody).GetAwaiter().GetResult();
                sw.FlushAsync().GetAwaiter().GetResult();
                // sw.DisposeAsync().GetAwaiter().GetResult();
            }
            _Logger.LogInformation($"Returns: {thisContext.Response.StatusCode}");
        }

        /// <summary>
        /// Determine if a channel name already exists.
        /// </summary>
        /// <param name="thisContext"></param>
        /// <param name="authToken">To authenticate the action permission.</param>
        /// <param name="channel"></param>
        public void ChannelExists(HttpContext thisContext, string authToken, string channel)
        {
            _Logger.LogInformation($"channelExists: {channel}");
            var payloadBody = string.Empty;

            AuthorizationType authorizationType = GetAuthorizationType(
                authToken, thisContext, QuarantinedStateAction.Disallow);
            if (authorizationType < AuthorizationType.Administrative)
            {
                thisContext.Response.ContentType = "text/plain";
                switch (authorizationType)
                {
                    case AuthorizationType.Blocked:
                        thisContext.Response.StatusCode = (int)HttpStatusCode.UnavailableForLegalReasons;
                        payloadBody = "Excessive authentication failures";
                        break;
                    default:
                        thisContext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        payloadBody = "Invalid authentication for request (requires administrative or higher token value)";
                        break;
                }
            }
            else
            {
                try
                {
                    if (!_Channels.ContainsKey(channel))
                    {
                        thisContext.Response.StatusCode = (int)HttpStatusCode.NoContent;
                    }
                    else
                    {
                        thisContext.Response.StatusCode = (int)HttpStatusCode.OK;
                    }
                    thisContext.Response.ContentType = "text/plain";
                }
                catch (Exception ex)
                {
                    _Logger.LogError(DetailedException.WithEnterpriseContent(ref ex));
                    payloadBody = $"Exception: {ex.Message}\r\nStack Trace:\r\n{ex.StackTrace}";
                    thisContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    thisContext.Response.ContentType = "text/plain";
                }
            }
            using (var sw = new StreamWriter(thisContext.Response.Body))
            {
                sw.WriteAsync(payloadBody).GetAwaiter().GetResult();
                sw.FlushAsync().GetAwaiter().GetResult();
                // sw.DisposeAsync().GetAwaiter().GetResult();
            }
            _Logger.LogInformation($"Returns: {thisContext.Response.StatusCode}");
        }

        /// <summary>
        /// Create a new channel
        /// </summary>
        /// <param name="thisContext"></param>
        /// <param name="authToken"></param>
        /// <param name="channel"></param>
        public void ChannelCreate(HttpContext thisContext, string authToken, string channel)
        {
            var payloadBody = string.Empty;
            thisContext.Response.ContentType = "text/plain";

            AuthorizationType authorizationType = GetAuthorizationType(
                authToken, thisContext, QuarantinedStateAction.Disallow);
            if (authorizationType < AuthorizationType.Administrative)
            {
                switch (authorizationType)
                {
                    case AuthorizationType.Blocked:
                        thisContext.Response.StatusCode = (int)HttpStatusCode.UnavailableForLegalReasons;
                        payloadBody = "Excessive authentication failures";
                        break;
                    default:
                        thisContext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        payloadBody = "Invalid authentication for request (requires administrative token value)";
                        break;
                }
            }
            else
            {
                try
                {
                    if (_Channels.ContainsKey(channel))
                    {
                        thisContext.Response.StatusCode = (int)HttpStatusCode.Conflict;
                    }
                    else
                    {
                        CreateChannelEntry(channel);
                        thisContext.Response.StatusCode = (int)HttpStatusCode.Created;
                    }
                }
                catch (Exception ex)
                {
                    payloadBody = $"Exception: {ex.Message}\r\nStack Trace:\r\n{ex.StackTrace}";
                    thisContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                }
            }
            using (var sw = new StreamWriter(thisContext.Response.Body))
            {
                sw.WriteAsync(payloadBody).GetAwaiter().GetResult();
                sw.FlushAsync().GetAwaiter().GetResult();
                // sw.DisposeAsync().GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// Get the statistics for the channel.
        /// </summary>
        /// <param name="thisContext"></param>
        /// <param name="authToken"></param>
        /// <param name="channel"></param>
        public void ChannelStatistics(HttpContext thisContext, string authToken, string channel)
        {
            var payloadBody = string.Empty;

            AuthorizationType authorizationType = GetAuthorizationType(
                authToken, thisContext, QuarantinedStateAction.Disallow);
            if (authorizationType < AuthorizationType.Administrative)
            {
                thisContext.Response.ContentType = "text/plain";
                switch (authorizationType)
                {
                    case AuthorizationType.Blocked:
                        thisContext.Response.StatusCode = (int)HttpStatusCode.UnavailableForLegalReasons;
                        payloadBody = "Excessive authentication failures";
                        break;
                    default:
                        thisContext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        payloadBody = "Invalid authentication for request (requires administrative token value)";
                        break;
                }
            }
            else if (!_Channels.ContainsKey(channel))
            {
                thisContext.Response.StatusCode = (int)HttpStatusCode.PreconditionRequired;
            }
            else
            {
                try
                {
                    lock (_ChannelIsLocked[channel])
                    {
                        payloadBody = ObjectJsonSerializer<ChannelStatistics>.CreateDocumentFormat(_Channels[channel].Statistics);
                        thisContext.Response.ContentType = "application/json";
                        thisContext.Response.StatusCode = (int)HttpStatusCode.OK;
                    }
                }
                catch (Exception ex)
                {
                    payloadBody = $"Exception: {ex.Message}\r\nStack Trace:\r\n{ex.StackTrace}";
                    thisContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                }
            }
            using (var sw = new StreamWriter(thisContext.Response.Body))
            {
                sw.WriteAsync(payloadBody).GetAwaiter().GetResult();
                sw.FlushAsync().GetAwaiter().GetResult();
                // sw.DisposeAsync().GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// Delete the channel.
        /// </summary>
        /// <param name="thisContext"></param>
        /// <param name="authToken"></param>
        /// <param name="channel"></param>
        public void ChannelDelete(HttpContext thisContext, string authToken, string channel)
        {
            var payloadBody = string.Empty;
            thisContext.Response.ContentType = "text/plain";

            AuthorizationType authorizationType = GetAuthorizationType(
                authToken, thisContext, QuarantinedStateAction.Disallow);
            if (authorizationType < AuthorizationType.Administrative)
            {
                switch (authorizationType)
                {
                    case AuthorizationType.Blocked:
                        thisContext.Response.StatusCode = (int)HttpStatusCode.UnavailableForLegalReasons;
                        payloadBody = "Excessive authentication failures";
                        break;
                    default:
                        thisContext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        payloadBody = "Invalid authentication for request (requires administrative token value)";
                        break;
                }
            }
            else if (!_Channels.ContainsKey(channel))
            {
                thisContext.Response.StatusCode = (int)HttpStatusCode.NoContent;
            }
            else
            {
                try
                {
                    lock (_ChannelIsLocked[channel])
                    {
                        RemoveChannelEntry(channel);
                        thisContext.Response.StatusCode = (int)HttpStatusCode.OK;
                    }
                    _ChannelIsLocked.Remove(channel);
                }
                catch (Exception ex)
                {
                    payloadBody = $"Exception: {ex.Message}\r\nStack Trace:\r\n{ex.StackTrace}";
                    thisContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                }
            }
            using (var sw = new StreamWriter(thisContext.Response.Body))
            {
                sw.WriteAsync(payloadBody).GetAwaiter().GetResult();
                sw.FlushAsync().GetAwaiter().GetResult();
                // sw.DisposeAsync().GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// Add a new item to the recipient's queue.
        /// </summary>
        /// <param name="thisContext"></param>
        /// <param name="authToken"></param>
        /// <param name="channel"></param>
        /// <param name="recipient"></param>
        public void QueueStore(HttpContext thisContext, string authToken, string channel, string recipient)
        {
            var payloadBody = string.Empty;
            thisContext.Response.ContentType = "text/plain";
            var requestBody = string.Empty;

            AuthorizationType authorizationType = GetAuthorizationType(
                authToken, thisContext, QuarantinedStateAction.Disallow);
            if (authorizationType < AuthorizationType.User)
            {
                thisContext.Response.ContentType = "text/plain";
                switch (authorizationType)
                {
                    case AuthorizationType.Blocked:
                        thisContext.Response.StatusCode = (int)HttpStatusCode.UnavailableForLegalReasons;
                        payloadBody = "Excessive authentication failures";
                        break;
                    default:
                        thisContext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        payloadBody = "Invalid authentication for request (requires user token value)";
                        break;
                }
            }
            else if (!_Channels.ContainsKey(channel))
            {
                thisContext.Response.StatusCode = (int)HttpStatusCode.PreconditionRequired;
                payloadBody = "Channel has not been created.";
            }
            else if (_Channels[channel].Statistics.QueueFileCount[recipient] >= _RelayHubConfig.DefaultChannelMetrics.MaxQueuedItemCount)
            {
                thisContext.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                payloadBody = $"Channel is at the queue count limit of {_RelayHubConfig.DefaultChannelMetrics.MaxQueuedItemCount} items.";
            }
            else if (thisContext.Request.ContentLength > _RelayHubConfig.DefaultChannelMetrics.MaxQueuedItemSize)
            {
                thisContext.Response.StatusCode = (int)HttpStatusCode.RequestEntityTooLarge;
                payloadBody = $"Queue item exceeds the maximum size limit of {_RelayHubConfig.DefaultChannelMetrics.MaxQueuedItemSize} bytes.";
            }
            else
            {
                try
                {
                    if (string.Compare(GetHeaderValue(thisContext, "Content-Type", "text/plain"), "text/plain", true) != 0)
                    {
                        thisContext.Response.StatusCode = (int)HttpStatusCode.UnsupportedMediaType;
                    }
                    else
                    {
                        if (thisContext.Request.ContentLength > 0)
                        {
                            using (var sr = new StreamReader(thisContext.Request.Body))
                            {
                                requestBody = new StreamReader(thisContext.Request.Body).ReadToEndAsync().GetAwaiter().GetResult();
                            }
                        }
                        var path = Path.Combine(_Channels[channel].QueuePath, recipient);
                        var filename = string.Empty;
                        lock (_Channels[channel].LockQueuedFilenames)
                        {
                            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                            if (!_Channels[channel].QueuedFilenames.ContainsKey(recipient))
                            {
                                _Channels[channel].QueuedFilenames.Add(recipient, new Queue<string>());
                                _Channels[channel].Statistics.QueueFileCount.Add(recipient, 0);
                                _Channels[channel].Statistics.QueueFileStorageSize.Add(recipient, 0L);
                            }
                            var maxTries = 5;
                            do
                            {
                                filename = MakeFilenameForQueueItem(path);
                            } while (maxTries-- > 0 && File.Exists(filename));

                            if (File.Exists(filename))
                            {
                                thisContext.Response.StatusCode = (int)HttpStatusCode.Conflict;  // conflict: can't get a unique file name.
                            }
                            else
                            {
                                using (var fs = new StreamWriter(filename, false))
                                {
                                    fs.Write(requestBody);
                                }
                                lock (_ChannelIsLocked[channel])
                                {
                                    _Channels[channel].Statistics.QueueFileCount[recipient]++;
                                    _Channels[channel].Statistics.QueueFileStorageSize[recipient] += requestBody.Length;
                                    _Channels[channel].Statistics.LastActivity = DateTime.Now;
                                }
                                thisContext.Response.StatusCode = (int)HttpStatusCode.Created;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    payloadBody = $"Exception: {ex.Message}\r\nStack Trace:\r\n{ex.StackTrace}";
                    thisContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                }
            }
            using (var sw = new StreamWriter(thisContext.Response.Body))
            {
                sw.WriteAsync(payloadBody).GetAwaiter().GetResult();
                sw.FlushAsync().GetAwaiter().GetResult();
                // sw.DisposeAsync().GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// Retrieve an item from the recipient's queue.
        /// </summary>
        /// <param name="thisContext"></param>
        /// <param name="authToken"></param>
        /// <param name="channel"></param>
        /// <param name="recipient"></param>
        public void QueueRetrieve(HttpContext thisContext, string authToken, string channel, string recipient)
        {
            var payloadBody = string.Empty;
            thisContext.Response.ContentType = "text/plain";

            AuthorizationType authorizationType = GetAuthorizationType(
                authToken, thisContext, QuarantinedStateAction.Disallow);
            if (authorizationType < AuthorizationType.User)
            {
                thisContext.Response.ContentType = "text/plain";
                switch (authorizationType)
                {
                    case AuthorizationType.Blocked:
                        thisContext.Response.StatusCode = (int)HttpStatusCode.UnavailableForLegalReasons;
                        payloadBody = "Excessive authentication failures";
                        break;
                    default:
                        thisContext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        payloadBody = "Invalid authentication for request (requires user token value)";
                        break;
                }
            }
            else if (!_Channels.ContainsKey(channel))
            {
                thisContext.Response.StatusCode = (int)HttpStatusCode.PreconditionRequired;
                payloadBody = "Channel has not been created.";
            }
            else
            {
                try
                {
                    if (!_Channels[channel].QueuedFilenames.ContainsKey(recipient))
                    {
                        var path = BuildQueueFolderPathForRecipient(channel, recipient);
                        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                        _Channels[channel].QueuedFilenames.Add(recipient, new Queue<string>());
                        _Channels[channel].Statistics.QueueFileCount.Add(recipient, 0);
                        _Channels[channel].Statistics.QueueFileStorageSize.Add(recipient, 0L);
                    }

                    var pass = 0;
                    var haveFile = false;
                    var filename = string.Empty;
                    lock (_Channels[channel].LockQueuedFilenames)
                    {
                        while (++pass < 3 && !haveFile)
                        {
                            while (!haveFile && _Channels[channel].QueuedFilenames[recipient].Count() > 0)
                            {
                                filename = _Channels[channel].QueuedFilenames[recipient].Dequeue();
                                if (File.Exists(filename)) haveFile = true;
                            }
                            if (!haveFile && pass == 1)
                            {
                                PopulateQueueFileNames(channel, recipient);
                            }
                        }
                    }
                    if (!haveFile)
                    {
                        thisContext.Response.StatusCode = (int)HttpStatusCode.NoContent;
                    }
                    else
                    {
                        using (var fs = new StreamReader(filename))
                        {
                            payloadBody = fs.ReadToEnd();
                        }
                        File.Delete(filename);
                        lock (_ChannelIsLocked[channel])
                        {
                            _Channels[channel].Statistics.QueueFileCount[recipient]--;
                            _Channels[channel].Statistics.QueueFileStorageSize[recipient] -= payloadBody.Length;
                            _Channels[channel].Statistics.LastActivity = DateTime.Now;
                        }
                        thisContext.Response.StatusCode = (int)HttpStatusCode.OK;
                        thisContext.Response.ContentType = "application/json";
                    }
                }
                catch (Exception ex)
                {
                    payloadBody = $"Exception: {ex.Message}\r\nStack Trace:\r\n{ex.StackTrace}";
                    thisContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                }
            }
            using (var sw = new StreamWriter(thisContext.Response.Body))
            {
                sw.WriteAsync(payloadBody).GetAwaiter().GetResult();
                sw.FlushAsync().GetAwaiter().GetResult();
                // sw.DisposeAsync().GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// Remove the queue for a specific recpient only.
        /// </summary>
        /// <param name="thisContext"></param>
        /// <param name="authToken"></param>
        /// <param name="channel"></param>
        /// <param name="recipient"></param>
        public void QueueRemoveRecipient(HttpContext thisContext, string authToken, string channel, string recipient)
        {
            var payloadBody = string.Empty;
            thisContext.Response.ContentType = "text/plain";

            AuthorizationType authorizationType = GetAuthorizationType(
                authToken, thisContext, QuarantinedStateAction.Disallow);
            if (authorizationType < AuthorizationType.Administrative)
            {
                thisContext.Response.ContentType = "text/plain";
                switch (authorizationType)
                {
                    case AuthorizationType.Blocked:
                        thisContext.Response.StatusCode = (int)HttpStatusCode.UnavailableForLegalReasons;
                        payloadBody = "Excessive authentication failures";
                        break;
                    default:
                        thisContext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        payloadBody = "Invalid authentication for request (requires addmin token value)";
                        break;
                }
            }
            else if (!_Channels.ContainsKey(channel))
            {
                thisContext.Response.StatusCode = (int)HttpStatusCode.PreconditionRequired;
                thisContext.Response.ContentType = "text/plain";
                payloadBody = "Channel has not been created.";
            }
            else if (!_Channels[channel].QueuedFilenames.ContainsKey(recipient))
            {
                thisContext.Response.StatusCode = (int)HttpStatusCode.NoContent;
                thisContext.Response.ContentType = "text/plain";
                payloadBody = "Recipient not found in the queue this channel";
            }
            else
            {
                try
                {
                    var path = BuildQueueFolderPathForRecipient(channel, recipient);
                    if (Directory.Exists(path)) Directory.Delete(path);
                    lock (_ChannelIsLocked[channel])
                    {
                        _Channels[channel].QueuedFilenames.Remove(recipient);
                        _Channels[channel].Statistics.QueueFileCount.Remove(recipient);
                        _Channels[channel].Statistics.QueueFileStorageSize.Remove(recipient);
                    }
                    thisContext.Response.StatusCode = (int)HttpStatusCode.OK;
                    thisContext.Response.ContentType = "text/plain";
                    payloadBody = "Recipient and its queued content removed from this channel";
                }
                catch (Exception ex)
                {
                    payloadBody = $"Exception: {ex.Message}\r\nStack Trace:\r\n{ex.StackTrace}";
                    thisContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    thisContext.Response.ContentType = "text/plain";
                }
            }
            using (var sw = new StreamWriter(thisContext.Response.Body))
            {
                sw.WriteAsync(payloadBody).GetAwaiter().GetResult();
                sw.FlushAsync().GetAwaiter().GetResult();
                // sw.DisposeAsync().GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// List the keys for reference items in the channel.
        /// </summary>
        /// <param name="thisContext"></param>
        /// <param name="authToken"></param>
        /// <param name="channel"></param>
        public void ReferenceKeys(HttpContext thisContext, string authToken, string channel)
        {
            var payloadBody = string.Empty;

            AuthorizationType authorizationType = GetAuthorizationType(
                authToken, thisContext, QuarantinedStateAction.Disallow);
            if (authorizationType < AuthorizationType.User)
            {
                thisContext.Response.ContentType = "text/plain";
                switch (authorizationType)
                {
                    case AuthorizationType.Blocked:
                        thisContext.Response.StatusCode = (int)HttpStatusCode.UnavailableForLegalReasons;
                        payloadBody = "Excessive authentication failures";
                        break;
                    default:
                        payloadBody = "Invalid authentication for request (requires user token value)";
                        break;
                }
                thisContext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                payloadBody = "Invalid authentication for request (requires user token value)";
            }
            else if (!_Channels.ContainsKey(channel))
            {
                thisContext.Response.ContentType = "text/plain";
                thisContext.Response.StatusCode = (int)HttpStatusCode.PreconditionRequired;
                payloadBody = "Channel has not been created.";
            }
            else
            {
                try
                {
                    lock (_Channels[channel].LockReferenceFilenames)
                    {
                        var keys = new List<string>();
                        foreach (var filename in Directory.GetFiles(_Channels[channel].ReferencePath, "*.txt", SearchOption.TopDirectoryOnly))
                        {
                            keys.Add(Path.GetFileNameWithoutExtension(filename));
                        }
                        if (keys.Count > 0)
                        {
                            payloadBody = Serializer<List<string>>.ToJson(keys);
                            thisContext.Response.ContentType = "application/json";
                            thisContext.Response.StatusCode = (int)HttpStatusCode.OK;
                        }
                        else
                        {
                            thisContext.Response.ContentType = "text/plain";
                            thisContext.Response.StatusCode = (int)HttpStatusCode.NoContent;
                        }
                    }
                }
                catch (Exception ex)
                {
                    payloadBody = $"Exception: {ex.Message}\r\nStack Trace:\r\n{ex.StackTrace}";
                    thisContext.Response.ContentType = "text/plain";
                    thisContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                }
            }
            using (var sw = new StreamWriter(thisContext.Response.Body))
            {
                sw.WriteAsync(payloadBody).GetAwaiter().GetResult();
                sw.FlushAsync().GetAwaiter().GetResult();
                // sw.DisposeAsync().GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// Retrieve the value of a reference item.
        /// </summary>
        /// <param name="thisContext"></param>
        /// <param name="authToken"></param>
        /// <param name="channel"></param>
        /// <param name="key"></param>
        public void ReferenceRead(HttpContext thisContext, string authToken, string channel, string key)
        {
            var payloadBody = string.Empty;
            thisContext.Response.ContentType = "text/plain";

            AuthorizationType authorizationType = GetAuthorizationType(
                authToken, thisContext, QuarantinedStateAction.Disallow);
            if (authorizationType < AuthorizationType.User)
            {
                switch (authorizationType)
                {
                    case AuthorizationType.Blocked:
                        thisContext.Response.StatusCode = (int)HttpStatusCode.UnavailableForLegalReasons;
                        payloadBody = "Excessive authentication failures";
                        break;
                    default:
                        thisContext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        payloadBody = "Invalid authentication for request (requires user token value)";
                        break;
                }
            }
            else if (!_Channels.ContainsKey(channel))
            {
                thisContext.Response.StatusCode = (int)HttpStatusCode.PreconditionRequired;
                payloadBody = "Channel has not been created.";
            }
            else
            {
                try
                {
                    var filename = Path.Combine(_Channels[channel].ReferencePath, key + ".txt");
                    lock (_Channels[channel].LockReferenceFilenames)
                    {
                        if (File.Exists(filename))
                        {
                            using (var fs = new StreamReader(filename))
                            {
                                payloadBody = fs.ReadToEnd();
                            }
                            thisContext.Response.StatusCode = (int)HttpStatusCode.OK;
                        }
                        else thisContext.Response.StatusCode = (int)HttpStatusCode.NoContent;
                    }
                }
                catch (Exception ex)
                {
                    payloadBody = $"Exception: {ex.Message}\r\nStack Trace:\r\n{ex.StackTrace}";
                    thisContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                }
            }
            using (var sw = new StreamWriter(thisContext.Response.Body))
            {
                sw.WriteAsync(payloadBody).GetAwaiter().GetResult();
                sw.FlushAsync().GetAwaiter().GetResult();
                // sw.DisposeAsync().GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// Write a value to a reference under the provided key.
        /// </summary>
        /// <param name="thisContext"></param>
        /// <param name="authToken"></param>
        /// <param name="channel"></param>
        /// <param name="key"></param>
        public void ReferenceWrite(HttpContext thisContext, string authToken, string channel, string key)
        {
            var payloadBody = string.Empty;
            thisContext.Response.ContentType = "text/plain";

            var requestBody = string.Empty;
            AuthorizationType authorizationType = GetAuthorizationType(
                authToken, thisContext, QuarantinedStateAction.Disallow);
            if (authorizationType < AuthorizationType.User)
            {
                switch (authorizationType)
                {
                    case AuthorizationType.Blocked:
                        thisContext.Response.StatusCode = (int)HttpStatusCode.UnavailableForLegalReasons;
                        payloadBody = "Excessive authentication failures";
                        break;
                    default:
                        thisContext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        payloadBody = "Invalid authentication for request (requires user token value)";
                        break;
                }
            }
            else if (!_Channels.ContainsKey(channel))
            {
                thisContext.Response.StatusCode = (int)HttpStatusCode.PreconditionRequired;
                payloadBody = "Channel has not been created.";
            }
            else if (_Channels[channel].Statistics.ReferenceFileCount > _RelayHubConfig.DefaultChannelMetrics.MaxReferenceItemCount)
            {
                thisContext.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                payloadBody = $"Channel is at the reference count limit of {_RelayHubConfig.DefaultChannelMetrics.MaxReferenceItemCount} items.";
            }
            else if (thisContext.Request.ContentLength > _RelayHubConfig.DefaultChannelMetrics.MaxReferenceItemSize)
            {
                thisContext.Response.StatusCode = (int)HttpStatusCode.RequestEntityTooLarge;
                payloadBody = $"Item exceeds the maximum size limit of {_RelayHubConfig.DefaultChannelMetrics.MaxQueuedItemSize} bytes.";
            }
            try
            {
                if (thisContext.Request.ContentLength > 0)
                {
                    using (var sr = new StreamReader(thisContext.Request.Body))
                    {
                        requestBody = new StreamReader(thisContext.Request.Body).ReadToEndAsync().GetAwaiter().GetResult();
                    }
                }
                var filename = Path.Combine(_Channels[channel].ReferencePath, key + ".txt");
                var filenameContentSize = 0L;
                lock (_Channels[channel].LockReferenceFilenames)
                {
                    var fileExists = File.Exists(filename);
                    if (fileExists)
                    {
                        filenameContentSize = new FileInfo(filename).Length;
                    }
                    using (var fs = new StreamWriter(filename, false))
                    {
                        fs.Write(requestBody);
                    }

                    lock (_ChannelIsLocked[channel])
                    {
                        if (fileExists)
                        {
                            thisContext.Response.StatusCode = (int)HttpStatusCode.OK;
                            _Channels[channel].Statistics.ReferenceFileStorageSize +=
                                (requestBody.Length - filenameContentSize);
                        }
                        else
                        {
                            thisContext.Response.StatusCode = (int)HttpStatusCode.Created;
                            _Channels[channel].Statistics.ReferenceFileCount++;
                            _Channels[channel].Statistics.ReferenceFileStorageSize += requestBody.Length;
                        }
                        _Channels[channel].Statistics.LastActivity = DateTime.Now;
                    }
                }
            }
            catch (Exception ex)
            {
                payloadBody = $"Exception: {ex.Message}\r\nStack Trace:\r\n{ex.StackTrace}";
                thisContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            }
            using (var sw = new StreamWriter(thisContext.Response.Body))
            {
                sw.WriteAsync(payloadBody).GetAwaiter().GetResult();
                sw.FlushAsync().GetAwaiter().GetResult();
                // sw.DisposeAsync().GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// Delete the reference item for the given key.
        /// </summary>
        /// <param name="thisContext"></param>
        /// <param name="authToken"></param>
        /// <param name="channel"></param>
        /// <param name="key"></param>
        public void ReferenceDelete(HttpContext thisContext, string authToken, string channel, string key)
        {
            var payloadBody = string.Empty;
            thisContext.Response.ContentType = "text/plain";

            AuthorizationType authorizationType = GetAuthorizationType(
                authToken, thisContext, QuarantinedStateAction.Disallow);
            if (authorizationType < AuthorizationType.User)
            {
                switch (authorizationType)
                {
                    case AuthorizationType.Blocked:
                        thisContext.Response.StatusCode = (int)HttpStatusCode.UnavailableForLegalReasons;
                        payloadBody = "Excessive authentication failures";
                        break;
                    default:
                        thisContext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        payloadBody = "Invalid authentication for request (requires user token value)";
                        break;
                }
            }
            else if (!_Channels.ContainsKey(channel))
            {
                thisContext.Response.StatusCode = (int)HttpStatusCode.PreconditionRequired;
                payloadBody = "Channel has not been created.";
            }
            else
            {
                try
                {
                    var filename = MakeFilenameForReferenceItem(_Channels[channel].ReferencePath, key);
                    if (!File.Exists(filename))
                    {
                        thisContext.Response.StatusCode = (int)HttpStatusCode.NoContent;
                    }
                    else
                    {
                        var filenameContentLength = new FileInfo(filename).Length;
                        lock (_ChannelIsLocked[channel])
                        {
                            _Channels[channel].Statistics.ReferenceFileCount--;
                            _Channels[channel].Statistics.ReferenceFileStorageSize -= filenameContentLength;
                            _Channels[channel].Statistics.LastActivity = DateTime.Now;
                        }
                        File.Delete(filename);
                    }
                    thisContext.Response.StatusCode = (int)HttpStatusCode.OK;
                }
                catch (Exception ex)
                {
                    payloadBody = $"Exception: {ex.Message}\r\nStack Trace:\r\n{ex.StackTrace}";
                    thisContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                }
            }
            using (var sw = new StreamWriter(thisContext.Response.Body))
            {
                sw.WriteAsync(payloadBody).GetAwaiter().GetResult();
                sw.FlushAsync().GetAwaiter().GetResult();
                // sw.DisposeAsync().GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// EXECUTIVE ONLY: List all channels being hosted
        /// </summary>
        /// <param name="thisContext"></param>
        /// <param name="authToken"></param>
        public void ListAllChannels(HttpContext thisContext, string authToken)
        {
            var payloadBody = string.Empty;

            AuthorizationType authorizationType = GetAuthorizationType(
                authToken, thisContext, QuarantinedStateAction.Disallow);
            if (authorizationType < AuthorizationType.Executive)
            {
                thisContext.Response.ContentType = "text/plain";
                switch (authorizationType)
                {
                    case AuthorizationType.Blocked:
                        thisContext.Response.StatusCode = (int)HttpStatusCode.UnavailableForLegalReasons;
                        payloadBody = "Excessive authentication failures";
                        break;
                    default:
                        thisContext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        payloadBody = "Invalid authentication for request (requires executive token value)";
                        break;
                }
            }
            else
            {
                try
                {
                    var channels = new List<string>(_Channels.Keys);
                    payloadBody = Serializer<List<string>>.ToJson(channels);
                    thisContext.Response.StatusCode = (channels.Count > 0) ?
                        (int)HttpStatusCode.OK :
                        (int)HttpStatusCode.NoContent;
                    thisContext.Response.ContentType = "application/json";
                }
                catch (Exception ex)
                {
                    payloadBody = $"Exception: {ex.Message}\r\nStack Trace:\r\n{ex.StackTrace}";
                    thisContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    thisContext.Response.ContentType = "text/plain";
                }
            }
            using (var sw = new StreamWriter(thisContext.Response.Body))
            {
                sw.WriteAsync(payloadBody).GetAwaiter().GetResult();
                sw.FlushAsync().GetAwaiter().GetResult();
                // sw.DisposeAsync().GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// EXECUTIVE ONLY: Returns an array of statistics for all channels being hosted.
        /// </summary>
        /// <param name="thisContext"></param>
        /// <param name="authToken"></param>
        public void ListAllChannelStatistics(HttpContext thisContext, string authToken)
        {
            var payloadBody = string.Empty;

            AuthorizationType authorizationType = GetAuthorizationType(
                authToken, thisContext, QuarantinedStateAction.Disallow);
            if (authorizationType < AuthorizationType.Executive)
            {
                thisContext.Response.ContentType = "text/plain";
                switch (authorizationType)
                {
                    case AuthorizationType.Blocked:
                        thisContext.Response.StatusCode = (int)HttpStatusCode.UnavailableForLegalReasons;
                        payloadBody = "Excessive authentication failures";
                        break;
                    default:
                        thisContext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        payloadBody = "Invalid authentication for request (requires executive token value)";
                        break;
                }
            }
            else
            {
                try
                {
                    var results = new List<ChannelStatistics>();
                    foreach (var key in _Channels.Keys)
                    {
                        results.Add(_Channels[key].Statistics);
                    }
                    payloadBody = Serializer<List<ChannelStatistics>>.ToJson(results);
                    thisContext.Response.StatusCode = (_Channels.Keys.Count > 0) ?
                        (int)HttpStatusCode.OK :
                        (int)HttpStatusCode.NoContent;
                    thisContext.Response.ContentType = "application/json";
                }
                catch (Exception ex)
                {
                    payloadBody = $"Exception: {ex.Message}\r\nStack Trace:\r\n{ex.StackTrace}";
                    thisContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    thisContext.Response.ContentType = "text/plain";
                }
            }
            using (var sw = new StreamWriter(thisContext.Response.Body))
            {
                sw.WriteAsync(payloadBody).GetAwaiter().GetResult();
                sw.FlushAsync().GetAwaiter().GetResult();
                // sw.DisposeAsync().GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// EXECUTIVE ONLY: Returns an array of statistics for all channels being hosted.
        /// </summary>
        /// <param name="thisContext"></param>
        /// <param name="authToken"></param>
        public void ListClientSecurity(HttpContext thisContext, string authToken)
        {
            var payloadBody = string.Empty;

            AuthorizationType authorizationType = GetAuthorizationType(
                authToken, thisContext, QuarantinedStateAction.AllowWithoutReset);
            if (authorizationType < AuthorizationType.Executive)
            {
                thisContext.Response.ContentType = "text/plain";
                switch (authorizationType)
                {
                    case AuthorizationType.Blocked:
                        thisContext.Response.StatusCode = (int)HttpStatusCode.UnavailableForLegalReasons;
                        payloadBody = "Excessive authentication failures";
                        break;
                    default:
                        thisContext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        payloadBody = "Invalid authentication for request (requires executive token value)";
                        break;
                }
            }
            else
            {
                try
                {
                    payloadBody = Serializer<Dictionary<string, Security>>.ToJson(_ClientSecurity);
                    thisContext.Response.StatusCode = (_ClientSecurity.Keys.Count > 0) ?
                        (int)HttpStatusCode.OK :
                        (int)HttpStatusCode.NoContent;
                    thisContext.Response.ContentType = "application/json";
                }
                catch (Exception ex)
                {
                    payloadBody = $"Exception: {ex.Message}\r\nStack Trace:\r\n{ex.StackTrace}";
                    thisContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    thisContext.Response.ContentType = "text/plain";
                }
            }
            using (var sw = new StreamWriter(thisContext.Response.Body))
            {
                sw.WriteAsync(payloadBody).GetAwaiter().GetResult();
                sw.FlushAsync().GetAwaiter().GetResult();
                // sw.DisposeAsync().GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// EXECUTIVE ONLY: Returns an array of statistics for all channels being hosted.
        /// </summary>
        /// <param name="thisContext"></param>
        /// <param name="authToken"></param>
        public void ResetClientSecurity(HttpContext thisContext, string authToken)
        {
            var payloadBody = string.Empty;

            AuthorizationType authorizationType = GetAuthorizationType(
                authToken, thisContext, QuarantinedStateAction.AllowAndReset);
            if (authorizationType < AuthorizationType.Executive)
            {
                thisContext.Response.ContentType = "text/plain";
                switch (authorizationType)
                {
                    case AuthorizationType.Blocked:
                        thisContext.Response.StatusCode = (int)HttpStatusCode.UnavailableForLegalReasons;
                        payloadBody = "Excessive authentication failures";
                        break;
                    default:
                        thisContext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        payloadBody = "Invalid authentication for request (requires executive token value)";
                        break;
                }
            }
            else
            {
                try
                {
                    _ClientSecurity.Clear();
                    payloadBody = "Client Watch list cleared";
                    thisContext.Response.StatusCode = (int)HttpStatusCode.OK;
                    thisContext.Response.ContentType = "text/plain";
                }
                catch (Exception ex)
                {
                    payloadBody = $"Exception: {ex.Message}\r\nStack Trace:\r\n{ex.StackTrace}";
                    thisContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    thisContext.Response.ContentType = "text/plain";
                }
            }
            using (var sw = new StreamWriter(thisContext.Response.Body))
            {
                sw.WriteAsync(payloadBody).GetAwaiter().GetResult();
                sw.FlushAsync().GetAwaiter().GetResult();
                // sw.DisposeAsync().GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// EXECUTIVE ONLY: Remove all channels.
        /// </summary>
        /// <param name="thisContext"></param>
        /// <param name="authToken"></param>
        public void RemoveAllChannels(HttpContext thisContext, string authToken)
        {
            _Logger.LogInformation($"Remove all channels");
            var payloadBody = string.Empty;

            AuthorizationType authorizationType = GetAuthorizationType(
                authToken, thisContext, QuarantinedStateAction.Disallow);
            if (authorizationType < AuthorizationType.Executive)
            {
                thisContext.Response.ContentType = "text/plain";
                switch (authorizationType)
                {
                    case AuthorizationType.Blocked:
                        thisContext.Response.StatusCode = (int)HttpStatusCode.UnavailableForLegalReasons;
                        payloadBody = "Excessive authentication failures";
                        break;
                    default:
                        thisContext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        payloadBody = "Invalid authentication for request (requires exeecutive token value)";
                        break;
                }
            }
            else
            {
                try
                {
                    var channelCount = _Channels.Keys.Count;
                    if (channelCount == 0)
                    {
                        thisContext.Response.ContentType = "text/plain";
                        thisContext.Response.StatusCode = (int)HttpStatusCode.NoContent;
                        payloadBody = $"No channels to remove.";
                    }
                    else
                    {
                        foreach (var channelName in _Channels.Keys)
                        {
                            lock (_ChannelIsLocked[channelName])
                            {
                                _Logger.LogDebug($"Remove channel: {channelName}");
                                RemoveChannelEntry(channelName);
                            }
                            _ChannelIsLocked.Remove(channelName);
                        }
                        thisContext.Response.ContentType = "text/plain";
                        thisContext.Response.StatusCode = (int)HttpStatusCode.OK;
                        payloadBody = $"All {channelCount} channel(s) removed.";
                    }
                }
                catch (Exception ex)
                {
                    _Logger.LogError(DetailedException.WithEnterpriseContent(ref ex));
                    payloadBody = $"Exception: {ex.Message}\r\nStack Trace:\r\n{ex.StackTrace}";
                    thisContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    thisContext.Response.ContentType = "text/plain";
                }
            }
            using (var sw = new StreamWriter(thisContext.Response.Body))
            {
                sw.WriteAsync(payloadBody).GetAwaiter().GetResult();
                sw.FlushAsync().GetAwaiter().GetResult();
                // sw.DisposeAsync().GetAwaiter().GetResult();
            }
            _Logger.LogInformation($"Returns: {thisContext.Response.StatusCode}");
        }

        /// <summary>
        /// EXECUTIVE ONLY: Returns an array of statistics for all channels being hosted.
        /// </summary>
        /// <param name="thisContext"></param>
        /// <param name="authToken"></param> 
        public void GetSettings(HttpContext thisContext, string authToken)
        {
            var payloadBody = string.Empty;

            AuthorizationType authorizationType = GetAuthorizationType(
                authToken, thisContext, QuarantinedStateAction.Disallow);
            if (authorizationType < AuthorizationType.Executive)
            {
                thisContext.Response.ContentType = "text/plain";
                switch (authorizationType)
                {
                    case AuthorizationType.Blocked:
                        thisContext.Response.StatusCode = (int)HttpStatusCode.UnavailableForLegalReasons;
                        payloadBody = "Excessive authentication failures";
                        break;
                    default:
                        thisContext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        payloadBody = "Invalid authentication for request (requires executive token value)";
                        break;
                }
            }
            else
            {
                try
                {
                    payloadBody = Serializer<Dictionary<string, Security>>.ToJson(_ClientSecurity);
                    thisContext.Response.StatusCode = (_ClientSecurity.Keys.Count > 0) ?
                        (int)HttpStatusCode.OK :
                        (int)HttpStatusCode.NoContent;
                    thisContext.Response.ContentType = "application/json";
                }
                catch (Exception ex)
                {
                    payloadBody = $"Exception: {ex.Message}\r\nStack Trace:\r\n{ex.StackTrace}";
                    thisContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    thisContext.Response.ContentType = "text/plain";
                }
            }
            using (var sw = new StreamWriter(thisContext.Response.Body))
            {
                sw.WriteAsync(payloadBody).GetAwaiter().GetResult();
                sw.FlushAsync().GetAwaiter().GetResult();
                // sw.DisposeAsync().GetAwaiter().GetResult();
            }
        }



        /// <summary>
        /// EXECUTIVE ONLY: Issues a Exit() command to stop the app.  If registered as a service, the service manager will restart it.
        /// </summary>
        /// <param name="thisContext"></param>
        /// <param name="authToken"></param>
        public void Shutdown(HttpContext thisContext, string authToken)
        {
            _Logger.LogInformation($"Shutdown");
            string payloadBody = string.Empty;
            AuthorizationType authorizationType = GetAuthorizationType(
                authToken, thisContext, QuarantinedStateAction.Disallow);
            if (authorizationType < AuthorizationType.Executive)
            {
                thisContext.Response.ContentType = "text/plain";
                thisContext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                payloadBody = "Invalid authentication for request (requires executive token value)";
            }
            else
            {
                _Logger.LogInformation($"Issuing shutdown command");
                thisContext.Response.ContentType = "text/plain";
                thisContext.Response.StatusCode = (int)HttpStatusCode.OK;
                payloadBody = $"Shutdown command issued.";
                _AppShutdownTimer.Enabled = true;
            }
            using (var sw = new StreamWriter(thisContext.Response.Body))
            {
                sw.WriteAsync(payloadBody).GetAwaiter().GetResult();
                sw.FlushAsync().GetAwaiter().GetResult();
                // sw.DisposeAsync().GetAwaiter().GetResult();
            }
            _Logger.LogInformation($"Returns: {thisContext.Response.StatusCode}");
        }
        #endregion

        #region Helpers
        private string ResolveLocalSpec(string localFolderSpec)
        {
            string result = localFolderSpec;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                result = result.Replace(@"/", @"\").ResolvePathPlaceholders();
                result = result.Replace("$HOME", Environment.GetEnvironmentVariable("USERPROFILE"));
                while (result.Length > 0 && result[result.Length - 1] == '\\') result = result.Substring(0, result.Length - 1);
                result += "\\";
            }
            else
            {
                result = result.Replace("$HOME", Environment.GetEnvironmentVariable("HOME"));
                while (result.Length > 0 && result[result.Length - 1] == '/') result = result.Substring(0, result.Length - 1);
                result += "/";
            }
            return result;
        }

        private AuthorizationType GetAuthorizationType(string authToken, HttpContext context, QuarantinedStateAction quarantinedStateAction)
        {
            var clientIp = ExtractClientIpAddress(context);
            var result = AuthorizationType.None;

            var isQuarantinedClient = (
                _ClientSecurity.ContainsKey(clientIp) &&
                _ClientSecurity[clientIp].DenyAccessAttemptsUntil > DateTime.Now
            );

            if (string.Compare(authToken, _RelayHubConfig.ExecutiveTokenValue, false) == 0)
            {
                result = AuthorizationType.Executive;
                if (isQuarantinedClient && quarantinedStateAction == QuarantinedStateAction.Disallow)
                {
                    result = AuthorizationType.Blocked;
                }
            }
            else if (string.Compare(authToken, _RelayHubConfig.AdminTokenValue, false) == 0)
            {
                result = isQuarantinedClient ? AuthorizationType.Blocked : AuthorizationType.Administrative;
            }
            else if (string.Compare(authToken, _RelayHubConfig.UserTokenValue, false) == 0)
            {
                result = isQuarantinedClient ? AuthorizationType.Blocked : AuthorizationType.User;
            }

            var dropFromSecurityList = false;
            switch (result)
            {
                case AuthorizationType.None:
                    if (!_ClientSecurity.ContainsKey(clientIp))
                    {
                        _ClientSecurity.Add(clientIp, new Security());
                    }
                    else
                    {
                        _ClientSecurity[clientIp].FailedTokenValues++;
                        if (_ClientSecurity[clientIp].FailedTokenValues > _RelayHubConfig.SecurityMaxInvalidTokenAttempts)
                        {
                            _ClientSecurity[clientIp].DenyAccessAttemptsUntil =
                                DateTime.Now.AddSeconds(
                                    (_ClientSecurity[clientIp].FailedTokenValues - _RelayHubConfig.SecurityMaxInvalidTokenAttempts)
                                    * _RelayHubConfig.SecurityDelaySecondsFactor
                                );
                            result = AuthorizationType.Blocked;
                        }
                    }
                    break;
                case AuthorizationType.Blocked:
                    break; // don't remove client from the list //
                case AuthorizationType.Executive:
                    dropFromSecurityList = (isQuarantinedClient && quarantinedStateAction == QuarantinedStateAction.AllowAndReset);
                    break;
                default:
                    dropFromSecurityList = true;
                    break;
            }
            if (dropFromSecurityList)
            {
                if (_ClientSecurity.ContainsKey(clientIp))
                {
                    _ClientSecurity.Remove(clientIp);
                }
            }
            return result;
        }

        private static string ExtractClientIpAddress(HttpContext context)
        {
            var result = (context.Connection.RemoteIpAddress ?? IPAddress.Parse(IpAddressForNoClient)).ToString();
            if (context.Request.Headers.ContainsKey("X-Forwarded-For"))
            {
                IPAddress ipAddr;
                if (IPAddress.TryParse(context.Request.Headers["X-Forwarded-For"], out ipAddr))
                {
                    result = ipAddr.ToString();
                }
            }
            return result;
        }

        private string BuildChannelPath(string channel)
        {
            return Path.Combine(_RelayHubConfig.RootStoragePath, channel);
        }

        private string BuildReferenceFolderPath(string channel)
        {
            return Path.Combine(BuildChannelPath(channel), "_r");
        }

        private string BuildQueueFolderPath(string channel)
        {
            return Path.Combine(BuildChannelPath(channel), "_q");
        }

        private string BuildQueueFolderPathForRecipient(string channel, string recipient)
        {
            return Path.Combine(_Channels[channel].QueuePath, recipient);
        }

        private void CreateChannelEntry(string channelName)
        {
            var channelObj = new Entity.Channel
            {
                Path = BuildChannelPath(channelName),
                QueuePath = BuildQueueFolderPath(channelName),
                ReferencePath = BuildReferenceFolderPath(channelName)
            };
            channelObj.Statistics.ChannelName = channelName;
            _ChannelIsLocked.Add(channelName, new object());
            lock (_ChannelIsLocked[channelName])
            {
                if (!Directory.Exists(channelObj.Path)) Directory.CreateDirectory(channelObj.Path);
                if (!Directory.Exists(channelObj.QueuePath)) Directory.CreateDirectory(channelObj.QueuePath);
                if (!Directory.Exists(channelObj.ReferencePath)) Directory.CreateDirectory(channelObj.ReferencePath);
                _Channels.Add(channelName, channelObj);
            }
        }

        private void RecoverChannelEntry(string channelName)
        {
            var channelRootPath = BuildChannelPath(channelName);

            if (!Directory.Exists(channelRootPath)) Directory.CreateDirectory(channelRootPath);
            _ChannelIsLocked.Add(channelName, new object());
            var channelObj = new Entity.Channel
            {
                Path = BuildChannelPath(channelName),
                QueuePath = BuildQueueFolderPath(channelName),
                ReferencePath = BuildReferenceFolderPath(channelName)
            };
            channelObj.Statistics.ChannelName = channelName;
            channelObj.Statistics.ExistedAtStartup = true;
            lock (_ChannelIsLocked[channelName])
            {
                // add stats for queue items
                foreach (var recipientDir in Directory.EnumerateDirectories(channelObj.QueuePath, "*.*", SearchOption.TopDirectoryOnly))
                {
                    var recipient = recipientDir.Substring(channelObj.QueuePath.Length + 1);
                    channelObj.Statistics.QueueFileCount.Add(recipient, 0);
                    channelObj.Statistics.QueueFileStorageSize.Add(recipient, 0);
                    channelObj.QueuedFilenames.Add(
                        recipient,
                        new Queue<string>()
                    );
                    var queueFiles = Directory.EnumerateFiles(recipientDir, "*.*", SearchOption.TopDirectoryOnly);
                    foreach (var thisFilename in queueFiles)
                    {
                        channelObj.Statistics.QueueFileCount[recipient]++;
                        channelObj.Statistics.QueueFileStorageSize[recipient] += new FileInfo(thisFilename).Length;
                    }
                    PopulateQueueFileNames(channelName, recipient);
                }

                // add stats for reference items
                foreach (var thisReferenceFile in Directory.EnumerateFiles(channelObj.ReferencePath, "*.*", SearchOption.TopDirectoryOnly))
                {
                    channelObj.Statistics.ReferenceFileCount++;
                    channelObj.Statistics.ReferenceFileStorageSize += new FileInfo(thisReferenceFile).Length;
                }
                _Channels.Add(channelName, channelObj);
            }
        }

        private void RemoveChannelEntry(string channelName)
        {
            if (Directory.Exists(_Channels[channelName].Path)) Directory.Delete(_Channels[channelName].Path, true);
            if (_Channels.ContainsKey(channelName)) _Channels.Remove(channelName);
        }

        private void PopulateQueueFileNames(string channel, string recipient)
        {
            if (!_Channels.ContainsKey(channel)) return;
            lock (_Channels[channel].LockQueuedFilenames)
            {
                var folder = BuildQueueFolderPathForRecipient(channel, recipient);
                if (!_Channels[channel].QueuedFilenames.ContainsKey(recipient))
                {
                    _Channels[channel].QueuedFilenames.Add(recipient, new Queue<string>());
                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                }
                if (_Channels[channel].QueuedFilenames[recipient].Count > _RelayHubConfig.MaxCountQueuedFilenames) return;

                var queueFiles = Directory.EnumerateFiles(folder, "*.*", SearchOption.TopDirectoryOnly).OrderBy(f => f).Take(20);
                foreach (var thisFilename in queueFiles)
                {
                    _Channels[channel].QueuedFilenames[recipient].Enqueue(thisFilename);
                    if (_Channels[channel].QueuedFilenames[recipient].Count >= _RelayHubConfig.MaxCountQueuedFilenames) break;
                }
            }
        }

        private string MakeFilenameForQueueItem(string path)
        {
            // uniwue to 100th of a second.
            var now = DateTime.Now;
            return Path.Combine(path, string.Format("{0:yyyyMMdd-HHmmss-}{1}.txt", now, (now.Millisecond).ToString().PadLeft(4, '0')));
        }

        private string MakeFilenameForReferenceItem(string path, string key)
        {
            // unique to 1000th of a second.
            var now = DateTime.Now;
            return Path.Combine(path, string.Format("{0}.txt", key));
        }

        private string GetHeaderValue(HttpContext thisContext, string key, string defaultValue)
        {
            var result = defaultValue ?? string.Empty;
            if (thisContext.Request.Headers.ContainsKey(key))
            {
                result = thisContext.Request.Headers[key].FirstOrDefault() ?? result;
            }
            return result;
        }
        #endregion

        #region Shutdown management

        /// <summary>
        /// Shuts down down the web server, requiring restart at the command line IF not part of systemctl daemon service
        /// or hosted by IIS.
        /// </summary>
        public void Shutdown()
        {
            // triggers a timer, which does the actual shutdown after allow time for the thread to get to an idle state.
            _AppShutdownTimer.Enabled = true;
        }

        #endregion
    }
}
