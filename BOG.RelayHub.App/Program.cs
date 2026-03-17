using BOG.RelayHub.App;
using BOG.RelayHub.Common.Entity;
using BOG.SwissArmyKnife;
using Figgle;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Configuration;
using NReco.Logging.File;
using Microsoft.Extensions.Configuration.Json;

namespace BOG.RelayHub
{
    class Program
    {
        static void Main(string[] args)
        {
            #region Site Builder

            Console.WriteLine(FiggleFonts.Roman.Render("BOG.RelayHub.App"));

            var _AssemblyVersion = new AssemblyVersion();

            var builder = WebApplication.CreateBuilder(args);

            var _Config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();

            using ILoggerFactory factory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddConfiguration(_Config)
                    //.AddFile(_Config, o =>
                    //{
                    //    o.Append = true;
                    //    o.FileSizeLimitBytes = 1024 * 1024;
                    //    o.MaxRollingFiles = 20;
                    //})
                    .AddConsole();
            });
            ILogger _Logger = factory.CreateLogger("Program");

            _Logger.LogInformation(_AssemblyVersion.ToString());

            _Logger.LogDebug("Services setup...");

            // Add services to the container.
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
                {
                    Version = $"v{_AssemblyVersion.Version}, {_AssemblyVersion.BuildDate.ToShortDateString()}",
                    Title = "BOG.RelayHub API",
                    Description = "A drop-off and pickup location with persistence, for inter-application data handoff",
                    Contact = new Microsoft.OpenApi.Models.OpenApiContact { Name = "John J Schultz", Email = "", Url = new Uri("https://github.com/rambotech") },
                    License = new Microsoft.OpenApi.Models.OpenApiLicense { Name = "MIT License", Url = new Uri("https://opensource.org/licenses/MIT") }
                });
                // Set the comments path for the Swagger JSON and UI.
                // var xmlPath = Path.Combine(PlatformServices.Default.Application.ApplicationBasePath, "BOG.RelayHub.xml");
                // c.IncludeXmlComments(xmlPath);
            });

            var app = builder.Build();
            app.UseMiddleware<ExceptionMiddleware>();

            _Logger.LogDebug("Services configuration ...");

            // Configure the HTTP request pipeline.
            app.UseSwagger();
            app.UseSwaggerUI();

            // app.UseHttpsRedirection();

            _Logger.LogInformation("Read configuration sources");

            var _RelayHubConfig = _Config.GetSection("RelayHubConfig").Get<RelayHubConfig>();
            if (_RelayHubConfig == null)
            {
                _Logger.LogDebug($"RelayHubConfig from default");
                _RelayHubConfig = new RelayHubConfig();
            }
            else
            {
                _Logger.LogDebug($"RelayHubConfig from configuration");
            }

            _RelayHubConfig.ExecutiveTokenValue = _Config.GetValue<string>("ExecutiveToken") ?? (_RelayHubConfig.ExecutiveTokenValue ?? "YourExecutiveTokenValue Here");
            _Logger.LogDebug($"ExecutiveTokenValue: {_RelayHubConfig.ExecutiveTokenValue}");

            _RelayHubConfig.AdminTokenValue = _Config.GetValue<string>("AdminToken") ?? (_RelayHubConfig.AdminTokenValue ?? "YourAdminAccessTokenValueHere");
            _Logger.LogDebug($"_AdminToken value: {_RelayHubConfig.AdminTokenValue}");

            _RelayHubConfig.UserTokenValue = _Config.GetValue<string>("UserToken") ?? (_RelayHubConfig.UserTokenValue ?? "YourUserAccessTokenValueHere");
            _Logger.LogDebug($"_UserToken value: {_RelayHubConfig.UserTokenValue}");

            _RelayHubConfig.MaxCountQueuedFilenames = _Config.GetValue("MaxCountQueuedFilenames", 20);
            _Logger.LogDebug($"_RelayHubConfig.MaxCountQueuedFilenames: {_RelayHubConfig.MaxCountQueuedFilenames}");

            _RelayHubConfig.RootStoragePath = _Config.GetValue<string>("RootStoragePath") ?? (_RelayHubConfig.RootStoragePath ?? "$HOME");
            _Logger.LogDebug($"_RelayHubConfig.RootStoragePath: {_RelayHubConfig.RootStoragePath}");

            _RelayHubConfig.FreshStart = _Config.GetValue<bool>("FreshStart", false);
            _Logger.LogDebug($"FreshStart: {_RelayHubConfig.FreshStart}");

            _RelayHubConfig.SecurityDelaySecondsFactor = _Config.GetValue<int>("SecurityDelaySecondsFactor", _RelayHubConfig.SecurityDelaySecondsFactor);
            _Logger.LogDebug($"SecurityDelaySecondsFactor: {_RelayHubConfig.SecurityDelaySecondsFactor}");

            _RelayHubConfig.SecurityMaxInvalidTokenAttempts = _Config.GetValue<int>("SecurityMaxInvalidTokenAttempts", _RelayHubConfig.SecurityMaxInvalidTokenAttempts);
            _Logger.LogDebug($"SecurityMaxInvalidTokenAttempts: {_RelayHubConfig.SecurityMaxInvalidTokenAttempts}");

            _RelayHubConfig.Listeners = _Config.GetValue<string>("Listeners", _RelayHubConfig.Listeners) ?? "http://*:5050";
            _Logger.LogDebug($"Listeners: {_RelayHubConfig.Listeners}");

            var hostedByIIS = _Config.GetValue<bool>("HostedByIIS");
            _Logger.LogDebug($"HostedByIIS: {hostedByIIS}");

            #endregion

            try
            {
                var _Processor = new EndpointProcessor(_Config, _Logger, new AssemblyVersion(), _RelayHubConfig);

                // ************************************************
                // *** No Auth Needed ***
                // ************************************************

                app.MapGet("/api/v1",
                    (HttpContext context) => _Processor.Heartbeat(context))
                .WithDescription("Called to validate site availability")
                .WithTags(" Public access");

                // ************************************************
                // *** Executive Functions ***
                // ************************************************

                var executiveGroup = app.MapGroup("/api/v1/executive")
                .WithDescription("Executive management functions (requires Executive access token value")
                .WithTags("Executive")
                .WithDescription("Bulk channel management and system control");

                executiveGroup.MapGet("channels/list",
                    (HttpContext context, [FromHeader] string AuthToken) => _Processor.ListAllChannels(context, AuthToken))
                .WithSummary("The quick brown fox jumps over the lazy dog")
                .WithDescription("List all channels currently defined.")
                .WithTags("Executive")
                .WithName("List all channels");

                executiveGroup.MapGet("channels/statistics",
                    (HttpContext context, [FromHeader] string AuthToken) => _Processor.ListAllChannelStatistics(context, AuthToken))
                .WithTags("Executive")
                .WithName("List statistics for all channels");

                executiveGroup.MapGet("security",
                    (HttpContext context, [FromHeader] string AuthToken) => _Processor.ListClientSecurity(context, AuthToken))
                .WithTags("Executive")
                .WithName("Show clients submitting invalid token values");

                executiveGroup.MapDelete("security",
                    (HttpContext context, [FromHeader] string AuthToken) => _Processor.ResetClientSecurity(context, AuthToken))
                .WithTags("Executive")
                .WithName("Reset list of clients submitting invalid token values");

                executiveGroup.MapDelete("channels",
                    (HttpContext context, [FromHeader] string AuthToken) => _Processor.RemoveAllChannels(context, AuthToken))
                .WithTags("Executive")
                .WithName("Remove all channels");

                executiveGroup.MapGet("settings",
                    (HttpContext context, [FromHeader] string AuthToken) => _Processor.GetSettings(context, AuthToken))
                .WithTags("Executive")
                .WithName("Review the configuration in use");

                executiveGroup.MapGet("shutdown",
                    (HttpContext context, [FromHeader] string AuthToken) => _Processor.Shutdown(context, AuthToken))
                .WithDescription("forces a service restart, or otherwise stops the application.")
                .WithTags("Executive")
                .WithName("Stop App/Restart Service");

                // ************************************************
                // *** CHANNEL ***
                // ************************************************

                var channelGroup = app.MapGroup("/api/v1/channel")
                .WithDescription("Owner management functions (requires Admin access token value")
                .WithTags("Channel");

                // Check if channel exists (Requires USER token value)
                // 200: Found
                // 204: Channel not in list
                // 400: Bad request (invalid channel name, etc)
                // 401: Unauthorized
                channelGroup.MapGet("manage/{Channel:regex(^[A-Za-z_][A-Za-z0-9_\\-\\.]{{0,39}}$)}",
                    (HttpContext context, [FromHeader] string AuthToken, string Channel)
                    => _Processor.ChannelExists(context, AuthToken, Channel))
                .WithName("Check existing");

                // Create channel (Requires ADMIN token value)
                // 201: Created
                // 400: Bad request (invalid channel name, etc)
                // 409: Channel already exists
                // 401: Unauthorized
                channelGroup.MapPost(@"manage/{Channel:regex(^[A-Za-z_][A-Za-z0-9_\-\.]{{0,39}}$)}",
                    (HttpContext context, [FromHeader] string AuthToken, string Channel)
                    => _Processor.ChannelCreate(context, AuthToken, Channel))
                .WithName("Create");

                // Remove channel and all its queues payloads and references. (Requires ADMIN or EXECUTIVE token access)
                // 201: OK, removed
                // 204: Channel not found
                // 400: Bad request (invalid channel name, etc)
                // 401: Unauthorized: invalid admin token value
                channelGroup.MapDelete(@"manage/{Channel:regex(^[A-Za-z_][A-Za-z0-9_\-\.]{{0,39}}$)}",
                    (HttpContext context, [FromHeader] string AuthToken, string Channel)
                    => _Processor.ChannelDelete(context, AuthToken, Channel))
                .WithName("Delete");

                // Show statistics for a channel. (Requires ADMIN or EXECUTIVE token access)
                // 201: OK, removed
                // 204: Channel not found
                // 400: Bad request (invalid channel name, etc)
                // 401: Unauthorized: invalid admin token value
                channelGroup.MapGet(@"statistics/{Channel:regex(^[A-Za-z_][A-Za-z0-9_\-\.]{{0,39}}$)}",
                    (HttpContext context, [FromHeader] string AuthToken, string Channel)
                    => _Processor.ChannelStatistics(context, AuthToken, Channel))
                .WithName("Statistics");

                // ************************************************
                // *** QUEUE ***
                // ************************************************

                var queueGroup = app.MapGroup("/api/v1/queue")
                    .WithTags("Queues");

                // Insert a new payload into the queue for the given recipient
                // 201: OK, created
                // 204: Channel not found
                // 400: Bad request (invalid channel name, etc)
                // 401: Unauthorized: invalid admin/access token value
                // 428: Missing Pre-requisite: The channel must be creeated.
                // 429: Too many requests: Queue storage or count is at maximum.  I.e.: queus is backlogging
                queueGroup.MapPost(@"{Channel:regex(^[A-Za-z_][A-Za-z0-9_\-\.]{{0,39}}$)}/{recipient:regex(^[A-Za-z_][A-Za-z0-9_\-\.]{{0,79}}$)}",
                    (HttpContext context, [FromHeader] string AuthToken, string channel, string recipient)
                    => _Processor.QueueStore(context, AuthToken, channel, recipient))
                .WithName("Enqueue Item");

                // Retrieve a payload from the queue for the given recipient
                // 200: OK, retrieved
                // 204: No item in the queue to retrieve
                // 400: Bad request (invalid channel name, etc)
                // 401: Unauthorized: invalid admin/access token value
                // 428: Missing Pre-requisite: The channel must be creeated.
                queueGroup.MapGet(@"{Channel:regex(^[A-Za-z_][A-Za-z0-9_\-\.]{{0,39}}$)}/{Recipient:regex(^[A-Za-z_][A-Za-z0-9_\-\.]{{0,79}}$)}",
                    (HttpContext context, [FromHeader] string AuthToken, string Channel, string Recipient)
                    => _Processor.QueueRetrieve(context, AuthToken, Channel, Recipient))
                .WithName("Dequeue Item");

                // Drops all pending queue entries for the given recipient.
                // 200: OK, removed
                // 204: No such recipient.
                // 400: Bad request (invalid channel name, etc)
                // 401: Unauthorized: invalid admin token value
                // 428: Missing Pre-requisite: The channel must be creeated.
                queueGroup.MapDelete(@"{Channel:regex(^[A-Za-z_][A-Za-z0-9_\-\.]{{0,39}}$)}/{Recipient:regex(^[A-Za-z_][A-Za-z0-9_\-\.]{{0,79}}$)}",
                    (HttpContext context, [FromHeader] string AuthToken, string Channel, string Recipient)
                    => _Processor.QueueRemoveRecipient(context, AuthToken, Channel, Recipient))
                .WithName("Delete Items From Queue For The Recipient");

                // ************************************************
                // *** REFERENCE ***
                // ************************************************

                var referenceGroup = app.MapGroup("/api/v1/reference")
                    .WithTags("References");

                // Get list of reference keys
                // 200: OK, retrieved
                // 400: Bad request (invalid channel name, etc)
                // 401: Unauthorized: invalid admin/access token value
                // 428: Missing Pre-requisite: The channel must be creeated.
                referenceGroup.MapGet(@"/{Channel:regex(^[A-Za-z_][A-Za-z0-9_\-\.]{{0,39}}$)}",
                    (HttpContext context, [FromHeader] string AuthToken, string Channel)
                    => _Processor.ReferenceKeys(context, AuthToken, Channel))
                .WithName("List available keys");

                // Get payload of specific reference
                // 200: OK, retrieved
                // 204: No matching key in the collection to retrieve
                // 400: Bad request (invalid channel name, etc)
                // 401: Unauthorized: invalid admin/access token value
                // 428: Missing Pre-requisite: The channel must be creeated.
                referenceGroup.MapGet(@"{Channel:regex(^[A-Za-z_][A-Za-z0-9_\-\.]{{0,39}}$)}/{Key:regex(^[A-Za-z_][A-Za-z0-9_\-\.]{{0,79}}$)}",
                    (HttpContext context, [FromHeader] string AuthToken, string Channel, string Key)
                    => _Processor.ReferenceRead(context, AuthToken, Channel, Key))
                .WithName("Retrieve key's value");

                // Create/Update payload of reference
                // 200: OK, updated content
                // 201: OK, created
                // 204: No matching key in the collection to retrieve
                // 400: Bad request (invalid channel name, etc)
                // 401: Unauthorized: invalid admin/access token value
                // 428: Missing Pre-requisite: The channel must be creeated.
                // 429: Too many requests: Queue storage or count is at maximum.  I.e.: queus is backlogging
                referenceGroup.MapPost(@"{Channel:regex(^[A-Za-z_][A-Za-z0-9_\-\.]{{0,39}}$)}/{Key:regex(^[A-Za-z_][A-Za-z0-9_\-\.]{{0,79}}$)}",
                    (HttpContext context, [FromHeader] string AuthToken, string Channel, string Key)
                    => _Processor.ReferenceWrite(context, AuthToken, Channel, Key))
                .WithName("Set value for key");

                // Delete reference
                // 200: OK, removed
                // 400: Bad request (invalid channel name, etc)
                // 401: Unauthorized: invalid admin/access token value
                // 428: Missing Pre-requisite: The channel must be creeated.
                referenceGroup.MapDelete(@"{Channel:regex(^[A-Za-z_][A-Za-z0-9_\-\.]{{0,39}}$)}/{Key:regex(^[A-Za-z_][A-Za-z0-9_\-\.]{{0,79}}$)}",
                    (HttpContext context, [FromHeader] string AuthToken, string Channel, string Key)
                    => _Processor.ReferenceDelete(context, AuthToken, Channel, Key))
                .WithName("Delete key value from collection");

                if (hostedByIIS)
                {
                    app.Run();
                }
                else
                {
                    app.Run(_RelayHubConfig.Listeners);
                }
            }
            catch (Exception ex)
            {
                _Logger.LogError(ex, "Fatal error starting web application", new object[0]);
                Console.WriteLine(DetailedException.WithEnterpriseContent(
                    ref ex,
                    "Failure startomg the web application", string.Empty
                ));
                System.Environment.ExitCode = 1;
            }
#if DEBUG
            Console.WriteLine("Complete.  Press ENTER to close.");
            Console.ReadLine();
#endif
        }
    }
}