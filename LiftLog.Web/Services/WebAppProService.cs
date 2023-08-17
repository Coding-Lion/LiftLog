using LiftLog.Lib.Models;
using LiftLog.Ui.Services;

namespace LiftLog.Web.Services;

public class WebAppProService : IAppPurchaseService
{

    public Task<string?> GetProKeyAsync()
    {
        return Task.FromResult<string?>("102bc25a-f46b-4423-9149-b0fa39b32f1e");
    }

    public AppStore GetAppStore()
    {
        return AppStore.Web;
    }
}
