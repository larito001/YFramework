using System.Collections;
using System.Collections.Generic;
using Codice.CM.Common;
using UnityEngine;
using YOTO;

public class CardPlugin : LogicPluginBase
{
    private Dictionary<int, int> typeCount;

    
    public static CardPlugin Instance;

    public CardPlugin()
    {
        Instance = this;
    }
    

    protected override void OnInstall()
    {
        base.OnInstall();
    }

    protected override void OnUninstall()
    {
        base.OnUninstall();
    }


}