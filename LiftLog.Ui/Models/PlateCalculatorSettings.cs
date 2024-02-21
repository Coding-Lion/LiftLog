using System.Collections.Immutable;
using LiftLog.Lib;

namespace LiftLog.Ui.Models;

public record PlateCalculatorSettings(decimal BarOrMachineWeight, ImmutableListValue<decimal> PlateSizes){
    public ImmutableListValue<decimal> Calculate(decimal targetWeight){
        var result = new List<decimal>();
        var remainingWeight = targetWeight - BarOrMachineWeight;
        // Plates are always added in pairs
        remainingWeight /= 2;
        var plateSizes = PlateSizes.OrderByDescending(x => x).ToList();
        var index = 0;
        while (remainingWeight > 0 && index < plateSizes.Count){
            var plate = plateSizes[index];
            if (remainingWeight >= plate){
                result.Add(plate);
                remainingWeight -= plate;
            }
            else{
                index++;
            }
        }
        return result.ToImmutableList();
    }

    public static PlateCalculatorSettings DefaultMetric => new (20, [20, 10, 5, 2.5m, 1.25m]);
}
