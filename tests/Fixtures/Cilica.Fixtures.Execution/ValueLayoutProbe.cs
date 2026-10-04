namespace Cilica.Fixtures.Execution
{

public struct ValueLeaf
{
    public int Number;
    public object Reference;
    public string Text;
}

public struct ValuePair
{
    public ValueLeaf Left;
    public ValueLeaf Right;
    public int[] Numbers;
    public object[] Objects;
}
}
