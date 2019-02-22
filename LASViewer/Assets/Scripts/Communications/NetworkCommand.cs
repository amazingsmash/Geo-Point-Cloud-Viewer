using System;
using System.Runtime;

[Serializable()]
public class NetworkCommand {

    public readonly string name;

    public NetworkCommand(string name)
    {
        this.name = name;
    }
}

public class SelectPointCmd : NetworkCommand
{
    public float x, y, z;

    public SelectPointCmd(float x, float y, float z): base("SelectPoints") {
        this.x = x;
        this.y = y;
        this.z = z;
    }

}

