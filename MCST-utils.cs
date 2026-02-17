using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GlokusSharp
{
    internal partial class MCST
    {
        public void printChildren(GameStateNode node)
        {

            Console.WriteLine($"current value: {node.value:F2} total visits: {node.visits}");

            if (node.children == null || node.children.Length == 0)
            {
                Console.WriteLine("no children ... ");
                return;
            }

            Console.WriteLine("Index\tValue\tVisits\t\tUCT\t\tCorresponding Move");

            for (int i = 0; i < node.children.Length; i++)
            {

                if (node.children[i] == null) continue;

                String value = $" {node.children[i].value:F1}";
                if (node.children[i].value < 0)
                {
                    value = $"{node.children[i].value:F1}";
                }
                if (node.children[i].is_terminal_or_forcing)
                {
                    value += "T";
                }
                if (node.children[i].has_terminal_or_forcing_children)
                {
                    value += "C";
                }

                String uct = $" {node.children[i].most_recent_UCT:F1}";
                if (node.children[i].most_recent_UCT < 0)
                {
                    uct = $"{node.children[i].most_recent_UCT:F1}";
                }

                MoveStruct ms = node.children[i].previous_move;
                String move_struct = $"\t\t[{ms.minnow_index} {ms.minnow_transform_index} {ms.x} {ms.y} {ms.shiftX} {ms.shiftY}]";

                Console.WriteLine($"{i}\t" + value + $"\t{node.children[i].visits}\t\t" + uct + move_struct);

            }
        }

        public void SelectMove(int move_index)
        {
            if (move_index < 0) return;
            if (move_index > working_node.possible_moves.Count()) return;
            working_node = working_node.children[move_index];
            VisitedNodes.Add(working_node);
        }

        public void _SelectMovesBulk(int[] moveArray)
        {
            foreach (int move in moveArray)
            {
                mcst_selector.PopulateAllChildren(working_node);
                SelectMove(move);
            }
        }

        public double GetScore(GameStateNode node)
        {
            // defined so that positive means that player 1 (0) did better than player 2 (1)
            double player1score = node.game_state.getScore(0);
            double player2score = node.game_state.getScore(1);
            return player2score - player1score; // high playerscores are bad (indicates unused pieces), but positive gamescore means player 1 won
        }

        public string ConvertTreeToText()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"|{this.root_node.value:F3}|{this.root_node.visits}\n");

            if (this.root_node.children != null && this.root_node.children.Length > 0)
            {
                foreach (GameStateNode child in this.root_node.children)
                {
                    string result = ConvertNodeToText(child, "");
                    if (!string.IsNullOrEmpty(result))
                    {
                        sb.Append(result);
                    }
                }
            }

            return sb.ToString();
        }

        public string ConvertNodeToText(GameStateNode node, string family_history)
        {
            if (node.visits == 0) return "";

            string nextLocation = $"{node._index_in_parent},";
            if (family_history == "")
            {
                nextLocation = nextLocation.Remove(nextLocation.Length - 1);
            }
            // Note: the list is created "backwards" to enable efficient popping from end of list as one traverses
            family_history = nextLocation + family_history;

            StringBuilder sb = new StringBuilder();
            sb.Append($"{family_history}|{node.value:F3}|{node.visits}\n");

            if (node.children != null && node.children.Length > 0)
            {
                foreach (GameStateNode child in node.children)
                {
                    string result = ConvertNodeToText(child, family_history);
                    if (!string.IsNullOrEmpty(result))
                    {
                        sb.Append(result);
                    }
                }
            }

            return sb.ToString();
        }

        public void ReadFromFile(string path)
        {
            try
            {
                IEnumerable<string> inFile = File.ReadLines(path);

                foreach (string line in inFile)
                {
                    string[] values = line.Split('|');

                    if (values.Length != 3)
                    {
                        Console.WriteLine($"Error parsing line: {line}");
                        continue;
                    }

                    double cur_value = Convert.ToDouble(values[1]);
                    int num_visits = Convert.ToInt32(values[2]);

                    if (string.IsNullOrEmpty(values[0]))
                    {
                        this.root_node.value = cur_value;
                        this.root_node.visits = num_visits;
                        continue;
                    }

                    string[] treeCoordinatesString = values[0].Split(',');
                    List<int> treeCoordinatesInt = treeCoordinatesString.Select(int.Parse).ToList();
                    UpdateNodeFromFile(this.root_node, treeCoordinatesInt, cur_value, num_visits);

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

        }

        public void UpdateNodeFromFile(GameStateNode node, List<int> coords, double cur_value, int num_visits)
        {

            if (coords.Count == 0)
            {
                node.value = cur_value;
                node.visits = num_visits;
                return;
            }

            int nextChild = coords[coords.Count - 1];
            coords.RemoveAt(coords.Count - 1);

            /*
            if (node.children == null || node.children.Length == 0)
            {
                node.PopulateAllChildren();
            }
            */

            node.CreateChild(nextChild);
            UpdateNodeFromFile(node.children[nextChild], coords, cur_value, num_visits);

        }

        public void SaveToDisk(string id)
        {
            string result = ConvertTreeToText();
            string file_name = @"C:\tmp\glokus_" + id + "_.txt";

            try
            {
                File.WriteAllText(file_name, result);
            }
            catch (Exception e)
            {
                Console.WriteLine($"An error occurred: {e.Message}");
            }
        }

        public void SaveToDiskForNN(string id)
        {
            string result = ConvertTreeToNNText();
            string file_name = @"C:\tmp\glokusNN_" + id + ".txt";

            try
            {
                File.AppendAllText(file_name, result);
            }
            catch (Exception e)
            {
                Console.WriteLine($"An error occurred: {e.Message}");
            }

        }

        public string ConvertTreeToNNText()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"{GetScore(working_node)} ");

            for (int i = 0; i < VisitedNodes.Count; i++)
            {
                if (i > 0) sb.Append('|');
                sb.Append(VisitedNodes[i].previous_move.ToString());
            }
            sb.Append('\n');
            return sb.ToString();
        }
    }
}
