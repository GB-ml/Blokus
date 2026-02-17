using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GlokusSharp
{

    internal class GameConfig
    {
        public MinnowList minnow_list;
        public int board_size = 0;
        int[] board; // todo: Do I actually need this since I cannot implicitly deepcopy in C# anyway?
        public int num_players = 2;
        public int[] board_player_number;
        public int[] starting_location; // 
        public Random random = new Random();

        public GameConfig(int board_size, MinnowList minnow_list) 
        {
            this.board_size = board_size;
            this.board = new int[board_size * board_size];
            for (int i = 0; i < this.board.Length; i++)
                this.board[i] = 0;

            this.board_player_number = new int[this.num_players];
            for (int i = 0; i < this.num_players; i++)
                this.board_player_number[i] = i + 1;

            this.minnow_list = minnow_list;

            starting_location = [ 0, 0, board_size - 1, board_size - 1];
        }

    }

    public struct MoveStruct
    {
        public int x;
        public int y;
        public int minnow_index;
        public int minnow_transform_index;
        public int shiftX;
        public int shiftY;

        public MoveStruct(int x, int y, int minnow_index, int minnow_transform_index, int shiftX, int shiftY)
        {
            this.x = x;
            this.y = y;
            this.minnow_index = minnow_index;
            this.minnow_transform_index = minnow_transform_index;
            this.shiftX = shiftX;
            this.shiftY = shiftY;
        }

        public MoveStruct(string fileString)
        {
            string[] values = fileString.Split(',');

            this.minnow_index = int.Parse(values[0]);
            this.minnow_transform_index = int.Parse(values[1]);

            this.x = int.Parse(values[2]);
            this.y = int.Parse(values[3]);
            this.shiftX = int.Parse(values[4]);
            this.shiftY = int.Parse(values[5]);
        }

        public override string ToString()
        {
            return $"{minnow_index},{minnow_transform_index},{x},{y},{shiftX},{shiftY}";
        }
    }

    internal class GameState
    {
        public GameConfig game_config;
        public int[] board;
        public bool[] used_minnow;
        public bool[] played_first_move;
        public int player_to_move = 0;

        public GameState(GameConfig gc)
        {
            this.game_config = gc;
            this.board = new int[this.game_config.board_size * this.game_config.board_size];
            for (int i = 0; i < this.board.Length; i++)
                this.board[i] = 0;

            this.used_minnow = new bool[this.game_config.num_players * this.game_config.minnow_list.number]; // false by default in C#
            this.played_first_move = new bool[this.game_config.num_players]; // false by default in C#
        }
        
        public GameState(GameState parent_state)
        {
            this.game_config = parent_state.game_config;
            this.board = new int[this.game_config.board_size * this.game_config.board_size];
            Array.Copy(parent_state.board, this.board, this.board.Length);

            this.used_minnow = new bool[this.game_config.num_players * this.game_config.minnow_list.number];
            Array.Copy(parent_state.used_minnow, this.used_minnow, this.used_minnow.Length);

            this.played_first_move = new bool[this.game_config.num_players];
            Array.Copy(parent_state.played_first_move, this.played_first_move, this.played_first_move.Length);

            this.player_to_move = parent_state.player_to_move;
        }

        public int getValueAtLocation(int x, int y)
        {
            if (x < 0 || y < 0) return -1;
            if (x >= this.game_config.board_size || y >= this.game_config.board_size) return -1;

            return this.board[x + y * this.game_config.board_size];
        }

        public void togglePlayerNumber()
        {
            this.player_to_move = 1 - this.player_to_move;
        }

        public int getScore(int player_number)
        {
            int score = 0;

            for (int i = 0; i < this.game_config.minnow_list.number; i++)
            {
                if (this.used_minnow[i + player_number * this.game_config.minnow_list.number] == false)
                    score += game_config.minnow_list.minnows[i].tile_length;
            }

            return score;
        }

        public int[] getValidOpenSpots()
        {

            if (!this.played_first_move[player_to_move])
            {
                return this.game_config.starting_location[
                    (player_to_move * game_config.num_players)..(player_to_move * game_config.num_players + 2)];
            }

            /* 2 is from x,y coordinate; 
             * 3 is fudge factor; 
             * Without a rigorous proof, I feel that max open spots is O(sqrt(board_size^2)), so 3x this to be safe
             * But it also doesn't really matter, if we run out of open spots we just use the ones allocated
             * Full board size is 20x20, so at most we're allocating 120 * 4 = 480 bytes per call
            */
            int max_list_size = 2 * 3 * this.game_config.board_size;
            int[] list_of_tiles = new int[max_list_size];

            int index = 0;
            for (int j = 0; j < this.game_config.board_size; j++)
            {
                for (int i = 0; i < this.game_config.board_size; i++)
                {

                    // okay to 'inline' getValueAtLocation here and only here since for loops perform implicit bounds checking
                    if (this.board[i + j * this.game_config.board_size] == 0)
                    {

                        bool possibly_a_corner = false;

                        if (getValueAtLocation(i + 1, j + 1) == player_to_move + 1 ||
                            getValueAtLocation(i + 1, j - 1) == player_to_move + 1 ||
                            getValueAtLocation(i - 1, j + 1) == player_to_move + 1 ||
                            getValueAtLocation(i - 1, j - 1) == player_to_move + 1)
                        {

                            possibly_a_corner = true;
                        }

                        if (!possibly_a_corner)
                            continue;

                        if (getValueAtLocation(i + 1, j) == player_to_move + 1 ||
                            getValueAtLocation(i - 1, j) == player_to_move + 1 ||
                            getValueAtLocation(i, j + 1) == player_to_move + 1 ||
                            getValueAtLocation(i, j - 1) == player_to_move + 1)
                        {
                            continue;
                        }

                        list_of_tiles[index * 2] = i;
                        list_of_tiles[index * 2 + 1] = j;
                        index++;
                    }

                    if (index * 2 == max_list_size)
                    {
                        Console.WriteLine("Warning 41: completely filled up possible move locations");
                        break;
                    }

                }
                if (index * 2 == max_list_size)
                {
                    break;
                }

            }

            if (index * 2 < max_list_size)
            {
                list_of_tiles[index * 2] = -1;
                list_of_tiles[index * 2 + 1] = -1;
            }

            return list_of_tiles;
        }

        public bool isMoveLegal(MoveStruct move)
        {
            bool move_is_legal = true;
            
            int board_x = 0;
            int board_y = 0;

            for (int i = 0; i < this.game_config.minnow_list.minnows[move.minnow_index].tile_length; i++)
            {
                board_x = move.x - move.shiftX;
                board_x += this.game_config.minnow_list.minnows[move.minnow_index].
                    list_of_transforms[move.minnow_transform_index, i, 0];
                board_y = move.y - move.shiftY;
                board_y += this.game_config.minnow_list.minnows[move.minnow_index].
                    list_of_transforms[move.minnow_transform_index, i, 1];

                if (getValueAtLocation(board_x, board_y) != 0)
                {
                    move_is_legal = false;
                    break;
                }

                // check for adjacency of own tiles:
                if (getValueAtLocation(board_x + 1, board_y) == player_to_move + 1 ||
                    getValueAtLocation(board_x - 1, board_y) == player_to_move + 1 ||
                    getValueAtLocation(board_x, board_y + 1) == player_to_move + 1 ||
                    getValueAtLocation(board_x, board_y - 1) == player_to_move + 1)
                {
                    move_is_legal = false;
                    break;
                }
            }
            return move_is_legal;
        }

        public bool AreThereAnyLegalMoves()
        {
            int[] starting_locations = getValidOpenSpots();
            if (starting_locations.Length == 0)
            {
                return false;
            }
            if (starting_locations[0] == -1)
            {
                return false;
            }
            if (!this.played_first_move[player_to_move])
            {
                return true;
            }
            for (int starting_location_i = 0; starting_location_i * 2 < starting_locations.Length; starting_location_i += 2)
            {
                if (starting_locations[starting_location_i] == -1){
                    return false;
                }

                for (int minnow_i = 0; minnow_i < game_config.minnow_list.number; minnow_i++)
                {
                    if (this.used_minnow[minnow_i + player_to_move * game_config.minnow_list.number])
                        continue;

                    for (int corner_list_i = 0; corner_list_i < game_config.minnow_list.minnows[minnow_i].number_of_transforms; corner_list_i++)
                    {

                        for (int tile_i = 0; tile_i < game_config.minnow_list.minnows[minnow_i].number_of_corners; tile_i++)
                        {

                            MoveStruct proposed_move = new
                                (starting_locations[starting_location_i], starting_locations[starting_location_i + 1],
                                minnow_i, corner_list_i,
                                game_config.minnow_list.minnows[minnow_i].list_of_corners[corner_list_i, tile_i, 0],
                                game_config.minnow_list.minnows[minnow_i].list_of_corners[corner_list_i, tile_i, 1]
                                );

                            if (isMoveLegal(proposed_move))
                            {
                                return true;
                            }

                        }

                    }
                }

            }
            return false;
        }

        public void applyMove(MoveStruct move)
        {

            int board_x = 0;
            int board_y = 0;

            for (int i = 0; i < this.game_config.minnow_list.minnows[move.minnow_index].tile_length; i++)
            {
                board_x = move.x - move.shiftX;
                board_x += this.game_config.minnow_list.minnows[move.minnow_index].
                    list_of_transforms[move.minnow_transform_index, i, 0];
                board_y = move.y - move.shiftY;
                board_y += this.game_config.minnow_list.minnows[move.minnow_index].
                    list_of_transforms[move.minnow_transform_index, i, 1];

                this.board[board_x + board_y * this.game_config.board_size] = this.player_to_move + 1;

            }
            this.used_minnow[move.minnow_index + player_to_move * this.game_config.minnow_list.number] = true;

            this.played_first_move[this.player_to_move] = true;
            togglePlayerNumber();
        }

        public void _printBoard()
        {
            string _out = "";

            for (int j = this.game_config.board_size - 1; j >= 0; j--)
            {
                for (int i = 0; i < this.game_config.board_size; i++) { 

                    if (this.board[i + j * this.game_config.board_size] == 0)
                    {
                        _out += " ` ";
                    }
                    else
                    {
                        _out += $" {this.board[i + j * this.game_config.board_size]} ";

                    }


                }
                _out += "\n";
            }

            Console.Write( _out );

        }
    
    }

}
