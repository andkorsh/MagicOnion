using MagicOnion.HttpGateway.Swagger;
using Microsoft.Owin;

namespace MagicOnion.HttpGateway
{
    public class SwaggerOptionsOwin : SwaggerOptions<IOwinContext>
    {
        public SwaggerOptionsOwin(string title, string description, string apiBasePath) : base(title, description, apiBasePath)
        {
        }
    }
}
