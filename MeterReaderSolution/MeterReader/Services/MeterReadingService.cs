#undef jwt
#define cert

using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using MeterReader.gRPC;
using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using static MeterReader.gRPC.MeterReadingService;

namespace MeterReader.Services;

#if jwt
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
#endif
#if cert
[Authorize(AuthenticationSchemes = CertificateAuthenticationDefaults.AuthenticationScheme)]
#endif
public class MeterReadingService : MeterReadingServiceBase
{
    private readonly IReadingRepository _repository;
    private readonly ILogger<MeterReadingService> _logger;
    private readonly JwtTokenValidationService _tokenService;

    public MeterReadingService(
        IReadingRepository repository, 
        ILogger<MeterReadingService> logger, 
        JwtTokenValidationService tokenService)
    {
        _repository = repository;
        _logger = logger;
        _tokenService = tokenService;
    }

    [AllowAnonymous]
    public override async Task<TokenResponse> GenerateToken(TokenRequest request, ServerCallContext context)
    {
        var cred = new CredentialModel
        {
            UserName = request.Username,
            Passcode = request.Password
        };

        var result = await _tokenService.GenerateTokenModelAsync(cred);

        if (result.Success)
        {
            return new TokenResponse
            {
                Success = true,
                Token = result.Token,
                Expiration = Timestamp.FromDateTime(result.Expiration)
            };
        }
        else
        {
            return new TokenResponse
            {
                Success = false
            };
        }

    }

    public override async Task<StatusMessage> AddReading(ReadingPacket request, ServerCallContext context)
    {
        if (request.Successful == ReadingStatus.Success)
        {
            foreach (var reading in request.Readings)
            {
                var readingValue = new MeterReading()
                {
                    CustomerId = reading.CustomerId,
                    Value = reading.ReadingValue,
                    ReadingDate = reading.ReadingTime.ToDateTime()
                };
                _logger.LogInformation($"Adding {reading.ReadingValue}");
                _repository.AddEntity(readingValue);
            }
        }

        if (await _repository.SaveAllAsync())
        {
            _logger.LogInformation("Successfully saved new reading...");
            return new StatusMessage
            {
                Notes = "Successfully added to database.",
                Status = ReadingStatus.Success
            };
        }
        _logger.LogError("Failed to save new reading...");
        return new StatusMessage
        {
            Notes = "Failed to store readings in database.",
            Status = ReadingStatus.Failure
        };
    }

    public override async Task<Empty> AddReadingStream(
        IAsyncStreamReader<ReadingMessage> requestStream, 
        ServerCallContext context)
    {
        while (await requestStream.MoveNext())
        {
            var msg = requestStream.Current;
            
            var readingValue = new MeterReading()
            {
                CustomerId = msg.CustomerId,
                Value = msg.ReadingValue,
                ReadingDate = msg.ReadingTime.ToDateTime()
            };
            _logger.LogInformation($"Adding {msg.ReadingValue} from stream");
            _repository.AddEntity(readingValue);

            await _repository.SaveAllAsync();
        }

        return new Empty();
    }

    public override async Task AddReadingDuplexStream(
        IAsyncStreamReader<ReadingMessage> requestStream, 
        IServerStreamWriter<ErrorMessage> responseStream, 
        ServerCallContext context)
    {
        while (await requestStream.MoveNext())
        {
            var msg = requestStream.Current;

            if (msg.ReadingValue < 500)
            {
                await responseStream.WriteAsync(
                    new ErrorMessage 
                    { 
                        Message = $"Value less than 500. Value: {msg.ReadingValue}" 
                    });
            }

            var readingValue = new MeterReading()
            {
                CustomerId = msg.CustomerId,
                Value = msg.ReadingValue,
                ReadingDate = msg.ReadingTime.ToDateTime()
            };
            _logger.LogInformation($"Adding {msg.ReadingValue} from stream");
            _repository.AddEntity(readingValue);

            await _repository.SaveAllAsync();
        }

    }
}
