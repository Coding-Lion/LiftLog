using System.Collections.Immutable;
using System.Text;
using Fluxor;
using Google.Protobuf;
using LiftLog.Lib;
using LiftLog.Lib.Models;
using LiftLog.Lib.Services;
using LiftLog.Ui.Models;
using LiftLog.Ui.Models.SessionHistoryDao;
using LiftLog.Ui.Services;
using LiftLog.Ui.Store.App;
using LiftLog.Ui.Store.Program;
using Microsoft.Extensions.Logging;
using static LiftLog.Ui.Models.UserEventPayload;

namespace LiftLog.Ui.Store.Feed;

public partial class FeedEffects(
    IState<FeedState> state,
    IState<ProgramState> programState,
    ProgressRepository progressRepository,
    IFeedApiService feedApiService,
    FeedFollowService feedFollowService,
    FeedIdentityService feedIdentityService,
    IEncryptionService encryptionService,
    FeedInboxDecryptionService feedInboxDecryptionService,
    ILogger<FeedEffects> logger
)
{
    [EffectMethod]
    public async Task HandleFetchAndSetSharedFeedUserAction(
        FetchAndSetSharedFeedUserAction action,
        IDispatcher dispatcher
    )
    {
        dispatcher.Dispatch(new SetSharedFeedUserAction(RemoteData.Loading));
        var result = await feedApiService.GetUserAsync(action.IdOrLookup);
        if (result.IsSuccess)
        {
            dispatcher.Dispatch(
                new SetSharedFeedUserAction(
                    RemoteData.Success(
                        FeedUser.FromShared(
                            result.Data.Id,
                            new RsaPublicKey(result.Data.RsaPublicKey),
                            action.Name
                        )
                    )
                )
            );
        }
        else
        {
            dispatcher.Dispatch(
                new FeedApiErrorAction("Failed to fetch user", result.Error, action)
            );
            dispatcher.Dispatch(
                new SetSharedFeedUserAction(RemoteData.Errored("Failed to fetch user"))
            );
        }
    }

    [EffectMethod]
    public async Task HandleRequestFollowSharedUserAction(
        RequestFollowUserAction action,
        IDispatcher dispatcher
    )
    {
        var identity = state.Value.Identity;
        if (identity == null || !state.Value.SharedFeedUser.IsSuccess)
        {
            return;
        }

        var sharedFeedUser = state.Value.SharedFeedUser.Data;
        var result = await feedFollowService.RequestToFollowAUserAsync(identity, sharedFeedUser);

        if (result.IsSuccess)
        {
            dispatcher.Dispatch(new PutFollowedUsersAction(sharedFeedUser));
            dispatcher.Dispatch(new SetSharedFeedUserAction(RemoteData.NotAsked));
        }
        else
        {
            dispatcher.Dispatch(
                new FeedApiErrorAction("Failed to request follow user", result.Error, action)
            );
        }
    }

    [EffectMethod]
    public Task HandleProcessFollowResponsesAction(
        ProcessFollowResponsesAction action,
        IDispatcher dispatcher
    )
    {
        var acceptResponses = action.Responses.Where(x => x.Accepted).ToDictionary(x => x.UserId);
        var rejectedUsers = action
            .Responses.Where(x => !x.Accepted)
            .Select(x => x.UserId)
            .ToHashSet();
        var usersAfterResponses = state
            .Value.FollowedUsers.Select(x =>
                acceptResponses.TryGetValue(x.Key, out var value)
                    ? x.Value with
                    {
                        FollowSecret = value.FollowSecret,
                        AesKey = value.AesKey,
                    }
                    : x.Value
            )
            .Where(x => !rejectedUsers.Contains(x.Id))
            .ToImmutableList();

        dispatcher.Dispatch(new ReplaceFeedFollowedUsersAction(usersAfterResponses));
        dispatcher.Dispatch(new FetchSessionFeedItemsAction(false));

        return Task.CompletedTask;
    }

    [EffectMethod]
    public async Task UnfollowFeedUser(UnfollowFeedUserAction action, IDispatcher dispatcher)
    {
        var identity = state.Value.Identity;
        if (identity is null)
        {
            return;
        }

        if (action.FeedUser.FollowSecret is null)
        {
            return;
        }

        var inboxMessage = new InboxMessageDao
        {
            FromUserId = identity.Id,
            UnfollowNotification = new UnFollowNotification
            {
                FollowSecret = action.FeedUser.FollowSecret,
            },
        };
        inboxMessage.Signature = ByteString.CopyFrom(
            await encryptionService.SignRsaPssSha256Async(
                FeedInboxDecryptionService.GetSignaturePayload(inboxMessage, action.FeedUser.Id),
                identity.RsaKeyPair.PrivateKey
            )
        );
        var encryptedInboxMessage = await encryptionService.EncryptRsaOaepSha256Async(
            inboxMessage.ToByteArray(),
            action.FeedUser.PublicKey
        );
        await feedApiService.PutInboxMessageAsync(
            new PutInboxMessageRequest(
                EncryptedMessage: encryptedInboxMessage.DataChunks,
                ToUserId: action.FeedUser.Id
            )
        );
    }

    [EffectMethod]
    public Task HandleAcceptFollowRequestAction(
        AcceptFollowRequestAction action,
        IDispatcher dispatcher
    ) =>
        IfIdentityExists(async identity =>
        {
            if (
                state.Value.Followers.TryGetValue(action.Request.UserId, out var existingFollower)
                && existingFollower.FollowSecret is not null
            )
            {
                await feedFollowService.RevokeFollowSecretAsync(
                    identity,
                    existingFollower.FollowSecret
                );
            }

            var userResponse = await feedApiService.GetUserAsync(action.Request.UserId.ToString());
            if (!userResponse.IsSuccess)
            {
                dispatcher.Dispatch(
                    new FeedApiErrorAction("Failed to fetch user", userResponse.Error, action)
                );
                return;
            }
            var user = userResponse.Data;

            var followSecretResponse = await feedFollowService.AcceptFollowRequestAsync(
                identity,
                action.Request,
                new RsaPublicKey(user.RsaPublicKey)
            );

            if (!followSecretResponse.IsSuccess)
            {
                dispatcher.Dispatch(
                    new FeedApiErrorAction(
                        "Failed to accept follow request",
                        followSecretResponse.Error,
                        action
                    )
                );
                return;
            }

            dispatcher.Dispatch(
                new AddFollowerAction(
                    FeedUser.FromShared(
                        action.Request.UserId,
                        new RsaPublicKey(user.RsaPublicKey),
                        action.Request.Name
                    ) with
                    {
                        FollowSecret = followSecretResponse.Data,
                    }
                )
            );
            dispatcher.Dispatch(new RemoveFollowRequestAction(action.Request));
        });

    [EffectMethod]
    public Task HandleDenyFollowRequestAction(
        DenyFollowRequestAction action,
        IDispatcher dispatcher
    ) =>
        IfIdentityExists(async identity =>
        {
            var userResponse = await feedApiService.GetUserAsync(action.Request.UserId.ToString());
            if (!userResponse.IsSuccess)
            {
                logger.LogError(
                    "Failed to deny follow request with error {Error}. Removing request anyway.",
                    userResponse.Error
                );
                dispatcher.Dispatch(new RemoveFollowRequestAction(action.Request));
                return;
            }
            var result = await feedFollowService.DenyFollowRequestAsync(
                identity,
                action.Request,
                new RsaPublicKey(userResponse.Data.RsaPublicKey)
            );
            if (!result.IsSuccess && result.Error.Type != ApiErrorType.NotFound)
            {
                logger.LogError(
                    "Failed to deny follow request with error {Error}. Removing request anyway.",
                    result.Error
                );
            }

            dispatcher.Dispatch(new RemoveFollowRequestAction(action.Request));
        });

    [EffectMethod]
    public Task HandleStartRemoveFollowerAction(
        StartRemoveFollowerAction action,
        IDispatcher dispatcher
    ) =>
        IfIdentityExists(async identity =>
        {
            if (action.User.FollowSecret is not null)
            {
                var deleteFollowSecretResponse = await feedFollowService.RevokeFollowSecretAsync(
                    identity,
                    action.User.FollowSecret!
                );
                if (
                    !deleteFollowSecretResponse.IsSuccess
                    && deleteFollowSecretResponse.Error.Type != ApiErrorType.NotFound
                )
                {
                    dispatcher.Dispatch(
                        new FeedApiErrorAction(
                            "Failed to remove follower",
                            deleteFollowSecretResponse.Error,
                            action
                        )
                    );
                    return;
                }
            }

            dispatcher.Dispatch(new RemoveFollowerAction(action.User));
        });
}
