using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static TorchSharp.torch.optim.lr_scheduler.impl.CyclicLR;

namespace GlokusSharp
{

    internal class Basic_MoveSelector
    {

        protected MCST parent_mcst;

        public Basic_MoveSelector(MCST mcst) 
        {
            parent_mcst = mcst;
        }

        public virtual MoveStruct[] GetLegalMoves(GameState gs)
        {
            List<MoveStruct> list_of_moves = new();

            int[] starting_locations = gs.getValidOpenSpots();

            if (!gs.played_first_move[gs.player_to_move])
            {
                // in the true head-to-head game, we need to consider that any piece of the tile, not just corners, can be used to start the game
                for (int minnow_i = 0; minnow_i < gs.game_config.minnow_list.number; minnow_i++)
                {
                    for (int transform_i = 0; transform_i < gs.game_config.minnow_list.minnows[minnow_i].number_of_transforms; transform_i++)
                    {
                        for (int tile_i = 0; tile_i < gs.game_config.minnow_list.minnows[minnow_i].tile_length; tile_i++)
                        {

                            MoveStruct proposed_move = new
                                (starting_locations[0], starting_locations[1],
                                minnow_i, transform_i,
                                gs.game_config.minnow_list.minnows[minnow_i].list_of_transforms[transform_i, tile_i, 0],
                                gs.game_config.minnow_list.minnows[minnow_i].list_of_transforms[transform_i, tile_i, 1]
                                );

                            if (gs.isMoveLegal(proposed_move))
                            {
                                list_of_moves.Add(proposed_move);
                            }

                        }

                    }

                }
                return list_of_moves.ToArray();
            }

            for (int starting_location_i = 0; starting_location_i * 2 < starting_locations.Length; starting_location_i += 2)
            {
                if (starting_locations[starting_location_i] == -1)
                    break;

                for (int minnow_i = 0; minnow_i < gs.game_config.minnow_list.number; minnow_i++)
                {
                    if (gs.used_minnow[minnow_i + gs.player_to_move * gs.game_config.minnow_list.number])
                        continue;

                    for (int corner_list_i = 0; corner_list_i < gs.game_config.minnow_list.minnows[minnow_i].number_of_transforms; corner_list_i++)
                    {

                        for (int tile_i = 0; tile_i < gs.game_config.minnow_list.minnows[minnow_i].number_of_corners; tile_i++)
                        {

                            MoveStruct proposed_move = new
                                (starting_locations[starting_location_i], starting_locations[starting_location_i + 1],
                                minnow_i, corner_list_i,
                                gs.game_config.minnow_list.minnows[minnow_i].list_of_corners[corner_list_i, tile_i, 0],
                                gs.game_config.minnow_list.minnows[minnow_i].list_of_corners[corner_list_i, tile_i, 1]
                                );

                            // todo: concerned that I did not 100% identically replicate the python move_struct logic here
                            // only matters if we're sending move data between the two programs, but I don't have a scenario for that now

                            if (gs.isMoveLegal(proposed_move))
                            {
                                list_of_moves.Add(proposed_move);
                            }

                        }

                    }
                }

            }
            return list_of_moves.ToArray();
        }

        public void PopulateMoves(GameStateNode gsn)
        {

            if (gsn.possible_moves != null && gsn.possible_moves.Length > 0)
            {
                return;
            }

            if (gsn.parent_node == null)
            {
                gsn.possible_moves = GetLegalMoves(gsn.game_state);
                gsn.children = new GameStateNode[gsn.possible_moves.Length];
                return;
            }

            if (gsn.parent_node.possible_child_moves == null)
            {
                GeneratePoolOfChildMoves(gsn.parent_node);
            }

            List<MoveStruct> legalMoves = new();
            for (int i = 0; i < gsn.parent_node.possible_child_moves.Length; i++)
            {
                if (gsn.game_state.isMoveLegal(gsn.parent_node.possible_child_moves[i]))
                {
                    legalMoves.Add(gsn.parent_node.possible_child_moves[i]);
                }
            }

            if (legalMoves.Count() == 0)
            {
                gsn.game_state.player_to_move = gsn.parent_node.game_state.player_to_move;
                gsn.possible_moves = GetLegalMoves(gsn.game_state);
                gsn.children = new GameStateNode[gsn.possible_moves.Length];
                return;
            }

            gsn.possible_moves = legalMoves.ToArray();
            gsn.children = new GameStateNode[gsn.possible_moves.Length];
        }

        public void GeneratePoolOfChildMoves(GameStateNode gsn)
        {
            gsn.game_state.togglePlayerNumber();
            gsn.possible_child_moves = GetLegalMoves(gsn.game_state);
            gsn.game_state.togglePlayerNumber();
        }

        public void PopulateAllChildren(GameStateNode gsn)
        {

            if (gsn.possible_moves == null || gsn.possible_moves.Length == 0)
            {
                PopulateMoves(gsn);
            }

            if (gsn.children == null)
            {
                gsn.children = new GameStateNode[gsn.possible_moves.Length];
            }

            for (int i = 0; i < gsn.children.Length; i++)
            {
                if (gsn.children[i] == null)
                {
                    gsn.CreateChild(i);
                }
            }
        }

        public virtual int SelectRolloutMove(GameStateNode node)
        {
            
            int total = 0;

            for (int i = 0; i < node.possible_moves.Length; i++)
            {
                total += parent_mcst.game_config.minnow_list.minnows[node.possible_moves[i].minnow_index].tile_length *
                         parent_mcst.game_config.minnow_list.minnows[node.possible_moves[i].minnow_index].tile_length;
            }

            int random_index = parent_mcst.game_config.random.Next(0, total);
            total = 0;

            for (int i = 0; i < node.possible_moves.Length; i++)
            {
                total += parent_mcst.game_config.minnow_list.minnows[node.possible_moves[i].minnow_index].tile_length *
                    parent_mcst.game_config.minnow_list.minnows[node.possible_moves[i].minnow_index].tile_length;
                if (total > random_index)
                {
                    return i;
                }
            }

            return node.possible_moves.Length - 1;
        }

        public virtual MoveStruct SelectMove(GameStateNode working_node)
        {
            
            if (working_node.children == null || working_node.children.Length == 0)
            {
                Console.WriteLine("Assert failed, reached move selection without performing MCST");
                return new MoveStruct(-1, -1, -1, -1, -1, -1);
            }

            int most_visited_non_terminal_idx = -1;
            int most_visited_non_terminal_num = -1;

            int highest_value_terminal_idx = -1;
            double highest_value_terminal_value = Double.NegativeInfinity;
            if(working_node.game_state.player_to_move == 1)
            {
                highest_value_terminal_value = Double.PositiveInfinity;
            }

            int index = -1;
            foreach(GameStateNode child in working_node.children)
            {
                index++;

                if (child.is_terminal_or_forcing)
                {
                    if (working_node.game_state.player_to_move == 0)
                    {
                        if (child.value > highest_value_terminal_value)
                        {
                            highest_value_terminal_value = child.value;
                            highest_value_terminal_idx = index;
                            continue;
                        }
                    }
                    else
                    {
                        if (child.value < highest_value_terminal_value)
                        {
                            highest_value_terminal_value = child.value;
                            highest_value_terminal_idx = index;
                            continue;
                        }
                    }
                }
                else
                {
                    if (child.visits > most_visited_non_terminal_num)
                    {
                        most_visited_non_terminal_num = child.visits;
                        most_visited_non_terminal_idx = index;
                        continue;
                    }
                }
            }

            /* If you have a winning move, take it every time
             * If you have non-terminal moves, take the most popular
             * Last, simply take the least painful move
             * *Maybe the last two could be more sophisticated idk
             */

            if ( (working_node.game_state.player_to_move == 0 && highest_value_terminal_value > 0) ||
                (working_node.game_state.player_to_move == 1 && highest_value_terminal_value < 0))
            {
                parent_mcst.SelectMove(highest_value_terminal_idx);
                return parent_mcst.working_node.previous_move;
            }

            if (most_visited_non_terminal_idx > -1)
            {
                parent_mcst.SelectMove(most_visited_non_terminal_idx);
                return parent_mcst.working_node.previous_move;
            }

            parent_mcst.SelectMove(highest_value_terminal_idx);
            return parent_mcst.working_node.previous_move;
        }
    }


    internal class Greedy_MoveSelector : Basic_MoveSelector
    {

        public double distance_weight = 1.0;
        // at which level in the tree and rollouts can we start using 1-minnows, 2-minnows, etc.
        public int[] threshold_level_to_use_piece = [4 * 2, 4 * 2, 4 * 2, 3 * 2, 0 * 2];

        public Greedy_MoveSelector(MCST mcst) : base(mcst)
        { 

        }

        public override MoveStruct[] GetLegalMoves(GameState gs)
        {
            List<MoveStruct> list_of_moves = new();
            int[] starting_locations = gs.getValidOpenSpots();

            int level = 0;
            foreach(bool _value in gs.used_minnow)
            {
                if (_value) level++;
            }

            if (!gs.played_first_move[gs.player_to_move])
            {
                // in the true head-to-head game, we need to consider that any piece of the tile, not just corners, can be used to start the game
                for (int minnow_i = 0; minnow_i < gs.game_config.minnow_list.number; minnow_i++)
                {
                    // if the tile is too short to be played, based on the threshold defined in Chooser_Params
                    if (level < threshold_level_to_use_piece[parent_mcst.game_config.minnow_list.minnows[minnow_i].tile_length - 1] )
                    {
                        continue;
                    }

                    for (int transform_i = 0; transform_i < gs.game_config.minnow_list.minnows[minnow_i].number_of_transforms; transform_i++)
                    {
                        for (int tile_i = 0; tile_i < gs.game_config.minnow_list.minnows[minnow_i].tile_length; tile_i++)
                        {

                            MoveStruct proposed_move = new
                                (starting_locations[0], starting_locations[1],
                                minnow_i, transform_i,
                                gs.game_config.minnow_list.minnows[minnow_i].list_of_transforms[transform_i, tile_i, 0],
                                gs.game_config.minnow_list.minnows[minnow_i].list_of_transforms[transform_i, tile_i, 1]
                                );

                            if (gs.isMoveLegal(proposed_move))
                            {
                                list_of_moves.Add(proposed_move);
                            }

                        }

                    }

                }
                return list_of_moves.ToArray();
            }

            for (int starting_location_i = 0; starting_location_i * 2 < starting_locations.Length; starting_location_i += 2)
            {
                if (starting_locations[starting_location_i] == -1)
                    break;

                for (int minnow_i = 0; minnow_i < gs.game_config.minnow_list.number; minnow_i++)
                {
                    if (gs.used_minnow[minnow_i + gs.player_to_move * gs.game_config.minnow_list.number])
                        continue;

                    // if the tile is too short to be played, based on the threshold defined in Chooser_Params
                    if (level < threshold_level_to_use_piece[parent_mcst.game_config.minnow_list.minnows[minnow_i].tile_length - 1])
                    {
                        continue;
                    }

                    for (int corner_list_i = 0; corner_list_i < gs.game_config.minnow_list.minnows[minnow_i].number_of_transforms; corner_list_i++)
                    {

                        for (int tile_i = 0; tile_i < gs.game_config.minnow_list.minnows[minnow_i].number_of_corners; tile_i++)
                        {

                            MoveStruct proposed_move = new
                                (starting_locations[starting_location_i], starting_locations[starting_location_i + 1],
                                minnow_i, corner_list_i,
                                gs.game_config.minnow_list.minnows[minnow_i].list_of_corners[corner_list_i, tile_i, 0],
                                gs.game_config.minnow_list.minnows[minnow_i].list_of_corners[corner_list_i, tile_i, 1]
                                );

                            if (gs.isMoveLegal(proposed_move))
                            {
                                list_of_moves.Add(proposed_move);
                            }

                        }

                    }
                }

            }
            return list_of_moves.ToArray();

        }

        public override int SelectRolloutMove(GameStateNode node)
        {

            int total = 0;

            int[] distance_value = new int[node.possible_moves.Length];
            int board_x = 0;
            int board_y = 0;

            for (int i = 0; i < node.possible_moves.Length; i++)
            {
                total += parent_mcst.game_config.minnow_list.minnows[node.possible_moves[i].minnow_index].tile_length *
                         parent_mcst.game_config.minnow_list.minnows[node.possible_moves[i].minnow_index].tile_length;

                for (int j = 0; j < parent_mcst.game_config.minnow_list.minnows[node.possible_moves[i].minnow_index].tile_length; j++)
                {
                    // ripped from GameState.applyMove(), don't worry about it:
                    board_x = node.possible_moves[i].x - node.possible_moves[i].shiftX;
                    board_x += parent_mcst.game_config.minnow_list.minnows[node.possible_moves[i].minnow_index].
                        list_of_transforms[node.possible_moves[i].minnow_transform_index, j, 0];
                    board_y = node.possible_moves[i].y - node.possible_moves[i].shiftY;
                    board_y += parent_mcst.game_config.minnow_list.minnows[node.possible_moves[i].minnow_index].
                        list_of_transforms[node.possible_moves[i].minnow_transform_index, j, 1];

                    if (node.game_state.player_to_move == 0)
                    {
                        distance_value[i] += board_x + board_y;
                    }
                    else
                    {
                        // (board_size - boardx - 1) + (board_size - boardy - 1) simplifies to ->
                        distance_value[i] += 2 * parent_mcst.game_config.board_size - board_x - board_y - 2;
                    }

                }
                distance_value[i] = (int)(distance_weight * distance_value[i]);
                total += distance_value[i];

            }


            int random_index = parent_mcst.game_config.random.Next(0, total);
            total = 0;

            for (int i = 0; i < node.possible_moves.Length; i++)
            {
                total += parent_mcst.game_config.minnow_list.minnows[node.possible_moves[i].minnow_index].tile_length *
                    parent_mcst.game_config.minnow_list.minnows[node.possible_moves[i].minnow_index].tile_length;
                total += distance_value[i];
                if (total > random_index)
                {
                    return i;
                }
            }

            return node.possible_moves.Length - 1;
        }

        /* public void PopulateMoves(GameStateNode gsn)
        
        {

            List<MoveStruct> list_of_moves = new();
            GameState gs = gsn.game_state;
            int[] starting_locations = gs.getValidOpenSpots();

            if (!gs.played_first_move[gs.player_to_move])
            {
                // in the true head-to-head game, we need to consider that any piece of the tile, not just corners, can be used to start the game
                for (int minnow_i = 0; minnow_i < gs.game_config.minnow_list.number; minnow_i++)
                {
                    // if the tile is too short to be played, based on the threshold defined in Chooser_Params
                    if (mcst.working_node.level < 
                        players[player_index[mcst.working_node.game_state.player_to_move]].
                        threshold_level_to_use_piece[mcst.game_config.minnow_list.minnows[minnow_i].tile_length - 1]
                         )
                    {
                        continue;
                    }

                    for (int transform_i = 0; transform_i < gs.game_config.minnow_list.minnows[minnow_i].number_of_transforms; transform_i++)
                    {
                        for (int tile_i = 0; tile_i < gs.game_config.minnow_list.minnows[minnow_i].tile_length; tile_i++)
                        {

                            MoveStruct proposed_move = new
                                (starting_locations[0], starting_locations[1],
                                minnow_i, transform_i,
                                gs.game_config.minnow_list.minnows[minnow_i].list_of_transforms[transform_i, tile_i, 0],
                                gs.game_config.minnow_list.minnows[minnow_i].list_of_transforms[transform_i, tile_i, 1]
                                );

                            if (gs.isMoveLegal(proposed_move))
                            {
                                list_of_moves.Add(proposed_move);
                            }

                        }

                    }

                }
                gsn.possible_moves = list_of_moves.ToArray();
            }

            for (int starting_location_i = 0; starting_location_i * 2 < starting_locations.Length; starting_location_i += 2)
            {
                if (starting_locations[starting_location_i] == -1)
                    break;

                for (int minnow_i = 0; minnow_i < gs.game_config.minnow_list.number; minnow_i++)
                {
                    if (gs.used_minnow[minnow_i + gs.player_to_move * gs.game_config.minnow_list.number])
                        continue;

                    // if the tile is too short to be played, based on the threshold defined in Chooser_Params
                    if (mcst.working_node.level <
                        players[player_index[mcst.working_node.game_state.player_to_move]].
                        threshold_level_to_use_piece[mcst.game_config.minnow_list.minnows[minnow_i].tile_length - 1]
                         )
                    {
                        continue;
                    }

                    for (int corner_list_i = 0; corner_list_i < gs.game_config.minnow_list.minnows[minnow_i].number_of_transforms; corner_list_i++)
                    {

                        for (int tile_i = 0; tile_i < gs.game_config.minnow_list.minnows[minnow_i].number_of_corners; tile_i++)
                        {

                            MoveStruct proposed_move = new
                                (starting_locations[starting_location_i], starting_locations[starting_location_i + 1],
                                minnow_i, corner_list_i,
                                gs.game_config.minnow_list.minnows[minnow_i].list_of_corners[corner_list_i, tile_i, 0],
                                gs.game_config.minnow_list.minnows[minnow_i].list_of_corners[corner_list_i, tile_i, 1]
                                );

                            if (gs.isMoveLegal(proposed_move))
                            {
                                list_of_moves.Add(proposed_move);
                            }

                        }

                    }
                }

            }
            gsn.possible_moves = list_of_moves.ToArray();

        } */


    }
}
