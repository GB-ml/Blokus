using GlokusSharp;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using TorchSharp;
using static Tensorboard.CostGraphDef.Types;
using static TorchSharp.torch;

namespace MyApp
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Train8x8();
            // Train14x14();
            //Generate14x14NNData(800);
            // ManualPlayout();

            /*
            for (int i = 0; i < 6; i++)
            {
            
                SmartGeneration();
                Console.WriteLine($" # # {i} # # ");
            }
            */

            // TestGreedyMoveSelector();
        }

        static void TorchTest1()
        {
            MCST mcst = new(new GameConfig(8, new MinnowList(4)));

            mcst.mcst_selector.PopulateAllChildren(mcst.working_node);
            mcst.SelectMove(20); // P1 2x2 brick
            mcst.mcst_selector.PopulateAllChildren(mcst.working_node);
            mcst.SelectMove(20); // P2 2x2 brick
            mcst.mcst_selector.PopulateAllChildren(mcst.working_node);
            mcst.SelectMove(29); // P1 zigzag
            mcst.mcst_selector.PopulateAllChildren(mcst.working_node);

            torch.Tensor dummy = TorchUtils.GameNodeToTensor(mcst.working_node.game_state);

            var device = torch.cuda.is_available() ? torch.CUDA : torch.CPU;
            Console.WriteLine($"Running on: {device}");
            dummy = dummy.to(DeviceType.CUDA);
            Console.WriteLine($"{dummy.str()}");
        }

        static void TorchTest2()
        {
            GameConfig test_14 = new(14, new MinnowList(5));
            test_14.starting_location = new int[] { 4, 4, 9, 9 };
            MCST mcst = new(test_14);
            mcst.num_threads = 6;

            mcst.mcst_selector.PopulateAllChildren(mcst.working_node);
            mcst.SelectMove(362);
            mcst.mcst_selector.PopulateAllChildren(mcst.working_node);
            mcst.SelectMove(288); 
            mcst.mcst_selector.PopulateAllChildren(mcst.working_node);
            mcst.SelectMove(173); 
            mcst.mcst_selector.PopulateAllChildren(mcst.working_node);
            mcst.SelectMove(111);
            mcst.mcst_selector.PopulateAllChildren(mcst.working_node);
            mcst.SelectMove(209);
            mcst.mcst_selector.PopulateAllChildren(mcst.working_node);
            mcst.SelectMove(345);
            mcst.mcst_selector.PopulateAllChildren(mcst.working_node);
            mcst.SelectMove(107);
            mcst.mcst_selector.PopulateAllChildren(mcst.working_node);
            mcst.SelectMove(78);

            torch.Tensor dummy = TorchUtils.GameNodeToTensor(mcst.working_node.game_state);
            Console.WriteLine($"{dummy.str()}");
        }

        static void TorchTest3() 
        {
            //
            // Create dummy resnet CNN, feed it believable data, run forward() once
            // Perfecto
            // 

            torch.Tensor dummy_data;
            MCST mcst = new(new GameConfig(8, new MinnowList(4)));
            mcst.mcst_selector.PopulateAllChildren(mcst.working_node);
            mcst.SelectMove(20); // P1 2x2 brick
            mcst.mcst_selector.PopulateAllChildren(mcst.working_node);
            mcst.SelectMove(20); // P2 2x2 brick
            dummy_data = TorchUtils.GameNodeToTensor(mcst.working_node.game_state);
            mcst.mcst_selector.PopulateAllChildren(mcst.working_node);
            mcst.SelectMove(29); // P1 zigzag
            dummy_data = torch.concat(new [] { dummy_data, TorchUtils.GameNodeToTensor(mcst.working_node.game_state) });
            mcst.mcst_selector.PopulateAllChildren(mcst.working_node);
            mcst.SelectMove(15); // P2 L -> HUUGE mistake
            dummy_data = torch.concat(new[] { dummy_data, TorchUtils.GameNodeToTensor(mcst.working_node.game_state) });
            mcst.mcst_selector.PopulateAllChildren(mcst.working_node);
            mcst.SelectMove(55); // P1 4x1 -> killer blow
            mcst.mcst_selector.PopulateAllChildren(mcst.working_node);
            mcst.SelectMove(17); // P1 something random, but double play for P1
            mcst.mcst_selector.PopulateAllChildren(mcst.working_node);
            dummy_data = torch.concat(new[] { dummy_data, TorchUtils.GameNodeToTensor(mcst.working_node.game_state) });

            var dummy_model = new ResNet1("test", (int)dummy_data.size(1), 512, 3);         
            torch.Tensor dummy_output = dummy_model.forward(dummy_data);
            Console.WriteLine($"{dummy_output.str()}");

        }

        static void TorchTest4()
        {
            //
            // Save two trees to a file
            // Read the files back, and convert them to training data, with the correct flips for P2 and negative scores
            // 

            torch.Tensor dummy_x, dummy_y;
            MCST mcst = new(new GameConfig(8, new MinnowList(4)));
            mcst._SelectMovesBulk([20, 20, 29, 15, 55, 17, 4]); // this is the classic example where P2 loses early so P1 has multiple moves in a row
            mcst.SaveToDiskForNN("test");
            mcst.ResetWorkingTreeButKeepFirstNode();
            mcst._SelectMovesBulk([19, 18, 22, 21, 16, 14, 6, 7, 6, 7]); // just something random idk
            mcst.SaveToDiskForNN("test");
            mcst.ResetWorkingTreeButKeepFirstNode();
            mcst._SelectMovesBulk([16, 17, 24, 23, 18, 19, 11, 4, 2, 0, 9]); // a third one just so we have it
            mcst.SaveToDiskForNN("test");
            mcst.ResetWorkingTreeButKeepFirstNode();

            (dummy_x, dummy_y) = TorchUtils.FileToTrainingData(@"C:\tmp\glokusNN_test.txt", mcst.game_config);

            Console.WriteLine($"{dummy_x[16].str()}");
            Console.WriteLine($"{dummy_x[17].str()}");
            Console.WriteLine($"{dummy_y.str()}");

        }

        static void TorchTest5()
        {
            //
            // Testing local dataloader
            //

            List<torch.Tensor> tempX = new();
            List<torch.Tensor> tempY = new();

            for (int i = 0; i < 7; i++) // 7 entries, so batches of 2 or 3 will always drop one item, no biggie
            {
                tempX.Add(torch.full([2, 2], i).unsqueeze_(0));
                tempY.Add(torch.full([2, 1], i).unsqueeze_(0));
            }
            torch.Tensor fX = torch.concat(tempX).to(CUDA);
            torch.Tensor fY = torch.concat(tempY).to(CUDA);

            LocalDataLoader dl = new(3, fX, fY);

            foreach(var entry in dl.GetShuffled()) // shuffle once
            {
                Console.WriteLine(entry.Item1.str());
                Console.WriteLine(entry.Item2.str());
            }
            foreach (var entry in dl.GetShuffled()) // shuffle a second time
            {
                Console.WriteLine(entry.Item1.str());
                Console.WriteLine(entry.Item2.str());
            }
            foreach (var entry in dl.GetItem()) // no shuffle
            {
                Console.WriteLine(entry.Item1.str());
                Console.WriteLine(entry.Item2.str());
            }

        }

        static void Train8x8()
        {
            MCST mcst = new(new GameConfig(8, new MinnowList(4)));
            Greedy_MoveSelector gms = new(mcst);
            gms.distance_weight = 1.0;
            gms.threshold_level_to_use_piece = [3 * 2, 2 * 2, 0 * 2, 0 * 2, 0 * 2];
            mcst.mcst_selector = gms;

            ResNet1 r0 = new("8x8Test", 3 + 2 * 9, 256, 4);
            Console.WriteLine("Okay to continue? ... ");
            Console.ReadKey(true);

            LocalTrainer trainer = new();

            (trainer.xTrain, trainer.yTrain) = TorchUtils.FileToTrainingDataSparse(@"C:\tmp\glokusNN_8x8smartFull.txt", mcst.game_config, frac_to_keep: 0.96);
            (trainer.xVal, trainer.yVal) = TorchUtils.FileToTrainingDataSparse(@"C:\tmp\glokusNN_8x8smartShort.txt", mcst.game_config, frac_to_keep: 0.34, seed: 314);
            (trainer.xVal2, trainer.yVal2) = TorchUtils.FileToTrainingDataSparse(@"C:\tmp\glokusNN_8x8smartFull324.txt", mcst.game_config, frac_to_keep: 0.038, togglePolarity: true);


            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            trainer.Train(r0);

            stopwatch.Stop();
            TimeSpan ts = stopwatch.Elapsed;
            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            Console.WriteLine($"Time to train {trainer.epochs} epochs: " + elapsedTime);

        }

        static void Generate8x8NNData(int num = 1)
        {

            for (int i = 0; i < num; i++)
            {
                MCST mcst = new(new GameConfig(8, new MinnowList(4)));
                Scenarios.SimpleGeneration(mcst);
                mcst.SaveToDiskForNN("8x8short");

            }

        }

        static void Generate14x14NNData(int num = 1)
        {

            for (int i = 0; i < num; i++)
            {
                MCST mcst = new(new GameConfig(14, new MinnowList(5)));
                mcst.game_config.starting_location = new int[] { 4, 4, 9, 9 };
                Scenarios.SimpleGeneration(mcst);
                mcst.SaveToDiskForNN("14x14_800");

            }

        }

        static void Train14x14()
        {
            MCST mcst = new(new GameConfig(14, new MinnowList(5)));
            mcst.game_config.starting_location = new int[] { 4, 4, 9, 9 };
            LocalTrainer trainer = new();
            // (trainer.xTrain, trainer.yTrain) = TorchUtils.FileToTrainingData(@"C:\tmp\glokusNN_14x14short.txt", mcst.game_config);

            (trainer.xTrain, trainer.yTrain) = TorchUtils.FileToTrainingData(@"C:\tmp\glokusNN_14x14_500.txt", mcst.game_config);
            (trainer.xVal, trainer.yVal) = TorchUtils.FileToTrainingData(@"C:\tmp\glokusNN_14x14val.txt", mcst.game_config);

            ResNet1 v0 = new("14x14Test", 2 + 2 * 21, 128, 3);
            trainer.Train(v0);
            
        }


        static void Realsies1()
        {
            MinnowList minnow_list = new(5);
            GameConfig test_14 = new(14, minnow_list);
            test_14.starting_location = new int[] { 4, 4, 9, 9 };
            MCST mcst = new(test_14);
            mcst.num_threads = 4;

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            mcst.FullyExpandNodeThreaded(mcst.root_node);

            TimeSpan ts = stopwatch.Elapsed;

            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            Console.WriteLine($"RunTime: {elapsedTime} Number of rollouts: {mcst.root_node.visits}");
            mcst.printChildren(mcst.root_node);
            mcst.SaveToDisk("14x14real");

            for (int i = 0; i < mcst.root_node.children.Length; i++)
            {
                stopwatch = new Stopwatch();
                stopwatch.Start();

                mcst.FullyExpandNodeThreaded(mcst.root_node.children[i]);

                ts = stopwatch.Elapsed;
                elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
                Console.WriteLine($"i: {i} RunTime: {elapsedTime} Number of rollouts: {mcst.root_node.visits}");

                if (i > 0 && i % 10 == 0)
                {
                    mcst.SaveToDisk("14x14real");
                    Console.WriteLine("Saved.");
                }

            }
            mcst.RunSearchTreeThreadPool(mcst.root_node, 1);
            mcst.printChildren(mcst.root_node);
            mcst.SaveToDisk("14x14real");
            Console.WriteLine("Saved.");

            for (int i = 0; i < 10; i++)
            {
                stopwatch = new Stopwatch();
                stopwatch.Start();

                mcst.RunSearchTreeThreadPool(mcst.root_node, (int)1E5);
                
                ts = stopwatch.Elapsed;
                elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
                Console.WriteLine($"RunTime: {elapsedTime} Number of rollouts: {mcst.root_node.visits}");
                mcst.SaveToDisk("14x14real");
                Console.WriteLine("Saved.");
            }
            
        }

        static void testFastExpansion()
        {
            // RESULT: We run out of DRAM very quickly, around root child 38-46 when doing 'mcst.ExpandTreeToLevel(mcst.root_node, 2)'
            // Does the garbage collector not run when we have for loops with recursive functions? Kind of mirroring the issue I saw with threadpooling?
            MinnowList minnow_list = new(5);
            GameConfig test_14 = new(14, minnow_list);
            test_14.starting_location = new int[] { 4, 4, 9, 9 };
            MCST mcst = new(test_14);
            mcst.num_threads = 4;

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            mcst.ExpandTreeToLevel(mcst.root_node, 1);
            TimeSpan ts = stopwatch.Elapsed;
            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
            ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            Console.WriteLine("To expand empty tree: " + elapsedTime);

            /*
            stopwatch = new Stopwatch();
            stopwatch.Start();
            mcst.ReadFromFile(@"C:\tmp\glokus_14x14real_.txt");
            ts = stopwatch.Elapsed;
            elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            Console.WriteLine("To Load From Disk with pre-expanded tree: " + elapsedTime);

            
            MCST mcst2 = new(test_14);
            stopwatch = new Stopwatch();
            stopwatch.Start();
            mcst2.ReadFromFile(@"C:\tmp\glokus_14x14real_.txt");
            ts = stopwatch.Elapsed;
            elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            Console.WriteLine("To Load From Disk normally: " + elapsedTime);
            */
        }

        static void SmartGeneration()
        {

            MCST mcst = new(new GameConfig(8, new MinnowList(4)));
            Greedy_MoveSelector gms = new(mcst);
            gms.distance_weight = 1.0;
            gms.threshold_level_to_use_piece = [3 * 2, 2 * 2, 0 * 2, 0 * 2, 0 * 2];
            mcst.mcst_selector = gms;

            /*
            MCST mcst = new(new(14, new MinnowList(5)));
            mcst.game_config.starting_location = [4, 4, 9, 9];
            Greedy_MoveSelector gms = new(mcst);           
            gms.threshold_level_to_use_piece = [3 * 2, 2 * 2, 0 * 2, 0 * 2, 0 * 2];
            gms.distance_weight = 1.3;
            mcst.mcst_selector = gms;
            */

      
            mcst.PopulateWorkingNodeChildren();
            for (int i = 0; i < mcst.working_node.children.Length; i += 1)
            {

                for (int j = 0; j < mcst.working_node.children.Length; j += 1)
                {
                    mcst._SelectMovesBulk([i, j]);
                    Scenarios.SmartGeneration8x8(mcst);
                    mcst.SaveToDiskForNN("8x8smartFull");
                    Console.WriteLine($"Finished {i} {j}");
                    mcst.ResetWorkingTreeButKeepFirstNode();
                }
            }


            /*
            // validate load:
            LocalTrainer trainer = new();
            (trainer.xVal, trainer.yVal) = TorchUtils.FileToTrainingDataSparse(@"C:\tmp\glokusNN_8x8smartShort.txt", mcst.game_config, frac_to_keep: 0.2, togglePolarity: true);
            */
        }

        static void ManualPlayout()
        {
            /*GameConfig test_14 = new(14, new MinnowList(5));
            test_14.starting_location = new int[] { 4, 4, 9, 9 };
            MCST mcst = new(test_14); */
            GameConfig test_8 = new(8, new(4));
            MCST mcst = new(test_8); 
            mcst.num_threads = 6;

            // mcst._SelectMovesBulk([20, 20, 11, 14, 10, 2]); // white to win

            // mcst._SelectMovesBulk([20, 20, 11, 14, 10, 2, 20]); // 20 is a soft move
            // mcst._SelectMovesBulk([20, 20, 11, 14, 10, 2, 20, 6]); // 6 is a great move
            // mcst._SelectMovesBulk([20, 20, 11, 14, 10, 2, 20, 7]); // 7 is a terrible move

            Greedy_MoveSelector gms = new(mcst);
            gms.distance_weight = 0.8;
            gms.threshold_level_to_use_piece = [2 * 2, 2 * 2, 0 * 2, 0 * 2, 0 * 2];
            mcst.mcst_selector = gms;

            while (true)
            {
                mcst.mcst_selector.PopulateAllChildren(mcst.working_node);
                if (mcst.working_node.children.Length == 0)
                {
                    mcst.working_node.game_state.togglePlayerNumber();
                    mcst.mcst_selector.PopulateAllChildren(mcst.working_node);
                }
                if (mcst.working_node.children.Length == 0)
                {
                    break;
                }
                mcst.working_node.game_state._printBoard();

                // mcst.RunSearchTreeThreaded(mcst.working_node, (int)1E0);
                mcst.printChildren(mcst.working_node);
                int inputMove = 0;
                bool validInput = false;

                while (!validInput)
                {
                    Console.WriteLine($"Player [{mcst.working_node.game_state.player_to_move + 1}] to move");
                    Console.WriteLine("Enter a move to apply, or <o> for one rollout, <h> for 100, <t> for 1000 rollouts");
                    Console.WriteLine("<q> to specify 100, <a> to specify 1, <d> for deepdive");
                    string tempInput = Console.ReadLine();

                    if (string.IsNullOrEmpty(tempInput))
                    {
                        continue;
                    }

                    if (Char.IsLetter(tempInput[0]))
                    {
                        if (tempInput[0] == 't')
                        {
                            mcst.RunSearchTreeThreaded(mcst.working_node, (int)1E3);
                            mcst.printChildren(mcst.working_node);
                            continue;
                        }
                        if (tempInput[0] == 'h')
                        {
                            mcst.RunSearchTreeThreaded(mcst.working_node, (int)1E2);
                            mcst.printChildren(mcst.working_node);
                            continue;
                        }
                        if (tempInput[0] == 'o')
                        {
                            mcst.RunSearchTreeThreaded(mcst.working_node, (int)1);
                            mcst.printChildren(mcst.working_node);
                            continue;
                        }
                        if (tempInput[0] == 'q' || tempInput[0] == 'a')
                        {
                            Console.WriteLine("Enter node to search: ");
                            string tempInput2 = Console.ReadLine();

                            if (string.IsNullOrEmpty(tempInput2) || Char.IsLetter(tempInput2[0]))
                            {
                                continue;
                            }

                            int numberOfTrials = (int)1E2;
                            if (tempInput[0] == 'a') numberOfTrials = 1;

                            if (int.TryParse(tempInput2, out inputMove))
                            {
                                if (inputMove >= 0 && inputMove < mcst.working_node.children.Length)
                                {
                                    mcst.RunSearchTreeThreaded(mcst.working_node.children[inputMove], numberOfTrials);
                                    Console.WriteLine("Child node:");
                                    mcst.printChildren(mcst.working_node.children[inputMove]);
                                    Console.WriteLine("Working node:");
                                    mcst.printChildren(mcst.working_node);
                                }
                            }
                            continue;
                        }
                        if (tempInput[0] == 'd')
                        {
                            Console.WriteLine("Enter node to search: ");
                            string tempInput2 = Console.ReadLine();

                            if (string.IsNullOrEmpty(tempInput2) || Char.IsLetter(tempInput2[0]))
                            {
                                continue;
                            }

                            Console.WriteLine("Enter child node to search: ");
                            string tempInput3 = Console.ReadLine();

                            if (string.IsNullOrEmpty(tempInput3) || Char.IsLetter(tempInput3[0]))
                            {
                                continue;
                            }

                            int numberOfTrials = 1;
                            int inputMove1 = 0;
  
                            if (int.TryParse(tempInput2, out inputMove) && int.TryParse(tempInput3, out inputMove1))
                            {
                                if (inputMove >= 0 && inputMove < mcst.working_node.children.Length)
                                {
                                    mcst.RunSearchTreeThreaded(mcst.working_node.children[inputMove].children[inputMove1], numberOfTrials);
                                    Console.WriteLine("Child's child node:");
                                    mcst.printChildren(mcst.working_node.children[inputMove].children[inputMove1]);
                                    Console.WriteLine("Child node:");
                                    mcst.printChildren(mcst.working_node.children[inputMove]);
                                    Console.WriteLine("Working node:");
                                    mcst.printChildren(mcst.working_node);
                                }
                            }
                            continue;
                        }


                    }

                    if (int.TryParse(tempInput, out inputMove))
                    {
                        if (inputMove >= 0 && inputMove < mcst.working_node.children.Length)
                        {
                            mcst.SelectMove(inputMove);
                            validInput = true;
                        }
                    }
                } 

            }

            mcst.working_node.game_state._printBoard();
            Console.WriteLine($"Game over. Final score: {mcst.GetScore(mcst.working_node)}");

        }

        static void testCorrectFirstPlacementLogic()
        {

            MinnowList minnow_list = new(5);
            GameConfig test_14 = new(14, minnow_list);
            test_14.starting_location = new int[] { 4, 4, 9, 9 };
            MCST mcst = new(test_14);

            // MoveStruct[] legal_moves = mcst.root_node.game_state.getLegalMoves();
            ;
        }

        static void testThreaded()
        {
            MinnowList minnow_list = new(4);
            GameConfig test_8 = new(8, minnow_list);
            MCST mcst = new(test_8);

            // QueueUpLeaves is not  optimized, use RunSearchTreeThreadPool() instead in real situations
            mcst.QueueUpLeaves(27); // there are only 20 valid spots in this specific board
            mcst.RolloutQueuedLeavesThreaded();
            mcst.runOneSearch(mcst.root_node); // to calculate UCT
            mcst.printChildren(mcst.root_node);

        }
        static void testThreaded2()
        {
            MinnowList minnow_list = new(4);
            GameConfig test_8 = new(8, minnow_list);
            MCST mcst = new(test_8);

            mcst.FullyExpandNodeAndChildrenThreaded(mcst.root_node);
            mcst.RunSearchTreeThreaded(mcst.root_node, 20000); // there are only 20 valid spots in this specific board
            mcst.printChildren(mcst.root_node);
            mcst.printChildren(mcst.root_node.children[17]);

        }

        static void testThreadedExpandRootAndChildren()
        {
            MinnowList minnow_list = new(4);
            GameConfig test_8 = new(8, minnow_list);
            MCST mcst = new(test_8);

            Stopwatch stopwatch = new Stopwatch();

            stopwatch.Start();

            mcst.FullyExpandNodeAndChildrenThreaded(mcst.root_node);
            for (int i = 0; i < mcst.root_node.children.Length; i++)
            {
                mcst.fullyExpandNodeAndChildren(mcst.root_node.children[i]);
            }

            TimeSpan ts = stopwatch.Elapsed;

            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            Console.WriteLine("RunTime: " + elapsedTime);

            mcst.runOneSearch(mcst.root_node); // to calculate UCT
            mcst.printChildren(mcst.root_node);
            mcst.printChildren(mcst.root_node.children[17]);
        }

        static void testThreadedvsPooled()
        {
            MinnowList minnow_list = new(4);
            GameConfig test_8 = new(8, minnow_list);
            MCST mcst = new(test_8);

            Stopwatch stopwatch = new Stopwatch();

            mcst.FullyExpandNodeAndChildrenThreaded(mcst.root_node);
            for (int i = 0; i < mcst.root_node.children.Length; i++)
            {
                mcst.fullyExpandNodeAndChildren(mcst.root_node.children[i]);
            }

            stopwatch.Start();

            mcst.num_threads = 6;
            mcst.RunSearchTreeThreadPool(mcst.root_node, (int)1E5);

            /* For 1E5 iterations on i7-7700k 4 Core (8 logical processors)
             * Thread or Pool   num_threads     time1   time2   time3
             * Pool             2               13.53   14.00   13.55
             * Pool             4               8.92    9.29    9.68    9.23
             * Pool             6               8.83    8.22    7.80    7.88   No hangs as far as I can remember
             * Pool             7               7.62    7.85    7.33            This one hung twice while measuring
             * Pool             8               7.23    6.79    6.57    6.68    Finally hung on 5th trial
             * Pool             12              6.92    Would hang repeatedly ...
             * Pool no wait     6               Fails to run every iteration ...
             * 
             * Thread           2               20.83   19.91   20.58
             * Thread           6               14.06   13.93   13.97
             * Thread           12              12.80   12.08   12.34
             * Thread           24              11.39   11.50   11.64
             */

            TimeSpan ts = stopwatch.Elapsed;

            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            Console.WriteLine("RunTime: " + elapsedTime);

            mcst.printChildren(mcst.root_node);
        }

        static void testLargerGridThreaded()
        {
            MinnowList minnow_list = new(5);
            GameConfig test_14 = new(14, minnow_list);
            test_14.starting_location = new int[] { 4, 4, 9, 9 };
            MCST mcst = new(test_14);
            mcst.num_threads = 4;

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            mcst.FullyExpandNodeAndChildrenThreaded(mcst.root_node);
            /*for (int i = 0; i < mcst.root_node.children.Length; i++)
            {
                mcst.fullyExpandNodeAndChildren(mcst.root_node.children[i]);
            }*/
            mcst.RunSearchTreeThreadPool(mcst.root_node, (int)1E4);

            TimeSpan ts = stopwatch.Elapsed;

            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            Console.WriteLine("RunTime: " + elapsedTime);

            mcst.printChildren(mcst.root_node);
            /* With pool, 6 threads, 95790 visits, takes 10:24.83 to run, but it did not seem to be correctly threaded?
             * But moving the 2nd call to RolloutQueuedLeavesThreadPool() in FullyExpandNodeAndChildrenThreaded() to outside the j for loop fixes this!
             *  -> So there are weird circumstances where you are super nested that the software won't start separate threads for you
             * 
             * UPDATE 1/15/26 after all the code changes: 4 threads 181810 visits in 6:52.48 -> 440 visits per second
             */
        }

        static void setupKnownEndState()
        {

            MCST mcst = new(new GameConfig(8, new MinnowList(4)));
            mcst.num_threads = 6;

            mcst.mcst_selector.PopulateAllChildren(mcst.working_node);
            mcst.SelectMove(20); // P1 2x2 brick
            mcst.mcst_selector.PopulateAllChildren(mcst.working_node);
            mcst.SelectMove(20); // P2 2x2 brick
            mcst.mcst_selector.PopulateAllChildren(mcst.working_node);
            // mcst.SelectMove(29); // P1 zigzag
            // mcst.working_node.PopulateAllChildren();    

            mcst.FullyExpandNodeThreadPool(mcst.working_node);
            for (int i = 0; i < 1; i++)
                mcst.RunSearchTreeThreaded(mcst.working_node, (int)5E4);

            // mcst.SelectMove(15); // P2 L -> HUUGE mistake
            // mcst.working_node.PopulateAllChildren();
            mcst.working_node.game_state._printBoard();




            /*
            mcst.visitEachChild_nTimes(mcst.working_node, 67);
            mcst.runSearchTree_nTimes(mcst.working_node, (int)1E4);
            */
            /*
            mcst.SelectMove(55); // P1 4x1 -> killer blow
            mcst.working_node.PopulateAllChildren();
            */

            mcst.printChildren(mcst.working_node);
            mcst.mcst_selector.PopulateAllChildren(mcst.working_node.children[29]);
            mcst.printChildren(mcst.working_node.children[29]);
            //mcst.printChildren(mcst.working_node);

            



        }

        static void setupKnownEndState2()
        {
            MCST mcst = new(new GameConfig(8, new MinnowList(4)))
                ;
            mcst._SelectMovesBulk([20, 20, 11, 14, 10, 2]);
            // at this point P2 made a weak move with #2 and there are several potential forcing moves
            // however, this response from P1:
            mcst.mcst_selector.PopulateAllChildren(mcst.working_node);
            mcst.SelectMove(20);
            // is weak and gives P2 a chance to come back
            mcst.mcst_selector.PopulateAllChildren(mcst.working_node);
            // so comment out the following line and it should be forcing eventually:
            mcst.SelectMove(6); // 6 is terminally winning for P2, while 7 is troll gives P1 the game

            mcst.working_node.game_state._printBoard();

            // TODO: write code that lets you know if a position is terminally winning or losing:
            // make the min/max score the score of that node, since there is no ambiguity
            // remove it from UCT considerations, to let the program see if other positions are better or worse
            // And then when it comes to choose, make sure the program doesn't go by visit counts, rather scores, if there are terminal positions


        }

        static void testFileSaving()
        {
            /*MinnowList minnow_list = new(5);
            GameConfig test_14 = new(14, minnow_list);
            test_14.starting_location = new int[] { 4, 4, 9, 9 };
            MCST mcst = new(test_14); */

            MinnowList minnow_list = new(4);
            GameConfig test_8 = new(8, minnow_list);
            MCST mcst = new(test_8);
            mcst.num_threads = 2;

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            mcst.FullyExpandNodeAndChildrenThreaded(mcst.root_node);
            /*for (int i = 0; i < mcst.root_node.children.Length; i++)
            {
                mcst.fullyExpandNodeAndChildren(mcst.root_node.children[i]);
            }*/
            mcst.RunSearchTreeThreadPool(mcst.root_node, (int)4E2);

            TimeSpan ts = stopwatch.Elapsed;

            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            Console.WriteLine("RunTime: " + elapsedTime);

            mcst.printChildren(mcst.root_node);

            mcst.SaveToDisk("test01");

            MCST mcst2 = new(test_8);
            mcst2.ReadFromFile(@"C:\tmp\glokus_test01_.txt");
            mcst2.printChildren(mcst2.root_node);
        }

        static void timeFileSaving()
        {
            MinnowList minnow_list = new(5);
            GameConfig test_14 = new(14, minnow_list);
            test_14.starting_location = new int[] { 4, 4, 9, 9 };
            MCST mcst = new(test_14);
            mcst.num_threads = 4;

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            mcst.FullyExpandNodeAndChildrenThreaded(mcst.root_node);
            /*for (int i = 0; i < mcst.root_node.children.Length; i++)
            {
                mcst.fullyExpandNodeAndChildren(mcst.root_node.children[i]);
            }*/
            mcst.RunSearchTreeThreadPool(mcst.root_node, (int)1E5);

            TimeSpan ts = stopwatch.Elapsed;

            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            Console.WriteLine("To Generate: " + elapsedTime);

            stopwatch = new Stopwatch();
            stopwatch.Start();
            mcst.SaveToDisk("14x14");
            ts = stopwatch.Elapsed;
            elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            Console.WriteLine("To Save to Disk: " + elapsedTime);

            MCST mcst2 = new(test_14);
            stopwatch = new Stopwatch();
            stopwatch.Start();
            mcst2.ReadFromFile(@"C:\tmp\glokus_14x14_.txt");
            ts = stopwatch.Elapsed;
            elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            Console.WriteLine("To Load From Disk: " + elapsedTime);

            /*  For 195790 entries with 4 threads:
             *      8:04.91 to generate
             *      0.46 seconds to write to disk
             *      49.56 seconds to read back from disk and populate
             *          -> We likely could shave off a lot of time by multithreading a tree "touch" procedure since populating children is so slow
             */
        }

        static void test1()
        {
            /*
            MinnowList minnow_list = new(4);
            GameConfig test_8 = new(8, minnow_list);
            GameState test_state = new(test_8);

            int[] should_be_zero_zero = test_state.getValidOpenSpots();
            MoveStruct[] should_be_several = test_state.getLegalMoves();
            test_state.applyMove(should_be_several[0]);

            test_state.togglePlayerNumber(); // back to player 1 for testing
            int[] should_be_one_one = test_state.getValidOpenSpots();
            MoveStruct[] should_be_a_ton = test_state.getLegalMoves();

            test_state._printBoard();

            test_state.applyMove(should_be_a_ton[31]);

            Console.WriteLine("\n\n\n");
            test_state._printBoard();

            test_state.togglePlayerNumber(); // back to player 1 for testing
            int[] too_many_now = test_state.getValidOpenSpots();
            Console.WriteLine("\n\n\n");
            */
        }
    
        static void test2()
        {
            /*
            MinnowList minnow_list = new(4);
            GameConfig test_8 = new(8, minnow_list);
            GameState test_state = new(test_8);
            MoveStruct[] possible_moves = test_state.getLegalMoves();
            test_state.applyMove(possible_moves[test_state.pickMoveAtRandomWeightedSquared(possible_moves)]);

            possible_moves = test_state.getLegalMoves();
            test_state.applyMove(possible_moves[test_state.pickMoveAtRandomWeightedSquared(possible_moves)]);

            possible_moves = test_state.getLegalMoves();
            test_state.applyMove(possible_moves[test_state.pickMoveAtRandomWeightedSquared(possible_moves)]);
            test_state._printBoard();
            */
        }

        static void test3()
        {
            MinnowList minnow_list = new(4);
            GameConfig test_8 = new(8, minnow_list);
            MCST mcst = new(test_8);

            for (int i = 0; i < 10000; i++)
            {
                mcst.runOneSearch(mcst.root_node);
            }
            mcst.printChildren(mcst.root_node);
        }

        static void test4()
        {
            MinnowList minnow_list = new(4);
            GameConfig test_8 = new(8, minnow_list);
            MCST mcst = new(test_8);

            mcst.fullyExpandNodeAndChildren(mcst.root_node);
            mcst.runOneSearch(mcst.root_node); // to calculate UCT
            mcst.printChildren(mcst.root_node);
            mcst.printChildren(mcst.root_node.children[7]);

        }

        static void test5()
        {
            MinnowList minnow_list = new(4);
            GameConfig test_8 = new(8, minnow_list);
            MCST mcst = new(test_8);

            Stopwatch stopwatch = new Stopwatch();

            stopwatch.Start();
            mcst.fullyExpandNodeAndChildren(mcst.root_node);
            mcst.visitEachChild_nTimes(mcst.root_node, 454);
            mcst.runOneSearch(mcst.root_node); // to calculate UCT
            stopwatch.Stop();

            TimeSpan ts = stopwatch.Elapsed;

            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            Console.WriteLine("RunTime: " + elapsedTime); // 15 million in an hour?

            mcst.printChildren(mcst.root_node);
            mcst.printChildren(mcst.root_node.children[17]);
        }

        static void test6()
        {
            MinnowList minnow_list = new(5);
            GameConfig test_14 = new(14, minnow_list);
            test_14.starting_location = new int[]{ 4, 4, 9, 9};
            MCST mcst = new(test_14);

            Stopwatch stopwatch = new Stopwatch();

            stopwatch.Start();
            mcst.fullyExpandNodeAndChildren(mcst.root_node);
            mcst.visitEachChild_nTimes(mcst.root_node, 1);
            mcst.runOneSearch(mcst.root_node); // to calculate UCT
            stopwatch.Stop();

            TimeSpan ts = stopwatch.Elapsed;

            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            Console.WriteLine("RunTime: " + elapsedTime); 

            // 96100 visits in 10:13.26, 311 visits each; 564k/hr
            // 234841 visits in 25:50.84, 760 visits each; 544k/hr

            mcst.printChildren(mcst.root_node);
        }

        static void testPythonPPTpg10()
        {
            MinnowList minnow_list = new(4);
            GameConfig test_8 = new(8, minnow_list);
            MCST mcst = new(test_8);

            Stopwatch stopwatch = new Stopwatch();

            stopwatch.Start();
            mcst.fullyExpandNodeAndChildren(mcst.root_node);
            for (int i = 0; i < mcst.root_node.children.Length; i++)
            {
                mcst.fullyExpandNodeAndChildren(mcst.root_node.children[i]);
            }
            mcst.runSearchTree_nTimes(mcst.root_node.children[0], 5000);
            mcst.runSearchTree_nTimes(mcst.root_node.children[16], 30000);
            mcst.runSearchTree_nTimes(mcst.root_node.children[18], 30000);
            mcst.runSearchTree_nTimes(mcst.root_node.children[20], 30000);
            mcst.runSearchTree_nTimes(mcst.root_node, (int)1E5);

            stopwatch.Stop();

            TimeSpan ts = stopwatch.Elapsed;

            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            Console.WriteLine("RunTime: " + elapsedTime);

            // 123585 iterations in 30.28 minutes
            // 213585 iterations in 52.18 minutes -> 13 times faster than Python!

            mcst.printChildren(mcst.root_node);
        }

        static void countLayers()
        {
            // note this won't work unless PopulateMoves() in GameStateNode has key lines commented out

            // MCST mcst = new(new GameConfig(8, new MinnowList(4)));
            /*GameConfig test_14 = new(14, new MinnowList(5));
            test_14.starting_location = new int[] { 4, 4, 9, 9 };
            MCST mcst = new(test_14); */
            MCST mcst = new(new GameConfig(12, new MinnowList(4, "FILTUVX")));
            mcst.game_config.starting_location = new int[] { 3, 3, 8, 8 };

            mcst.mcst_selector.PopulateMoves(mcst.working_node);
            Console.WriteLine($"Possible first moves: {mcst.working_node.possible_moves.Length}");

            mcst.mcst_selector.PopulateAllChildren(mcst.working_node);
            int count1 = 0;
            int count2 = 0;
            for (int i = 0; i < mcst.working_node.children.Length; i++)
            {
                mcst.working_node.children[i].game_state.togglePlayerNumber();
                mcst.mcst_selector.PopulateAllChildren(mcst.working_node.children[i]);
                count1 += mcst.working_node.children[i].children.Length;

                for (int j = 0; j < mcst.working_node.children[i].children.Length; j++)
                {
                    mcst.working_node.children[i].children[j].game_state.togglePlayerNumber();
                    mcst.mcst_selector.PopulateMoves(mcst.working_node.children[i].children[j]);
                    count2 += mcst.working_node.children[i].children[j].children.Length;
                }

            }

            Console.WriteLine($"Possible second moves: {count1}");
            Console.WriteLine($"Possible third moves: {count2}");

        }

        static void TestOtherSizesAndPartial5thRankPieces()
        {
            MCST mcst = new(new GameConfig(9, new MinnowList(4, "WT")));
            mcst.mcst_selector.PopulateAllChildren(mcst.working_node);
            mcst.printChildren(mcst.working_node);
            mcst.SelectMove(21);            
            mcst.working_node.game_state._printBoard();
        }

        static void TestGreedyMoveSelector()
        {
            MCST mcst = new(new GameConfig(8, new MinnowList(4)));
            Greedy_MoveSelector gms = new(mcst);
            gms.distance_weight = 2.4;
            gms.threshold_level_to_use_piece = [4 * 2, 4 * 2, 4 * 2, 0 * 2, 0 * 2];
            mcst.mcst_selector = gms;

            mcst.PopulateWorkingNodeChildren();
            mcst.printChildren(mcst.working_node);
        }


    }

}
