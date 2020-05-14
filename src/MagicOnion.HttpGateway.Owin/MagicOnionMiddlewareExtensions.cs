using Grpc.Core;
using MagicOnion.HttpGateway;
using MagicOnion.HttpGateway.Swagger;
using MagicOnion.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Owin;

namespace MagicOnion
{
    public static class MagicOnionMiddlewareExtensions
    {
        public static IAppBuilder UseMagicOnionHttpGateway(this IAppBuilder app, IReadOnlyList<MethodHandler> handlers, Channel channel)
        {
            return app.Use<MagicOnionHttpGatewayMiddleware>(handlers, channel);
        }

        public static IAppBuilder UseMagicOnionSwagger(this IAppBuilder app, IReadOnlyList<MethodHandler> handlers, SwaggerOptionsOwin options)
        {
            return app.Use<MagicOnionSwaggerMiddleware>(handlers, options);
        }
    }
}