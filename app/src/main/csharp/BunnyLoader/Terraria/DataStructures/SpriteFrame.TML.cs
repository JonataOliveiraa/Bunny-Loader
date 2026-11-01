namespace Terraria.DataStructures;

public partial struct SpriteFrame
{
    public SpriteFrame(byte columns, byte rows, byte currentColumn, byte currentRow)
    {
        ColumnCount = columns;
        RowCount = rows;
        _currentColumn = currentColumn;
        _currentRow = currentRow;
        PaddingX = 2;
        PaddingY = 2;
    }

    public SpriteFrame(byte columns, byte rows) : this(columns, rows, 0, 0)
    {
    }
}
