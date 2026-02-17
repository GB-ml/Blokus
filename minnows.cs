using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GlokusSharp
{

    public struct Coord
    {
        public int X; public int Y;

        public Coord(int x, int y)
        {
            this.X = x;
            this.Y = y;
        }
    }

    internal class Minnow
    {
        public int tile_length = 0;
        public int number_of_transforms = 0;
        public int number_of_corners = 0;
        public int[,,] list_of_transforms;
        public int[,,] list_of_corners;

        public Minnow(int[][] coord_list) 
        {
            CompileTransformsAndCorners(coord_list);
            
        }

        private void CompileTransformsAndCorners(int[][] coord_list) 
        {
            List<List<Coord>> temp_list = new();

            List<Coord> temp = new();
            for (int i = 0; i < coord_list.GetLength(0); i++)
            {
                Coord next_coord = new(coord_list[i][0], coord_list[i][1]);
                temp.Add(next_coord);
            }
            temp_list.Add(temp);
           
            for(int i = 0; i < 3; i++)
            {
                temp = Rotate90(temp);
                if (IsThisUniqueTransform(temp, temp_list))
                    temp_list.Add(temp);
            }

            temp = FlipHorizontal(temp);
            if (IsThisUniqueTransform(temp, temp_list))
                temp_list.Add(temp);

            for (int i = 0; i < 3; i++)
            {
                temp = Rotate90(temp);
                if (IsThisUniqueTransform(temp, temp_list))
                    temp_list.Add(temp);
            }

            this.number_of_transforms = temp_list.Count;
            this.tile_length = temp_list[0].Count;
            this.list_of_transforms = new int[this.number_of_transforms, this.tile_length, 2];

            for(int i = 0; i < this.number_of_transforms; i++)
            {
                for(int j = 0; j < this.tile_length; j++)
                {
                    this.list_of_transforms[i, j, 0] = temp_list[i][j].X;
                    this.list_of_transforms[i, j, 1] = temp_list[i][j].Y;
                }
            }

            List<List<Coord>> temp_corner_list = new();
            for (int i = 0; i < this.number_of_transforms; i++)
            {
                List<Coord> temp_corner = new();
                foreach (Coord coord in temp_list[i])
                {

                    Coord above = new(coord.X, coord.Y + 1);
                    Coord below = new(coord.X, coord.Y - 1);
                    if (temp_list[i].Contains(above) && temp_list[i].Contains(below))
                    {
                        continue;
                    }

                    Coord left = new(coord.X - 1, coord.Y);
                    Coord right = new(coord.X + 1, coord.Y);
                    if (temp_list[i].Contains(left) && temp_list[i].Contains(right))
                    {
                        continue;
                    }

                    temp_corner.Add(coord);
                }
                temp_corner_list.Add(temp_corner);
            }

            this.number_of_corners = temp_corner_list[0].Count;
            this.list_of_corners = new int[this.number_of_transforms, this.number_of_corners, 2];

            for (int i = 0; i < this.number_of_transforms; i++)
            {
                for (int j = 0; j < this.number_of_corners; j++)
                {
                    this.list_of_corners[i, j, 0] = temp_corner_list[i][j].X;
                    this.list_of_corners[i, j, 1] = temp_corner_list[i][j].Y;
                }
            }

        }

        private static bool IsThisUniqueTransform(List<Coord> coord_list, List<List<Coord>> existing_list)
        {
            foreach(List<Coord> existing_transform in existing_list) 
            {
                bool completelyIdentical = true;
                foreach (Coord coord in coord_list)
                {
                    if (!existing_transform.Contains(coord))
                    {
                        completelyIdentical = false;
                        break;
                    }
                }
                if (completelyIdentical)
                    return false;
            }
            return true;
        }

        private static List<Coord> Rotate90(List<Coord> coord_list)
        {
            List<Coord> new_list = new();
            foreach (Coord coord in coord_list)
            {
                new_list.Add(new Coord(coord.Y, - coord.X));
            }

            return CenterAtZero(new_list);
        }

        private static List<Coord> FlipHorizontal(List<Coord> coord_list)
        {
            List<Coord> new_list = new();
            foreach (Coord coord in coord_list)
            {
                new_list.Add(new Coord(-coord.X, coord.Y));
            }

            return CenterAtZero(new_list);
        }

        private static List<Coord> CenterAtZero(List<Coord> coord_list)
        {
            int x_min = 999;
            int y_min = 999;

            foreach (Coord coord in coord_list)
            {
                if (coord.X < x_min)
                    x_min = coord.X;
                if (coord.Y < y_min)
                    y_min = coord.Y;
            }

            List<Coord> new_list = new();
            foreach (Coord coord in coord_list)
            {
                new_list.Add(new Coord(coord.X - x_min, coord.Y - y_min));
            }

            return new_list;
        }

        static public readonly int[][][] one_two_minnow = new int[][][]{

            new int[][] { new int [] {0, 0} },
            new int[][] { new int [] {0, 0}, new int [] {0, 1} },

        };
        static public readonly int[][][] three_minnow = new int[][][]{

            new int[][] { new int [] {0, 0}, new int [] {0, 1}, new int [] {0, 2} },
            new int[][] { new int [] {0, 0}, new int [] {0, 1}, new int [] {1, 0} },

        };
        static public readonly int[][][] four_minnow = new int[][][]{

            // Index 5
            new int[][] { new int [] {0, 0}, new int [] {0, 1}, new int [] {0, 2}, new int [] {0, 3} }, // 4x1 line 
            new int[][] { new int [] {0, 0}, new int [] {0, 1}, new int [] {1, 1}, new int [] {2, 1} }, // L
            new int[][] { new int [] {0, 0}, new int [] {0, 1}, new int [] {0, 2}, new int [] {1, 1} }, // T
            new int[][] { new int [] {0, 0}, new int [] {1, 0}, new int [] {1, 1}, new int [] {2, 1} }, // zigzag
            new int[][] { new int [] {0, 0}, new int [] {0, 1}, new int [] {1, 1}, new int [] {1, 0} }, // square

        };
        static public readonly int[][][] five_minnow = new int[][][]{

            // Index 9
            new int[][] { new int [] {0, 0}, new int [] {0, 1}, new int [] {0, 2}, new int [] {0, 3}, new int [] {0, 4} }, // I = 5x1 line 
            new int[][] { new int [] {0, 0}, new int [] {0, 1}, new int [] {0, 2}, new int [] {0, 3}, new int [] {1, 0} }, // L
            new int[][] { new int [] {0, 0}, new int [] {0, 1}, new int [] {0, 2}, new int [] {0, 3}, new int [] {1, 1} }, // Y
            new int[][] { new int [] {0, 0}, new int [] {0, 1}, new int [] {0, 2}, new int [] {1, 0}, new int [] {1, 1} }, // P
            new int[][] { new int [] {0, 0}, new int [] {0, 1}, new int [] {0, 2}, new int [] {1, 0}, new int [] {1, 2} }, // U or C
            new int[][] { new int [] {0, 0}, new int [] {0, 1}, new int [] {0, 2}, new int [] {1, 2}, new int [] {1, 3} }, // N
            new int[][] { new int [] {0, 1}, new int [] {1, 0}, new int [] {1, 1}, new int [] {1, 2}, new int [] {2, 2} }, // F
            new int[][] { new int [] {0, 0}, new int [] {0, 1}, new int [] {0, 2}, new int [] {1, 1}, new int [] {2, 1} }, // T
            new int[][] { new int [] {0, 0}, new int [] {0, 1}, new int [] {0, 2}, new int [] {1, 0}, new int [] {2, 0} }, // V or big L
            new int[][] { new int [] {0, 0}, new int [] {1, 0}, new int [] {1, 1}, new int [] {2, 1}, new int [] {2, 2} }, // W
            new int[][] { new int [] {0, 1}, new int [] {1, 0}, new int [] {1, 1}, new int [] {1, 2}, new int [] {2, 1} }, // X
            new int[][] { new int [] {0, 0}, new int [] {1, 0}, new int [] {1, 1}, new int [] {1, 2}, new int [] {2, 2} }, // Z

        };

        static public readonly string five_minnow_order = "ILYPUNFTVWXZ";
    }

    internal class MinnowList
    {
        public Minnow[] minnows;
        public int number = 0;

        public MinnowList(int minnow_rank, string additional_5th_rank = "")
        {
            List<Minnow> temp_minnows = new List<Minnow>();

            AddMinnows(Minnow.one_two_minnow, temp_minnows);
            AddMinnows(Minnow.three_minnow, temp_minnows);
            if (minnow_rank >= 4)
                AddMinnows(Minnow.four_minnow, temp_minnows);
            if (minnow_rank >= 5)
                AddMinnows(Minnow.five_minnow, temp_minnows);

            if (additional_5th_rank.Length > 0)
            {
                foreach (char ch in additional_5th_rank)
                {
                    int idx = Minnow.five_minnow_order.IndexOf(ch);
                    AddMinnows([Minnow.five_minnow[idx]], temp_minnows);
                }
            }

            minnows = temp_minnows.ToArray();
        }

        public void AddMinnows(int[][][] minnow_coords, List<Minnow> temp_minnows)
        {

            for(int i = 0; i < minnow_coords.Length; i++)
            {
                Minnow temp = new(minnow_coords[i]);
                temp_minnows.Add(temp);
                number++;
            }

        }
    }
}
