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
        #region Fields

        private CalculationState? currentState = null;
        private bool isRunning = false;

        private readonly Stack<Cell> stack = new();
        private readonly Queue<Cell> inputExpression = new();
        private readonly Queue<Cell> outputExpression = new();
        private readonly Stack<Cell> resultExpression = new();

        private readonly Dictionary<string, (int, Func<Cell, Cell, Cell>)> operators = new()
        {
            ["("] = (-1, null),
            [")"] = (0, null),
            ["-"] = (1, (cell1, cell2) => new Cell((int.Parse(cell1.Value) - int.Parse(cell2.Value)).ToString())),
            ["+"] = (1, (cell1, cell2) => new Cell((int.Parse(cell1.Value) + int.Parse(cell2.Value)).ToString())),
            ["*"] = (2, (cell1, cell2) => new Cell((int.Parse(cell1.Value) * int.Parse(cell2.Value)).ToString())),
            ["/"] = (2, (cell1, cell2) => new Cell((int.Parse(cell1.Value) / int.Parse(cell2.Value)).ToString())),
            ["^"] = (3, (cell1, cell2) => new Cell((Math.Pow(int.Parse(cell1.Value), int.Parse(cell2.Value))).ToString()))
        };

        private Thread calculatingThread;

        private GraphWindow graphWindow = null;

        #endregion

        #region Constructors

        public MainWindow()
        {
            InitializeComponent();
            SpeedSlider.ValueChanged += this.Slider_ValueChanged;
            SpeedSlider.Value = 0.4;
            this.PauseButton.IsEnabled = false;
            this.StopButton.IsEnabled = false;
            this.ContinueButton.IsEnabled = false;
        }

        #endregion

        #region GuiHandlers

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            this.ParseExpression(this.ExpressionTextBox.Text);
            if (this.calculatingThread != null && (calculatingThread.IsAlive || this.calculatingThread.ThreadState == ThreadState.Running))
            {
                this.calculatingThread.Abort();
            }
            this.ContinueButton_Click(sender, e);
        }

        private void ParseButton_Click(object sender, RoutedEventArgs e) => ParseExpression(this.ExpressionTextBox.Text);

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            this.ChangeIsRunning();
            if (this.calculatingThread != null && (this.calculatingThread.IsAlive || this.calculatingThread.ThreadState == ThreadState.Running))
            {
                this.calculatingThread.Abort();

            }
        }

        private void StopButton_Copy_Click(object sender, RoutedEventArgs e)
        {
            this.ChangeIsRunning();
            this.currentState = null;
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e) => this.ChangeIsRunning();

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            this.ChangeIsRunning();
            this.calculatingThread = new Thread(Calculate);
            this.calculatingThread.Start();
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
            => _ = true switch
            {
                true when sender == this.CopyExpressionButton => new Func<int>(() => { Clipboard.SetText(string.Join(" ", this.inputExpression.Select(c => c.Value))); return 0; }).Invoke(),
                true when sender == this.CopyStackButton => new Func<int>(() => { Clipboard.SetText(string.Join(" ", this.stack.Reverse().Select(c => c.Value))); return 0; }).Invoke(),
                true when sender == this.CopyOutputExpressionButton => new Func<int>(() => { Clipboard.SetText(string.Join(" ", this.outputExpression.Select(c => c.Value))); return 0; }).Invoke(),
                true when sender == this.CopyResultButton => new Func<int>(() => { Clipboard.SetText(string.Join(" ", this.resultExpression.Reverse().Select(c => c.Value))); return 0; }).Invoke(),
                _ => 0
            };

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => SpeedTextBlock.Text = $"Speed: {e.NewValue:0.000} seconds per move";

        #endregion

        #region Private methods

        private void ChangeIsRunning()
        {
            this.isRunning = !this.isRunning;
            this.ParseButton.IsEnabled = !this.isRunning;
            this.StartButton.IsEnabled = !this.isRunning;
            this.StopButton.IsEnabled = this.isRunning;
            this.ShowGraphCheckbox.IsEnabled = !this.isRunning;
            this.PauseButton.IsEnabled = this.isRunning;
            this.ContinueButton.IsEnabled = !this.isRunning && this.currentState != null;
        }

        private void Calculate()
        {
            int millisecondsPerMove = 0;
            if (this.currentState is null)
            {
                this.Dispatcher.Invoke(() => this.OutputGrid.Items.Clear());
                this.Dispatcher.Invoke(() => this.StackGrid.Items.Clear());
                this.Dispatcher.Invoke(() => this.ResultGrid.Items.Clear());
                this.isRunning = true;
                this.currentState = CalculationState.MoveToStack;
            }

            while (this.inputExpression.Any() && this.currentState == CalculationState.MoveToStack)
            {
                if (!this.isRunning)
                {
                    return;
                }

                this.Dispatcher.Invoke(() => millisecondsPerMove = (int)(this.SpeedSlider.Value * 1000));
                var cell = this.inputExpression.Dequeue();
                switch (cell.Type)
                {
                    case SymbolTypes.Number:
                        this.AddToGui(this.outputExpression, this.OutputGrid, cell);
                        break;
                    case SymbolTypes.Operator:
                        if (!this.stack.Any() || operators[cell.Value].Item1 > operators[this.stack.Peek().Value].Item1)
                        {
                            this.AddToGui(this.stack, this.StackGrid, cell);
                        }
                        else
                        {
                            this.AddToGui(this.outputExpression, this.OutputGrid, this.RemoveFromGui(this.stack, this.StackGrid));
                            this.AddToGui(this.stack, this.StackGrid, cell);
                        }
                        break;
                    case SymbolTypes.BracketOpen:
                        this.AddToGui(this.stack, this.StackGrid, cell);
                        break;
                    case SymbolTypes.BracketClose:
                        while (this.stack.Peek().Type != SymbolTypes.BracketOpen)
                        {
                            this.AddToGui(this.outputExpression, this.OutputGrid, this.RemoveFromGui(this.stack, this.StackGrid));
                        }
                        this.RemoveFromGui(this.stack, this.StackGrid);
                        break;
                }
                Thread.Sleep(millisecondsPerMove);
            }

            if (this.currentState == CalculationState.MoveToStack)
            {
                this.currentState = CalculationState.GetExpression;
            }

            while (this.stack.Any() && this.currentState == CalculationState.GetExpression)
            {
                if (!this.isRunning)
                {
                    return;
                }

                this.Dispatcher.Invoke(() => millisecondsPerMove = (int)(this.SpeedSlider.Value * 1000));
                this.AddToGui(this.outputExpression, this.OutputGrid, this.RemoveFromGui(this.stack, this.StackGrid));
                Thread.Sleep(millisecondsPerMove);
            }

            if (this.currentState == CalculationState.GetExpression)
            {
                this.Dispatcher.Invoke(() => MessageBox.Show($"Output expression is {string.Join(" ", this.outputExpression.Select(c => c.Value))}; starting counting result"));
                this.currentState = CalculationState.MathematicalOperations;
            }

            if (this.Dispatcher.Invoke(() => this.ShowGraphCheckbox.IsChecked == true))
            {
                this.Dispatcher.Invoke(() =>
                {
                    this.graphWindow ??= new GraphWindow();
                    graphWindow.Show();
                    graphWindow.CreateGraph(new(this.outputExpression));
               });
            }

            while (this.outputExpression.Any() && this.currentState == CalculationState.MathematicalOperations)
            {
                if (!this.isRunning)
                {
                    return;
                }
                this.Dispatcher.Invoke(() => millisecondsPerMove = (int)(this.SpeedSlider.Value * 1000));
                var cell = this.outputExpression.Dequeue();
                switch (cell.Type)
                {
                    case SymbolTypes.Number:
                        this.AddToGui(this.resultExpression, this.ResultGrid, cell);
                        break;
                    case SymbolTypes.Operator:
                        if (this.resultExpression.Count > 1)
                        {
                            var cell2 = this.RemoveFromGui(this.resultExpression, this.ResultGrid);
                            var cell1 = this.RemoveFromGui(this.resultExpression, this.ResultGrid);
                            this.AddToGui(this.resultExpression, this.ResultGrid, operators[cell.Value].Item2(cell1, cell2));
                        }
                        else
                        {
                            this.Dispatcher.Invoke(() => { MessageBox.Show("Not enough operands."); this.ChangeIsRunning(); });
                            this.currentState = null;
                            return;
                        }
                        break;
                    case SymbolTypes.BracketOpen:
                    case SymbolTypes.BracketClose:
                        this.Dispatcher.Invoke(() => { MessageBox.Show("Parenthesis in output: something wrong."); this.ChangeIsRunning(); });
                        return;
                }
                Thread.Sleep(millisecondsPerMove);
            }

            this.Dispatcher.Invoke(() => this.ChangeIsRunning());

            if (this.resultExpression.Count == 1)
            {
                this.Dispatcher.Invoke(() => MessageBox.Show($"Result is {this.resultExpression.Peek().Value}"));
            }
            else
            {
                this.Dispatcher.Invoke(() => MessageBox.Show("More then one value in result expression, error occurred."));
            }

            this.currentState = null;
        }

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
            this.inputExpression.Clear();
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
                    this.AddToGui(this.inputExpression, this.ParsedExpressionGrid, new Cell(currentValue, SymbolTypes.Number));
                    currentValue = string.Empty;
                }
                if (operators.ContainsKey(symbol.ToString()))
                {
                    var (parenthesisKind, symbolType) = symbol == '(' ? (1, SymbolTypes.BracketOpen) : symbol == ')' ? (-1, SymbolTypes.BracketClose) : (0, SymbolTypes.Operator);
                    parenthesisCounter += parenthesisKind;
                    if (parenthesisCounter < 0)
                    {
                        MessageBox.Show("Wrong parenthesis.");
                        return false;
                    }
                    if (operatorNextIsForbidden && symbolType != SymbolTypes.BracketOpen && this.inputExpression.Last().Type != SymbolTypes.BracketClose)
                    {
                        MessageBox.Show("Two operators should be separated by operand in infix notation.");
                        return false;
                    }
                    this.AddToGui(this.inputExpression, this.ParsedExpressionGrid, new Cell(symbol, symbolType));
                    operatorNextIsForbidden = symbol != ')';
                }
                else
                {
                    MessageBox.Show("Unknown symbol.");
                    return false;
                }
            }
            if (currentValue.Length != 0)
            {
                this.AddToGui(this.inputExpression, this.ParsedExpressionGrid, new Cell(currentValue, SymbolTypes.Number));
                operatorNextIsForbidden = false;
            }
            if (operatorNextIsForbidden)
            {
                MessageBox.Show("Expression cannot ends with operator.");
                return false;
            }
            return true;
        }

        private void AddToGui(IEnumerable<Cell> collection, DataGrid grid, Cell cell)
        {
            _ = collection is Stack<Cell> stack ? new Func<int>(() => { stack.Push(cell); return 0; }).Invoke() : collection is Queue<Cell> queue ? new Func<int>(() => { queue.Enqueue(cell); return 0; }).Invoke() : 0;
            this.Dispatcher.Invoke(() => grid.Items.Add(new Cell(cell.Value)));
        }

        private Cell RemoveFromGui(IEnumerable<Cell> collection, DataGrid grid)
        {
            this.Dispatcher.Invoke(() => grid.Items.RemoveAt(collection.Count() - 1));
            return collection is Stack<Cell> stack ? stack.Pop() : collection is Queue<Cell> queue ? queue.Dequeue() : null;
        }

        #endregion
    }
}