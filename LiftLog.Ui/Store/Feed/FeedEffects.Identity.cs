using System.Text;
using Fluxor;
using Google.Protobuf;
using LiftLog.Lib.Models;
using LiftLog.Lib.Services;
using LiftLog.Ui.Models;
using LiftLog.Ui.Services;
using LiftLog.Ui.Store.App;
using Microsoft.Extensions.Logging;

namespace LiftLog.Ui.Store.Feed;

public partial class FeedEffects
{
    [EffectMethod]
    public async Task HandleCreateFeedIdentityAction(
        CreateFeedIdentityAction action,
        IDispatcher dispatcher
    )
    {
        if (state.Value.IsLoadingIdentity)
        {
            return;
        }

        dispatcher.Dispatch(new SetIsLoadingIdentityAction(true));
        var identityResult = await feedIdentityService.CreateFeedIdentityAsync(
            action.Name,
            action.ProfilePicture,
            action.PublishBodyweight,
            action.PublishPlan,
            action.PublishWorkouts,
            programState.Value.GetActivePlanSessionBlueprints()
        );
        dispatcher.Dispatch(new SetIsLoadingIdentityAction(false));
        if (!identityResult.IsSuccess)
        {
            dispatcher.Dispatch(
                new FeedApiErrorAction("Failed to create user", identityResult.Error, action)
            );
            return;
        }

        dispatcher.Dispatch(new PutFeedIdentityAction(identityResult.Data));
    }

    [EffectMethod]
    public Task HandleUpdateFeedIdentityAction(
        UpdateFeedIdentityAction action,
        IDispatcher dispatcher
    ) =>
        IfIdentityExists(async identity =>
        {
            dispatcher.Dispatch(
                new PutFeedIdentityAction(
                    identity with
                    {
                        Name = action.Name,
                        ProfilePicture = action.ProfilePicture,
                        PublishBodyweight = action.PublishBodyweight,
                        PublishPlan = action.PublishPlan,
                        PublishWorkouts = action.PublishWorkouts,
                    }
                )
            );
            var result = await feedIdentityService.UpdateFeedIdentityAsync(
                identity.Id,
                identity.Lookup,
                identity.Password,
                identity.AesKey,
                identity.RsaKeyPair,
                action.Name,
                action.ProfilePicture,
                action.PublishBodyweight,
                action.PublishPlan,
                action.PublishWorkouts,
                programState.Value.GetActivePlanSessionBlueprints()
            );
            if (!result.IsSuccess)
            {
                dispatcher.Dispatch(new PutFeedIdentityAction(identity));

                dispatcher.Dispatch(
                    new FeedApiErrorAction("Failed to update user", result.Error, action)
                );
                return;
            }

            dispatcher.Dispatch(new PutFeedIdentityAction(result.Data));
        });

    [EffectMethod]
    public async Task HandlePublishIdentityIfEnabledAction(
        PublishIdentityIfEnabledAction action,
        IDispatcher dispatcher
    )
    {
        if (state.Value.Identity is null)
        {
            return;
        }

        var result = await feedIdentityService.UpdateFeedIdentityAsync(
            state.Value.Identity.Id,
            state.Value.Identity.Lookup,
            state.Value.Identity.Password,
            state.Value.Identity.AesKey,
            state.Value.Identity.RsaKeyPair,
            state.Value.Identity.Name,
            state.Value.Identity.ProfilePicture,
            state.Value.Identity.PublishBodyweight,
            state.Value.Identity.PublishPlan,
            state.Value.Identity.PublishWorkouts,
            programState.Value.GetActivePlanSessionBlueprints()
        );
        if (!result.IsSuccess)
        {
            dispatcher.Dispatch(
                new FeedApiErrorAction("Failed to publish user", result.Error, action)
            );
            return;
        }
    }

    [EffectMethod]
    public Task HandleDeleteFeedIdentityAction(
        DeleteFeedIdentityAction action,
        IDispatcher dispatcher
    ) =>
        IfIdentityExists(async identity =>
        {
            var response = await feedApiService.DeleteUserAsync(
                new DeleteUserRequest(Id: identity.Id, Password: identity.Password)
            );
            if (!response.IsSuccess && response.Error.Type != ApiErrorType.NotFound)
            {
                dispatcher.Dispatch(
                    new FeedApiErrorAction("Failed to delete account", response.Error, action)
                );
                return;
            }

            dispatcher.Dispatch(new PutFeedIdentityAction(null));
            dispatcher.Dispatch(new SetIsLoadingIdentityAction(false));
            dispatcher.Dispatch(new ReplaceFeedItemsAction([]));
            dispatcher.Dispatch(new ReplaceFeedFollowedUsersAction([]));
            dispatcher.Dispatch(
                new CreateFeedIdentityAction(null, null, false, false, false, false)
            );
        });

    private Task IfIdentityExists(
        Func<FeedIdentity, Task> whenIdentityExists,
        Func<Task>? whenNoIdentity = null
    )
    {
        var identity = state.Value.Identity;
        if (identity is null)
        {
            return whenNoIdentity?.Invoke() ?? Task.CompletedTask;
        }

        return whenIdentityExists(identity);
    }
}
