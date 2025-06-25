using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using appMpower.Serializers;
using System.Runtime.CompilerServices;

namespace appMpower; 

public class SingleQueryMiddleware
{
    private readonly static KeyValuePair<string, StringValues> _headerServer =
         new KeyValuePair<string, StringValues>("Server", new StringValues("k"));
    private readonly static KeyValuePair<string, StringValues> _headerContentType =
         new KeyValuePair<string, StringValues>("Content-Type", new StringValues("application/json"));

    [ThreadStatic]
    private static Utf8JsonWriter _utf8JsonWriter;

    private readonly RequestDelegate _nextStage;

    public SingleQueryMiddleware(RequestDelegate nextStage)
    {
        _nextStage = nextStage;
    }

    public unsafe Task Invoke(HttpContext httpContext)
    {
        if (httpContext.Request.Path.StartsWithSegments("/db", StringComparison.Ordinal))
        {
            var response = httpContext.Response; 
            response.Headers.Add(_headerServer);
            response.Headers.Add(_headerContentType);

            int payloadLength;
            IntPtr handlePointer; 

            IntPtr bytePointer = NativeMethods.Db(out payloadLength, out handlePointer);

            //byte[] byteArray = new byte[payloadLength];

            /*
            fixed (byte* destination = byteArray)
            {
                // Use Unsafe.CopyBlock to copy the memory from source to destination
                Unsafe.CopyBlock(destination, (void*)bytePointer, (uint)payloadLength);
            }
            */

            ReadOnlySpan<byte> sourceSpan = new ReadOnlySpan<byte>((void*)bytePointer, payloadLength);

            // Create a managed array and copy the data
            //sourceSpan.CopyTo(byteArray);

            //Marshal.Copy(bytePointer, byteArray, 0, payloadLength);
            //NativeMethods.FreeHandlePointer(handlePointer);

            //byte[] byteArray = DotnetMethods.Db();

            var pipeWriter = httpContext.Response.BodyWriter; 
            Utf8JsonWriter utf8JsonWriter = _utf8JsonWriter ??= new Utf8JsonWriter(pipeWriter, new JsonWriterOptions { SkipValidation = true });
            utf8JsonWriter.Reset(pipeWriter);

            WorldSerializer.Serialize(utf8JsonWriter, 
                                      BinaryPrimitives.ReadInt16LittleEndian(sourceSpan.Slice(0, 2)),
                                      BinaryPrimitives.ReadInt16LittleEndian(sourceSpan.Slice(2, 2)));
            utf8JsonWriter.Flush();
            NativeMethods.FreeHandlePointer(handlePointer);
            
            response.Headers.Add(
                new KeyValuePair<string, StringValues>("Content-Length", utf8JsonWriter.BytesCommitted.ToString()));

            //return response.Body.WriteAsync(Encoding.UTF8.GetBytes(json), 0, Encoding.UTF8.GetBytes(json).Length);
            return Task.CompletedTask;
        }

        return _nextStage(httpContext);
    }
}

public static class SingleQueryMiddlewareExtensions
{
    public static IApplicationBuilder UseSingleQuery(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SingleQueryMiddleware>();
    }
}