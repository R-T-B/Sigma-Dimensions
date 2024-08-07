﻿using UnityEngine;

namespace SigmaDimensionsPlugin
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class Version : MonoBehaviour
    {
        //from CashnipLeaf: Is this really necessary? Can you not add the version as a string?
        public static System.Version Number => new System.Version("0.11.3");

        void Awake()
        {
            UnityEngine.Debug.Log("[SigmaLog] Version Check:   Sigma Dimensions v" + Number);
        }
    }
}
