using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using TorchSharp;
using static TorchSharp.torch;


namespace GlokusSharp
{
    internal class TorchUtils
    {

        public static torch.Tensor GameNodeToTensor(GameState gs)
        {
            var b1 = torch.from_array(gs.board, device: CPU);
            var b2 = torch.clone(b1);

            b1 = b1 * (b1 == 1);
            b2 = (b2 * (b2 == 2)) / 2;

            b1.unsqueeze_(0);
            b2.unsqueeze_(0);
            b1 = torch.cat(new[] { b1, b2 }, dim: 0);

            if (gs.player_to_move == 0)
            {
                b2 = torch.ones(1, b1.size(1));
            } 
            else
            {
                b2 = torch.zeros(1, b1.size(1));
            }
            b1 = torch.cat(new[] { b1, b2 }, dim: 0);

            b2 = torch.ones(gs.used_minnow.Length, b1.size(1));
            for (int i = 0; i < gs.used_minnow.Length; i++)
            {
                if (gs.used_minnow[i])
                {
                    b2[i].fill_(0);
                }
            }
            b1 = torch.cat(new[] { b1, b2 }, dim: 0);

            return b1.reshape(1,-1, gs.game_config.board_size, gs.game_config.board_size);
        }

        public static torch.Tensor GameNodeToTensorReversed(GameState gs)
        {
            int[] reversed_array = gs.board.Reverse().ToArray();
            var b1 = torch.from_array(reversed_array, device: CPU);
            b1 = torch.flip(b1);
            var b2 = torch.clone(b1);

            b1 = (b1 * (b1 == 2)) / 2;
            b2 = b2 * (b2 == 1);

            b1.unsqueeze_(0);
            b2.unsqueeze_(0);
            b1 = torch.cat(new[] { b1, b2 }, dim: 0);

            if (gs.player_to_move == 1)
            {
                b2 = torch.ones(1, b1.size(1));
            }
            else
            {
                b2 = torch.zeros(1, b1.size(1));
            }
            b1 = torch.cat(new[] { b1, b2 }, dim: 0);

            b2 = torch.ones(gs.used_minnow.Length, b1.size(1));

            bool[] flipped_used_minnow = new bool[gs.used_minnow.Length];
            Array.Copy(gs.used_minnow, 0, flipped_used_minnow, flipped_used_minnow.Length / 2, flipped_used_minnow.Length / 2);
            Array.Copy(gs.used_minnow, flipped_used_minnow.Length / 2, flipped_used_minnow, 0, flipped_used_minnow.Length / 2);
            for (int i = 0; i < flipped_used_minnow.Length; i++)
            {
                if (flipped_used_minnow[i])
                {
                    b2[i].fill_(0);
                }
            }
            b1 = torch.cat(new[] { b1, b2 }, dim: 0);

            return b1.reshape(1, -1, gs.game_config.board_size, gs.game_config.board_size);
        }

        public static (torch.Tensor x, torch.Tensor y) FileToTrainingData(string path, GameConfig gameConfig)
        {
            var xTensors = new List<torch.Tensor>();
            var yTensors = new List<torch.Tensor>();

            int numLines = 0;

            try
            {
                IEnumerable<string> inFile = File.ReadLines(path);

                foreach (string line in inFile)
                {
                    numLines++;
                    string[] values = line.Split(' ');

                    if (values.Length != 2)
                    {
                        Console.WriteLine($"Error in FileToTrainingData parsing line: {line}");
                        continue;
                    }
                    int result = int.Parse(values[0]);

                    string[] moveStructEntries = values[1].Split("|");
                    GameState gs = new(gameConfig);

                    foreach (string moveStructString in moveStructEntries)
                    {

                        if (!gs.AreThereAnyLegalMoves())
                        {
                            gs.togglePlayerNumber();
                        }

                        MoveStruct ms = new(moveStructString);
                        if (!gs.isMoveLegal(ms))
                        {
                            gs._printBoard();

                            Console.WriteLine($"Error in line: {line}, move {moveStructString} is illegal.");
                        }

                        gs.applyMove(ms);
                        xTensors.Add(GameNodeToTensor(gs));
                        yTensors.Add(torch.tensor(result, dtype: ScalarType.Int8).unsqueeze_(0).unsqueeze_(0));
                        xTensors.Add(GameNodeToTensorReversed(gs));
                        yTensors.Add(torch.tensor(-1 * result, dtype: ScalarType.Int8).unsqueeze_(0).unsqueeze_(0));

                    }

                }

            }
            catch (FileNotFoundException)
            {
                Console.WriteLine($"Could not find {path}.");
            }
            catch (IOException e)
            {
                Console.WriteLine($"Error while reading {path}:\n{e.Message}");
            }

            Console.WriteLine($"File: {path} has {numLines} entries and yielded {xTensors.Count()} training samples.");

            return  (torch.cat(xTensors, dim: 0), torch.cat(yTensors)); ;
        }

        public static (torch.Tensor x, torch.Tensor y) FileToTrainingDataSparse(
            string path, 
            GameConfig gameConfig, 
            double frac_to_keep = 0.5, 
            int seed = 1337, 
            bool togglePolarity = false)
        {

            var xTensors = new List<torch.Tensor>();
            var yTensors = new List<torch.Tensor>();

            Random random = new Random(seed);
            double rnd_num = 0;
            int numLines = 0;

            try
            {
                IEnumerable<string> inFile = File.ReadLines(path);

                foreach (string line in inFile)
                {
                    numLines++;
                    string[] values = line.Split(' ');

                    if (values.Length != 2)
                    {
                        Console.WriteLine($"Error in FileToTrainingData parsing line: {line}");
                        continue;
                    }
                    int result = int.Parse(values[0]);

                    string[] moveStructEntries = values[1].Split("|");
                    GameState gs = new(gameConfig);

                    foreach (string moveStructString in moveStructEntries)
                    {

                        if (!gs.AreThereAnyLegalMoves())
                        {
                            gs.togglePlayerNumber();
                        }

                        MoveStruct ms = new(moveStructString);
                        if (!gs.isMoveLegal(ms))
                        {
                            gs._printBoard();

                            Console.WriteLine($"Error in line: {line}, move {moveStructString} is illegal.");
                        }

                        gs.applyMove(ms);
                        
                        rnd_num = random.NextDouble();
                        if ( (!togglePolarity && rnd_num <= frac_to_keep) || (togglePolarity && rnd_num > (1 - frac_to_keep) ) )
                        {
                            xTensors.Add(GameNodeToTensor(gs));
                            yTensors.Add(torch.tensor(result, dtype: ScalarType.Int8).unsqueeze_(0).unsqueeze_(0));
                        }

                        rnd_num = random.NextDouble();
                        if ((!togglePolarity && rnd_num <= frac_to_keep) || (togglePolarity && rnd_num > (1 - frac_to_keep)))
                        {
                            xTensors.Add(GameNodeToTensorReversed(gs));
                            yTensors.Add(torch.tensor(-1 * result, dtype: ScalarType.Int8).unsqueeze_(0).unsqueeze_(0));
                        }

                        /*
                        rnd_num = random.NextDouble();
                        if ((!togglePolarity && rnd_num >= frac_to_keep) || (togglePolarity && rnd_num < (1 - frac_to_keep)))
                        {
                            continue;
                        }

                        xTensors.Add(GameNodeToTensor(gs));
                        yTensors.Add(torch.tensor(result, dtype: ScalarType.Int8).unsqueeze_(0).unsqueeze_(0));
                        xTensors.Add(GameNodeToTensorReversed(gs));
                        yTensors.Add(torch.tensor(-1 * result, dtype: ScalarType.Int8).unsqueeze_(0).unsqueeze_(0));
                                                */
                    }

                }

            }
            catch (FileNotFoundException)
            {
                Console.WriteLine($"Could not find {path}.");
            }
            catch (IOException e)
            {
                Console.WriteLine($"Error while reading {path}:\n{e.Message}");
            }

            Console.WriteLine($"File: {path} has {numLines} entries and yielded {xTensors.Count()} training samples.");

            return (torch.cat(xTensors, dim: 0), torch.cat(yTensors)); ;
        }

    }
}
