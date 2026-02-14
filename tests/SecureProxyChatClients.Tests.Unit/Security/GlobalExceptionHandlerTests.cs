using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using SecureProxyChatClients.Server.Security;

namespace SecureProxyChatClients.Tests.Unit.Security;

public class GlobalExceptionHandlerTests
{
    private static GlobalExceptionHandler CreateHandler() =>
        new(NullLogger<GlobalExceptionHandler>.Instance);

    [Fact]
    public async Task TryHandleAsync_Returns500_ForGenericException()
    {
        var handler = CreateHandler();
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        bool handled = await handler.TryHandleAsync(context, new Exception("boom"), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(500, context.Response.StatusCode);
    }

    [Fact]
    public async Task TryHandleAsync_Returns504_ForTimeoutException()
    {
        var handler = CreateHandler();
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        bool handled = await handler.TryHandleAsync(context, new TimeoutException(), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(504, context.Response.StatusCode);
    }

    [Fact]
    public async Task TryHandleAsync_Returns403_ForUnauthorizedAccessException()
    {
        var handler = CreateHandler();
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        bool handled = await handler.TryHandleAsync(context, new UnauthorizedAccessException(), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(403, context.Response.StatusCode);
    }

    [Fact]
    public async Task TryHandleAsync_DoesNotLeakExceptionDetails()
    {
        var handler = CreateHandler();
        var context = new DefaultHttpContext();
        var body = new MemoryStream();
        context.Response.Body = body;

        await handler.TryHandleAsync(context, new Exception("SECRET_INTERNAL_ERROR"), CancellationToken.None);

        body.Position = 0;
        string responseBody = await new StreamReader(body).ReadToEndAsync();
        Assert.DoesNotContain("SECRET_INTERNAL_ERROR", responseBody);
        Assert.Contains("An internal error occurred", responseBody);
    }
}
