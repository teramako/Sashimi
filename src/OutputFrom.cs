namespace Sashimi;

[Flags]
public enum OutputFrom
{
    Stdout = 1,
    Stderr = 2,
    Both = Stdout | Stderr
}

