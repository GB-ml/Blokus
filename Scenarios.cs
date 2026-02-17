using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace GlokusSharp
{
    internal class Scenarios
    {

        public static void SimpleGeneration(MCST mcst)
        {
            while (true)
            {
                mcst.mcst_selector.PopulateAllChildren(mcst.working_node);
                if (mcst.working_node.possible_moves.Length == 0)
                {
                    break;
                }

                mcst.SelectMove(mcst.mcst_selector.SelectRolloutMove(mcst.working_node));
            }

        }

        public static void SmartGeneration8x8(MCST mcst)
        {
            
            while (true)
            {
                mcst.mcst_selector.PopulateAllChildren(mcst.working_node);
                mcst.VisitEachChildNTimesThreaded(mcst.working_node, 16);
                mcst.RunSearchTreeThreaded(mcst.working_node, 10000 - mcst.working_node.visits);
                //mcst.working_node.game_state._printBoard();
                //mcst.printChildren(mcst.working_node);
                MoveStruct moveStruct = mcst.SelectMoveByAlgo();
                // Console.WriteLine($"Player {(1 - mcst.working_node.game_state.player_to_move) + 1} selected [{moveStruct}]");
                // Console.ReadKey(true);

                if (mcst.working_node.possible_moves.Length == 0)
                {
                    break;
                }

            }

            //mcst.working_node.game_state._printBoard();
            //Console.WriteLine($"Game over. Final score: {mcst.GetScore(mcst.working_node)}");
            //Console.ReadKey(true);
        }
    }
}
