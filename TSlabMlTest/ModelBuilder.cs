using System;
using System.IO;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace TSLab.ML.Net
{
    public class ModelBuilder<TObservation, TPrediction>
                                    where TObservation : class
                                    where TPrediction : class, new()
    {
        private readonly MLContext m_mlContext;
        public IEstimator<ITransformer> TrainingPipeline { get; private set; }
        public ITransformer TrainedModel { get; private set; }

        public ModelBuilder(
            MLContext mlContext,
            IEstimator<ITransformer> dataProcessPipeline
        )
        {
            m_mlContext = mlContext;
            TrainingPipeline = dataProcessPipeline;
        }

        public void AddTrainer(IEstimator<ITransformer> trainer)
        {
            this.AddEstimator(trainer);
        }

        public void AddEstimator(IEstimator<ITransformer> estimator)
        {
            TrainingPipeline = TrainingPipeline.Append(estimator);
        }

        public ITransformer Train(IDataView trainingData)
        {
            TrainedModel = TrainingPipeline.Fit(trainingData);
            return TrainedModel;
        }

        public RegressionMetrics EvaluateRegressionModel(IDataView testData, string label, string score)
        {
            CheckTrained();
            var predictions = TrainedModel.Transform(testData);
            var metrics = m_mlContext.Regression.Evaluate(predictions, label, score);
            return metrics;
        }

        // public void SaveModelAsFile(string persistedModelPath)
        // {
        //     CheckTrained();
        //
        //     using (var fs = new FileStream(persistedModelPath, FileMode.Create, FileAccess.Write, FileShare.Write))
        //         m_mlContext.Model.Save(TrainedModel, fs);
        //     Console.WriteLine("The model is saved to {0}", persistedModelPath);
        // }

        private void CheckTrained()
        {
            if (TrainedModel == null)
                throw new InvalidOperationException("Cannot test before training. Call Train() first.");
        }

    }
}
