using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace UncomfortablePolishCow
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Dictionary<string, int> operatorsPriority = new Dictionary<string, int>()
        {
            ["("] = -1,
            [")"] = 0,
            ["-"] = 1,
            ["+"] = 1,
            ["*"] = 2,
            ["/"] = 2,
            ["^"] = 3,
        };

        private Dictionary<string, Func<Cell, Cell, Cell>> operatorsFuncs = new Dictionary<string, Func<Cell, Cell, Cell>>()
        {
            ["-"] = (cell1, cell2) => new Cell((int.Parse(cell1.Value) - int.Parse(cell2.Value)).ToString()),
            ["+"] = (cell1, cell2) => new Cell((int.Parse(cell1.Value) + int.Parse(cell2.Value)).ToString()),
            ["*"] = (cell1, cell2) => new Cell((int.Parse(cell1.Value) * int.Parse(cell2.Value)).ToString()),
            ["/"] = (cell1, cell2) => new Cell((int.Parse(cell1.Value) / int.Parse(cell2.Value)).ToString()),
            ["^"] = (cell1, cell2) => new Cell((Math.Pow(int.Parse(cell1.Value), int.Parse(cell2.Value))).ToString())
        };

        public enum SymbolTypes
        {
            Number,
            Operator,
            ParenthesisOpen,
            PrenthesisClose
        }

        private Thread RunThread;

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
        }

        private bool IsRunning = false;

        private Stack<Cell> Stack = new Stack<Cell>();
        private Queue<Cell> InputExpression = new Queue<Cell>();
        private Queue<Cell> OutputExpression = new Queue<Cell>();
        private Stack<Cell> ResultExpression = new Stack<Cell>();

        private static string expression = string.Empty;

        public MainWindow()
        {
            InitializeComponent();
            SpeedSlider.ValueChanged += this.Slider_ValueChanged;
            SpeedSlider.Value = 0.4;
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                this.ParseExpression(expression = this.ExpressionTextBox.Text);
            }
            if (this.RunThread != null && (RunThread.IsAlive || this.RunThread.ThreadState == ThreadState.Running))
            {
                this.RunThread.Abort();
            }
            this.ChangeEnability();
            this.RunThread = new Thread(Go);
            this.RunThread.Start();
        }

        private void ParseButton_Click(object sender, RoutedEventArgs e) => ParseExpression(expression = this.ExpressionTextBox.Text);

        private bool ParseExpression(string currentExpression)
        {
            if (string.IsNullOrWhiteSpace(currentExpression))
            {
                MessageBox.Show("No expression entered.");
                return false;
            }
            string currentValue = string.Empty;
            int parenthesisCounter = 0;
            bool operatorNextIsForbidden = true;
            this.ParsedExpressionGrid.Items.Clear();
            foreach (var symbol in currentExpression)
            {
                if (char.IsWhiteSpace(symbol))
                {
                    continue;
                }
                if (char.IsDigit(symbol))
                {
                    currentValue += symbol;
                    operatorNextIsForbidden = false;
                    continue;
                }

                if (currentValue.Length != 0)
                {
                    this.AddToGui(this.InputExpression, this.ParsedExpressionGrid, new Cell(currentValue, SymbolTypes.Number));
                    currentValue = string.Empty;
                }
                if (operatorsPriority.ContainsKey(symbol.ToString()))
                {
                    var (parenthesisKind, symbolType) = symbol == '(' ? (1, SymbolTypes.ParenthesisOpen) : symbol == ')' ? (-1, SymbolTypes.PrenthesisClose) : (0, SymbolTypes.Operator);
                    parenthesisCounter += parenthesisKind;
                    if (parenthesisCounter < 0)
                    {
                        MessageBox.Show("Wrong parenthesis.");
                        return false;
                    }
                    if (operatorNextIsForbidden && symbolType != SymbolTypes.ParenthesisOpen && this.InputExpression.Last().Type != SymbolTypes.PrenthesisClose)
                    {
                        MessageBox.Show("Two operators should be separated by operand in infix notation.");
                        return false;
                    }
                    this.AddToGui(this.InputExpression, this.ParsedExpressionGrid, new Cell(symbol, symbolType));
                    operatorNextIsForbidden = true;
                }
                else
                {
                    MessageBox.Show("Unknown symbol.");
                    return false;
                }
            }
            if (currentValue.Length != 0)
            {
                this.AddToGui(this.InputExpression, this.ParsedExpressionGrid, new Cell(currentValue, SymbolTypes.Number));
                operatorNextIsForbidden = false;
            }
            if (operatorNextIsForbidden)
            {
                MessageBox.Show("Expression cannot ends with operator.");
                return false;
            }
            return true;
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            this.ChangeEnability();
            if (this.RunThread != null && (this.RunThread.IsAlive || this.RunThread.ThreadState == ThreadState.Running))
            {
                this.RunThread.Abort();
                
            }
        }

        private void CopyExpressionButton_Click(object sender, RoutedEventArgs e) => Clipboard.SetText(string.Join(" ", this.InputExpression.Select(c => c.Value)));

        private void CopyStackButton_Click(object sender, RoutedEventArgs e) => Clipboard.SetText(string.Join(" ", this.Stack.Reverse().Select(c => c.Value)));

        private void CopyOutputExpressionButton_Click(object sender, RoutedEventArgs e) => Clipboard.SetText(string.Join(" ", this.OutputExpression.Select(c => c.Value)));

        private void CopyResultButton_Click(object sender, RoutedEventArgs e) => Clipboard.SetText(string.Join(" ", this.ResultExpression.Reverse().Select(c => c.Value)));

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => SpeedTextBlock.Text = $"Speed: {e.NewValue:0.000} seconds per move";

        private void ChangeEnability()
        {
            this.IsRunning = !this.IsRunning;
            this.ParseButton.IsEnabled = !this.IsRunning;
            this.StartButton.IsEnabled = !this.IsRunning;
            this.StopButton.IsEnabled = this.IsRunning;
        }

        private void Go()
        {
            int millisecondsPerMove = 0;
            this.Dispatcher.Invoke(() => this.OutputGrid.Items.Clear());
            this.Dispatcher.Invoke(() => this.StackGrid.Items.Clear());
            this.Dispatcher.Invoke(() => this.ResultGrid.Items.Clear());
            while (this.InputExpression.Any())
            {
                this.Dispatcher.Invoke(() => millisecondsPerMove = (int)(this.SpeedSlider.Value * 1000));
                var cell = this.InputExpression.Dequeue();
                switch (cell.Type)
                {
                    case SymbolTypes.Number:
                        this.AddToGui(this.OutputExpression, this.OutputGrid, cell);
                        break;
                    case SymbolTypes.Operator:
                        if (!this.Stack.Any() || operatorsPriority[cell.Value] > operatorsPriority[this.Stack.Peek().Value])
                        {
                            this.AddToGui(this.Stack, this.StackGrid, cell);
                        }
                        else
                        {
                            this.AddToGui(this.OutputExpression, this.OutputGrid, this.RemoveFromGui(this.Stack, this.StackGrid));
                            this.AddToGui(this.Stack, this.StackGrid, cell);
                        }
                        break;
                    case SymbolTypes.ParenthesisOpen:
                        this.AddToGui(this.Stack, this.StackGrid, cell);
                        break;
                    case SymbolTypes.PrenthesisClose:
                        while (this.Stack.Peek().Type != SymbolTypes.ParenthesisOpen)
                        {
                            this.AddToGui(this.OutputExpression, this.OutputGrid, this.RemoveFromGui(this.Stack, this.StackGrid));
                        }
                        this.RemoveFromGui(this.Stack, this.StackGrid);
                        break;
                }
                Thread.Sleep(millisecondsPerMove);
            }
            while (this.Stack.Any())
            {
                this.Dispatcher.Invoke(() => millisecondsPerMove = (int)(this.SpeedSlider.Value * 1000));
                this.AddToGui(this.OutputExpression, this.OutputGrid, this.RemoveFromGui(this.Stack, this.StackGrid));
                Thread.Sleep(millisecondsPerMove);
            }
            this.Dispatcher.Invoke(() => MessageBox.Show($"Output expression is {string.Join(" ", this.OutputExpression.Select(c => c.Value))}; starting counting result"));

            while (this.OutputExpression.Any())
            {
                this.Dispatcher.Invoke(() => millisecondsPerMove = (int)(this.SpeedSlider.Value * 1000));
                var cell = this.OutputExpression.Dequeue();
                switch (cell.Type)
                {
                    case SymbolTypes.Number:
                        this.AddToGui(this.ResultExpression, this.ResultGrid, cell);
                        break;
                    case SymbolTypes.Operator:
                        if (this.ResultExpression.Count > 1)
                        {
                            var cell2 = this.RemoveFromGui(this.ResultExpression, this.ResultGrid);
                            var cell1 = this.RemoveFromGui(this.ResultExpression, this.ResultGrid);
                            this.AddToGui(this.ResultExpression, this.ResultGrid, operatorsFuncs[cell.Value](cell1, cell2));
                        }
                        else
                        {
                            this.Dispatcher.Invoke(() => { MessageBox.Show("Not enough operands."); this.ChangeEnability(); });
                            return;
                        }
                        break;
                    case SymbolTypes.ParenthesisOpen:
                    case SymbolTypes.PrenthesisClose:
                        this.Dispatcher.Invoke(() => { MessageBox.Show("Parenthesis in output: something wrong."); this.ChangeEnability(); });
                        return;
                }
                Thread.Sleep(millisecondsPerMove);
            }
            this.Dispatcher.Invoke(() => this.ChangeEnability());
            if (this.ResultExpression.Count == 1)
            {
                this.Dispatcher.Invoke(() => MessageBox.Show($"Result is {this.ResultExpression.Peek().Value}"));
            }
            else
            {
                this.Dispatcher.Invoke(() => MessageBox.Show("More then one value in result expression, error occurred."));
            }
        }

        private void AddToGui(Stack<Cell> stack, DataGrid grid, Cell cell)
        {
            stack.Push(cell);
            this.Dispatcher.Invoke(() => grid.Items.Add(new Cell(cell.Value)));
        }

        private void AddToGui(Queue<Cell> queue, DataGrid grid, Cell cell)
        {
            queue.Enqueue(cell);
            this.Dispatcher.Invoke(() => grid.Items.Add(new Cell(cell.Value)));
        }

        private Cell RemoveFromGui(Stack<Cell> stack, DataGrid grid)
        {
            this.Dispatcher.Invoke(() => grid.Items.RemoveAt(stack.Count - 1));
            return stack.Pop();
        }

        private Cell RemoveFromGui(Queue<Cell> queue, DataGrid grid)
        {
            this.Dispatcher.Invoke(() => grid.Items.RemoveAt(queue.Count - 1));
            return queue.Dequeue();
        }
    }
}