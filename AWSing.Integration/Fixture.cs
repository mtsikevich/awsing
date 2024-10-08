using Alba;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.LocalStack;

namespace AWSing.Integration;

public class Fixture : IAsyncLifetime
{
    public IAlbaHost host = null!;

    public readonly LocalStackContainer localStack = new LocalStackBuilder()
        .Build();

    public async Task InitializeAsync()
    {
        Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", "ow!");
        Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", "ai!");

        await localStack.StartAsync();
        host = await AlbaHost.For<Program>(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll(typeof(IAmazonS3));
                services.AddSingleton<IAmazonS3>(new AmazonS3Client(new AmazonS3Config
                {
                    ServiceURL = localStack.GetConnectionString(),
                    UseHttp = true,
                }));
            });
        });
    }

    public async Task DisposeAsync()
    {
        await host.DisposeAsync();
        await localStack.DisposeAsync();
    }
}

public class Tests(Fixture fixture) : IClassFixture<Fixture>
{
    [Fact]
    public async Task Counter()
    {
        //
        var config = new AmazonSimpleNotificationServiceConfig
        {
            ServiceURL = fixture.localStack.GetConnectionString(),
            RegionEndpoint = RegionEndpoint.AFSouth1,
        };
        using var client = new AmazonSimpleNotificationServiceClient(config);
        var topic = await client.CreateTopicAsync("my-topic");

        var request = new PublishRequest
        {
            TopicArn = topic.TopicArn,
            Message = "OTP: 34343",
            PhoneNumber = "+27848648121",
        };
        
        //
        var response = await client.PublishAsync(request);

        //
        response.MessageId.Should().NotBeNullOrWhiteSpace();
    }
}