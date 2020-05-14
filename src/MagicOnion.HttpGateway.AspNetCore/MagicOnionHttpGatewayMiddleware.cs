using Grpc.Core;
using System.Reflection;
using MagicOnion.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace MagicOnion.HttpGateway
{
    public class MagicOnionHttpGatewayMiddleware
    {
        readonly RequestDelegate next;
        readonly IDictionary<string, MethodHandler> handlers;
        readonly Channel channel;

        public MagicOnionHttpGatewayMiddleware(RequestDelegate next, IReadOnlyList<MethodHandler> handlers, Channel channel)
        {
            this.next = next;
            this.handlers = handlers.ToDictionary(x => "/" + x.ToString());
            this.channel = channel;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            try
            {
                var path = httpContext.Request.Path.Value;

                MethodHandler handler;
                if (!handlers.TryGetValue(path, out handler))
                {
                    await next(httpContext);
                    return;
                }

                // from form...
                object deserializedObject;
                if (httpContext.Request.ContentType == "application/x-www-form-urlencoded")
                {
                    //object parameters
                    var args = new List<object>();
                    var typeArgs = new List<Type>();

                    foreach (var p in handler.MethodInfo.GetParameters())
                    {
                        typeArgs.Add(p.ParameterType);

                        StringValues stringValues;
                        if (httpContext.Request.Form.TryGetValue(p.Name, out stringValues))
                        {
                            args.Add(Utils.ParseParameter(p, stringValues));
                        }
                        else
                        {
                            if (p.HasDefaultValue)
                            {
                                args.Add(p.DefaultValue);
                            }
                            else
                            {
                                args.Add(null);
                            }
                        }
                    }

                    deserializedObject = typeArgs.Count == 1 ?
                        args[0] : MagicOnionMarshallers.InstantiateDynamicArgumentTuple(typeArgs.ToArray(), args.ToArray());
                }
                else
                {
                    string body;
                    using (var sr = new StreamReader(httpContext.Request.Body, Encoding.UTF8))
                    {
                        body = sr.ReadToEnd();
                    }

                    if (handler.RequestType == typeof(byte[]) && string.IsNullOrWhiteSpace(body))
                    {
                        body = "[]";
                    }
                    deserializedObject = Newtonsoft.Json.JsonConvert.DeserializeObject(body, handler.RequestType);
                }

                // JSON to C# Object to MessagePack
                var requestObject = handler.BoxedSerialize(deserializedObject);

                var method = new Method<byte[], byte[]>(MethodType.Unary, handler.ServiceName, handler.MethodInfo.Name, MagicOnionMarshallers.ThroughMarshaller, MagicOnionMarshallers.ThroughMarshaller);

                // create header
                var metadata = new Metadata();
                foreach (var header in httpContext.Request.Headers)
                {
                    metadata.Add(header.Key, header.Value);
                }

                var invoker = new DefaultCallInvoker(channel);
                var rawResponse = await invoker.AsyncUnaryCall(method, null, default(CallOptions).WithHeaders(metadata), requestObject);

                // MessagePack -> Object -> Json
                var obj = handler.BoxedDeserialize(rawResponse);
                var v = JsonConvert.SerializeObject(obj, new[] { new Newtonsoft.Json.Converters.StringEnumConverter() });
                httpContext.Response.ContentType = "application/json";
                await httpContext.Response.WriteAsync(v);
            }
            catch (Exception ex)
            {
                httpContext.Response.StatusCode = 500;
                httpContext.Response.ContentType = "text/plain";
                await httpContext.Response.WriteAsync(ex.ToString());
            }
        }
    }
}