using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;
using nn = TorchSharp.torch.nn;
using torch = TorchSharp.torch;

namespace GlokusSharp
{
    public class ResBlock : nn.Module<torch.Tensor, torch.Tensor>
    {

        private readonly nn.Module<torch.Tensor, torch.Tensor> _layers;
        private readonly nn.Module<torch.Tensor, torch.Tensor> _shortcut;

        public ResBlock(string name, int in_channels, int out_channels) : base(name)
        {
            _layers = nn.Sequential(
                nn.Conv2d(in_channels, out_channels, kernel_size: 3, stride: 1, padding: 1, bias: false),
                nn.BatchNorm2d(out_channels),
                nn.ReLU(),
                nn.Conv2d(out_channels, out_channels, kernel_size: 3, stride: 1, padding: 1, bias: false),
                nn.BatchNorm2d(out_channels)
                );


            if (in_channels != out_channels)
            {
                _shortcut = nn.Sequential(
                    nn.Conv2d(in_channels, out_channels, kernel_size: 1, stride: 1, padding: 1, bias: false),
                    nn.BatchNorm2d(out_channels)
                    );
            }
            else
            {
                _shortcut = nn.Sequential();
            }

            RegisterComponents();
        } 

        public override torch.Tensor forward(torch.Tensor x)
        {
            var y = _layers.forward(x);
            var shortcut = _shortcut.forward(x);

            // return nn.functional.relu(y) + shortcut; // post activation
            return nn.functional.relu(y + shortcut); // pre-activation

        }
    }

    public class ResNet1 : nn.Module<torch.Tensor, torch.Tensor>
    {

        private readonly nn.Module<torch.Tensor, torch.Tensor> _initialLayer;
        private readonly nn.Module<torch.Tensor, torch.Tensor> _blocks;
        private readonly nn.Module<torch.Tensor, torch.Tensor> _finalLayer;

        public ResNet1(string name, int in_channels, int body_channels, int num_blocks) : base(name)
        {
            _initialLayer = nn.Sequential(
                nn.Conv2d(in_channels, body_channels, kernel_size: 3, stride: 1, padding: 1, bias: false),
                nn.BatchNorm2d(body_channels),
                nn.ReLU()
                );

            var _tempBlocks = new List<(string, nn.Module<torch.Tensor, torch.Tensor>)>();
            for (int i = 0; i < num_blocks; i++)
            {
                _tempBlocks.Add(($"block{i}", new ResBlock($"block{i}", body_channels, body_channels) ));
            }

            _blocks = nn.Sequential(_tempBlocks.ToArray());

            _finalLayer = nn.Sequential(
                nn.AdaptiveAvgPool2d(1),
                nn.Flatten(),
                nn.Linear(body_channels, 1)
                );

            RegisterComponents();
            Console.WriteLine("ResNet1 param count (thousands): " + this.parameters().Where(p => p.requires_grad).Sum(p => p.numel())/1000);
        }

        public override torch.Tensor forward(torch.Tensor x)
        {
            x = _initialLayer.forward(x);
            x = _blocks.forward(x);
            x = _finalLayer.forward(x);
            return nn.functional.tanh(x);

        }
    
    }

        /*
    public void test()
    {
        var device = torch.cuda.is_available() ? torch.CUDA : torch.CPU;
        Console.WriteLine($"Running on: {device}");

        var model = nn.Sequential(
            nn.Linear(11, 16),
            nn.GELU(),
            nn.Linear(16, 8),
            nn.GELU(),
            nn.Linear(8, 1)
        );

        model.to(device);
        model.train();

        var optimizer = torch.optim.SGD(model.parameters(), learningRate: 0.03);
        var mseLoss = nn.MSELoss();

        var inputs = torch.randn(8, 11).to(device);
        var targets = torch.randn(8, 1).to(device);

        Console.WriteLine("\n--- Starting Training Loop ---");

        // 4. Dummy Training Loop
        for (int epoch = 1; epoch <= 10; epoch++)
        {

            // Zero the gradients before the backward pass
            optimizer.zero_grad();

            // Forward pass
            var outputs = model.forward(inputs);
            var loss = mseLoss.forward(outputs, targets);

            // Backward pass and optimize
            loss.backward();
            optimizer.step();

            Console.WriteLine($"Epoch: {epoch} | Loss: {loss.item<float>():F4}");
        }

        Console.WriteLine("--- Training Complete ---");
        Console.WriteLine("\nTest passed successfully!");
    } */
    
}

