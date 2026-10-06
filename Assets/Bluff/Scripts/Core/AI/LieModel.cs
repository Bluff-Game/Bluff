using System;

namespace Bluff.Core.AI
{
    /// <summary>
    /// Regresi logistik kecil yang memperkirakan peluang sebuah taruhan itu bohong.
    /// Bobotnya dilatih dari ribuan game simulasi: Tests/Bluff.Core.Tests -> dotnet run -c Release -- train
    /// </summary>
    public static class LieModel
    {
        public const int FeatureCount = 14;

        public static readonly string[] FeatureNames =
        {
            "bias",
            "pHave",
            "leader",
            "leader*pHave",
            "count2",
            "count3",
            "handBefore/13",
            "crowd",
            "lieHistory",
            "pileBefore/10",
            "targetDanger",
            "roundPos",
            "endgame",
            "dumpRatio",
        };

        public static readonly double[] Weights = { 4.7868, -3.0101, -0.0084, -3.9187, -0.5824, -1.8708, 0.3198, 0.3100, -1.2016, 0.4207, -2.4931, -1.2827, 0.0903, -5.8392 };

        public static double Predict(double[] features)
        {
            double z = 0.0;
            for (int i = 0; i < FeatureCount; i++)
                z += Weights[i] * features[i];
            return 1.0 / (1.0 + Math.Exp(-z));
        }
    }
}
