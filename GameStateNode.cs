using GlokusSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GlokusSharp
{
    internal class GameStateNode
    {

        public GameState game_state;
        public bool fully_expanded = false;
        public bool is_terminal_or_forcing = false;
        public bool has_terminal_or_forcing_children = false;
        public int visits = 0;
        private bool queued_for_visit = false;
        public double value = 0;
        public double most_recent_UCT = 0;
        public GameStateNode? parent_node = null;
        public GameStateNode[]? children = null;
        public MoveStruct previous_move;
        public MoveStruct[]? possible_moves = null;
        public MoveStruct[]? possible_child_moves = null;
        //public List<MoveStruct>? possible_moves = null;
        //public List<MoveStruct>? possible_child_moves = null;
        public int _index_in_parent = -1;
        private readonly object _lock_ = new();
        public int level = 0;

        public GameStateNode(GameState gs)
        {
            // explicitly make a copy
            this.game_state = new(gs);

        }

        public void CreateChild(int move_index)
        {

            if (possible_moves == null)
            {
                Console.WriteLine("Assert failed in CreateChild: no moves exist yet.");
                return;
            }

            if (children == null)
            {
                children = new GameStateNode[possible_moves.Length];
            }

            if (children[move_index] != null)
            {
                return;
            }

            children[move_index] = new(game_state);
            children[move_index].parent_node = this;
            children[move_index]._index_in_parent = move_index;
            children[move_index].game_state.applyMove(possible_moves[move_index]);
            children[move_index].previous_move = possible_moves[move_index];
            children[move_index].level = this.level + 1;

        }

        public bool IsUnvisited()
        {
            lock (_lock_)
            {
                if (this.visits == 0 && this.queued_for_visit == false) return true;
                return false;
            }
        }

        public double UpdateUCT(double c1, double uct_const)
        {
            lock (_lock_)
            {
                if (this.visits == 0)
                {
                    this.most_recent_UCT = c1 * this.value;
                    return this.most_recent_UCT;
                }
                this.most_recent_UCT = c1 * this.value + uct_const / Math.Sqrt(this.visits);
                return this.most_recent_UCT;
            }
        }

        public void UpdateResult(double result)
        {
            lock (_lock_)
            {
                this.visits += 1;
                this.value += (result - this.value) / this.visits; // equation 2.4 from RL book
                this.queued_for_visit = false;
            }
        }

        public GameStateNode GetChildbyUCT(double exploration_constant)
        {
            // UCT equation from https://int8.io/monte-carlo-tree-search-beginners-guide/#upper-confidence-bound-applied-to-trees

            double c1 = 1.0;
            if (game_state.player_to_move == 1)
                c1 = -1.0;

            double uct_const = 0;
            if (visits > 0)
                uct_const = exploration_constant * Math.Sqrt(Math.Log(visits));

            double largest_so_far = double.NegativeInfinity;
            int index_of_largest = 0;

            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].is_terminal_or_forcing) continue;
                children[i].most_recent_UCT = c1 * children[i].value + uct_const / Math.Sqrt(children[i].visits);

                if (children[i].most_recent_UCT > largest_so_far)
                {
                    largest_so_far = children[i].most_recent_UCT;
                    index_of_largest = i;
                }
            }

            return children[index_of_largest];
        }

        public double GetMaxValueOfTerminalOrForcing()
        {
            double max = Double.NegativeInfinity;
            if (children == null || children.Length == 0)
            {
                return max;
            }            
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] == null) continue;

                if (children[i].is_terminal_or_forcing && children[i].value > max)
                {
                    max = children[i].value;
                }
            }
            return max;
        }

        public double GetMinValueOfTerminalOrForcing()
        {
            double min = Double.PositiveInfinity;
            if (children == null || children.Length == 0)
            {
                return min;
            }
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] == null) continue;

                if (children[i].is_terminal_or_forcing && children[i].value < min)
                {
                    min = children[i].value;
                }
            }
            return min;
        }

        public bool OnlyHasTerminalOrForcingChildren()
        {

            if (children == null || children.Length == 0)
            {
                Console.WriteLine("Probable Assert Error, should not have gotten here (563)");
                return false;
            }
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] == null || !children[i].is_terminal_or_forcing)
                {
                    return false;
                }
            }
            return true;

        }

        public void BackPropogateThreaded(double result)
        {

            lock (_lock_)
            {
                visits += 1;
                queued_for_visit = false;
            }

            if (is_terminal_or_forcing || has_terminal_or_forcing_children)
            {
                result = this.CheckIfForcedWin(result);
            }
            else 
            {
                lock (_lock_)
                {
                    value += (result - value) / visits; // equation 2.4 from RL book
                }
            }

            if (parent_node == null) return;
            parent_node.BackPropogateThreaded(result);

        }
        
        public double CheckIfForcedWin(double result)
        {

            double children_extrema = 0;
            if (game_state.player_to_move == 0)
            {
                children_extrema = GetMaxValueOfTerminalOrForcing();
            }
            else
            {
                children_extrema = GetMinValueOfTerminalOrForcing();
            }

            if (is_terminal_or_forcing)
            {
                if (has_terminal_or_forcing_children)
                {
                    result = children_extrema;
                } 
                lock (_lock_)
                {
                    value = result;
                }
                return result;

            }

            if (OnlyHasTerminalOrForcingChildren())
            {

                lock (_lock_)
                {
                    value = children_extrema;
                    is_terminal_or_forcing = true;
                    if (parent_node != null)
                    {
                        parent_node.has_terminal_or_forcing_children = true;
                    }
                    return children_extrema;
                }
            }

            // always take a winning move
            if ((game_state.player_to_move == 0 && children_extrema > 0) ||
                    (game_state.player_to_move == 1 && children_extrema < 0))
            {
                lock (_lock_)
                {
                    value = children_extrema;
                    is_terminal_or_forcing = true;
                    if (parent_node != null)
                    {
                        parent_node.has_terminal_or_forcing_children = true;
                    }
                    return children_extrema;
                }
            }

            // keep searching to try to clawback
            lock (_lock_)
            {
                value += (result - value) / visits;
            }
            return value;
        }

    }
}
