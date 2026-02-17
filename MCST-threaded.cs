using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GlokusSharp
{
    internal partial class MCST
    {

        public void TraverseThreaded(GameStateNode node)
        {
            if (node.possible_moves == null)
            {
                mcst_selector.PopulateMoves(node);
            }

            if (node.possible_moves.Length == 0)
            {
                // TODO: properly handle case where there are no legal moves left for one player, and then the second
                return;
            }

            if (node.children == null)
            {
                node.children = new GameStateNode[node.possible_moves.Length];
            }

            if (!node.fully_expanded)
            {
                int number_unvisited = 0;
                for (int i = 0; i < node.children.Length; i++)
                {
                    if (node.children[i] == null)
                    {
                        number_unvisited++;
                        continue;
                    }
                    if (node.children[i].IsUnvisited()) number_unvisited++;
                }
                if (number_unvisited == 0)
                {
                    node.fully_expanded = true;
                }
                else
                {
                    int random_index = this.game_config.random.Next(0, number_unvisited);
                    int current_index = 0;
                    for (int i = 0; i < node.children.Length; i++)
                    {
                        if (node.children[i] == null)
                        {
                            if (current_index == random_index)
                            {
                                current_index = i;
                                node.CreateChild(i);
                                break;
                            }
                            current_index++;
                        }
                        if (node.children[i].IsUnvisited())
                        {
                            if (current_index == random_index)
                            {
                                current_index = i;
                                break;
                            }
                            current_index++;
                        }
                    }
                    this.leavesToRollOut.Enqueue(node.children[current_index]);
                    return;
                }

            }

            GetChildbyUCTThreaded(node);
        }

        public int TraverseThreaded(GameStateNode node, int n)
        {
            int numberQueued = 0;

            if (node.possible_moves == null)
            {
                mcst_selector.PopulateMoves(node);
            }
            if (node.possible_moves.Length == 0)
            {
                return numberQueued;
            }
            if (node.children == null)
            {
                node.children = new GameStateNode[node.possible_moves.Length];
            }

            if (!node.fully_expanded)
            {
                int number_unvisited = 0;
                bool[] unvisited = new bool[node.children.Length]; // defaults to 0 == false
                for (int i = 0; i < node.children.Length; i++)
                {
                    if (node.children[i] == null)
                    {
                        number_unvisited++;
                        unvisited[i] = true;
                        continue;
                    }
                    if (node.children[i].IsUnvisited())
                    {
                        number_unvisited++;
                        unvisited[i] = true;
                    }
                }
                if (number_unvisited == 0)
                {
                    node.fully_expanded = true;
                }
                else
                {
                    if (number_unvisited <= n)
                    {
                        // all should enqueued
                        numberQueued += number_unvisited;
                        n -= number_unvisited;
                        for (int i = 0; i < node.children.Length; i++)
                        {
                            if (unvisited[i])
                            {
                                if (node.children[i] == null) node.CreateChild(i);
                                this.leavesToRollOut.Enqueue(node.children[i]);
                            }
                        }
                    }
                    else
                    {
                        /* Pseudorandomly and quickly enqueue n leaves:
                         *  Make an array of all the unvisited indexes
                         *  Randomly remove some indices by setting their value to -1
                         *  Enqueue the remaining indices
                         */
                        int[] unvisitedIndexes = new int[number_unvisited];
                        int ii = 0;
                        for (int i = 0; i < node.children.Length; i++)
                        {

                            if (node.children[i] == null)
                            {
                                unvisitedIndexes[ii] = i;
                                ii++;
                                continue;
                            }
                            if (unvisited[i])
                            {
                                unvisitedIndexes[ii] = i;
                                ii++;
                            }
                        }

                        int removedFromQueue = 0;
                        ii = -1;
                        while (removedFromQueue < number_unvisited - n)
                        {
                            ii++;
                            if (ii >= number_unvisited) ii = 0;

                            if (unvisitedIndexes[ii] == -1) continue;

                            if (0.33 < this.game_config.random.NextDouble())
                            {
                                unvisitedIndexes[ii] = -1;
                                removedFromQueue++;
                            }
                        }

                        for (int i = 0; i < number_unvisited; i++)
                        {
                            if (unvisitedIndexes[i] > -1)
                            {
                                if (node.children[unvisitedIndexes[i]] == null) node.CreateChild(unvisitedIndexes[i]);
                                this.leavesToRollOut.Enqueue(node.children[unvisitedIndexes[i]]);
                            }
                        }

                        numberQueued += n;
                        n = 0;
                        return numberQueued;
                    }
                }

            }
            if (n > 0)
            {
                numberQueued += GetChildbyUCTThreaded(node, n);
            }

            return numberQueued;
        }

        public int GetChildbyUCTThreaded(GameStateNode node, int n)
        {

            // UCT equation from https://int8.io/monte-carlo-tree-search-beginners-guide/#upper-confidence-bound-applied-to-trees

            double c1 = 1.0;
            if (node.game_state.player_to_move == 1)
                c1 = -1.0;

            double uct_const = this.exploration_constant * Math.Sqrt(Math.Log(node.visits));
            double _1st_largest = Double.NegativeInfinity;
            double _2nd_largest = Double.NegativeInfinity;
            int idx_1st = 0;
            int idx_2nd = 0;

            for (int i = 0; i < node.children.Length; i++)
            {
                if (node.children[i].is_terminal_or_forcing) continue;

                double currentUCT = node.children[i].UpdateUCT(c1, uct_const);
                if (currentUCT > _1st_largest)
                {
                    _2nd_largest = _1st_largest;
                    idx_2nd = idx_1st;

                    _1st_largest = currentUCT;
                    idx_1st = i;
                }
                else if (currentUCT > _2nd_largest)
                {
                    _2nd_largest = currentUCT;
                    idx_2nd = i;
                }
            }

            // todo: replace the 4 with like max( log(this.visits), 3) or something
            int n2 = n / 4;
            int n1 = n - n2;

            int numberQueued = TraverseThreaded(node.children[idx_1st], n1);
            if (n2 > 0)
            {
                numberQueued += TraverseThreaded(node.children[idx_2nd], n2);
            }

            return numberQueued;
        }

        public void GetChildbyUCTThreaded(GameStateNode node)
        {

            // UCT equation from https://int8.io/monte-carlo-tree-search-beginners-guide/#upper-confidence-bound-applied-to-trees

            double c1 = 1.0;
            if (node.game_state.player_to_move == 1)
                c1 = -1.0;

            double uct_const = this.exploration_constant * Math.Sqrt(Math.Log(node.visits));
            double largest_so_far = Double.NegativeInfinity;
            int index_of_largest = 0;

            for (int i = 0; i < node.children.Length; i++)
            {
                if (node.children[i].is_terminal_or_forcing) continue;
                double currentUCT = node.children[i].UpdateUCT(c1, uct_const);
                if (currentUCT > largest_so_far)
                {
                    largest_so_far = currentUCT;
                    index_of_largest = i;
                }
            }

            TraverseThreaded(node.children[index_of_largest]);
        }

        public void RolloutQueuedLeavesThreaded()
        {

            Thread[] threads = new Thread[this.num_threads];

            while (!this.leavesToRollOut.IsEmpty)
            {

                for (int i = 0; i < threads.Length; i++)
                {

                    if (this.leavesToRollOut.TryDequeue(out GameStateNode? nextLeaf))
                    {
                        threads[i] = new Thread(() => ConductRolloutThreaded(nextLeaf));
                        threads[i].Start();
                    }
                    else
                    {
                        break;
                    }

                }
                for (int i = 0; i < threads.Length; i++)
                {
                    threads[i]?.Join();
                }
            }
        }

        public void RolloutQueuedLeavesThreadPool()
        {
            while (!this.leavesToRollOut.IsEmpty)
            {

                int task_length = this.leavesToRollOut.Count;
                if (this.num_threads < task_length)
                {
                    task_length = this.num_threads;
                }

                Task[] tasks = new Task[task_length];
                for (int i = 0; i < task_length; i++)
                {

                    if (this.leavesToRollOut.TryDequeue(out GameStateNode? nextLeaf))
                    {
                        tasks[i] = new Task(() => ConductRolloutThreaded(nextLeaf));
                        tasks[i].Start();
                    }
                    else
                    {
                        break;
                    }

                }
                Task.WhenAll(tasks).Wait();
            }
        }

        public void ConductRolloutThreaded(GameStateNode leaf)
        {
            lock (_MCST_lock_)
            {
                this.count += 1;
            }
            double result = beginRollout(leaf);
            leaf.BackPropogateThreaded(result);
        }

        public void RunSearchTreeThreadPool(GameStateNode node, int n)
        {

            int local_max_queued = max_queued_threaded;
            int actually_queued = 0;

            while (n > 0)
            {
                if (n < local_max_queued)
                {
                    local_max_queued = n;
                }
                actually_queued = TraverseThreaded(node, local_max_queued);
                n -= actually_queued;
                RolloutQueuedLeavesThreadPool();

            }
        }

        public void RunSearchTreeThreaded(GameStateNode node, int n = 1)
        {

            int local_max_queued = max_queued_threaded;
            int actually_queued = 0;

            while (n > 0)
            {
                if (n < local_max_queued)
                {
                    local_max_queued = n;
                }
                actually_queued = TraverseThreaded(node, local_max_queued);
                if (actually_queued == 0)
                {
                    actually_queued = TraverseThreaded(node, 1);
                }
                if (actually_queued == 0)
                {
                    // Console.WriteLine($"Ending search early with {n} remaining rollouts.");
                    break;
                }

                n -= actually_queued;
                RolloutQueuedLeavesThreaded();
                ;
            }
        }


        ///////////////////////////////////////////////////////////////
        /// Misc Utility functions to deliberately search tree instead of typical traverse paradigm
        ///////////////////////////////////////////////////////////////

        public void QueueUpLeaves(int n)
        {
            for (int i = 0; i < n; i++)
            {
                TraverseThreaded(this.working_node);
            }
        }

        public void FullyExpandNodeThreadPool(GameStateNode node)
        {
            mcst_selector.PopulateAllChildren(node);
            for (int i = 0; i < node.children.Length; i++)
            {
                this.leavesToRollOut.Enqueue(node.children[i]);
            }
            RolloutQueuedLeavesThreadPool();
        }

        public void FullyExpandNodeAndChildrenThreaded(GameStateNode node)
        {

            FullyExpandNodeThreadPool(node);

            for (int i = 0; i < node.children.Length; i++)
            {
                mcst_selector.PopulateAllChildren(node.children[i]);

                for (int j = 0; j < node.children[i].children.Length; j++)
                {
                    this.leavesToRollOut.Enqueue(node.children[i].children[j]);
                }
            }
            // moving this upwards unfortunately causes the operation to be single threaded
            RolloutQueuedLeavesThreadPool();
        }

        public void FullyExpandNodeThreaded(GameStateNode node)
        {
            mcst_selector.PopulateAllChildren(node);
            for (int i = 0; i < node.children.Length; i++)
            {
                this.leavesToRollOut.Enqueue(node.children[i]);
            }
            RolloutQueuedLeavesThreaded();
        }

        public void VisitEachChildNTimesThreaded(GameStateNode node, int n)
        {
            if (node.children == null || node.children.Length == 0)
            {
                Console.WriteLine("Assert failed, reach 348 without valid children");
                return;
            }

            for (int i = 0; i < n; i++)
            {
                foreach (GameStateNode child in node.children)
                {
                    this.leavesToRollOut.Enqueue(child);
                }
                // tested once and this appears to spawn threads correctdly, despite being in a for loop:
                RolloutQueuedLeavesThreaded(); 
            }
        }

        public void ExpandTreeToLevel(GameStateNode node, int n)
        {
            // 0: only children of root have been expanded
            // 1: two generations of children (one move for each player)
            // 2: three generations of children (two moves for player one)
            // 3: four generations of children (two moves for each player)

            mcst_selector.PopulateAllChildren(node);
            if (node.level == 1)
            {
                Console.WriteLine($"Root's child: {node._index_in_parent}");
            }
            if (node.level < n)
            {
                for (int i = 0; i < node.children.Length; i++)
                {
                    ExpandTreeToLevel(node.children[i], n);
                }
            }


        }

    }
}
