using System;
using Microsoft.ML;
using Microsoft.ML.Data;
using TSLab.Script.Handlers;

namespace TSLab.ML.Net
{
    public abstract class BinaryModelBase<T> : IContextUses where T : struct
    {
        #region Prediction Data

        // ReSharper disable UnusedMember.Global
        // ReSharper disable InconsistentNaming

        protected class PredictionData
        {
            public int Index;

            public VBuffer<T> Features;

            public float Probability = 1;

            public bool Preview;
        }

        protected class PredictionResult
        {
            [ColumnName("PredictedLabel")]
            public bool Prediction { get; set; }

            public float Probability { get; set; }

            public float Score { get; set; }
        }
        // ReSharper restore UnusedMember.Global
        // ReSharper restore InconsistentNaming

        #endregion

        public IContext Context { get; set; }

        [HandlerParameter(true, "50", Min = "5", Max = "100", Step = "5", EditorMin = "1")]
        public int HistoryBarsBack { get; set; } = 20;

        [HandlerParameter(true, Default = "", NotOptimized = true)]
        public string ModelPath { get; set; } = "";

        protected static SchemaDefinition GetSchemaDefinition(int size)
        {
            var scheme = SchemaDefinition.Create(typeof(PredictionData));
            scheme["Preview"].ColumnType = BooleanDataViewType.Instance;
            scheme["Preview"].ColumnName = "Label";
            scheme["Probability"].ColumnType = NumberDataViewType.Single;
            scheme["Features"].ColumnType = new VectorDataViewType(NumberDataViewType.Single, size);
            return scheme;
        }

        protected static MLContext MakeMlContext()
        {
            return new MLContext(1);
        }

        protected IEstimator<ITransformer> GetEstimator(MLContext mlContext, int typeOfTrainer, int numberOfIterations)
        {
            IEstimator<ITransformer> trainer;
            switch (typeOfTrainer)
            {
                case 2:
                    trainer = mlContext.BinaryClassification.Trainers.LightGbm(numberOfIterations: numberOfIterations * 50);
                    break;
                case 3:
                    trainer = mlContext.BinaryClassification.Trainers.FastTree(numberOfLeaves: numberOfIterations * 10);
                    break;
                case 4:
                    trainer = mlContext.BinaryClassification.Trainers.LbfgsLogisticRegression(historySize: numberOfIterations * 10);
                    break;
                case 5:
                    trainer = mlContext.BinaryClassification.Trainers.SdcaLogisticRegression(maximumNumberOfIterations: numberOfIterations * 10);
                    break;
                case 6:
                    trainer = mlContext.BinaryClassification.Trainers.Gam(numberOfIterations: numberOfIterations * 3000);
                    break;
                default:
                    trainer = mlContext.BinaryClassification.Trainers.LinearSvm(numberOfIterations: numberOfIterations);
                    break;
            }

            return trainer;
        }
    }
}