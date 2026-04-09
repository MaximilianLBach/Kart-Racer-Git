using Unity.Netcode.Components;
using UnityEngine;

namespace Kart
{
    // Change the name of the Enum type from "AuthorityMode" to "KartAuthority"
    public enum KartAuthority
    {
        Server,
        Client
    }

    [DisallowMultipleComponent]
    public class ClientNetworkTransform : NetworkTransform
    {
        // Use the new enum name here
        public KartAuthority Authority = KartAuthority.Client;

        protected override bool OnIsServerAuthoritative()
        {
            return Authority == KartAuthority.Server;
        }
    }
}