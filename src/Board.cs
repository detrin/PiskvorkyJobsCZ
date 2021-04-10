using System;

namespace PiskvorkyJobsCZ
{
    public class Board
    {
        public int SizeX { get; set; }
        public int SizeY { get; set; }
        public int StartX { get; set; }
        public int StartY { get; set; }
        private int RelativeX { get; set; }
        private int RelativeY { get; set; }
        public bool BoardIsEmpty { get; set; }

        public int[,] BoardData { get; set; }
        // 0 - not set
        // 1 - me
        // 2 - the other player

        public char[] BoardDataString { get; set; }

        public Board()
        {
            SizeX = 58; SizeY = 40;
            StartX = -29; StartY = -19;
            InitializeBoard();
        }

        public Board(int sizeX, int sizeY)
        {
            SizeX = sizeX; SizeY = sizeY;
            StartX = -29; StartY = -19;
            InitializeBoard();
        }

        public Board(int sizeX, int sizeY, int startX, int startY)
        {
            SizeX = sizeX; SizeY = sizeY;
            StartX = startX; StartY = startY;
            InitializeBoard();
        }

        private void InitializeBoard()
        {
            BoardData = new int[SizeX, SizeY];
            BoardDataString = new char[SizeX * SizeY];
            for (int i = 0; i < SizeX; i++)
            {
                for (int j = 0; j < SizeY; j++)
                {
                    BoardData[i, j] = 0;
                    BoardDataString[i + j * SizeX] = '0';
                }
            }
            BoardIsEmpty = true;
        }

        private static char itoa(int num)
        {
            switch (num)
            {
                case 0:
                    return '0';
                case 1:
                    return '1';
                case 2:
                    return '2';
                default:
                    return '0';
;            }
        }

        public void Move(int player, int X, int Y, bool relative=true)
        {
            // relative true  - (-n, n)
            // relative false - (0, 2n+1)
            if (relative)
            {
                RelativeX = X-StartX; RelativeY = Y-StartY;
            } else {
                RelativeX = X; RelativeY = Y;
            }
            // Console.WriteLine($"{RelativeX} {RelativeY}");
            
            if (BoardData[RelativeX, RelativeY] != 0)
                throw new InvalidMoveException();

            BoardData[RelativeX, RelativeY] = player;
            BoardDataString[RelativeX + RelativeY * SizeX] = itoa(player);

            if (BoardIsEmpty) BoardIsEmpty = false;
        }

        public string BoardToString()
        {
            return new string(BoardDataString);
        }

        public void PrintBoard(int offset=0)
        {
            char placeChar = '_';
            for (int j = offset; j < SizeY-offset; j++)
            {
                for (int i = offset; i < SizeX-offset; i++)
                {
                    placeChar = itoa(BoardData[i, j]);
                    if (placeChar == '0')
                    {
                        Console.Write(" .");
                    } else {
                        Console.Write(" "+placeChar);
                    }
                }
                Console.WriteLine();
            }
        }

    }
}