using System;

/*
 * Индексы кораблей:
 * 0000
 * 111
 * 222
 * 33
 * 44
 * 55
 * 6
 * 7
 * 8
 * 9
 * +10 для подбитого
 * 
 * 253 мимо
 * 254 Заблокированная ячейка
 * 255 Пустая ячейка
 */

namespace SeaBattleNextGen
{
    /// <summary>
    /// Класс карты
    /// </summary>
    public class Map
    {
        /// <summary>
        /// Индекс пустой ячейки
        /// </summary>
        public const int EmptyField = 255;

        /// <summary>
        /// Индекс блокированной ячейки
        /// Например, возле ячейки корабля
        /// </summary>
        public const int BlockedField = 254;

        /// <summary>
        /// Индекс ячейки промаха
        /// </summary>
        public const int MissField = 253;
        
        private byte[,] array;

        /// <summary>
        /// Ширина поля
        /// </summary>
        public int Width = 10;

        /// <summary>
        /// Высота поля
        /// </summary>
        public int Height = 10;

        /// <summary>
        /// Доступ по координатам
        /// </summary>
        public byte this[int x, int y]
        {
            get
            {
                return array[x, y];
            }
            set
            {
                array[x, y] = value;
            }
        }

        /// <summary>
        /// Пустая карта
        /// </summary>
        public Map()
        {
            array = new byte[Width, Height];
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    array[x, y] = EmptyField;
                }
            }
        }

        /// <summary>
        /// Конструктор для создания из строки
        /// </summary>
        public Map(string mapInfo)
        {
            array = new byte[Width, Height];
            var temp = mapInfo.Split('|');
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    array[x, y] = Convert.ToByte(temp[y * 10 + x]);
                }
            }
        }

        /// <summary>
        /// Является ли индекс ячейки индексом корабля (целого)
        /// </summary>
        public static bool IsShipIndex(int index)
        {
            return index >= 0 && index <= 9;
        }

        /// <summary>
        /// Является ли индекс ячейки индексом разбитого корабля
        /// </summary>
        public static bool IsKilledShipIndex(int index)
        {
            return index >= 10 && index <= 19;
        }

        /// <summary>
        /// Можно ли выстрелить в указанную точку
        /// </summary>
        public bool CanBeShoted(int x, int y)
        {
            var index = this[x, y];
            // Можем выстрелить по 0-9, 20-252, 254-255
            return !IsKilledShipIndex(index)
                && index != MissField;
        }

        /// <summary>
        /// Выстрел в указаную точку
        /// </summary>
        public bool Shot(int x, int y)
        {
            if (!CanBeShoted(x, y))
                return false;

            if (this[x, y] < 10)
            {
                this[x, y] += 10;
                MissAroundKilledShip(this[x, y]);

                // Если попали, то сохраняем ход
                return true;
            }
            else
            {
                this[x, y] = MissField;

                // Если попали, то сохраняем ход
                return false;
            }
        }
        
        /// <summary>
        /// Уничтожен ли корабль с данныи индексу
        /// </summary>
        public bool IsShipKilled(int shipIndex)
        {
            if (IsKilledShipIndex(shipIndex))
                shipIndex -= 10;

            return GetShipPartsCount(shipIndex) == 0;
        }

        /// <summary>
        /// Есть ли хоть один корабль
        /// </summary>
        public bool HasAnyShip()
        {
            for (int index = 0; index < 10; index++)
                if (!IsShipKilled(index))
                    return true;
            return false;
        }

        /// <summary>
        /// Находит разбитый корабль и окружает его промахами
        /// </summary>
        private void MissAroundKilledShip(int index)
        {
            if (!IsShipKilled(index))
                return;

            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    if (this[x, y] == index)
                        MissAround(x, y);
        }

        /// <summary>
        /// Окружает координату промахами, где это возможно
        /// </summary>
        private void MissAround(int x, int y)
        {
            var hasLeft = x - 1 >= 0;
            var hasRight = x + 1 < array.GetLength(0);
            var hasTop = y + 1 < array.GetLength(1);
            var hasBottom = y - 1 >= 0;

            if (hasLeft)
            {
                if (hasTop && this[x - 1, y + 1] >= BlockedField)
                    this[x - 1, y + 1] = MissField;

                if (hasBottom && this[x - 1, y - 1] >= BlockedField)
                    this[x - 1, y - 1] = MissField;

                if (this[x - 1, y] >= BlockedField)
                    this[x - 1, y] = MissField;
            }
            if (hasRight)
            {
                if (hasTop && this[x + 1, y + 1] >= BlockedField)
                    this[x + 1, y + 1] = MissField;

                if (hasBottom && this[x + 1, y - 1] >= BlockedField)
                    this[x + 1, y - 1] = MissField;

                if (this[x + 1, y] >= BlockedField)
                    this[x + 1, y] = MissField;
            }
            if (hasTop && this[x, y + 1] >= BlockedField)
                this[x, y + 1] = MissField;
            if (hasBottom && this[x, y - 1] >= BlockedField)
                this[x, y - 1] = MissField;
        }

        /// <summary>
        /// Возвращает число выставленных ячеек корабля по индексу
        /// </summary>
        public int GetShipPartsCount(int shipIndex)
        {
            int num = 0;
            for (int i = 0; i < 10; i++)
                for (int j = 0; j < 10; j++)
                    if (this[i, j] == shipIndex)
                        num++;
            return num;
        }

        /// <summary>
        /// Максимальный размер корабля по индексу
        /// </summary>
        public int MaxShipSizeByIndex(int shipIndex)
        {
            var ar = new[] { 4, 3, 3, 2, 2, 2, 1, 1, 1, 1 };
            if (shipIndex > 9)
                shipIndex -= 10;
            if (!IsShipIndex(shipIndex))
                return 0;
            return ar[shipIndex];
        }

        /// <summary>
        /// Выставлен ли корабль полностью
        /// </summary>
        public bool IsShipSetted(int shipIndex)
        {
            var ar = new[] { 4, 3, 3, 2, 2, 2, 1, 1, 1, 1 };
            return IsShipIndex(shipIndex)
                && GetShipPartsCount(shipIndex) == ar[shipIndex];
        }

        /// <summary>
        /// Находится ли рядом ячейка с определенным индексом
        /// </summary>
        public bool IsNear(int x, int y, int type)
        {
            var hasLeft = x - 1 >= 0;
            var hasRight = x + 1 < array.GetLength(0);
            var hasTop = y + 1 < array.GetLength(1);
            var hasBottom = y - 1 >= 0;

            if (hasLeft)
            {
                if ((hasTop && this[x - 1, y + 1] == type)
                    || (hasBottom && this[x - 1, y - 1] == type)
                    || this[x - 1, y] == type)
                    return true;
            }
            if (hasRight)
            {
                if ((hasTop && this[x + 1, y + 1] == type)
                    || (hasBottom && this[x + 1, y - 1] == type)
                    || this[x + 1, y] == type)
                    return true;
            }
            if (hasTop && this[x, y + 1] == type)
                return true;
            if (hasBottom && this[x, y - 1] == type)
                return true;

            return false;
        }

        /// <summary>
        /// Возвращет, есть ли место по вертикале и горизонтале для размешения корабля
        /// </summary>
        public bool HasPlaceForShip(int x, int y, int typeIndex)
        {
            int neededSite = MaxShipSizeByIndex(typeIndex);

            int w = 0, h = 0;
            for (int i = x; i < Width && (this[i, y] == EmptyField || this[i, y] == typeIndex); i++)
                w++;
            for (int i = x - 1; i >= 0 && (this[i, y] == EmptyField || this[i, y] == typeIndex); i--)
                w++;

            if (w >= neededSite)
                return true;

            for (int i = y; i < Height && (this[x, i] == EmptyField || this[x, i] == typeIndex); i++)
                h++;
            for (int i = y - 1; i >= 0 && (this[x, i] == EmptyField || this[x, i] == typeIndex); i--)
                h++;

            return h >= neededSite;
        }
        /// <summary>
        /// Находит корабль и блокирует все ячейки вокруг него
        /// </summary>
        public void BlockShipNeighbors(int index)
        {
            if (!IsShipSetted(index))
                return;

            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    if (this[x, y] == index)
                        BlockNeighbors(x, y);
        }

        /// <summary>
        /// Блокирует поля возле кораблей по ходу их расстановки
        /// </summary>
        /// <param name="x">X координата</param>
        /// <param name="y">Y координата</param>
        public void BlockNeighbors(int x, int y)
        {
            var hasLeft = x - 1 >= 0;
            var hasRight = x + 1 < array.GetLength(0);
            var hasBottom = y + 1 < array.GetLength(1);
            var hasTop = y - 1 >= 0;

            var index = this[x, y];

            //  [1] [2] [3]
            //  [4] [x] [6]
            //  [7] [8] [9]

            // Блокировка по диагоналям - [1] [3] [7] [9]
            if (hasLeft)
            {
                if (hasBottom && this[x - 1, y + 1] == EmptyField)
                    this[x - 1, y + 1] = BlockedField;

                if (hasTop && this[x - 1, y - 1] == EmptyField)
                    this[x - 1, y - 1] = BlockedField;
            }
            if (hasRight)
            {
                if (hasBottom && this[x + 1, y + 1] == EmptyField)
                    this[x + 1, y + 1] = BlockedField;

                if (hasTop && this[x + 1, y - 1] == EmptyField)
                    this[x + 1, y - 1] = BlockedField;
            }

            // Если корабль выстроен полностью, то и по горизонталям/вертикалям
            if (IsShipSetted(index))
            {
                if (hasLeft && this[x - 1, y] == EmptyField)
                    this[x - 1, y] = BlockedField;
                
                if (hasBottom && this[x, y + 1] == EmptyField)
                    this[x, y + 1] = BlockedField;
                
                if (hasTop && this[x, y - 1] == EmptyField)
                    this[x, y - 1] = BlockedField;
                
                if (hasRight && this[x + 1, y] == EmptyField)
                    this[x + 1, y] = BlockedField;
            }
        }

        /// <summary>
        /// Выводит карту в виде строки
        /// </summary>
        public override string ToString()
        {
            var data = string.Empty;

            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    data += this[x, y] + "|";

            return data.Trim('|');
        }
    }
}