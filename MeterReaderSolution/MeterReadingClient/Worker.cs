#undef stream
#undef streamDuplex
#undef jwt
#define cert


using Grpc.Core;
using Grpc.Net.Client;
using MeterReader.gRPC;
using System.Security.Cryptography.X509Certificates;
using static MeterReader.gRPC.MeterReadingService;

namespace MeterReadingClient
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly ReadingGenerator _generator;
        private readonly int _customerId;
        private readonly string? _serviceUrl;
        private readonly IConfiguration _config;
        private string _token;
        private DateTime _expiration;

        public Worker(ILogger<Worker> logger, ReadingGenerator generator, IConfiguration config)
        {
            _logger = logger;
            _generator = generator;
            _customerId = config.GetValue<int>("CustomerId");
            _serviceUrl = config["ServiceUrl"];
            _config = config;
            _token = "";
            _expiration = DateTime.MinValue;
        }

        bool NeedsLogin()
        {
            return string.IsNullOrEmpty(_token) || _expiration > DateTime.Now;
        }

        async Task<bool> RequestToken()
        {
            try
            {
                var req = new TokenRequest
                {
                    Username = _config["Settings:Username"],
                    Password = _config["Settings:Password"]
                };

                var result = await CreateClient().GenerateTokenAsync(req);

                if (result.Success)
                {
                    _token = result.Token;
                    _expiration = result.Expiration.ToDateTime();
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }
            return false;
        }

        MeterReadingServiceClient CreateClient()
        {
#if cert
            var certificate = new X509Certificate2(
                _config["Settings:Certificate:Name"]!, 
                _config["Settings:Certificate:Password"]);
            var handler = new HttpClientHandler();
            handler.ClientCertificates.Add(certificate);

            var httpClient = new HttpClient(handler);

            var options = new GrpcChannelOptions
            {
                HttpClient = httpClient
            };

            var channel = GrpcChannel.ForAddress(_serviceUrl!, options);
#endif
#if jwt
            var channel = GrpcChannel.ForAddress(_serviceUrl!);
#endif
            return new MeterReadingServiceClient(channel);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
#if jwt
                    if (!NeedsLogin() || await RequestToken())
                    {
                        //Get a JWT
                        var headers = new Metadata
                        {
                            { "Authorization", $"Bearer {_token}" }
                        };
#endif
#if !stream && !streamDuplex
                        var packet = new ReadingPacket
                        {
                            Successful = ReadingStatus.Success
                        };
#elif stream && !streamDuplex
#if jwt
                        var stream = CreateClient().AddReadingStream(headers);
#elif !jwt
                        var stream = CreateClient().AddReadingStream();   
#endif
#elif stream && streamDuplex
#if jwt
                        var stream = CreateClient().AddReadingDuplexStream(headers);
#elif !jwt
                        var stream = CreateClient().AddReadingDuplexStream();
#endif
#endif

                        for (var x = 0; x < 5; ++x)
                        {
                            var reading = await _generator.GenerateAsync(_customerId);
#if !stream && !streamDuplex
                            packet.Readings.Add(reading);
#elif stream
                            await stream.RequestStream.WriteAsync(reading);
                            await Task.Delay(500, stoppingToken);
#endif
                        }
#if !stream && !streamDuplex
                        var status = CreateClient().AddReading(packet);
                        if (status.Status == ReadingStatus.Success)
                        {
                            _logger.LogInformation("Successfully called GRPC");
                        }
                        else
                        {
                            _logger.LogError("Failed to call GRPC");
                        }
#elif stream
                        await stream.RequestStream.CompleteAsync();
                        //var result = await stream.ResponseAsync;
#endif
#if streamDuplex
                        while (await stream.ResponseStream.MoveNext(new CancellationToken()))
                        {
                            _logger.LogWarning($"From server: {stream.ResponseStream.Current.Message}");
                        }
                        _logger.LogInformation("Finished calling GRPC");
#endif
#if jwt
                    }
                    else
                    {
                        _logger.LogInformation("Failed to get JWT Token");
                    }
#endif
                }
                catch (RpcException rex)
                {
                    _logger.LogError(rex.Message);
                }
                await Task.Delay(5000, stoppingToken);
            }
        }
    }
}
