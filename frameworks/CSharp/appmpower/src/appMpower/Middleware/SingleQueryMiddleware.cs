using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace appMpower;

public class SingleQueryMiddleware
{
    private readonly static KeyValuePair<string, StringValues> _headerServer =
         new KeyValuePair<string, StringValues>("Server", new StringValues("k"));
    private readonly static KeyValuePair<string, StringValues> _headerContentType =
         new KeyValuePair<string, StringValues>("Content-Type", new StringValues("application/json"));

    private readonly RequestDelegate _nextStage;

    public SingleQueryMiddleware(RequestDelegate nextStage)
    {
        _nextStage = nextStage;
    }

    //public unsafe async Task Invoke(HttpContext httpContext)
    public async Task Invoke(HttpContext httpContext)
    {
        if (httpContext.Request.Path.StartsWithSegments("/db", StringComparison.Ordinal))
        {
            var response = httpContext.Response;
            response.Headers.Add(_headerServer);
            response.Headers.Add(_headerContentType);

            await NativeEndpoint.HandleAsync(response);
            return;
        }
        await _nextStage(httpContext);
    }
}

public static class SingleQueryMiddlewareExtensions
{
    public static IApplicationBuilder UseSingleQuery(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SingleQueryMiddleware>();
    }
}

internal static class NativeEndpoint
{
    public static async Task HandleAsync(HttpResponse response)
    {
        PipeWriter writer = response.BodyWriter;

        // Pick a safe upper bound for your JSON size.
        // For a single /db row this can be small (e.g. 1024 / 2048 / 4096).
        //const int SizeHint = 4096;
        const int SizeHint = 32;

        Memory<byte> memory = writer.GetMemory(SizeHint);

        int written;

        unsafe
        {
            using var handle = memory.Pin();
            //IntPtr ptr = (IntPtr)handle.Pointer;
            byte* buffer = (byte*)handle.Pointer;

            //written = NativeMethods.DbFill(ptr, memory.Length);
            written = NativeMethods.DbFill(buffer, memory.Length);
        }

        // Optionally: detect impossible issues
        // if (written < 0 || written > memory.Length) throw ...

        response.Headers.Add(
            new KeyValuePair<string, StringValues>("Content-Length", written.ToString()));

        writer.Advance(written);
        await writer.FlushAsync();
    }
}