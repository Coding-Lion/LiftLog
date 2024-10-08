using LiftLog.Lib.Models;
using LiftLog.Ui.Models;

namespace LiftLog.Ui.Store.CurrentSession;

public enum SessionTarget
{
    WorkoutSession,
    HistorySession,
    FeedSession,
}

public record SetCurrentSessionHydratedAction();

public record SetActiveSessionDateAction(SessionTarget Target, DateOnly Date);

public record CycleExerciseRepsAction(SessionTarget Target, int ExerciseIndex, int SetIndex);

public record SetExerciseRepsAction(
    SessionTarget Target,
    int ExerciseIndex,
    int SetIndex,
    int? Reps
);

public record UpdateExerciseWeightAction(SessionTarget Target, int ExerciseIndex, decimal Weight);

public record UpdateNotesForExerciseAction(SessionTarget Target, int ExerciseIndex, string? Notes);

public record UpdateBodyweightAction(SessionTarget Target, decimal? Bodyweight);

public record ToggleExercisePerSetWeightAction(SessionTarget Target, int ExerciseIndex);

public record UpdateWeightForSetAction(
    SessionTarget Target,
    int ExerciseIndex,
    int SetIndex,
    decimal Weight
);

public record EditExerciseInActiveSessionAction(
    SessionTarget Target,
    int ExerciseIndex,
    ExerciseBlueprint NewBlueprint
);

public record AddExerciseToActiveSessionAction(
    SessionTarget Target,
    ExerciseBlueprint ExerciseBlueprint
);

public record RemoveExerciseFromActiveSessionAction(SessionTarget Target, int ExerciseIndex);

public record SetCurrentSessionAction(SessionTarget Target, Session? Session);

public record SetCurrentSessionFromBlueprintAction(
    SessionTarget Target,
    SessionBlueprint Blueprint
);

public record PersistCurrentSessionAction(SessionTarget Target);

public record NotifySetTimerAction(SessionTarget Target);

public record ClearSetTimerNotificationAction();

public record SetLatestSetTimerNotificationIdAction(Guid Id);

public record CompleteSetFromNotificationAction(SessionTarget Target);

public record DeleteSessionAction(Session Session);
