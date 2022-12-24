namespace UncomfortablePolishCow
{
    public enum SymbolTypes
    {
        Number,
        Operator,
        BracketOpen,
        BracketClose
    }
    public enum CalculationState
    {
        MoveToStack,
        GetExpression,
        MathematicalOperations
    }
}