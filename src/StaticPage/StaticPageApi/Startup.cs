using ApiShared;

namespace StaticPageApi;

public class Startup(IConfiguration configuration) : AbstractStartup(configuration)
{
    public override string ApiName => "StaticPage";
}
