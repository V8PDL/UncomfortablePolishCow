using System.Windows.Media;

namespace UncomfortablePolishCow
{
    public class Cell
    {
        public string Value { get; set; }
        public SymbolTypes Type { get; set; }
        public Cell() => Value = string.Empty;
        public Cell(string val) => Value = val;
        public Cell(char val) => Value = val.ToString();
        public Cell(string val, SymbolTypes type)
        {
            this.Value = val;
            this.Type = type;
        }
        public Cell(char val, SymbolTypes type)
        {
            this.Value = val.ToString();
            this.Type = type;
        }
        public override string ToString() => Value;
    }

    public class Vertex
    {
        public string Value { get; set; }
        public SolidColorBrush Brush { get; set; }
        public int? Order { get; set; }
        public Vertex(string value, int? order = null)
        {
            this.Value = value;
            this.Brush = null;
            this.Order = order;
        }
    }
}