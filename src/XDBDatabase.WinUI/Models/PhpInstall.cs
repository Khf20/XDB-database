namespace XDBDatabase_WinUI.Models;

public sealed class PhpInstall
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string Version { get; set; } = "";
    public bool Active { get; set; }
    public bool HasApacheModule { get; set; }

    public override string ToString()
    {
        return Name + " | " + Version + (Active ? " | ACTIVE" : "") + (HasApacheModule ? "" : " | CLI only");
    }
}
