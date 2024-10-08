using LiftLog.Lib.Models;

namespace LiftLog.Api.Service;

public class PurchaseVerificationService(
    GooglePlayPurchaseVerificationService googlePlayPurchaseVerificationService,
    WebAuthPurchaseVerificationService webAuthPurchaseVerificationService,
    AppleAppStorePurchaseVerificationService appleAppStorePurchaseVerificationService
)
{
    public Task<bool> IsValidPurchaseToken(AppStore appStore, string proToken)
    {
        return appStore switch
        {
            AppStore.Google => googlePlayPurchaseVerificationService.IsValidPurchaseToken(proToken),
            AppStore.Apple => appleAppStorePurchaseVerificationService.IsValidPurchaseToken(
                proToken
            ),
            AppStore.Web => webAuthPurchaseVerificationService.IsValidPurchaseToken(proToken),
        };
    }
}
