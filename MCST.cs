using Microsoft.VisualBasic;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using static Tensorboard.CostGraphDef.Types;

namespace GlokusSharp
{

    internal partial class MCST
    {
        public GameConfig game_config;
        public GameStateNode root_node;
        public GameStateNode working_node;
        public Basic_MoveSelector mcst_selector;
        int round = 0;
        int count = 0;
        public int num_threads = 6;
        double exploration_constant = 2.2;
        ConcurrentQueue<GameStateNode> leavesToRollOut = new ConcurrentQueue<GameStateNode>();
        private static readonly object _MCST_lock_ = new();
        public List<GameStateNode> VisitedNodes = new();
        public int max_queued_threaded = 12;

        public MCST(GameConfig gc)
        {
            this.game_config = gc;
            GameState gs = new(this.game_config);
            root_node = new(gs);
            working_node = root_node;
            mcst_selector = new(this);
        }

        public void resetSearchTree()
        {
            GameState gs = new(this.game_config);
            root_node = new(gs);
            round = 0;
            count = 0;
        }

        public void ResetWorkingTreeButKeepFirstNode()
        {
            MoveStruct NodeToPrune = VisitedNodes[0].previous_move;
            int nodeIdx = Array.IndexOf(root_node.possible_moves, NodeToPrune);
            root_node.children[nodeIdx].value = double.NegativeInfinity;
            root_node.children[nodeIdx].children = null;
            VisitedNodes.Clear();
            working_node = root_node;
        }

        public void runOneSearch(GameStateNode node)
        {
            this.count += 1;
            GameStateNode leaf = traverse(node);
            if (leaf == null) return;
            double result = beginRollout(leaf);
            backPropogate(leaf, result);

        }
        
        public GameStateNode? traverse(GameStateNode node)
        {
            if (node.possible_moves == null)
            {
                mcst_selector.PopulateMoves(node);
            }
            if (node.possible_moves.Length == 0)
            {
                return null;
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
                    if (node.children[i].visits == 0)
                    {
                        number_unvisited++;
                    }
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
                                node.CreateChild(current_index);
                                break;
                            }
                            current_index++;
                            continue;
                        }
                        if (node.children[i].visits == 0)
                        {
                            if (current_index == random_index)
                            {
                                current_index = i;
                                break;
                            }
                            current_index++;
                        }
                    }
                    return node.children[current_index];
                }

            }

            return traverse(node.GetChildbyUCT(this.exploration_constant));
        }

        public double beginRollout(GameStateNode node)
        {
            if (!node.game_state.AreThereAnyLegalMoves())
            {
                node.game_state.togglePlayerNumber();
            }            
            if (!node.game_state.AreThereAnyLegalMoves())
            {
                mcst_selector.PopulateMoves(node); // need this for backPropogate() logic
                node.is_terminal_or_forcing = true;
                if (node.parent_node != null)
                {   
                    node.parent_node.has_terminal_or_forcing_children = true;
                }
                return GetScore(node);
            }

            // rollout happens 'in-place' (no branching), so a COPY of the gamestatenode is required
            return GetScore(rollout(new(node.game_state)));
        }

        public GameStateNode rollout(GameStateNode node)
        {

            node.possible_moves = mcst_selector.GetLegalMoves(node.game_state);
            if (node.possible_moves.Length == 0)
            {
                node.game_state.togglePlayerNumber();
                node.possible_moves = mcst_selector.GetLegalMoves(node.game_state);
            }
            if (node.possible_moves.Length == 0)
            {
                return node;
            }
            applyRolloutMove(node);
            return rollout(node);
        }

        public void applyRolloutMove(GameStateNode node)
        {
            int move_index = mcst_selector.SelectRolloutMove(node);
            node.game_state.applyMove(node.possible_moves[move_index]);
        }

        public void backPropogate(GameStateNode node, double result)
        {
            node.visits += 1;

            if (node.is_terminal_or_forcing)
            {
                node.value = result;

                // unless the same leaf got queued up twice this is impossible to reach, but I am going to leave it for now
                if (node.possible_moves != null && node.possible_moves.Length > 0)
                {
                    if (node.game_state.player_to_move == 0)
                    {
                        node.value = node.GetMaxValueOfTerminalOrForcing();
                    }
                    else
                    {
                        node.value = node.GetMinValueOfTerminalOrForcing();
                    }

                }

            }
            else if (node.has_terminal_or_forcing_children)
            {
                double temp_value = 0;
                if (node.game_state.player_to_move == 0)
                {
                    temp_value = node.GetMaxValueOfTerminalOrForcing();
                }
                else
                {
                    temp_value = node.GetMinValueOfTerminalOrForcing();
                }

                /*
                 * Need to update UCT to exclude terminally marked nodes in searches
                 * Also there is a threaded version of this function
                 */

                if (node.OnlyHasTerminalOrForcingChildren())
                {
                    node.value = temp_value;
                    node.is_terminal_or_forcing = true;
                    result = temp_value;
                }
                else // some moves are terminal, some are not
                {
                    // if the next player has a winning move, they'll always take it
                    if ((node.game_state.player_to_move == 0 && temp_value > 0) ||
                            (node.game_state.player_to_move == 1 && temp_value < 0))
                    {
                        node.value = temp_value;
                        node.is_terminal_or_forcing = true;
                        result = temp_value;
                    } 
                    else // keep searching to try to clawback
                    {
                        node.value += (result - node.value) / node.visits;
                    }

                }

            }
            else
            {
                node.value += (result - node.value) / node.visits; // equation 2.4 from RL book
            }
            
            if (node.parent_node == null) return;
            backPropogate(node.parent_node, result);
        }

        public void fullyExpandNodeAndChildren(GameStateNode node)
        {

            mcst_selector.PopulateAllChildren(node);
            for (int i = 0; i < node.children.Length; i++)
            {
                runOneSearch(node);
            }
            for (int i = 0; i < node.children.Length; i++)
            {
                mcst_selector.PopulateAllChildren(node.children[i]);
                for (int j = 0; j < node.children[i].children.Length; j++)
                {
                    runOneSearch(node.children[i]);
                }
            }
        }

        public void visitEachChild_nTimes(GameStateNode node, int n)
        {
            if (node.children == null)
            {
                mcst_selector.PopulateAllChildren(node);
            }

            for (int i = 0; i < node.children.Length; i++)
            {

                for (int j = 0; j < n; j++)
                {
                    runOneSearch(node.children[i]);
                }
            }
        }

        public void runSearchTree_nTimes(GameStateNode node, int n)
        {
            for (int i = 0; i < n; i++)
            {
                runOneSearch(node);
            }
        }

        public void PopulateWorkingNodeChildren()
        {
            mcst_selector.PopulateAllChildren(working_node);
        }

        public MoveStruct SelectMoveByAlgo()
        {
            if (!working_node.fully_expanded)
            {
                FullyExpandNodeThreaded(working_node);
            }

            return mcst_selector.SelectMove(working_node);


        }

    }

}
