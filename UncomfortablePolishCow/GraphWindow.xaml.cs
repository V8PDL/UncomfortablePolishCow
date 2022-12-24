using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using QuickGraph;

namespace UncomfortablePolishCow
{
    /// <summary>
    /// Interaction logic for GraphWindow.xaml
    /// </summary>
    public partial class GraphWindow : Window
    {
        private readonly Stack<Vertex> vertexStack = new();
        private readonly List<Vertex> activeVertexes = new();
        private readonly BidirectionalGraph<object, IEdge<object>> graph = new();

        private readonly SolidColorBrush[] brushes = new SolidColorBrush[]
        {
            Brushes.AliceBlue,
            Brushes.OrangeRed,
            Brushes.BlueViolet,
            Brushes.Coral,
            Brushes.Gray,
            Brushes.Cyan,
            Brushes.Green,
            Brushes.Maroon,
            Brushes.Navy,
            Brushes.SandyBrown,
            Brushes.Tomato,
            Brushes.Yellow,
        };

        public GraphWindow()
        {
            InitializeComponent();
        }

        public void CreateGraph(Queue<Cell> cells)
        {
            this.vertexStack.Clear();
            this.graph.Clear();
            this.activeVertexes.Clear();

            while (cells.Any())
            {
                var cell = cells.Dequeue();
                switch (cell.Type)
                {
                    case SymbolTypes.Number:
                        var numberVertex = new Vertex(cell.Value, 0);
                        this.vertexStack.Push(numberVertex);
                        this.graph.AddVertex(numberVertex);
                        this.activeVertexes.Add(numberVertex);
                        break;
                    case SymbolTypes.Operator:
                        if (this.vertexStack.Count > 1)
                        {
                            var operatorVertex = new Vertex(cell.Value);
                            this.graph.AddVertex(operatorVertex);
                            this.graph.AddEdge(new Edge<object>(this.vertexStack.Pop(), operatorVertex));
                            this.graph.AddEdge(new Edge<object>(this.vertexStack.Pop(), operatorVertex));
                            this.vertexStack.Push(operatorVertex);
                            this.graph.AddVertex(operatorVertex);
                        }
                        else
                        {
                            this.Dispatcher.Invoke(() => MessageBox.Show("Not enough operands."));
                            return;
                        }
                        break;
                    case SymbolTypes.BracketOpen:
                    case SymbolTypes.BracketClose:
                        this.Dispatcher.Invoke(() => MessageBox.Show("Parenthesis in output: something wrong."));
                        return;
                }
            }

            this.ColorGraph();

            this.GraphLayout.Graph = this.graph;
        }

        private void ColorGraph()
        {
            var coloredVertexes = new List<Vertex>(this.activeVertexes);
            int order = 1;
            var colorIndex = 0;

            while (coloredVertexes.Count < this.graph.VertexCount)
            {
                var handledVertexes = new List<Vertex>();
                var newVertexes = new List<Vertex>();
                foreach (var vertex in this.activeVertexes)
                {
                    if (!this.graph.TryGetOutEdges(vertex, out var outEdges))
                    {
                        continue;
                    }

                    var subVertexes = outEdges.Select(e => e.Target as Vertex);
                    if (subVertexes.All(s => s is Vertex v && s.Order.HasValue))
                    {
                        handledVertexes.Add(vertex);
                        continue;
                    }

                    foreach (var subVertex in subVertexes)
                    {
                        if (!this.graph.TryGetInEdges(subVertex, out var inEdges))
                        {
                            continue;
                        }
                        var sourceVertexes = inEdges.Select(e => e.Source);
                        if (sourceVertexes.All(s => s is Vertex v && v.Order.HasValue))
                        {
                            subVertex.Order = order;
                            subVertex.Brush = this.brushes[colorIndex];
                            coloredVertexes.Add(subVertex);
                            newVertexes.Add(subVertex);
                        }
                    }
                }
                this.activeVertexes.AddRange(newVertexes);
                handledVertexes.ForEach(v => this.activeVertexes.Remove(v));
                order++;
                colorIndex = (colorIndex + 1) % this.brushes.Length;
            }
        }
    }
}