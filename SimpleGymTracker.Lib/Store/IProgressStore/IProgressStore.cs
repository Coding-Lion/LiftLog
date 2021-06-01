using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SimpleGymTracker.Lib.Models;

namespace SimpleGymTracker.Lib.Store
{
  public interface IProgressStore
  {
    ValueTask<WorkoutDayDao?> GetCurrentDayAsync();

    ValueTask SaveCurrentDayAsync(WorkoutDayDao day);

    IAsyncEnumerable<WorkoutDayDao> GetAllWorkoutDaysAsync();

    IAsyncEnumerable<WorkoutDayDao> GetWorkoutDaysForPlanAsync(WorkoutPlan plan);

    ValueTask SaveCompletedDayAsync(WorkoutDayDao dao);
  }
}
