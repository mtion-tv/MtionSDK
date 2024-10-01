using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace mtion.room.sdk
{
    public interface IResetStateListener
    {
        public string ClubhouseGuid { get; set; }
        public string ElementGuid { get; set; }

        public void ResetState();
    }
}

