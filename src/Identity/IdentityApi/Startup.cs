using ApiShared;

namespace IdentityApi;

public class Startup(IConfiguration configuration) : AbstractStartup(configuration)
{
    public override string ApiName => "Identity";
}
