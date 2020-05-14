using MagicOnion.HttpGateway.Swagger;
using Microsoft.AspNetCore.Http;

namespace MagicOnion.HttpGateway
{
    public class SwaggerOptionsAspNetCore : SwaggerOptions<HttpContext>
    {
        public SwaggerOptionsAspNetCore(string title, string description, string apiBasePath) : base(title, description, apiBasePath)
        {
        }
    }
}
