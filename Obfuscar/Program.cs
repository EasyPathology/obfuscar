namespace Obfuscar;

public static class Program
{
    public static void Main(string[] args)
    {
        new Obfuscator(args[0]).RunRules();
    }
}
