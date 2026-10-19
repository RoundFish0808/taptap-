using System;
using System.Collections.Generic;

namespace ReverseDefense
{
    public static class FixedLoopPath
    {
        // 起点为左下角。每个格子只加入一次，最后一格的下一步是起点。
        public static List<Cell> Create(int columns, int rows, int left, int bottom, int right, int top)
        {
            if (columns < 2 || rows < 2 || left < 0 || bottom < 0 ||
                right >= columns || top >= rows || left >= right || bottom >= top)
                throw new ArgumentException("道路必须在网格内，且左右、上下边界不能重合。");

            var path = new List<Cell>();
            for (int y = bottom; y <= top; y++) path.Add(new Cell(left, y));
            for (int x = left + 1; x <= right; x++) path.Add(new Cell(x, top));
            for (int y = top - 1; y >= bottom; y--) path.Add(new Cell(right, y));
            for (int x = right - 1; x > left; x--) path.Add(new Cell(x, bottom));
            return path;
        }
    }
}
