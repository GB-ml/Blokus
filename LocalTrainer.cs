using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TorchSharp;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;
using static TorchSharp.torch.utils.data;

namespace GlokusSharp
{
     internal class LocalTrainer
    {

        public int batchSize = 24;
        public int epochs = 10;
        public double learningRate = 0.001;
        // I can set torch.Tensor to be null but trying to check if it is null triggers error CS0128
        // My naive reading is that my torchsharp version did not correctly overload the == operator or something?
        public torch.Tensor xTrain, yTrain, xVal, yVal, xVal2, yVal2;

        public LocalTrainer()
        {
            xTrain = torch.empty(0);
            yTrain = torch.empty(0);
            xVal = torch.empty(0);
            yVal = torch.empty(0);
            xVal2 = torch.empty(0);
            yVal2 = torch.empty(0);
        }

        public void Train(Module<torch.Tensor, torch.Tensor> model)
        {

            var device = torch.cuda.is_available() ? torch.CUDA : torch.CPU;
            Console.WriteLine($"Running on: {device.type}");

            if (xTrain.numel() == 0 || yTrain.numel() == 0)
            {
                Console.WriteLine("No training data found");
                return;
            }
            xTrain = xTrain.cuda();
            yTrain = nn.functional.tanh(yTrain);
            yTrain = yTrain.cuda();
            LocalDataLoader training_data = new(batchSize, xTrain, yTrain);

            LocalDataLoader? val_data = null;
            if (xVal.numel() > 0 && yVal.numel() > 0)
            {
                xVal = xVal.cuda();
                yVal = nn.functional.tanh(yVal);
                yVal = yVal.cuda();
                val_data = new(batchSize, xVal, yVal);
            } 
            else
            {
                Console.WriteLine("No validation data found, skipping");
            }

            LocalDataLoader? val_data2 = null;
            if (xVal2.numel() > 0 && yVal2.numel() > 0)
            {
                xVal2 = xVal2.cuda();
                yVal2 = nn.functional.tanh(yVal2);
                yVal2 = yVal2.cuda();
                val_data2 = new(batchSize, xVal2, yVal2);
            }
            else
            {
                Console.WriteLine("No 2nd validation data found, skipping");
            }

            model.to(device);
            using var optimizer = torch.optim.AdamW(model.parameters(), learningRate, weight_decay: 3E-4);
            var lossFunc = MSELoss();

            Console.WriteLine($"x-type: {xTrain.dtype}, y-type: {yTrain.dtype}, model: {model.parameters().First().dtype}");

            for (int epoch = 0; epoch < epochs; epoch++)
            {
                model.train();
                double trainLossSum = 0;
                long trainBatches = 0;

                foreach (var batch in training_data.GetShuffled())
                {
                    using var data = batch.Item1;
                    using var target = batch.Item2;

                    using var prediction = model.forward(data);
                    using var loss = lossFunc.forward(prediction, target);

                    optimizer.zero_grad();
                    loss.backward();
                    optimizer.step();

                    trainLossSum += loss.ToSingle();
                    trainBatches++;
                }

                double avgTrainLoss = trainLossSum / trainBatches;
                double avgValLoss = -999;

                if (val_data != null)
                {
                    model.eval();
                    double valLossSum = 0;
                    long valBatches = 0;

                    using (torch.no_grad())
                    {
                        foreach (var batch in val_data.GetItem())
                        {
                            using var data = batch.Item1;
                            using var target = batch.Item2;

                            using var prediction = model.forward(data);
                            using var loss = lossFunc.forward(prediction, target);

                            valLossSum += loss.ToSingle();
                            valBatches++;
                        }
                    }
                    avgValLoss = valLossSum / valBatches;
                }

                double avgValLoss2 = -999;
                if (val_data2 != null)
                {
                    model.eval();
                    double valLossSum = 0;
                    long valBatches = 0;

                    using (torch.no_grad())
                    {
                        foreach (var batch in val_data2.GetItem())
                        {
                            using var data = batch.Item1;
                            using var target = batch.Item2;

                            using var prediction = model.forward(data);
                            using var loss = lossFunc.forward(prediction, target);

                            valLossSum += loss.ToSingle();
                            valBatches++;
                        }
                    }
                    avgValLoss2 = valLossSum / valBatches;
                }

                Console.WriteLine($"Epoch {epoch} | Train Loss: {avgTrainLoss:F4} | Val Loss: {avgValLoss:F4} | Val2 Loss {avgValLoss2:F4}");
            }

            /*
            string savePath = "cnn_model_weights.dat";
            Console.WriteLine($"\nSaving model to {savePath}...");
            model.save(savePath);
            */

        }
    }

    public class LocalDataLoader
    {
        torch.Tensor featuresX;
        torch.Tensor targetsY;
        int batch_size;
        int num_entries;
        int num_batches;

        public LocalDataLoader(int batch_size, torch.Tensor features, torch.Tensor targets)
        {
            this.batch_size = batch_size;
            featuresX = features;
            targetsY = targets;
            num_entries = (int)featuresX.size(0);
            num_batches = num_entries / this.batch_size;
        }

        public IEnumerable<(torch.Tensor, torch.Tensor)> GetShuffled()
        {
            
            int[] numberSequence = Enumerable.Range(0, num_entries).ToArray();
            Random.Shared.Shuffle(numberSequence);

            /*
            foreach (int num in numberSequence) {
                Console.Write($"{num} ");
            } */
            
            for(int i = 0; i < num_batches; i++)
            {
                yield return (featuresX[numberSequence[(i * batch_size) .. (i * batch_size + batch_size)]],
                    targetsY[numberSequence[(i * batch_size)..(i * batch_size + batch_size)]]);
            }

        }
        public IEnumerable<(torch.Tensor, torch.Tensor)> GetItem()
        {

            for (int i = 0; i < num_batches; i++)
            {
                yield return (featuresX[(i * batch_size)..(i * batch_size + batch_size)],
                    targetsY[(i * batch_size)..(i * batch_size + batch_size)]);
            }

        }

    }
}
